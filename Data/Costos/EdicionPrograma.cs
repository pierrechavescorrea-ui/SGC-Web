using Microsoft.EntityFrameworkCore;
using SigWeb.Models;

namespace SigWeb.Data.Costos;

// Edición del PDT a mano, tipo MS Project: filas en memoria (insertar, eliminar, sangría, mover,
// duración, fechas, predecesoras por n.º de fila), recálculo con MotorPrograma y guardado como
// versión nueva o corrección de la versión mostrada, con bitácora por UID.
public sealed class EdicionPrograma
{
    public sealed class Fila
    {
        public int Id; public int Uid; public string Nombre = ""; public int Nivel = 1; public bool EsResumen;
        public int Duracion = 1; public DateTime Inicio; public DateTime Fin;
        public List<Vinculo> Preds = new();
        public decimal? Avance;                     // 0–1 (% completado de Project, referencia)
        public int? ItemId, TorreId, TiroId, ActividadObraId; public decimal Cantidad; public string? Cuadrilla, Nota; public decimal? ValorPlaneado;
        public int? Holgura; public bool Critica;
        public int? ItemOriginal; public decimal CantidadOriginal;
        public bool EsHito => !EsResumen && Duracion == 0;
    }

    public List<Fila> Filas { get; } = new();
    public string DiasLaborables { get; set; } = "1111111";
    public string? Festivos { get; set; }
    public List<string> Errores { get; private set; } = new();
    public bool Cambio { get; set; }
    private int _uidMax;

    public static EdicionPrograma Desde(ProgramaTrabajo? p, IEnumerable<ActividadPrograma> tareas, int uidMaxGlobal)
    {
        var e = new EdicionPrograma { DiasLaborables = CalendarioPdt.Normalizar(p?.DiasLaborables), Festivos = p?.Festivos };
        var cal = e.Calendario(tareas.Select(a => a.Inicio));
        // Versiones guardadas antes del motor no traen duración: se deduce de las fechas.
        var sinDuracion = tareas.All(a => a.Duracion == 0);
        foreach (var a in tareas.OrderBy(a => a.Orden))
            e.Filas.Add(new Fila
            {
                Id = a.Id, Uid = a.Uid ?? 0, Nombre = a.Nombre, Nivel = Math.Max(1, a.Nivel), EsResumen = a.EsResumen,
                Duracion = sinDuracion ? cal.Duracion(a.Inicio, a.Fin) : a.Duracion,
                Inicio = a.Inicio.Date, Fin = a.Fin.Date, Preds = MotorPrograma.Leer(a.Predecesoras), Avance = a.AvanceProject,
                ItemId = a.ItemPagoId, TorreId = a.ElementoTrazadoId, TiroId = a.TiroId, ActividadObraId = a.ActividadObraId,
                Cantidad = a.Cantidad, Cuadrilla = a.Cuadrilla, Nota = a.Nota, ValorPlaneado = a.ValorPlaneado, ItemOriginal = a.ItemPagoId, CantidadOriginal = a.Cantidad
            });
        e.TomarOriginales(tareas);
        e._uidMax = Math.Max(uidMaxGlobal, e.Filas.Count == 0 ? 0 : e.Filas.Max(f => f.Uid));
        foreach (var f in e.Filas.Where(f => f.Uid == 0)) f.Uid = ++e._uidMax;     // tareas sin UID (no deberían)
        e.MarcarResumenes();
        return e;
    }

    public CalendarioPdt Calendario(IEnumerable<DateTime>? fechas = null)
    {
        var min = (fechas ?? Filas.Select(f => f.Inicio)).DefaultIfEmpty(DateTime.Today).Min();
        return new CalendarioPdt(DiasLaborables, CalendarioPdt.LeerFestivos(Festivos), min.AddYears(-1));
    }

    public void MarcarResumenes()
    {
        for (var i = 0; i < Filas.Count; i++) Filas[i].EsResumen = i + 1 < Filas.Count && Filas[i + 1].Nivel > Filas[i].Nivel;
    }

