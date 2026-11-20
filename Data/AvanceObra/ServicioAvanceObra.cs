using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using SigWeb.Data.Modelos3D;
using SigWeb.Models;

namespace SigWeb.Data.AvanceObra;

public class PataAvance
{
    public int ComponenteId { get; set; }
    public string Letra { get; set; } = "";     // A, B, C, D
    public CotasPata Cotas { get; set; } = new();
}

public class TorreAvance
{
    public int Id { get; set; }
    public string Nombre { get; set; } = "";
    public string? TipoTexto { get; set; }
    public string Familia { get; set; } = "";
    public int Cuerpo { get; set; }
    public string? ModeloUrl { get; set; }       // JSON de Modelos 3D; null = silueta aproximada
    public string? ModeloCodigo { get; set; }
    public double? PesoModeloKg { get; set; }    // meta.peso_total_kg del modelo (montaje por peso)
    public List<PataAvance> Patas { get; set; } = new();
    public CotasPata Cotas { get; set; } = new();   // de la torre (para dibujar si no tiene patas en Trazado)
    public double Lat { get; set; }
    public double Lon { get; set; }
    public int? TiroId { get; set; }
    public PerfilTorre? Perfil { get; set; }     // vista Perfil (null = sin abscisa o cota en la tabla)
}

// Tiro de tendido con sus torres en el orden de la línea.
public class TiroAvance
{
    public int Id { get; set; }
    public string Nombre { get; set; } = "";
    public List<int> Torres { get; set; } = new();
}

// Estado de la obra de una torre en una fecha, como lo dibuja el visor 3D (fracciones 0 a 1; -1 = no aplica).
public class Cuadro3D
{
    public string Fecha { get; set; } = "";
    public Dictionary<string, Dictionary<string, double>> Patas { get; set; } = new();   // "A" → exc, sol, niv, ace, con, rel
    public Dictionary<string, double> Torre { get; set; } = new();                        // pre, mon, ver, ves
}

// Foto del avance de obra (todas las torres) leída de una vez: la página la carga en Cargar()
// y el render solo consulta memoria (nunca Db).
public class InstantaneaAvance
{
    public List<ActividadObra> Actividades { get; set; } = new();        // por torre o por pata
    public List<ActividadObra> ActividadesTiro { get; set; } = new();    // tendido (por tiro)
    public List<TiroAvance> Tiros { get; set; } = new();
    public List<DateTime> FechasTendido { get; set; } = new();
    public List<TorreAvance> Torres { get; set; } = new();
    public List<DateTime> Fechas { get; set; } = new();     // fechas con registros (orden ascendente)
    public ConfigCotasAvance Cotas { get; set; } = new();
    public List<string> ClavesTabla { get; set; } = new();  // columnas de la tabla de torres y de las patas (para Ajustes)
    public string? ClaveTipoUsada { get; set; }             // columna de la que salió el tipo de torre
    public string? ClaveCimUsada { get; set; }              // columna de la que salió el tipo de cimentación
    public Dictionary<string, string?> ClavesPerfil { get; set; } = new();  // campo de perfil → columna usada
    public List<string> ClavesTorre { get; set; } = new();  // columnas de la tabla de torres (sin las de patas)

    // (tiro, actividad) → registros de tendido en orden de fecha.
    private readonly Dictionary<(int, int), List<RegistroAvanceTiro>> _regsTiro = new();

    internal void IndexarTiro(IEnumerable<RegistroAvanceTiro> regs)
    {
        foreach (var g in regs.GroupBy(r => (r.TiroId, r.ActividadObraId)))
            _regsTiro[g.Key] = g.OrderBy(r => r.Fecha).ThenBy(r => r.Id).ToList();
    }

