using System.Text.RegularExpressions;
using ClosedXML.Excel;
using Microsoft.EntityFrameworkCore;
using SigWeb.Models;

namespace SigWeb.Data.AvanceObra;

// Importador GENÉRICO del avance acumulado.
// Lee una hoja con una fila por torre y las actividades en columnas, con el encabezado en varias filas (celdas combinadas):
//   grupo (OBRA CIVIL) › actividad (EXCAVACIÓN) › pata («A ó 1», «Pata B», «3») › rol (Fecha ini · Avance · Fecha fin, o Fecha · Avance).
// Analizar() propone la estructura; el usuario liga cada actividad del archivo a una del módulo, la crea o la ignora (el mapeo se
// recuerda por proyecto en ConfigCotasAvance.MapeoImportacion); Aplicar() escribe los RegistroAvance con las mismas reglas del F-054:
// NA → no aplica · Fecha ini → 0 % ese día · avance → a la fecha fin (sin fecha fin: a la fecha de corte) · repetir = actualizar.
public static class ImportadorAvanceExcel
{
    public enum Rol { Valor, Inicio, Fin, Avance }

    public sealed class ColumnasPata
    {
        public string? Letra { get; init; }        // null = toda la torre
        public int? Inicio { get; set; }
        public int? Avance { get; set; }
        public int? Fin { get; set; }
        public int? Valor { get; set; }            // columna sin rol (p. ej. 1 / 0 / NA): se toma como avance
        public int? ColAvance => Avance ?? Valor;
    }

    public sealed class BloqueActividad
    {
        public required string Clave { get; init; }          // grupo|actividad normalizado: identidad para recordar el mapeo
        public required string Grupo { get; init; }
        public required string Actividad { get; init; }
        public List<ColumnasPata> Patas { get; } = new();
        public bool PorPata => Patas.Any(p => p.Letra is not null);
        public int Datos { get; set; }                       // celdas con avance o fecha en las filas de torres
        public bool EsAvance { get; set; } = true;           // false = columna de datos (texto, SI/NO, cantidades): no es avance
        public string Ejemplo { get; set; } = "";
        public decimal? PesoSugerido { get; set; }
        public string Columnas { get; set; } = "";           // "BJ–BV" para mostrar
    }

    public sealed class Estructura
    {
        public List<string> Hojas { get; } = new();
        public string Hoja { get; set; } = "";
        public int FilaTorre { get; set; }
        public int ColTorre { get; set; }
        public int PrimeraFila { get; set; }
        public int UltimaFila { get; set; }
        public int Torres { get; set; }
        public DateTime? MaxFecha { get; set; }
        public List<BloqueActividad> Bloques { get; } = new();
        public string? Error { get; set; }
    }

    // ---------- Análisis ----------