    public void Recalcular()
    {
        MarcarResumenes();
        var cal = Calendario();
        var t = Filas.Select(f => new MotorPrograma.Tarea { Uid = f.Uid, Nivel = f.Nivel, Duracion = f.Duracion, Inicio = f.Inicio, Fin = f.Fin, Predecesoras = f.Preds }).ToList();
        Errores = MotorPrograma.Programar(t, cal);
        for (var i = 0; i < Filas.Count; i++)
        {
            var f = Filas[i];
            f.Inicio = t[i].Inicio; f.Fin = t[i].Fin; f.Holgura = t[i].Holgura; f.Critica = t[i].Critica;
            if (f.EsResumen) f.Duracion = t[i].Duracion;
        }
    }

    // ---- n.º de fila (como el Id de Project) ↔ UID ----
    public int? UidDeFila(int numero) => numero >= 1 && numero <= Filas.Count ? Filas[numero - 1].Uid : null;
    public string TextoPreds(Fila f)
    {
        var idx = Filas.Select((x, i) => (x.Uid, i)).GroupBy(x => x.Uid).ToDictionary(g => g.Key, g => g.First().i + 1);
        return string.Join(";", f.Preds.Select(v => (idx.TryGetValue(v.Uid, out var n) ? n.ToString() : "?" + v.Uid)
            + (v.Tipo == TipoVinculo.FC && v.Posposicion == 0 ? "" : v.Tipo.ToString()) + (v.Posposicion == 0 ? "" : v.Posposicion.ToString("+0;-0"))));
    }

    public string? FijarPreds(Fila f, string? texto)
    {
        var (v, err) = MotorPrograma.LeerCon(texto, UidDeFila);
        if (err.Count > 0) return "Predecesora no válida: " + string.Join(", ", err) + " (use n.º de fila y FC, CC, FF o CF con ±días, p. ej. 12FC+2).";
        if (v.Any(x => x.Uid == f.Uid)) return "Una tarea no puede ser su propia predecesora.";
        var resumenes = Filas.Where(x => x.EsResumen).Select(x => x.Uid).ToHashSet();
        if (v.Any(x => resumenes.Contains(x.Uid))) return "Las predecesoras deben ser tareas, no tareas resumen.";
        f.Preds = v; Cambio = true;
        return null;
    }

    public void FijarDuracion(Fila f, int d) { if (f.EsResumen) return; f.Duracion = Math.Max(0, d); Cambio = true; }

    // Con predecesoras el inicio lo fijan ellas; sin ellas, el inicio digitado queda fijo.
    public string? FijarInicio(Fila f, DateTime d)
    {
        if (f.EsResumen) return null;
        if (f.Preds.Count > 0) return "El inicio de esta tarea lo fijan sus predecesoras: quítelas o use una posposición.";
        f.Inicio = d.Date; Cambio = true; return null;
    }

    // Cambiar el fin = cambiar la duración.
    public void FijarFin(Fila f, DateTime d)
    {
        if (f.EsResumen) return;
        var cal = Calendario();
        f.Duracion = d.Date < f.Inicio.Date ? 0 : cal.Duracion(f.Inicio, d);
        Cambio = true;
    }

    // ---- estructura ----
    private int FinBloque(int i) { var j = i + 1; while (j < Filas.Count && Filas[j].Nivel > Filas[i].Nivel) j++; return j; }

    public int Insertar(int posicion, bool hito)
    {
        posicion = Math.Clamp(posicion, 0, Filas.Count);
        var nivel = posicion < Filas.Count ? Filas[posicion].Nivel : posicion > 0 ? (Filas[posicion - 1].EsResumen ? Filas[posicion - 1].Nivel + 1 : Filas[posicion - 1].Nivel) : 1;
        var ref0 = posicion > 0 ? Filas[posicion - 1].Inicio : posicion < Filas.Count ? Filas[posicion].Inicio : DateTime.Today;
        Filas.Insert(posicion, new Fila { Uid = ++_uidMax, Nombre = hito ? "Hito nuevo" : "Tarea nueva", Nivel = nivel, Duracion = hito ? 0 : 1, Inicio = ref0, Fin = ref0 });
        Cambio = true;
        return posicion;
    }