    // Avance de una actividad de tendido en un tiro (0–100; null = no aplica). Entre dos registros el avance se
    // reparte por día (APROXIMACIÓN, como el montaje), para que el diorama avance día a día.
    public decimal? PctTiro(TiroAvance t, ActividadObra a, DateTime hasta)
    {
        if (!_regsTiro.TryGetValue((t.Id, a.Id), out var l)) return 0;
        var d = hasta.Date;
        RegistroAvanceTiro? u = null, n = null;
        foreach (var r in l) { if (r.Fecha.Date > d) { n = r; break; } u = r; }
        if (u is null) return 0;
        if (u.NoAplica) return null;
        if (n is null || n.NoAplica || n.AvanceAcumulado <= u.AvanceAcumulado) return Math.Clamp(u.AvanceAcumulado, 0, 100);
        var dias = (decimal)(n.Fecha.Date - u.Fecha.Date).TotalDays;
        var f = (decimal)(d - u.Fecha.Date).TotalDays / dias;
        return Math.Clamp(u.AvanceAcumulado + (n.AvanceAcumulado - u.AvanceAcumulado) * f, 0, 100);
    }

    // Lo registrado ese mismo día (para editar desde la vista Tendido).
    public RegistroAvanceTiro? RegistroTiroDelDia(TiroAvance t, ActividadObra a, DateTime dia) =>
        _regsTiro.TryGetValue((t.Id, a.Id), out var l) ? l.LastOrDefault(r => r.Fecha.Date == dia.Date) : null;

    // % del tendido de un tiro: Σ peso × avance / Σ pesos; con todos los pesos en 0, promedio simple.
    public decimal? PctTendido(TiroAvance t, DateTime hasta)
    {
        decimal suma = 0, pesos = 0; int n = 0; decimal simple = 0;
        foreach (var a in ActividadesTiro)
        {
            if (PctTiro(t, a, hasta) is not decimal v) continue;
            suma += a.Peso * v; pesos += a.Peso; simple += v; n++;
        }
        return pesos > 0 ? suma / pesos : n > 0 ? simple / n : null;
    }

    public (DateTime? Inicio, DateTime? Fin) InicioFinTiro(TiroAvance t, ActividadObra a)
    {
        if (!_regsTiro.TryGetValue((t.Id, a.Id), out var l)) return (null, null);
        var regs = l.Where(r => !r.NoAplica).ToList();
        var ini = regs.FirstOrDefault()?.Fecha;
        var fin = regs.Count > 0 && regs[^1].AvanceAcumulado >= 100 ? regs.First(r => r.AvanceAcumulado >= 100).Fecha : (DateTime?)null;
        return (ini, fin);
    }

    // Avance por efecto del diorama (0 a 1) de un tiro en una fecha: lo que dibuja la vista Tendido.
    public Dictionary<string, double> CuadroTiro(TiroAvance t, DateTime hasta)
    {
        var r = new Dictionary<string, double>();
        foreach (var g in ActividadesTiro.Where(a => a.Efecto != EfectoAvance3D.Ninguno).GroupBy(a => a.Efecto))
        {
            var v = g.Select(a => PctTiro(t, a, hasta)).Where(x => x is not null).Select(x => (double)x!.Value).ToList();
            r[g.Key.ToString()] = v.Count == 0 ? 1 : v.Max() / 100.0;   // N/A = no frena el dibujo
        }
        return r;
    }

    // (torre, actividad) cuyo % sale de las cantidades de Costos y presupuestos (no se edita aquí).
    public HashSet<(int Torre, int Act)> DesdeCantidades { get; set; } = new();
    public bool EsDeCantidades(int torre, int act) => DesdeCantidades.Contains((torre, act));

    // (torre, componente o 0, actividad) → registros en orden de fecha (sin programados).
    private readonly Dictionary<(int, int, int), List<RegistroAvance>> _regs = new();

    internal void Indexar(IEnumerable<RegistroAvance> regs)
    {
        foreach (var g in regs.GroupBy(r => (r.ElementoTrazadoId, r.ComponenteId ?? 0, r.ActividadObraId)))
            _regs[g.Key] = g.OrderBy(r => r.Fecha).ThenBy(r => r.Id).ToList();
    }

