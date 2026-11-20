using System.Globalization;
using System.Text.RegularExpressions;

namespace SigWeb.Data.Costos;

// Motor de programación del PDT (tipo MS Project, en memoria): calendario laboral, predecesoras
// FC/CC/FF/CF con posposición en días laborables, fechas tempranas (pasada hacia adelante), fechas
// tardías y holgura total (pasada hacia atrás) → ruta crítica; resúmenes = mínimo/máximo de sus hijas.
// SUPUESTOS: una tarea sin predecesoras conserva su inicio (como "no comenzar antes de"); con
// predecesoras, el inicio lo fijan ellas; las predecesoras de una tarea resumen no se programan.
public sealed class CalendarioPdt
{
    private readonly bool[] _laboral = new bool[7];          // índice = DayOfWeek (0 = domingo)
    private readonly HashSet<DateTime> _festivos;
    private readonly DateTime _origen;
    private readonly List<DateTime> _dias = new();             // ordinal → fecha (solo días laborables)
    private readonly Dictionary<DateTime, int> _ordinal = new();

    // dias: 7 caracteres lunes…domingo, "1" = laboral (por defecto los 7).
    public CalendarioPdt(string? dias, IEnumerable<DateTime>? festivos, DateTime origen)
    {
        var d = string.IsNullOrWhiteSpace(dias) || dias.Length != 7 || !dias.Contains('1') ? "1111111" : dias;
        for (var i = 0; i < 7; i++) _laboral[(i + 1) % 7] = d[i] == '1';
        _festivos = (festivos ?? Enumerable.Empty<DateTime>()).Select(f => f.Date).ToHashSet();
        _origen = origen.Date.AddDays(-7);
        while (!EsLaboral(_origen)) _origen = _origen.AddDays(-1);
    }

    public bool EsLaboral(DateTime f) => _laboral[(int)f.DayOfWeek] && !_festivos.Contains(f.Date);

    // Ordinal del día laboral en o después de la fecha.
    public int Ordinal(DateTime f)
    {
        var x = f.Date < _origen ? _origen : f.Date;
        while (!EsLaboral(x)) x = x.AddDays(1);
        Extender(x);
        return _ordinal[x];
    }

    public DateTime Fecha(int ordinal)
    {
        if (ordinal < 0) return _origen.AddDays(ordinal);       // antes del origen: aproximado en días corridos
        while (_dias.Count <= ordinal) Extender((_dias.Count == 0 ? _origen : _dias[^1]).AddDays(Math.Max(8, ordinal - _dias.Count + 8)));
        return _dias[ordinal];
    }

    // Días laborables entre dos fechas, ambas incluidas (duración de una tarea).
    public int Duracion(DateTime inicio, DateTime fin) => fin.Date < inicio.Date ? 0 : Math.Max(1, OrdinalHasta(fin) - Ordinal(inicio) + 1);

    private int OrdinalHasta(DateTime f)     // último día laboral en o antes de la fecha
    {
        var x = f.Date;
        while (!EsLaboral(x) && x > _origen) x = x.AddDays(-1);
        return Ordinal(x);
    }

    private void Extender(DateTime hasta)
    {
        var x = _dias.Count == 0 ? _origen : _dias[^1].AddDays(1);
        for (; x <= hasta; x = x.AddDays(1))
            if (EsLaboral(x)) { _ordinal[x] = _dias.Count; _dias.Add(x); }
    }

    public static string Normalizar(string? dias) => string.IsNullOrWhiteSpace(dias) || dias.Length != 7 ? "1111111" : dias;

