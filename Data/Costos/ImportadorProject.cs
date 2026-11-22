using System.Globalization;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using Microsoft.EntityFrameworkCore;
using SigWeb.Models;

namespace SigWeb.Data.Costos;

// PDT desde MS Project guardado como XML (Archivo › Guardar como › "Formato XML (*.xml)").
// Cada tarea queda como ActividadPrograma con su UID (traza entre versiones). Se intenta ligar
// cada tarea a una torre (T100, TORRE 57N… en su nombre o en el de sus tareas resumen) y a un
// ítem de pago (código al inicio del nombre, descripción del ítem o actividad de obra ligada);
// lo que no se deduzca se liga a mano en la vista PDT.
public sealed class PlanPrograma
{
    public string NombreArchivo { get; set; } = "";
    public string? Titulo { get; set; }
    public string DiasLaborables { get; set; } = "1111111";   // del calendario base del proyecto
    public List<DateTime> Festivos { get; } = new();
    public List<TareaLeida> Tareas { get; } = new();
    public List<string> Avisos { get; } = new();
    public int ConItem => Tareas.Count(t => !t.EsResumen && t.ItemId is not null);
    public int ConTorre => Tareas.Count(t => !t.EsResumen && t.TorreId is not null);
    public int Hojas => Tareas.Count(t => !t.EsResumen);

    public sealed class TareaLeida
    {
        public int Uid; public string Nombre = ""; public string? Wbs; public int Nivel; public bool EsResumen; public int Orden;
        public DateTime Inicio; public DateTime Fin; public decimal? Costo; public string? Predecesoras; public string? Nota;
        public int? ItemId; public int? TorreId; public int? TiroId; public string? Cuadrilla; public int? ActividadObraId; public string? TorreNombre;
        public decimal? AvanceProject; public bool EsHito;
    }
}