    private RegistroAvance? Ultimo(int torre, int comp, int act, DateTime hasta)
    {
        if (!_regs.TryGetValue((torre, comp, act), out var l)) return null;
        RegistroAvance? u = null;
        foreach (var r in l) { if (r.Fecha.Date > hasta.Date) break; u = r; }
        return u;
    }

    // Avance de una pata en una actividad (0–100) o null si no aplica. Lo registrado para toda la torre
    // en una actividad por pata vale para las patas que no tengan registro propio.
    public decimal? PctPata(TorreAvance t, PataAvance p, ActividadObra a, DateTime hasta)
    {
        if (p.Cotas.Parrilla && a.Efecto is EfectoAvance3D.Acero or EfectoAvance3D.Concreto) return null;   // SUPUESTO
        // Pila: la excavación (un anillo de 0,5 m por día) se reparte en partes iguales entre sus registros.
        if (p.Cotas.Pila && a.Efecto == EfectoAvance3D.Excavacion
            && (Interpolado(t.Id, p.ComponenteId, a.Id, hasta) ?? Interpolado(t.Id, 0, a.Id, hasta)) is decimal vi) return vi;
        // Gana el registro más reciente: el de la pata o el de "todas las patas" (a igual fecha, el de la pata).
        var rp = Ultimo(t.Id, p.ComponenteId, a.Id, hasta);
        var rt = Ultimo(t.Id, 0, a.Id, hasta);
        var r = rp is null ? rt : rt is null || rp.Fecha.Date >= rt.Fecha.Date ? rp : rt;
        if (r is null) return 0;
        return r.NoAplica ? null : Math.Clamp(r.AvanceAcumulado, 0, 100);
    }

    // Avance de la torre en una actividad (0–100); null = no aplica.
    public decimal? PctActividad(TorreAvance t, ActividadObra a, DateTime hasta)
    {
        if (a.Ambito == AmbitoAvance.Pata)
        {
            if (t.Patas.Count == 0)
            {
                var r0 = Ultimo(t.Id, 0, a.Id, hasta);
                return r0 is null ? 0 : r0.NoAplica ? null : Math.Clamp(r0.AvanceAcumulado, 0, 100);
            }
            var vals = t.Patas.Select(p => PctPata(t, p, a, hasta)).Where(v => v is not null).Select(v => v!.Value).ToList();
            return vals.Count == 0 ? null : vals.Average();
        }
        if (a.Efecto == EfectoAvance3D.Montaje && Interpolado(t.Id, 0, a.Id, hasta) is decimal vi) return vi;
        var r = Ultimo(t.Id, 0, a.Id, hasta);
        return r is null ? 0 : r.NoAplica ? null : Math.Clamp(r.AvanceAcumulado, 0, 100);
    }

    // Montaje y excavación de pilas (APROXIMACIÓN): entre dos registros el avance se reparte en partes iguales por día. Si el primero
    // es 0 % (inicio del F-054), el día de inicio ya suma su parte: con N días de inicio a fin, cada día = 1/N.
    private decimal? Interpolado(int torre, int comp, int act, DateTime hasta)
    {
        if (!_regs.TryGetValue((torre, comp, act), out var l)) return null;
        var d = hasta.Date;
        RegistroAvance? u = null, n = null;
        foreach (var r in l) { if (r.Fecha.Date > d) { n = r; break; } u = r; }
        if (u is null || n is null || u.NoAplica || n.NoAplica || n.AvanceAcumulado <= u.AvanceAcumulado) return null;
        var d0 = u.Fecha.Date; var dias = (decimal)(n.Fecha.Date - d0).TotalDays; var k = (decimal)(d - d0).TotalDays;
        var f = u.AvanceAcumulado == 0 ? (k + 1) / (dias + 1) : k / dias;
        return Math.Clamp(u.AvanceAcumulado + (n.AvanceAcumulado - u.AvanceAcumulado) * f, 0, 100);
    }