    public static Estructura Analizar(byte[] libro, string? hoja = null)
    {
        var e = new Estructura();
        using var wb = SigWeb.Data.Excel.LibroExcel.Abrir(libro);
        e.Hojas.AddRange(wb.Worksheets.Where(w => w.Visibility == XLWorksheetVisibility.Visible).Select(w => w.Name));
        IXLWorksheet? ws = hoja is null ? null : wb.Worksheets.FirstOrDefault(w => w.Name == hoja);
        (int f, int c) torre = default;
        if (ws is null)
            foreach (var w in wb.Worksheets.Where(w => w.Visibility == XLWorksheetVisibility.Visible))
                if (BuscarTorre(w) is { } t) { ws = w; torre = t; break; }
        ws ??= wb.Worksheets.FirstOrDefault();
        if (ws is null) { e.Error = "El libro no tiene hojas."; return e; }
        e.Hoja = ws.Name;
        if (torre == default && BuscarTorre(ws) is { } t2) torre = t2;
        if (torre == default) { e.Error = $"No se encontró la columna de torre (encabezado «TORRE») en la hoja «{ws.Name}»."; return e; }
        e.FilaTorre = torre.f; e.ColTorre = torre.c;

        var ultimaFila = Math.Min(ws.LastRowUsed()?.RowNumber() ?? 0, 5000);
        var ultimaCol = Math.Min(ws.LastColumnUsed()?.ColumnNumber() ?? 0, 800);

        // Primera fila de datos: debajo del encabezado, la primera con torre que no sea parte de una celda combinada del encabezado.
        int primera = torre.f + 1;
        while (primera <= ultimaFila)
        {
            var cel = ws.Cell(primera, torre.c);
            bool delEncabezado = cel.IsMerged() && cel.MergedRange().FirstCell().Address.RowNumber <= torre.f;
            if (!delEncabezado && Texto(cel).Length > 0 && !EsRol(Norm(Texto(cel)))) break;
            primera++;
        }
        e.PrimeraFila = primera;
        // Última fila: antes de "TOTAL…", "AVANCE GENERAL…", "RENDIMIENTO…".
        int ultima = primera - 1;
        for (int f = primera; f <= ultimaFila; f++)
        {
            var t = Norm(Texto(ws.Cell(f, torre.c)));
            if (t.Length == 0) continue;
            if (t.StartsWith("total") || t.Contains("avance general") || t.Contains("rendimiento")) break;
            ultima = f; e.Torres++;
        }
        e.UltimaFila = ultima;

        // Filas del encabezado: las que tienen texto entre la fila 1 y la primera de datos (una fila solo con números = pesos).
        var filasEnc = Enumerable.Range(1, Math.Max(0, primera - 1))
            .Where(f => Enumerable.Range(1, ultimaCol).Any(c => { var v = ValorCombinado(ws, f, c); return v.Length > 0 && !double.TryParse(v, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out _); })).ToList();

        var porClave = new Dictionary<string, BloqueActividad>();
        var orden = new List<BloqueActividad>();
        for (int c = 1; c <= ultimaCol; c++)
        {
            if (c == torre.c) continue;
            var ruta = new List<string>();
            foreach (var f in filasEnc)
            {
                var v = Limpiar(ValorCombinado(ws, f, c));
                if (v.Length > 0 && (ruta.Count == 0 || Norm(ruta[^1]) != Norm(v))) ruta.Add(v);
            }
            if (ruta.Count == 0) continue;
            // Columnas de resumen o de pesos: no son avance.
            if (ruta.Any(r => { var n = Norm(r); return n == "total" || n.StartsWith("avance total") || n == "pesos" || n == "%" && ruta.Count > 1 && Norm(ruta[^2]) == "total"; })) continue;

            var rol = Rol.Valor; string? letra = null;
            var resto = new List<string>(ruta);
            if (resto.Count > 0 && RolDe(Norm(resto[^1])) is { } r0) { rol = r0; resto.RemoveAt(resto.Count - 1); }
            if (resto.Count > 0 && PataDe(Norm(resto[^1])) is { } l0) { letra = l0; resto.RemoveAt(resto.Count - 1); }
            if (resto.Count == 0) continue;
            if (Norm(resto[^1]) == "tipo") continue;     // "TIPO" (de cimentación, de cable): dato, no avance
            var grupo = resto.Count > 1 ? resto[0] : resto[0];
            var actividad = resto.Count > 2 ? string.Join(" · ", resto.Skip(1)) : resto[^1];
            var clave = Norm(grupo) + "|" + Norm(actividad);
            if (!porClave.TryGetValue(clave, out var b))
            {
                b = new BloqueActividad { Clave = clave, Grupo = grupo, Actividad = actividad };
                porClave[clave] = b; orden.Add(b);
            }
            var pc = b.Patas.FirstOrDefault(p => p.Letra == letra);
            if (pc is null) { pc = new ColumnasPata { Letra = letra }; b.Patas.Add(pc); }
            switch (rol)
            {
                case Rol.Inicio: pc.Inicio ??= c; break;
                case Rol.Fin: pc.Fin ??= c; break;
                case Rol.Avance: pc.Avance ??= c; break;
                default: pc.Valor ??= c; break;
            }
            b.Columnas = b.Columnas.Length == 0 ? Letra(c) : b.Columnas.Split('–')[0] + "–" + Letra(c);
        }

        // Datos, ejemplo y si es avance (columna sin fechas: valores 0–1, % o NA en al menos el 80 %).
        var pesos = PesosPorGrupo(ws, filasEnc, ultimaCol, primera);
        foreach (var b in orden)
        {
            int numEnRango = 0, otros = 0;
            foreach (var p in b.Patas)
                for (int f = primera; f <= ultima; f++)
                {
                    foreach (var col in new[] { p.Inicio, p.Fin })
                        if (col is int cc && LeerFecha(ws.Cell(f, cc)) is DateTime d)
                        { b.Datos++; if (d.Year >= 2015 && (e.MaxFecha is null || d > e.MaxFecha) && d <= DateTime.Today.AddDays(1)) e.MaxFecha = d.Date; }
                    if (p.ColAvance is int ca)
                    {
                        var cel = ws.Cell(f, ca);
                        var t = Texto(cel).Trim();
                        if (t.Length == 0) continue;
                        b.Datos++;
                        if (b.Ejemplo.Length == 0) b.Ejemplo = t.Length > 20 ? t[..20] : t;
                        var av = LeerAvance(cel, out var na);
                        if (na || av is not null && (t.EndsWith('%') || Numero(cel) is double x && x is >= 0 and <= 1.0001)) numEnRango++; else otros++;
                    }
                }
            bool tieneFechas = b.Patas.Any(p => p.Inicio is not null || p.Fin is not null);
            b.EsAvance = tieneFechas || b.Patas.Any(p => p.Avance is not null) || (numEnRango + otros > 0 && numEnRango >= 0.8 * (numEnRango + otros));
            var g = Norm(b.Grupo);
            var pg = pesos.FirstOrDefault(x => g.StartsWith(x.Key) || x.Key.StartsWith(g.Split(' ')[0]));
            if (pg.Key is not null) b.PesoSugerido = pg.Value;
        }
        // Peso del grupo repartido entre sus actividades de avance (sugerencia: el usuario lo cambia).
        foreach (var grp in orden.Where(b => b.EsAvance && b.PesoSugerido is not null).GroupBy(b => Norm(b.Grupo)))
        {
            var n = grp.Count();
            foreach (var b in grp) b.PesoSugerido = Math.Round(b.PesoSugerido!.Value / n, 4);
        }
        e.Bloques.AddRange(orden);
        return e;
    }