public static class ImportadorProject
{
    private static readonly Regex RxTorre = new(@"(?:\bTORRE|\bTR|\bT)\s*[-#.]?\s*(\d{1,4}[A-Z]?N*)\b", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex RxTorreInicio = new(@"^\s*(\d{1,4}[A-Z]?N*)(?=\s|$)", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex RxCodigo = new(@"^\s*([A-Z]{0,3}\d+(?:\.\d+)*)\b", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex RxTiro = new(@"\bTIRO\s*(\d+)\b", RegexOptions.IgnoreCase | RegexOptions.Compiled);

    public static PlanPrograma Analizar(AppDbContext db, InstantaneaCostos s, Stream xml, string nombreArchivo)
    {
        var plan = new PlanPrograma { NombreArchivo = nombreArchivo };
        using var lector = System.Xml.XmlReader.Create(xml, new System.Xml.XmlReaderSettings
        {
            DtdProcessing = System.Xml.DtdProcessing.Prohibit,
            XmlResolver = null,
            MaxCharactersInDocument = 80L * 1024 * 1024
        });
        var doc = XDocument.Load(lector);
        var ns = doc.Root?.Name.Namespace ?? XNamespace.None;
        if (doc.Root?.Name.LocalName != "Project") throw new InvalidOperationException("El archivo no es un XML de MS Project (falta el elemento Project).");
        plan.Titulo = doc.Root.Element(ns + "Title")?.Value ?? doc.Root.Element(ns + "Name")?.Value;
        LeerCalendario(doc.Root, ns, plan);
        var minutosDia = int.TryParse(doc.Root.Element(ns + "MinutesPerDay")?.Value, out var mpd) && mpd > 0 ? mpd : 480;
        // Posposición de Project: décimas de minuto; formatos "transcurridos" (8, 10…) cuentan 24 h por día.
        int Pospo(XElement p)
        {
            if (!long.TryParse(p.Element(ns + "LinkLag")?.Value, out var lag) || lag == 0) return 0;
            var transcurrido = int.TryParse(p.Element(ns + "LagFormat")?.Value, out var lf) && lf % 2 == 0 && lf >= 4 && lf <= 20 && lf != 7;
            return (int)Math.Round(lag / 10.0 / (transcurrido ? 1440 : minutosDia));
        }
        static TipoVinculo Tipo(string? t) => t switch { "0" => TipoVinculo.FF, "2" => TipoVinculo.CF, "3" => TipoVinculo.CC, _ => TipoVinculo.FC };

        var casador = new CasadorTorres(db);
        var items = s.Items.Where(i => !i.EsCapitulo).ToList();
        var porCodigo = items.GroupBy(i => i.Codigo.Trim().ToUpperInvariant()).ToDictionary(g => g.Key, g => g.First());
        var porDescripcion = items.Where(i => i.Descripcion.Length >= 6).Select(i => (Clave: Clave(i.Descripcion), It: i)).OrderByDescending(x => x.Clave.Length).ToList();
        var porActividad = items.Where(i => i.ActividadObraId is not null).GroupBy(i => i.ActividadObraId!.Value).ToDictionary(g => g.Key, g => g.OrderBy(i => i.Orden).ToList());
        var previstas = s.CantidadesTorre.Where(c => c.CantidadPrevista > 0).Select(c => (c.ElementoTrazadoId, c.ItemPagoId)).ToHashSet();
        var actividades = s.ActividadesObra.Select(a => (Clave: Clave(a.Nombre), a.Id)).Where(a => a.Clave.Length >= 5).OrderByDescending(a => a.Clave.Length).ToList();

        // Nombre completo de la actividad; si no, sinónimos usuales del PDT (SUPUESTO, ajustable a mano).
        int? Actividad(string k)
        {
            if (actividades.FirstOrDefault(a => k.Contains(a.Clave)) is { Id: var id } && id != 0) return id;
            foreach (var (palabra, destino) in Sinonimos)
                if (k.Contains(palabra) && actividades.FirstOrDefault(a => a.Clave.Contains(destino)) is { Id: var id2 } && id2 != 0) return id2;
            return null;
        }

        var pila = new List<PlanPrograma.TareaLeida>();      // ancestros por nivel
        var orden = 0;
        foreach (var t in doc.Root.Element(ns + "Tasks")?.Elements(ns + "Task") ?? Enumerable.Empty<XElement>())
        {
            string? V(string n) => t.Element(ns + n)?.Value;
            if (V("IsNull") == "1" || string.IsNullOrWhiteSpace(V("Name"))) continue;
            if (!int.TryParse(V("UID"), out var uid) || uid == 0) continue;         // UID 0 = tarea resumen del proyecto
            var tarea = new PlanPrograma.TareaLeida
            {
                Uid = uid, Nombre = V("Name")!.Trim(), Wbs = V("WBS"), Nivel = int.TryParse(V("OutlineLevel"), out var nv) ? nv : 1,
                EsResumen = V("Summary") == "1", Orden = ++orden,
                Inicio = Fecha(V("Start")) ?? DateTime.Today, Fin = Fecha(V("Finish")) ?? Fecha(V("Start")) ?? DateTime.Today,
                Costo = decimal.TryParse(V("Cost"), NumberStyles.Float, CultureInfo.InvariantCulture, out var c) && c > 0 ? c / 100m : null,
                Nota = V("Notes"),
                AvanceProject = decimal.TryParse(V("PercentComplete"), NumberStyles.Float, CultureInfo.InvariantCulture, out var pc) ? pc / 100m : null,
                Predecesoras = MotorPrograma.Escribir(t.Elements(ns + "PredecessorLink")
                    .Where(p => int.TryParse(p.Element(ns + "PredecessorUID")?.Value, out _))
                    .Select(p => new Vinculo(int.Parse(p.Element(ns + "PredecessorUID")!.Value), Tipo(p.Element(ns + "Type")?.Value), Pospo(p)))),
                EsHito = V("Milestone") == "1"
            };
            while (pila.Count >= tarea.Nivel) pila.RemoveAt(pila.Count - 1);
            var cadena = pila.Select(p => p.Nombre).Append(tarea.Nombre).ToList();

            // Torre y tiro: la tarea o la resumen más cercana que los nombre.
            foreach (var n in Enumerable.Reverse(cadena))
            {
                // "TIRO 11: T150 A T158" nombra un tiro, no una torre.
                if (tarea.TorreId is null && !RxTiro.IsMatch(n) && (RxTorre.Match(n) is { Success: true } mt ? mt : RxTorreInicio.Match(n)) is { Success: true } m2)
                {
                    tarea.TorreNombre ??= m2.Groups[1].Value.ToUpperInvariant();
                    tarea.TorreId = casador.Casar(m2.Groups[1].Value);
                }
                if (tarea.TiroId is null && RxTiro.Match(n) is { Success: true } mr)
                    tarea.TiroId = s.Tiros.FirstOrDefault(x => Regex.Match(x.Nombre, @"\d+").Value == mr.Groups[1].Value)?.Id;
            }
            if (tarea.TorreId is null) tarea.TorreId = pila.LastOrDefault(p => p.TorreId is not null)?.TorreId;

            // Actividad de obra: la de nombre más largo contenida en el nombre de la tarea (o de sus resúmenes).
            // Solo del nombre propio; de las tareas resumen solo si el nombre es apenas la torre ("T189").
            var propio = RxTorre.Replace(RxTorreInicio.Replace(tarea.Nombre, ""), "").Trim();
            var fuentes = propio.Length > 0 ? new List<string> { tarea.Nombre } : Enumerable.Reverse(cadena).ToList();
            foreach (var n in fuentes)
                if (Actividad(Clave(n)) is int aid) { tarea.ActividadObraId = aid; break; }
            // Ítem: código al inicio del nombre; si no, el ítem ligado a la actividad de obra que tenga cantidad
            // prevista en la torre (así sale el de zapata, parrilla o pila según la torre); si no, por descripción.
            foreach (var n in fuentes)
                if (RxCodigo.Match(n) is { Success: true } mc && mc.Groups[1].Value.Contains('.') && porCodigo.TryGetValue(mc.Groups[1].Value.ToUpperInvariant(), out var it1)) { tarea.ItemId = it1.Id; break; }
            if (tarea.ItemId is null && tarea.ActividadObraId is int act && porActividad.TryGetValue(act, out var candidatos))
                tarea.ItemId = (tarea.TorreId is int tid ? candidatos.FirstOrDefault(c => previstas.Contains((tid, c.Id))) : null)?.Id ?? candidatos[0].Id;
            if (tarea.ItemId is null && !tarea.EsResumen)
            {
                var k = Clave(tarea.Nombre);
                if (porDescripcion.FirstOrDefault(x => k.Contains(x.Clave)) is { It: { } it2 }) tarea.ItemId = it2.Id;
            }
            if (tarea.ItemId is int iid) tarea.Cuadrilla = s.ItemPorId[iid].CuadrillaPorDefecto;

            plan.Tareas.Add(tarea);
            pila.Add(tarea);
        }
        if (plan.Tareas.Count == 0) plan.Avisos.Add("El XML no trae tareas.");
        var hojas = plan.Tareas.Where(t => !t.EsResumen).ToList();
        var sinItemConAct = hojas.Count(t => t.ItemId is null && t.ActividadObraId is not null);
        if (sinItemConAct > 0) plan.Avisos.Add($"{sinItemConAct} tarea(s) con actividad de obra reconocida pero sin ítem de pago: ligue los ítems a sus actividades en el Formulario de precios y vuelva a cargar, o lígelas a mano.");
        var otras = hojas.Where(t => t.ActividadObraId is null && t.ItemId is null).Select(t => Regex.Replace(t.Nombre, @"^\s*\S*\d\S*\s+", "")).GroupBy(n => n).OrderByDescending(g => g.Count()).Take(12).Select(g => $"{g.Key} ({g.Count()})").ToList();
        if (otras.Count > 0) plan.Avisos.Add("Tareas sin actividad ni ítem (quedan solo como programa): " + string.Join(", ", otras));
        var sinTorre = casador.SinCasar.Distinct().ToList();
        if (sinTorre.Count > 0) plan.Avisos.Add($"Torres nombradas en el programa que no están en el Trazado: {string.Join(", ", sinTorre.Take(30))}");
        return plan;
    }

    // Guarda el programa como versión nueva (la primera es la línea base) y anota en la bitácora lo
    // que cambió respecto a la versión vigente anterior (por UID de tarea).
    public static async Task<ProgramaTrabajo> AplicarAsync(AppDbContext db, InstantaneaCostos s, PlanPrograma plan, string? motivo, string? aprobadoPor)
    {
        var previos = await db.ProgramasTrabajo.ToListAsync();
        var anterior = previos.Where(p => p.Vigente).OrderByDescending(p => p.Version).FirstOrDefault();
        foreach (var p in previos) p.Vigente = false;
        var prog = new ProgramaTrabajo
        {
            Version = previos.Count == 0 ? 1 : previos.Max(p => p.Version) + 1, Fecha = DateTime.Today,
            Nombre = string.IsNullOrWhiteSpace(plan.Titulo) ? Path.GetFileNameWithoutExtension(plan.NombreArchivo) : plan.Titulo!,
            LineaBase = previos.Count == 0, Vigente = true, Motivo = string.IsNullOrWhiteSpace(motivo) ? (previos.Count == 0 ? "Línea base" : null) : motivo.Trim(),
            AprobadoPor = aprobadoPor, DiasLaborables = plan.DiasLaborables,
            Festivos = plan.Festivos.Count == 0 ? null : string.Join(";", plan.Festivos.Distinct().OrderBy(f => f).Select(f => f.ToString("yyyy-MM-dd")))
        };
        var cal = new CalendarioPdt(prog.DiasLaborables, plan.Festivos, plan.Tareas.Count == 0 ? DateTime.Today : plan.Tareas.Min(t => t.Inicio));
        db.ProgramasTrabajo.Add(prog);
        await db.SaveChangesAsync();

        var prevista = s.CantidadesTorre.GroupBy(c => (c.ElementoTrazadoId, c.ItemPagoId)).ToDictionary(g => g.Key, g => g.Sum(c => c.CantidadPrevista));
        var nuevas = new List<ActividadPrograma>();
        foreach (var t in plan.Tareas)
        {
            var cant = t.ItemId is int i && t.TorreId is int tr ? prevista.GetValueOrDefault((tr, i)) : 0m;
            var a = new ActividadPrograma
            {
                ProgramaTrabajoId = prog.Id, Uid = t.Uid, Nombre = t.Nombre, Wbs = t.Wbs, Nivel = t.Nivel, EsResumen = t.EsResumen, Orden = t.Orden,
                ItemPagoId = t.EsResumen ? null : t.ItemId, ActividadObraId = t.ActividadObraId, AvanceProject = t.AvanceProject, ElementoTrazadoId = t.TorreId, TiroId = t.TiroId, Cuadrilla = t.Cuadrilla,
                Inicio = t.Inicio.Date, Fin = t.Fin.Date, Cantidad = cant, Duracion = t.EsHito ? 0 : cal.Duracion(t.Inicio, t.Fin), Predecesoras = t.Predecesoras, Nota = t.Nota,
                ValorPlaneado = t.EsResumen ? null : t.Costo ?? (t.ItemId is int i2 && cant > 0 ? cant * s.PrecioConAiu(s.ItemPorId[i2], t.Fin.Year) : null)
            };
            nuevas.Add(a);
            db.ActividadesPrograma.Add(a);
        }
        await db.SaveChangesAsync();

        if (anterior is not null)
        {
            var viejas = await db.ActividadesPrograma.AsNoTracking().Where(a => a.ProgramaTrabajoId == anterior.Id).ToListAsync();
            var porUid = viejas.Where(v => v.Uid is not null).GroupBy(v => v.Uid!.Value).ToDictionary(g => g.Key, g => g.First());
            foreach (var n in nuevas.Where(n => !n.EsResumen))
            {
                if (n.Uid is not int u || !porUid.TryGetValue(u, out var v))
                {
                    db.CambiosPrograma.Add(new CambioPrograma { ProgramaTrabajoId = prog.Id, ActividadProgramaId = n.Id, Campo = "Tarea nueva", Despues = n.Nombre, Motivo = prog.Motivo, Usuario = aprobadoPor });
                    continue;
                }
                void Cambio(string campo, string? antes, string? despues)
                {
                    if (antes != despues)
                        db.CambiosPrograma.Add(new CambioPrograma { ProgramaTrabajoId = prog.Id, ActividadProgramaId = n.Id, Campo = campo, Antes = antes, Despues = despues, Motivo = prog.Motivo, Usuario = aprobadoPor });
                }
                Cambio("Inicio", FormatoCostos.Fecha(v.Inicio), FormatoCostos.Fecha(n.Inicio));
                Cambio("Fin", FormatoCostos.Fecha(v.Fin), FormatoCostos.Fecha(n.Fin));
                Cambio("Nombre", v.Nombre, n.Nombre);
            }
            var uidsNuevos = nuevas.Where(n => n.Uid is not null).Select(n => n.Uid!.Value).ToHashSet();
            foreach (var v in viejas.Where(v => !v.EsResumen && v.Uid is int u && !uidsNuevos.Contains(u)))
                db.CambiosPrograma.Add(new CambioPrograma { ProgramaTrabajoId = prog.Id, Campo = "Tarea eliminada", Antes = v.Nombre, Motivo = prog.Motivo, Usuario = aprobadoPor });
            await db.SaveChangesAsync();
        }
        db.ChangeTracker.Clear();
        return prog;
    }

    private static readonly (string Palabra, string Actividad)[] Sinonimos =
    {
        ("PARRILLA", "NIVELACION"), ("NIVELACION", "NIVELACION"), ("PUESTA A TIERRA", "SISTEMA DE PUESTA A TIERRA"),
        ("ACERO", "ACERO DE REFUERZO"), ("RELLENO", "RELLENO"), ("EXCAVACION", "EXCAVACION"), ("SOLADO", "SOLADO"),
        ("CONCRETO", "CONCRETO"), ("MONTAJE", "MONTAJE"), ("VESTIDA", "VESTIDA"), ("PREARMADO", "PREARMADO"), ("PRE ARMADO", "PREARMADO"),
        ("REVISION Y ENTREGA", "REVISION Y AJUSTE"), ("VERTICALIDAD", "VERTICALIDAD"), ("SENALIZACION", "SENALIZACION"),
        ("TENDIDO CONDUCTOR", "TENDIDO DE CONDUCTOR"), ("CONDUCTOR", "TENDIDO DE CONDUCTOR"), ("OPGW", "OPGW"),
        ("GUARDA", "CABLE DE GUARDA"), ("POLEAS", "POLEAS"), ("FLECHADO", "FLECHADO"), ("SEPARADORES", "SEPARADORES"),
    };

    // Calendario base del proyecto: días de la semana laborables y excepciones no laborables (festivos).
    private static void LeerCalendario(XElement root, XNamespace ns, PlanPrograma plan)
    {
        var uid = root.Element(ns + "CalendarUID")?.Value;
        var cal = root.Element(ns + "Calendars")?.Elements(ns + "Calendar").FirstOrDefault(c => c.Element(ns + "UID")?.Value == uid)
                  ?? root.Element(ns + "Calendars")?.Elements(ns + "Calendar").FirstOrDefault(c => c.Element(ns + "IsBaseCalendar")?.Value == "1");
        if (cal is null) return;
        var dias = "1111111".ToCharArray();      // lunes…domingo
        void Excepcion(XElement? periodo)
        {
            if (Fecha(periodo?.Element(ns + "FromDate")?.Value) is DateTime a && Fecha(periodo?.Element(ns + "ToDate")?.Value) is DateTime b && (b - a).TotalDays < 400)
                for (var d = a.Date; d <= b.Date; d = d.AddDays(1)) plan.Festivos.Add(d);
        }
        foreach (var w in cal.Element(ns + "WeekDays")?.Elements(ns + "WeekDay") ?? Enumerable.Empty<XElement>())
        {
            var tipo = int.TryParse(w.Element(ns + "DayType")?.Value, out var dt) ? dt : -1;
            var trabaja = w.Element(ns + "DayWorking")?.Value == "1";
            if (tipo is >= 1 and <= 7) dias[(tipo + 5) % 7] = trabaja ? '1' : '0';     // 1 = domingo → índice 6
            else if (tipo == 0 && !trabaja) Excepcion(w.Element(ns + "TimePeriod"));
        }
        foreach (var x in cal.Element(ns + "Exceptions")?.Elements(ns + "Exception") ?? Enumerable.Empty<XElement>())
            if (x.Element(ns + "DayWorking")?.Value != "1") Excepcion(x.Element(ns + "TimePeriod"));
        if (dias.Contains('1')) plan.DiasLaborables = new string(dias);
    }

    private static DateTime? Fecha(string? v) =>
        DateTime.TryParse(v, CultureInfo.InvariantCulture, DateTimeStyles.None, out var f) ? f : null;

    // Texto comparable: mayúsculas, sin tildes ni signos, espacios simples.
    private static string Clave(string t)
    {
        var n = t.Normalize(System.Text.NormalizationForm.FormD);
        var sb = new System.Text.StringBuilder();
        foreach (var ch in n)
            if (CharUnicodeInfo.GetUnicodeCategory(ch) != UnicodeCategory.NonSpacingMark) sb.Append(char.IsLetterOrDigit(ch) ? char.ToUpperInvariant(ch) : ' ');
        return Regex.Replace(sb.ToString(), @"\s+", " ").Trim();
    }
}