    // % ponderado de la torre: Σ peso × avance / Σ pesos de lo que aplica (100 % = todo lo sembrado terminado).
    public decimal? PctTorre(TorreAvance t, DateTime hasta)
    {
        decimal suma = 0, pesos = 0;
        foreach (var a in Actividades.Where(a => a.Peso > 0))
        {
            if (PctActividad(t, a, hasta) is not decimal v) continue;
            suma += a.Peso * v; pesos += a.Peso;
        }
        return pesos == 0 ? null : suma / pesos;
    }

    public decimal? PctGrupo(TorreAvance t, string grupo, DateTime hasta)
    {
        decimal suma = 0, pesos = 0;
        foreach (var a in Actividades.Where(a => a.Peso > 0 && a.Grupo == grupo))
        {
            if (PctActividad(t, a, hasta) is not decimal v) continue;
            suma += a.Peso * v; pesos += a.Peso;
        }
        return pesos == 0 ? null : suma / pesos;
    }

    // ¿Hay algún registro (sin N/A) hasta la fecha en las actividades del grupo (null = todas)? El F-054 marca el
    // inicio con un registro en 0 %: una torre puede estar iniciada con avance 0.
    public bool Iniciada(TorreAvance t, string? grupo, DateTime hasta)
    {
        var ids = Actividades.Where(a => grupo is null || a.Grupo == grupo).Select(a => a.Id).ToHashSet();
        return _regs.Any(k => k.Key.Item1 == t.Id && ids.Contains(k.Key.Item3) && k.Value.Any(r => !r.NoAplica && r.Fecha.Date <= hasta.Date));
    }

    // Avance de la actividad de montaje de la estructura (efecto Montaje), 0–100; null = no hay o no aplica.
    public decimal? PctMontajeEstructura(TorreAvance t, DateTime hasta)
    {
        var a = Actividades.FirstOrDefault(x => x.Efecto == EfectoAvance3D.Montaje && x.Ambito == AmbitoAvance.Torre);
        return a is null ? null : PctActividad(t, a, hasta);
    }

    public decimal? PctProyecto(DateTime hasta)
    {
        var l = Torres.Select(t => PctTorre(t, hasta)).Where(v => v is not null).Select(v => v!.Value).ToList();
        return l.Count == 0 ? null : l.Average();
    }

    // Registros (sin N/A) que cuentan para una actividad en una torre o en una pata, en orden de fecha.
    public List<RegistroAvance> RegistrosDe(TorreAvance t, ActividadObra a, PataAvance? p = null)
    {
        var claves = p is not null ? new List<int> { p.ComponenteId, 0 }
            : a.Ambito == AmbitoAvance.Pata && t.Patas.Count > 0 ? t.Patas.Select(x => x.ComponenteId).Append(0).ToList()
            : new List<int> { 0 };
        return claves.SelectMany(c => _regs.TryGetValue((t.Id, c, a.Id), out var l) ? l : Enumerable.Empty<RegistroAvance>())
            .Where(r => !r.NoAplica).OrderBy(r => r.Fecha).ThenBy(r => r.Id).ToList();
    }

    // Registro de un día exacto de una torre (comp 0) o pata, incluido N/A (edición en Torre por torre).
    public RegistroAvance? RegistroDelDia(TorreAvance t, int comp, ActividadObra a, DateTime dia) =>
        _regs.TryGetValue((t.Id, comp, a.Id), out var l) ? l.LastOrDefault(r => r.Fecha.Date == dia.Date) : null;