    // Eliminar una tarea resumen elimina sus subtareas (como Project). Devuelve cuántas filas salieron.
    public int Eliminar(int i)
    {
        if (i < 0 || i >= Filas.Count) return 0;
        var fin = FinBloque(i);
        var uids = Filas.Skip(i).Take(fin - i).Select(f => f.Uid).ToHashSet();
        Filas.RemoveRange(i, fin - i);
        foreach (var f in Filas) f.Preds.RemoveAll(v => uids.Contains(v.Uid));
        Cambio = true;
        return uids.Count;
    }

    public void Sangria(int i, int delta)
    {
        if (i < 0 || i >= Filas.Count) return;
        var nuevo = Filas[i].Nivel + delta;
        if (nuevo < 1 || (delta > 0 && (i == 0 || nuevo > Filas[i - 1].Nivel + 1))) return;
        var fin = FinBloque(i);
        for (var j = i; j < fin; j++) Filas[j].Nivel += delta;
        MarcarResumenes();
        foreach (var r in Filas.Where(x => x.EsResumen)) foreach (var f in Filas) f.Preds.RemoveAll(v => v.Uid == r.Uid);
        Cambio = true;
    }

    // Mueve la tarea (con sus subtareas) antes o después de su hermana vecina. Devuelve la nueva posición.
    public int Mover(int i, int dir)
    {
        if (i < 0 || i >= Filas.Count) return i;
        var nivel = Filas[i].Nivel; var fin = FinBloque(i);
        if (dir < 0)
        {
            var k = i - 1;
            while (k >= 0 && Filas[k].Nivel > nivel) k--;
            if (k < 0 || Filas[k].Nivel != nivel) return i;
            var bloque = Filas.GetRange(i, fin - i); Filas.RemoveRange(i, fin - i); Filas.InsertRange(k, bloque);
            Cambio = true; return k;
        }
        if (fin >= Filas.Count || Filas[fin].Nivel != nivel) return i;
        var fin2 = FinBloque(fin);
        var sig = Filas.GetRange(fin, fin2 - fin); Filas.RemoveRange(fin, fin2 - fin); Filas.InsertRange(i, sig);
        Cambio = true; return i + sig.Count;
    }

    // EDT por esquema (1, 1.1, 1.2…), como la EDT por defecto de Project.
    public List<string> Edt()
    {
        var l = new List<string>(); var cont = new List<int>();
        foreach (var f in Filas)
        {
            while (cont.Count > f.Nivel) cont.RemoveAt(cont.Count - 1);
            while (cont.Count < f.Nivel) cont.Add(0);
            cont[^1]++;
            l.Add(string.Join(".", cont));
        }
        return l;
    }