    private static (int f, int c)? BuscarTorre(IXLWorksheet ws)
    {
        var ultimaCol = Math.Min(ws.LastColumnUsed()?.ColumnNumber() ?? 0, 60);
        for (int f = 1; f <= 20; f++)
            for (int c = 1; c <= ultimaCol; c++)
            {
                var t = Norm(Texto(ws.Cell(f, c)));
                if (t is "torre" or "torres" or "no. torre" or "no torre" or "n° torre" or "numero de torre" or "codigo torre" or "codigo de torre" or "estructura") return (f, c);
            }
        return null;
    }

    // Pesos por grupo si la hoja trae un bloque "PESOS" (fila con el valor y debajo el nombre del grupo) o un número sobre el grupo.
    private static Dictionary<string, decimal> PesosPorGrupo(IXLWorksheet ws, List<int> filasEnc, int ultimaCol, int primera)
    {
        var r = new Dictionary<string, decimal>();
        for (int c = 1; c <= ultimaCol; c++)
        {
            var col = filasEnc.Select(f => ValorCombinado(ws, f, c)).ToList();
            if (!col.Any(v => Norm(v) == "pesos")) continue;
            for (int i = 0; i + 1 < col.Count; i++)
                if (decimal.TryParse(col[i], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var p) && p > 0 && p <= 1)
                {
                    var nombre = Norm(col[i + 1]);
                    if (nombre.Length > 0 && nombre != "total") r[nombre switch { "spt" => "sistema de puesta a tierra", "obras comp." => "obras complementarias", _ => nombre.TrimEnd('.') }] = p;
                }
        }
        return r;
    }

    // ---------- Aplicar ----------

    public sealed record Destino(int? ActividadId, bool Crear, string Grupo, string Nombre, decimal Peso);