    // Registros que dan la fecha de inicio (el primero; el F-054 marca el inicio con 0 %) y la de fin
    // (el último que llegó a 100, si la actividad está terminada).
    public (RegistroAvance? Inicio, RegistroAvance? Fin) RegistrosInicioFin(TorreAvance t, ActividadObra a, PataAvance? p = null)
    {
        var regs = RegistrosDe(t, a, p);
        var ini = regs.FirstOrDefault();
        var v = p is not null ? PctPata(t, p, a, DateTime.MaxValue) : PctActividad(t, a, DateTime.MaxValue);
        var fin = v is decimal x && x >= 100 ? regs.LastOrDefault(r => r.AvanceAcumulado >= 100) : null;
        return (ini, fin);
    }

    public (DateTime? Inicio, DateTime? Fin) InicioFin(TorreAvance t, ActividadObra a, PataAvance? p = null)
    {
        var (ri, rf) = RegistrosInicioFin(t, a, p);
        return (ri?.Fecha, rf?.Fecha);
    }

    // Fechas con registros de una torre (para la línea de tiempo del visor).
    // Incluye cada día entre dos registros del montaje (avance repartido por día).
    public List<DateTime> FechasDe(TorreAvance t)
    {
        var f = _regs.Where(k => k.Key.Item1 == t.Id).SelectMany(k => k.Value).Select(r => r.Fecha.Date).ToHashSet();
        var series = new List<(int Comp, int Act)>();
        foreach (var a in Actividades.Where(a => a.Efecto == EfectoAvance3D.Montaje && a.Ambito == AmbitoAvance.Torre)) series.Add((0, a.Id));
        if (t.Patas.Any(p => p.Cotas.Pila))
            foreach (var a in Actividades.Where(a => a.Efecto == EfectoAvance3D.Excavacion))
            {
                series.Add((0, a.Id));
                foreach (var p in t.Patas.Where(p => p.Cotas.Pila)) series.Add((p.ComponenteId, a.Id));
            }
        foreach (var (comp, act) in series)
            if (_regs.TryGetValue((t.Id, comp, act), out var l))
                for (int i = 0; i + 1 < l.Count; i++)
                    if (!l[i].NoAplica && !l[i + 1].NoAplica && l[i + 1].AvanceAcumulado > l[i].AvanceAcumulado)
                        for (var d = l[i].Fecha.Date.AddDays(1); d < l[i + 1].Fecha.Date && f.Count < 5000; d = d.AddDays(1)) f.Add(d);
        return f.OrderBy(d => d).ToList();
    }

    // Lo que dibuja el visor en una fecha.
    public Cuadro3D Cuadro(TorreAvance t, DateTime hasta)
    {
        var c = new Cuadro3D { Fecha = hasta.ToString("dd/MM/yyyy") };
        double F(decimal? v) => v is decimal x ? (double)x / 100.0 : -1;
        double Efecto(EfectoAvance3D ef, Func<ActividadObra, decimal?> pct)
        {
            var acts = Actividades.Where(a => a.Efecto == ef).ToList();
            if (acts.Count == 0) return 0;
            var vals = acts.Select(pct).ToList();
            return vals.All(v => v is null) ? -1 : vals.Where(v => v is not null).Max(v => F(v));
        }
        var letras = t.Patas.Count > 0 ? t.Patas : new List<PataAvance> { new() { Letra = "A" }, new() { Letra = "B" }, new() { Letra = "C" }, new() { Letra = "D" } };
        foreach (var p in letras)
        {
            Func<ActividadObra, decimal?> pct = a => t.Patas.Count > 0 ? PctPata(t, p, a, hasta) : PctActividad(t, a, hasta);
            c.Patas[p.Letra] = new()
            {
                ["exc"] = Efecto(EfectoAvance3D.Excavacion, pct),
                ["sol"] = Efecto(EfectoAvance3D.Solado, pct),
                ["niv"] = Efecto(EfectoAvance3D.Nivelacion, pct),
                ["ace"] = Efecto(EfectoAvance3D.Acero, pct),
                ["con"] = Efecto(EfectoAvance3D.Concreto, pct),
                ["rel"] = Efecto(EfectoAvance3D.Relleno, pct),
            };
        }
        c.Torre["pre"] = Efecto(EfectoAvance3D.Prearmado, a => PctActividad(t, a, hasta));
        c.Torre["mon"] = Efecto(EfectoAvance3D.Montaje, a => PctActividad(t, a, hasta));
        c.Torre["ver"] = Efecto(EfectoAvance3D.Verticalidad, a => PctActividad(t, a, hasta));
        c.Torre["ves"] = Efecto(EfectoAvance3D.Vestida, a => PctActividad(t, a, hasta));
        return c;
    }
}