    // ---- guardar ----
    // nueva = versión nueva (vigente; la primera es la línea base); si no, corrige la versión base (no la línea base).
    public async Task<ProgramaTrabajo> GuardarAsync(AppDbContext db, InstantaneaCostos s, int? baseId, bool nueva, string nombre, string? motivo, string? usuario)
    {
        Recalcular();
        if (Errores.Any(e => e.Contains("ciclo"))) throw new InvalidOperationException(Errores.First(e => e.Contains("ciclo")));
        if (Filas.Count == 0) throw new InvalidOperationException("El programa no tiene tareas.");
        if (Filas.Any(f => string.IsNullOrWhiteSpace(f.Nombre))) throw new InvalidOperationException("Hay tareas sin nombre.");
        var edt = Edt();
        var previos = await db.ProgramasTrabajo.ToListAsync();
        var baseProg = baseId is int b ? previos.FirstOrDefault(p => p.Id == b) : null;
        var viejas = baseProg is null ? new List<ActividadPrograma>() : await db.ActividadesPrograma.Where(a => a.ProgramaTrabajoId == baseProg.Id).ToListAsync();
        ProgramaTrabajo prog;
        if (nueva || baseProg is null)
        {
            foreach (var p in previos) p.Vigente = false;
            prog = new ProgramaTrabajo
            {
                Version = previos.Count == 0 ? 1 : previos.Max(p => p.Version) + 1, Fecha = DateTime.Today, Nombre = nombre.Trim() == "" ? "PDT" : nombre.Trim(),
                LineaBase = previos.Count == 0, Vigente = true, Motivo = string.IsNullOrWhiteSpace(motivo) ? (previos.Count == 0 ? "Línea base" : null) : motivo.Trim(), AprobadoPor = usuario
            };
            db.ProgramasTrabajo.Add(prog);
        }
        else
        {
            if (baseProg.LineaBase) throw new InvalidOperationException("La línea base no se corrige: guarde una versión nueva.");
            prog = baseProg;
            if (!string.IsNullOrWhiteSpace(nombre)) prog.Nombre = nombre.Trim();
        }
        var calAntes = (prog.DiasLaborables, prog.Festivos);
        prog.DiasLaborables = CalendarioPdt.Normalizar(DiasLaborables);
        prog.Festivos = string.IsNullOrWhiteSpace(Festivos) ? null : string.Join(";", CalendarioPdt.LeerFestivos(Festivos).Select(f => f.ToString("yyyy-MM-dd")));
        await db.SaveChangesAsync();

        var prevista = s.CantidadesTorre.GroupBy(c => (c.ElementoTrazadoId, c.ItemPagoId)).ToDictionary(g => g.Key, g => g.Sum(c => c.CantidadPrevista));
        var porId = viejas.ToDictionary(v => v.Id);
        var guardadas = new List<(ActividadPrograma A, Fila F)>();
        for (var i = 0; i < Filas.Count; i++)
        {
            var f = Filas[i];
            ActividadPrograma a;
            if (!nueva && f.Id != 0 && porId.TryGetValue(f.Id, out var ex)) a = ex;
            else { a = new ActividadPrograma { ProgramaTrabajoId = prog.Id }; db.ActividadesPrograma.Add(a); }
            var cant = f.Cantidad == 0 && f.ItemId is int it0 && f.TorreId is int to ? prevista.GetValueOrDefault((to, it0)) : f.Cantidad;
            a.Uid = f.Uid; a.Nombre = f.Nombre.Trim(); a.Wbs = edt[i]; a.Nivel = f.Nivel; a.EsResumen = f.EsResumen; a.Orden = i + 1;
            a.Inicio = f.Inicio.Date; a.Fin = f.Fin.Date; a.Duracion = f.Duracion; a.Predecesoras = f.EsResumen ? null : MotorPrograma.Escribir(f.Preds);
            a.AvanceProject = f.Avance; a.ItemPagoId = f.EsResumen ? null : f.ItemId; a.ElementoTrazadoId = f.TorreId; a.TiroId = f.TiroId;
            a.ActividadObraId = f.ActividadObraId; a.Cantidad = f.EsResumen ? 0 : cant; a.Cuadrilla = f.Cuadrilla; a.Nota = f.Nota;
            // Valor planeado: el de Project mientras no cambie el ítem ni la cantidad; si no, cantidad × precio del año del fin.
            var ligadoIgual = f.ItemId == f.ItemOriginal && cant == f.CantidadOriginal;
            a.ValorPlaneado = f.EsResumen ? null
                : f.ValorPlaneado is decimal vp && vp > 0 && ligadoIgual ? vp
                : f.ItemId is int it && cant > 0 && s.ItemPorId.TryGetValue(it, out var ip) ? cant * s.PrecioConAiu(ip, f.Fin.Year) : null;
            guardadas.Add((a, f));
        }
        var quedan = guardadas.Select(g => g.A).ToHashSet();
        var borradas = nueva ? new List<ActividadPrograma>() : viejas.Where(v => !quedan.Contains(v)).ToList();
        db.ActividadesPrograma.RemoveRange(borradas);
        await db.SaveChangesAsync();

        // Bitácora por UID frente a la versión editada.
        var motivoBit = nueva ? prog.Motivo : string.IsNullOrWhiteSpace(motivo) ? "Corrección" : motivo.Trim();
        // Predecesoras con el nombre de la tarea (los UID no le dicen nada al usuario).
        var nombres = Originales.Values.GroupBy(v => v.Uid).ToDictionary(g => g.Key, g => g.First().Nombre);
        foreach (var f in Filas) nombres[f.Uid] = f.Nombre;
        string? PredsLegibles(string? t) => t is null ? null : string.Join("; ", MotorPrograma.Leer(t).Select(v =>
            (nombres.TryGetValue(v.Uid, out var n) ? n : "UID " + v.Uid) + (v.Tipo == TipoVinculo.FC && v.Posposicion == 0 ? "" : " " + v.Tipo + (v.Posposicion == 0 ? "" : v.Posposicion.ToString("+0;-0")))));
        // Se compara contra lo leído al abrir la edición (en una corrección las entidades ya cambiaron).
        var antesUid = Originales.Values.GroupBy(v => v.Uid).ToDictionary(g => g.Key, g => g.First());
        void Bit(int? id, string campo, string? antes, string? despues)
        {
            if (antes != despues) db.CambiosPrograma.Add(new CambioPrograma { ProgramaTrabajoId = prog.Id, ActividadProgramaId = id, Campo = campo, Antes = antes, Despues = despues, Motivo = motivoBit, Usuario = usuario });
        }
        if (baseProg is not null)
        {
            foreach (var (a, f) in guardadas)
            {
                if (!antesUid.TryGetValue(f.Uid, out var o)) { Bit(a.Id, "Tarea nueva", null, a.Nombre); continue; }
                Bit(a.Id, "Nombre", o.Nombre, a.Nombre);
                Bit(a.Id, "Inicio", FormatoCostos.Fecha(o.Inicio), FormatoCostos.Fecha(a.Inicio));
                Bit(a.Id, "Fin", FormatoCostos.Fecha(o.Fin), FormatoCostos.Fecha(a.Fin));
                if (!a.EsResumen) Bit(a.Id, "Duración", o.Duracion + " d", a.Duracion + " d");
                Bit(a.Id, "Predecesoras", PredsLegibles(o.Predecesoras), PredsLegibles(a.Predecesoras));
                Bit(a.Id, "Nivel", o.Nivel.ToString(), a.Nivel.ToString());
            }
            var uidsNuevos = Filas.Select(f => f.Uid).ToHashSet();
            foreach (var v in Originales.Values.Where(v => !uidsNuevos.Contains(v.Uid)))
                Bit(null, "Tarea eliminada", $"{v.Nombre} ({FormatoCostos.Fecha(v.Inicio)} – {FormatoCostos.Fecha(v.Fin)})", null);
            if (calAntes != (prog.DiasLaborables, prog.Festivos) && !nueva) Bit(null, "Calendario", $"{calAntes.DiasLaborables} {calAntes.Festivos}".Trim(), $"{prog.DiasLaborables} {prog.Festivos}".Trim());
        }
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();
        Cambio = false;
        return prog;
    }