    public sealed class Resultado
    {
        public DateTime FechaCorte { get; set; }
        public int TorresLeidas { get; set; }
        public int TorresCasadas { get; set; }
        public int Nuevos { get; set; }
        public int Actualizados { get; set; }
        public int InformesCreados { get; set; }
        public int ActividadesCreadas { get; set; }
        public int EfectosAsignados { get; set; }
        public int FechasInvalidas { get; set; }
        public List<string> TorresSinCasar { get; } = new();
        public List<string> Aproximadas { get; } = new();
        public List<string> TorresSinPatas { get; } = new();
    }

    public const string Marca = "Avance acumulado";

    public static Resultado Aplicar(AppDbContext db, byte[] libro, Estructura e, IReadOnlyDictionary<string, Destino> destinos, DateTime fechaCorte)
    {
        var res = new Resultado { FechaCorte = fechaCorte.Date };
        using var wb = SigWeb.Data.Excel.LibroExcel.Abrir(libro);
        var ws = wb.Worksheets.First(w => w.Name == e.Hoja);

        // Actividades nuevas (al final del orden).
        var orden = db.ActividadesObra.Select(a => (int?)a.Orden).Max() ?? 0;
        var idDe = new Dictionary<string, int>();
        foreach (var b in e.Bloques)
        {
            if (!destinos.TryGetValue(b.Clave, out var d)) continue;
            if (d.ActividadId is int id) { idDe[b.Clave] = id; continue; }
            if (!d.Crear) continue;
            var nueva = new ActividadObra
            {
                Grupo = string.IsNullOrWhiteSpace(d.Grupo) ? b.Grupo : d.Grupo.Trim(), Nombre = string.IsNullOrWhiteSpace(d.Nombre) ? b.Actividad : d.Nombre.Trim(),
                Ambito = b.PorPata ? AmbitoAvance.Pata : AmbitoAvance.Torre, Peso = d.Peso, Orden = ++orden, Activo = true,
            };
            db.ActividadesObra.Add(nueva); db.SaveChanges();
            idDe[b.Clave] = nueva.Id; res.ActividadesCreadas++;
        }
        // Sin esto las actividades nuevas quedaban «Solo en el %» y el visor no dibujaba la torre.
        res.EfectosAsignados = SembradorActividadesObra.AsignarEfectosFaltantes(db);

        // Torres del Trazado por código/nombre/alias (mismas reglas del F-054).
        var elementos = db.ElementosTrazado.AsNoTracking().Where(x => !x.Eliminado).Select(x => new { x.Id, x.Codigo, x.Nombre }).ToList();
        var porClave = new Dictionary<string, int>(); var porRaiz = new Dictionary<string, List<int>>();
        foreach (var x in elementos)
            foreach (var n in new[] { x.Nombre, x.Codigo }.Where(s => !string.IsNullOrWhiteSpace(s)).Distinct())
            {
                var k = ImportadorF054.ClaveTorre(n!); if (k.Length == 0) continue;
                porClave.TryAdd(k, x.Id);
                var rz = ImportadorF054.Raiz(k);
                if (!porRaiz.TryGetValue(rz, out var l)) porRaiz[rz] = l = new();
                if (!l.Contains(x.Id)) l.Add(x.Id);
            }
        var vivos = elementos.Select(x => x.Id).ToHashSet();
        foreach (var a in db.AliasTorres.AsNoTracking().Select(a => new { a.Texto, a.ElementoTrazadoId }).ToList())
            if (vivos.Contains(a.ElementoTrazadoId) && ImportadorF054.ClaveTorre(a.Texto) is { Length: > 0 } k) porClave.TryAdd(k, a.ElementoTrazadoId);
        var nombreDe = elementos.ToDictionary(x => x.Id, x => string.IsNullOrWhiteSpace(x.Nombre) ? x.Codigo : x.Nombre!);
        var rxPata = new Regex(@"^\s*pata\s*([A-Da-d])\b", RegexOptions.IgnoreCase);
        var patas = db.Componentes.AsNoTracking().Where(c => !c.Eliminado && !c.EsHojaDeVida).Select(c => new { c.Id, c.ElementoTrazadoId, c.Nombre }).ToList()
            .Select(c => (c.Id, c.ElementoTrazadoId, M: rxPata.Match(c.Nombre))).Where(c => c.M.Success)
            .GroupBy(c => c.ElementoTrazadoId)
            .ToDictionary(g => g.Key, g => g.GroupBy(c => c.M.Groups[1].Value.ToUpperInvariant()).ToDictionary(x => x.Key, x => x.First().Id));

        var tope = res.FechaCorte;
        DateTime? Fecha(int fila, int? col, ref bool inv)
        {
            if (col is not int c) return null;
            var cel = ws.Cell(fila, c);
            var d = LeerFecha(cel);
            if (d is null) { if (Texto(cel).Trim().Length > 0 && Norm(Texto(cel)) is not ("na" or "n/a")) inv = true; return null; }
            if (d.Value.Year < 2015 || d.Value.Date > tope) { inv = true; return null; }
            return d.Value.Date;
        }

        var planes = new List<(int Torre, int? Comp, int Act, DateTime Fecha, decimal Avance, bool NA)>();
        for (int f = e.PrimeraFila; f <= e.UltimaFila; f++)
        {
            var nom = Texto(ws.Cell(f, e.ColTorre)).Trim();
            if (nom.Length == 0) continue;
            res.TorresLeidas++;
            var clave = ImportadorF054.ClaveTorre(nom);
            int torre;
            if (porClave.TryGetValue(clave, out var id)) torre = id;
            else if (porRaiz.TryGetValue(ImportadorF054.Raiz(clave), out var ids) && ids.Count == 1) { torre = ids[0]; res.Aproximadas.Add($"{nom} → {nombreDe[torre]}"); }
            else { res.TorresSinCasar.Add(nom); continue; }
            res.TorresCasadas++;
            var pt = patas.TryGetValue(torre, out var pp) ? pp : null;

            foreach (var b in e.Bloques)
            {
                if (!idDe.TryGetValue(b.Clave, out var act)) continue;
                foreach (var p in b.Patas)
                {
                    int? comp = null;
                    if (p.Letra is not null)
                    {
                        if (pt is null || !pt.TryGetValue(p.Letra, out var cid)) { if (!res.TorresSinPatas.Contains(nombreDe[torre])) res.TorresSinPatas.Add(nombreDe[torre]); continue; }
                        comp = cid;
                    }
                    bool inv = false;
                    var ini = Fecha(f, p.Inicio, ref inv);
                    var fin = Fecha(f, p.Fin, ref inv);
                    decimal? av = null; bool na = false;
                    if (p.ColAvance is int ca) av = LeerAvance(ws.Cell(f, ca), out na);
                    if (inv) res.FechasInvalidas++;
                    if (na) { planes.Add((torre, comp, act, tope, 0, true)); continue; }
                    // Solo fecha fin sin avance = terminada ese día; solo fecha ini = iniciada.
                    if (av is null && fin is not null && p.ColAvance is null) av = 100;
                    if (av is null && ini is null) continue;
                    var v = av ?? 0;
                    if (v <= 0 && ini is null) continue;                  // no iniciada
                    if (fin is DateTime ff)
                    {
                        if (ini is DateTime i0 && i0 < ff) planes.Add((torre, comp, act, i0, 0, false));
                        planes.Add((torre, comp, act, ff, v, false));
                    }
                    else
                    {
                        if (ini is DateTime i0 && i0 < tope) planes.Add((torre, comp, act, i0, 0, false));
                        planes.Add((torre, comp, act, tope, v, false));
                    }
                }
            }
        }
        planes = planes.GroupBy(p => (p.Torre, p.Comp, p.Act, p.Fecha)).Select(g => g.Last()).ToList();

        // Informes por fecha (los crea si faltan).
        var fechas = planes.Select(p => p.Fecha).Append(tope).Distinct().ToList();
        var informes = db.InformesDiarios.Where(i => fechas.Contains(i.Fecha.Date)).ToList()
            .GroupBy(i => i.Fecha.Date).ToDictionary(g => g.Key, g => g.OrderBy(i => i.Id).First());
        foreach (var d in fechas.Where(d => !informes.ContainsKey(d)))
        {
            var i = new InformeDiario { Fecha = d, Observaciones = d == tope ? $"Corte del avance acumulado ({e.Hoja})." : "Reconstruido del avance acumulado." };
            db.InformesDiarios.Add(i); informes[d] = i; res.InformesCreados++;
        }
        db.SaveChanges();

        // Registros: se actualiza el de la misma torre, pata, actividad y fecha; si no, se crea.
        var torres = planes.Select(p => p.Torre).Distinct().ToList();
        var existentes = db.RegistrosAvance.Where(r => torres.Contains(r.ElementoTrazadoId) && !r.Programado).ToList()
            .GroupBy(r => (r.ElementoTrazadoId, r.ComponenteId, r.ActividadObraId, r.Fecha.Date))
            .ToDictionary(g => g.Key, g => g.OrderByDescending(r => r.Id).First());
        foreach (var p in planes)
        {
            var pct = Math.Round(Math.Clamp(p.Avance, 0, 100), 2);
            if (existentes.TryGetValue((p.Torre, p.Comp, p.Act, p.Fecha), out var r))
            {
                if (r.AvanceAcumulado == pct && r.NoAplica == p.NA) continue;
                r.AvanceAcumulado = pct; r.NoAplica = p.NA; res.Actualizados++;
            }
            else
            {
                db.RegistrosAvance.Add(new RegistroAvance
                {
                    InformeDiarioId = informes[p.Fecha].Id, Fecha = p.Fecha, ElementoTrazadoId = p.Torre, ComponenteId = p.Comp,
                    ActividadObraId = p.Act, AvanceAcumulado = pct, NoAplica = p.NA, Observaciones = Marca,
                });
                res.Nuevos++;
            }
        }
        db.SaveChanges();
        db.ChangeTracker.Clear();
        return res;
    }