public static class ServicioAvanceObra
{
    // Áreas de "Actividades complementarias" del informe diario (las del F-054).
    public static readonly string[] AreasInforme = { "Técnico", "Gestión social", "Gestión ambiental", "Topografía", "Arqueología", "Calidad", "SST" };

    private static readonly Regex RxPata = new(@"^\s*pata\s*([A-Da-d])\b", RegexOptions.Compiled | RegexOptions.IgnoreCase);
    public static InstantaneaAvance Cargar(AppDbContext db, string webRoot)
    {
        var s = new InstantaneaAvance();
        var cfgEmp = db.Configuracion.AsNoTracking().FirstOrDefault();
        s.Cotas = ConfigCotasAvance.Leer(cfgEmp?.AvanceObraCotas);
        var acts = db.ActividadesObra.AsNoTracking().Where(a => a.Activo).OrderBy(a => a.Orden).ThenBy(a => a.Id).ToList();
        s.Actividades = acts.Where(a => a.Ambito != AmbitoAvance.Tiro).ToList();
        s.ActividadesTiro = acts.Where(a => a.Ambito == AmbitoAvance.Tiro).ToList();

        var elementos = db.ElementosTrazado.AsNoTracking().Where(e => !e.Eliminado).ToList();
        var camposT = db.CamposExtraTorre.AsNoTracking().Where(c => !c.Eliminado).ToList();
        s.ClavesTabla = camposT.Select(c => c.Clave).Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(c => c).ToList();
        var porTorre = camposT.GroupBy(c => c.ElementoTrazadoId)
            .ToDictionary(g => g.Key, g => (IReadOnlyDictionary<string, string>)g.GroupBy(c => c.Clave, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(x => x.Key, x => x.First().Valor, StringComparer.OrdinalIgnoreCase));

        s.ClavesTorre = s.ClavesTabla.ToList();
        s.ClavesPerfil = PerfilLinea.Claves(s.Cotas, s.ClavesTorre, porTorre);

        s.ClaveTipoUsada = TiposTorre.ClaveTipo(camposT.Select(c => c.Clave).Distinct(), s.Cotas);

        var patas = db.Componentes.AsNoTracking().Where(c => !c.Eliminado && !c.EsHojaDeVida).Select(c => new { c.Id, c.ElementoTrazadoId, c.Nombre }).ToList();
        var idsPatas = patas.Select(p => p.Id).ToList();
        var camposC = db.CamposExtraComponente.AsNoTracking().Where(c => !c.Eliminado && idsPatas.Contains(c.ComponenteId)).ToList()
            .GroupBy(c => c.ComponenteId)
            .ToDictionary(g => g.Key, g => (IReadOnlyDictionary<string, string>)g.GroupBy(c => c.Clave, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(x => x.Key, x => x.First().Valor, StringComparer.OrdinalIgnoreCase));
        // Columnas de las patas (tipo de cimentación por pata, cotas…) también se pueden elegir en Ajustes.
        s.ClavesTabla = s.ClavesTabla.Union(camposC.Values.SelectMany(d => d.Keys), StringComparer.OrdinalIgnoreCase)
            .OrderBy(c => c, StringComparer.OrdinalIgnoreCase).ToList();
        var claveCim = s.ClaveCimUsada = CotasAvanceObra.ClaveCimentacion(s.Cotas, s.ClavesTabla);
        var patasPorTorre = patas.GroupBy(p => p.ElementoTrazadoId).ToDictionary(g => g.Key, g => g.ToList());

        List<ConfigModelo> modelos;
        try { modelos = ConfigModelos3D.Cargar(webRoot, db); } catch { modelos = new(); }
        var vacio = new Dictionary<string, string>();
        var tipos = TiposTorre.Cargar(db, modelos, s.Cotas);

        foreach (var e in elementos)
        {
            var t = new TorreAvance { Id = e.Id, Nombre = string.IsNullOrWhiteSpace(e.Nombre) ? e.Codigo : e.Nombre!, Lat = e.Latitud, Lon = e.Longitud, TiroId = e.TiroId };
            var campos = porTorre.TryGetValue(e.Id, out var ct) ? ct : vacio;
            t.Perfil = PerfilLinea.De(campos, s.ClavesPerfil);
            // Tipo, familia, cuerpo y modelo: la misma regla de Línea 3D (TiposTorre). Sin modelo = la misma
            // geometría paramétrica por familia y cuerpo que dibuja Línea 3D.
            if (tipos.TryGetValue(e.Id, out var tp))
            {
                t.TipoTexto = tp.Texto; t.Familia = tp.Familia; t.Cuerpo = tp.Cuerpo;
                if (tp.Modelo is { } mo) { t.ModeloUrl = mo.Url; t.ModeloCodigo = mo.Codigo; t.PesoModeloKg = PesoModelo(webRoot, mo.Url); }
            }

            if (patasPorTorre.TryGetValue(e.Id, out var ps))
                foreach (var p in ps)
                {
                    var m = RxPata.Match(p.Nombre);
                    if (!m.Success) continue;
                    var letra = m.Groups[1].Value.ToUpperInvariant();
                    if (t.Patas.Any(x => x.Letra == letra)) continue;
                    t.Patas.Add(new PataAvance
                    {
                        ComponenteId = p.Id, Letra = letra,
                        Cotas = CotasAvanceObra.Resolver(s.Cotas, campos, camposC.TryGetValue(p.Id, out var cc) ? cc : null, claveCim)
                    });
                }
            t.Patas = t.Patas.OrderBy(p => p.Letra).ToList();
            t.Cotas = CotasAvanceObra.Resolver(s.Cotas, campos, null, claveCim);
            s.Torres.Add(t);
        }
        s.Torres = s.Torres.OrderBy(t => NumeroInicial(t.Nombre)).ThenBy(t => t.Nombre, StringComparer.OrdinalIgnoreCase).ToList();

        // Tiros: sus torres en el orden de la lista de torres (el de la línea).
        var tiros = db.Tiros.AsNoTracking().Where(x => !x.Eliminado).OrderBy(x => x.Orden).ThenBy(x => x.Nombre).ToList();
        s.Tiros = tiros.Select(x => new TiroAvance { Id = x.Id, Nombre = x.Nombre, Torres = s.Torres.Where(t => t.TiroId == x.Id).Select(t => t.Id).ToList() })
            .Where(x => x.Torres.Count > 0).ToList();
        var regsT = db.RegistrosAvanceTiro.AsNoTracking().ToList();
        s.IndexarTiro(regsT);
        s.FechasTendido = regsT.Select(r => r.Fecha.Date).Distinct().OrderBy(d => d).ToList();

        var regs = db.RegistrosAvance.AsNoTracking().Where(r => !r.Programado).ToList();
        // Costos y presupuestos: donde hay cantidades por torre (ítems ligados a la actividad y cantidad
        // prevista), el % sale de ellas y reemplaza a los registros de % (que quedan de histórico).
        var deCantidades = AvanceDesdeCantidades(db);
        if (deCantidades.Count > 0)
        {
            s.DesdeCantidades = deCantidades.Select(r => (r.ElementoTrazadoId, r.ActividadObraId)).ToHashSet();
            regs = regs.Where(r => !s.DesdeCantidades.Contains((r.ElementoTrazadoId, r.ActividadObraId))).Concat(deCantidades).ToList();
        }
        s.Indexar(regs);
        s.Fechas = regs.Select(r => r.Fecha.Date).Distinct().OrderBy(d => d).ToList();
        return s;
    }

    // % acumulado por torre (y pata) y actividad de obra a partir de las cantidades de Costos y
    // presupuestos: Σ ejecutado / Σ previsto de los ítems de pago ligados a la actividad (ItemPago.ActividadObraId),
    // un registro por día con avance. Registros en memoria (Id 0): no se editan desde Avance de obra.
    public static List<RegistroAvance> AvanceDesdeCantidades(AppDbContext db)
    {
        var items = db.ItemsPago.AsNoTracking().Where(i => i.ActividadObraId != null && !i.EsCapitulo)
            .Select(i => new { i.Id, Act = i.ActividadObraId!.Value }).ToList();
        if (items.Count == 0) return new();
        var actDe = items.ToDictionary(i => i.Id, i => i.Act);
        var ids = actDe.Keys.ToList();
        var previstas = db.CantidadesTorre.AsNoTracking().Where(c => ids.Contains(c.ItemPagoId) && c.CantidadPrevista > 0).ToList()
            .GroupBy(c => (c.ElementoTrazadoId, Act: actDe[c.ItemPagoId])).ToDictionary(g => g.Key, g => g.Sum(c => c.CantidadPrevista));
        if (previstas.Count == 0) return new();
        var cants = db.RegistrosCantidad.AsNoTracking().Where(r => ids.Contains(r.ItemPagoId) && r.ElementoTrazadoId != null)
            .Select(r => new { Torre = r.ElementoTrazadoId!.Value, r.ItemPagoId, r.Fecha, r.Cantidad }).ToList();
        var l = new List<RegistroAvance>();
        foreach (var g in cants.GroupBy(r => (r.Torre, Act: actDe[r.ItemPagoId])))
        {
            if (!previstas.TryGetValue(g.Key, out var prev)) continue;
            decimal acum = 0;
            foreach (var d in g.GroupBy(r => r.Fecha.Date).OrderBy(x => x.Key))
            {
                acum += d.Sum(r => r.Cantidad);
                l.Add(new RegistroAvance
                {
                    Fecha = d.Key, ElementoTrazadoId = g.Key.Torre, ActividadObraId = g.Key.Act, Cantidad = d.Sum(r => r.Cantidad),
                    AvanceAcumulado = Math.Round(Math.Clamp(acum / prev * 100m, 0, 100), 2), Observaciones = "Costos y presupuestos (cantidades)"
                });
            }
        }
        return l;
    }

    // Peso total del modelo (meta.peso_total_kg), leído una vez por archivo y fecha de modificación.
    private static readonly System.Collections.Concurrent.ConcurrentDictionary<string, (DateTime Mod, double? Kg)> _pesos = new();
    public static double? PesoModelo(string webRoot, string url)
    {
        try
        {
            var ruta = Path.Combine(webRoot, url);
            if (!File.Exists(ruta)) return null;
            var mod = File.GetLastWriteTimeUtc(ruta);
            if (_pesos.TryGetValue(ruta, out var c) && c.Mod == mod) return c.Kg;
            double? kg = null;
            using (var fs = File.OpenRead(ruta))
            using (var doc = System.Text.Json.JsonDocument.Parse(fs))
                if (doc.RootElement.TryGetProperty("meta", out var meta) && meta.TryGetProperty("peso_total_kg", out var p)
                    && p.ValueKind == System.Text.Json.JsonValueKind.Number && p.GetDouble() > 0)
                    kg = p.GetDouble();
            _pesos[ruta] = (mod, kg);
            return kg;
        }
        catch { return null; }
    }

    private static int NumeroInicial(string n)
    {
        var d = new string(n.TakeWhile(char.IsDigit).ToArray());
        return int.TryParse(d, out var v) ? v : int.MaxValue;
    }
}