    // Estado de las tareas al abrir la edición (para la bitácora; en una corrección las entidades cambian).
    public sealed record Original(int Uid, string Nombre, DateTime Inicio, DateTime Fin, int Duracion, string? Predecesoras, int Nivel);
    public Dictionary<int, Original> Originales { get; } = new();
    public void TomarOriginales(IEnumerable<ActividadPrograma> tareas)
    {
        Originales.Clear();
        foreach (var a in tareas) Originales[a.Id] = new Original(a.Uid ?? 0, a.Nombre, a.Inicio, a.Fin, a.Duracion, a.Predecesoras, a.Nivel);
    }

    // Línea base = la versión elegida (solo una).
    public static async Task FijarLineaBaseAsync(AppDbContext db, int programaId, string? usuario)
    {
        var todos = await db.ProgramasTrabajo.ToListAsync();
        var anterior = todos.FirstOrDefault(p => p.LineaBase);
        foreach (var p in todos) p.LineaBase = p.Id == programaId;
        db.CambiosPrograma.Add(new CambioPrograma { ProgramaTrabajoId = programaId, Campo = "Línea base", Antes = anterior is null ? null : "v" + anterior.Version, Despues = "v" + todos.First(p => p.Id == programaId).Version, Usuario = usuario });
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();
    }
}