    // ---------- Celdas y textos ----------

    private static string Texto(IXLCell c) => ImportadorF054.Texto(c);
    private static double? Numero(IXLCell c) => ImportadorF054.Numero(c);
    private static DateTime? LeerFecha(IXLCell c) => ImportadorF054.LeerFecha(c);
    private static decimal? LeerAvance(IXLCell c, out bool na) => ImportadorF054.LeerAvance(c, out na);
    public static string Norm(string? s) => ImportadorF054.Norm(s);

    private static string ValorCombinado(IXLWorksheet ws, int f, int c)
    {
        var cel = ws.Cell(f, c);
        if (cel.IsMerged()) cel = cel.MergedRange().FirstCell();
        return Texto(cel).Trim();
    }

    // Quita aclaraciones entre paréntesis ("(% de avance o NA)") y saltos de línea, para mostrar y comparar.
    private static string Limpiar(string s)
    {
        var t = Regex.Replace(s.Replace('\n', ' ').Replace('\r', ' '), @"\s*\(.*?(\)|$)", "");
        return Regex.Replace(t, @"\s+", " ").Trim();
    }

    private static readonly Regex RxPata = new(@"^(pata\s*)?([a-d])(\s*(o|/|-)\s*[1-4])?$|^(pata\s*)?([1-4])$", RegexOptions.Compiled);
    private static string? PataDe(string n)
    {
        var m = RxPata.Match(n);
        if (!m.Success) return null;
        if (m.Groups[2].Success) return m.Groups[2].Value.ToUpperInvariant();
        return ((char)('A' + int.Parse(m.Groups[6].Value) - 1)).ToString();
    }

    private static Rol? RolDe(string n) => n switch
    {
        "fecha ini" or "fecha inicio" or "fecha de inicio" or "inicio" or "f. inicio" or "f inicio" => Rol.Inicio,
        "fecha fin" or "fecha final" or "fecha de fin" or "fecha terminacion" or "fin" or "f. fin" or "f fin" or "fecha" => Rol.Fin,
        "avance" or "%" or "% avance" or "% de avance" or "porcentaje" or "avance %" => Rol.Avance,
        _ => null,
    };
    private static bool EsRol(string n) => RolDe(n) is not null;

    private static string Letra(int c) => XLHelper.GetColumnLetterFromNumber(c);
}