    public static List<DateTime> LeerFestivos(string? texto) =>
        (texto ?? "").Split(new[] { ',', ';', '\n', ' ' }, StringSplitOptions.RemoveEmptyEntries)
            .Select(t => DateTime.TryParseExact(t.Trim(), "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var f) ? f : (DateTime?)null)
            .Where(f => f is not null).Select(f => f!.Value.Date).Distinct().OrderBy(f => f).ToList();
}

public enum TipoVinculo { FC, CC, FF, CF }

public readonly record struct Vinculo(int Uid, TipoVinculo Tipo, int Posposicion)
{
    // Texto guardado: "45", "45CC", "45FC+3", "45FF-2" (por UID de la tarea, estable entre versiones).
    public override string ToString() => $"{Uid}{(Tipo == TipoVinculo.FC && Posposicion == 0 ? "" : Tipo.ToString())}{(Posposicion == 0 ? "" : Posposicion.ToString("+0;-0"))}";
}

public static class MotorPrograma
{
    private static readonly Regex RxVinculo = new(@"^\s*(\d+)\s*(FC|CC|FF|CF|FS|SS|SF)?\s*([+-]\s*\d+)?\s*(d|dia|dias|día|días|ed)?\s*$", RegexOptions.IgnoreCase | RegexOptions.Compiled);

    public static List<Vinculo> Leer(string? texto) => LeerCon(texto, n => n).Vinculos;

    // Lee "12;15CC+2" (separador , o ;). numero→uid traduce el n.º de fila del editor (o deja el UID).
    public static (List<Vinculo> Vinculos, List<string> Errores) LeerCon(string? texto, Func<int, int?> numeroAUid)
    {
        var l = new List<Vinculo>(); var err = new List<string>();
        foreach (var parte in (texto ?? "").Split(new[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries))
        {
            var m = RxVinculo.Match(parte);
            if (!m.Success) { err.Add(parte.Trim()); continue; }
            if (numeroAUid(int.Parse(m.Groups[1].Value)) is not int uid) { err.Add(parte.Trim()); continue; }
            var tipo = m.Groups[2].Value.ToUpperInvariant() switch
            {
                "CC" or "SS" => TipoVinculo.CC, "FF" => TipoVinculo.FF, "CF" or "SF" => TipoVinculo.CF, _ => TipoVinculo.FC
            };
            var lag = m.Groups[3].Success ? int.Parse(m.Groups[3].Value.Replace(" ", "")) : 0;
            if (!l.Any(v => v.Uid == uid)) l.Add(new Vinculo(uid, tipo, lag));
        }
        return (l, err);
    }

    public static string? Escribir(IEnumerable<Vinculo> v) => v.Any() ? string.Join(";", v) : null;

    // Tarea para el motor. Inicio/Duracion de entrada; Inicio/Fin/Holgura de salida.
    public sealed class Tarea
    {
        public int Uid; public int Nivel = 1; public bool EsResumen; public int Duracion;   // días laborables (0 = hito)
        public DateTime Inicio; public DateTime Fin;
        public List<Vinculo> Predecesoras = new();
        public int? Holgura; public bool Critica;
    }

    // Programa en el orden de la lista (el nivel define los resúmenes). Devuelve los errores (ciclos,
    // predecesoras inexistentes); las tareas en ciclo conservan su inicio.
    public static List<string> Programar(IList<Tarea> tareas, CalendarioPdt cal)
    {
        var errores = new List<string>();
        for (var i = 0; i < tareas.Count; i++)
            tareas[i].EsResumen = i + 1 < tareas.Count && tareas[i + 1].Nivel > tareas[i].Nivel;
        var hojas = tareas.Where(t => !t.EsResumen).ToList();
        var porUid = hojas.GroupBy(t => t.Uid).ToDictionary(g => g.Key, g => g.First());
        // Vínculos a resúmenes o a tareas que no existen se ignoran.
        var preds = hojas.ToDictionary(t => t, t => t.Predecesoras.Where(v => v.Uid != t.Uid && porUid.ContainsKey(v.Uid)).ToList());
        foreach (var t in hojas)
            foreach (var v in t.Predecesoras.Where(v => !porUid.ContainsKey(v.Uid)))
                errores.Add($"La predecesora {v.Uid} de la tarea {t.Uid} no existe o es una tarea resumen.");

        // Orden topológico (Kahn).
        var sucesoras = hojas.ToDictionary(t => t, _ => new List<(Tarea S, Vinculo V)>());
        var pendientes = hojas.ToDictionary(t => t, t => preds[t].Count);
        foreach (var t in hojas) foreach (var v in preds[t]) sucesoras[porUid[v.Uid]].Add((t, v));
        var cola = new Queue<Tarea>(hojas.Where(t => pendientes[t] == 0));
        var orden = new List<Tarea>();
        while (cola.Count > 0)
        {
            var t = cola.Dequeue(); orden.Add(t);
            foreach (var (s, _) in sucesoras[t]) if (--pendientes[s] == 0) cola.Enqueue(s);
        }
        var enCiclo = hojas.Where(t => pendientes[t] > 0).ToList();
        if (enCiclo.Count > 0) errores.Add("Predecesoras en ciclo (tareas " + string.Join(", ", enCiclo.Take(10).Select(t => t.Uid)) + "): conservan sus fechas.");

        // Adelante: inicio temprano en ordinales de días laborables. FinEf = ES + dur − 1 (hito: ES − 1).
        var es = new Dictionary<Tarea, int>();
        foreach (var t in enCiclo) es[t] = cal.Ordinal(t.Inicio);
        foreach (var t in orden)
        {
            var d = Math.Max(0, t.Duracion);
            int? ini = null;
            foreach (var v in preds[t])
            {
                var p = porUid[v.Uid]; var ep = es[p]; var dp = Math.Max(0, p.Duracion);
                var x = v.Tipo switch
                {
                    TipoVinculo.FC => ep + dp + v.Posposicion,
                    TipoVinculo.CC => ep + v.Posposicion,
                    TipoVinculo.FF => ep + dp - d + v.Posposicion,
                    _ => ep + v.Posposicion - d
                };
                ini = ini is int y ? Math.Max(y, x) : x;
            }
            es[t] = ini ?? cal.Ordinal(t.Inicio);
        }
        foreach (var t in hojas)
        {
            var d = Math.Max(0, t.Duracion);
            t.Inicio = cal.Fecha(es[t]);
            t.Fin = d == 0 ? t.Inicio : cal.Fecha(es[t] + d - 1);
        }

        // Atrás: inicio tardío y holgura total.
        if (hojas.Count > 0)
        {
            var finProyecto = hojas.Max(t => es[t] + Math.Max(0, t.Duracion) - 1);
            var ls = hojas.ToDictionary(t => t, t => finProyecto - Math.Max(0, t.Duracion) + 1);
            foreach (var t in Enumerable.Reverse(orden))
                foreach (var (s, v) in sucesoras[t])
                {
                    int dp = Math.Max(0, t.Duracion), ds = Math.Max(0, s.Duracion);
                    var x = v.Tipo switch
                    {
                        TipoVinculo.FC => ls[s] - v.Posposicion - dp,
                        TipoVinculo.CC => ls[s] - v.Posposicion,
                        TipoVinculo.FF => ls[s] + ds - dp - v.Posposicion,
                        _ => ls[s] + ds - v.Posposicion
                    };
                    if (x < ls[t]) ls[t] = x;
                }
            foreach (var t in hojas)
            {
                t.Holgura = enCiclo.Contains(t) ? null : ls[t] - es[t];
                t.Critica = t.Holgura is <= 0;
            }
        }

        // Resúmenes: mínimo y máximo de sus hijas (de abajo hacia arriba).
        for (var i = tareas.Count - 1; i >= 0; i--)
        {
            var r = tareas[i];
            if (!r.EsResumen) continue;
            DateTime? a = null, b = null;
            for (var j = i + 1; j < tareas.Count && tareas[j].Nivel > r.Nivel; j++)
            {
                var h = tareas[j];
                a = a is DateTime x0 && x0 <= h.Inicio ? x0 : h.Inicio;
                b = b is DateTime y0 && y0 >= h.Fin ? y0 : h.Fin;
            }
            if (a is DateTime ia && b is DateTime fb)
            {
                r.Inicio = ia; r.Fin = fb; r.Duracion = cal.Duracion(ia, fb);
                r.Holgura = null; r.Critica = false;
            }
        }
        return errores;
    }
}
