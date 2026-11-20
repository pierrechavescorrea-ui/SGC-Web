using System.Globalization;
using Microsoft.EntityFrameworkCore;
using SigWeb.Data.AvanceObra;
using SigWeb.Models;

namespace SigWeb.Data.Costos;

// Estado de las torres (hoja "C. CONTROL" del libro: CONTROL DE AVANCE). El estado de cada torre es un
// EstadoTorreManual con fecha (historial); los estados posibles son el catálogo "Estado de torre" (nombre +
// colores, editables en Catálogos). Aquí: los estados de fábrica con sus colores, en qué etapa cuenta cada
// uno (resumen "Ejecución de torres") y la carga inicial del 01/11/2026.
public static class EstadoTorres
{
    public const string Catalogo = "Estado de torre";
    public const string NotaCarga = "Carga inicial: C. CONTROL 01/11/2026";

    public enum Etapa { Restriccion, Pendiente, Disponible, CivilProceso, CivilTerminado, CambioCimentacion, MontajeProceso, MontajeTerminado, MontajeEntregado, TendidoProceso, TendidoTerminado }

    public sealed record Def(string Nombre, string Fondo, string Texto, Etapa Etapa);

    // Mismo orden y colores que la leyenda del Excel, afinados (en proceso = mismo color con rayas en la vista).
    public static readonly Def[] Fabrica =
    {
        new("RESTRICCIÓN", "#E5484D", "#FFFFFF", Etapa.Restriccion),
        new("MOD2", "#8A9BB4", "#FFFFFF", Etapa.Pendiente),
        new("MOD4", "#F7D774", "#4A3A00", Etapa.Pendiente),
        new("INICIO 2027", "#7A5B14", "#FFFFFF", Etapa.Pendiente),
        new("DISPONIBLE", "#FFFFFF", "#33373D", Etapa.Disponible),
        new("CIVIL EN PROCESO", "#F5B43C", "#3D2800", Etapa.CivilProceso),
        new("CIVIL TERMINADO", "#F5B43C", "#3D2800", Etapa.CivilTerminado),
        new("CAMBIO DE CIMENTACIÓN", "#F5B43C", "#1D4ED8", Etapa.CambioCimentacion),
        new("MONTAJE EN PROCESO", "#7CC85A", "#123A08", Etapa.MontajeProceso),
        new("MONTAJE TERMINADO", "#7CC85A", "#123A08", Etapa.MontajeTerminado),
        new("MONTAJE ENTREGADO", "#7B3FB0", "#FFFFFF", Etapa.MontajeEntregado),
        new("TENDIDO EN PROCESO", "#22B8E6", "#06324A", Etapa.TendidoProceso),
        new("TENDIDO TERMINADO", "#22B8E6", "#06324A", Etapa.TendidoTerminado),
    };

    public static string Clave(string? s) => ImportadorF054.Norm(s).ToUpperInvariant();

    // Nombre visible de cada etapa (se guarda en ItemCatalogo.Descripcion: así un estado renombrado sigue contando igual).
    public static readonly Dictionary<Etapa, string> NombresEtapa = new()
    {
        [Etapa.Restriccion] = "Restricción", [Etapa.Pendiente] = "Pendiente / sin iniciar", [Etapa.Disponible] = "Disponible",
        [Etapa.CivilProceso] = "Obra civil en proceso", [Etapa.CivilTerminado] = "Obra civil terminada",
        [Etapa.CambioCimentacion] = "Cambio de cimentación", [Etapa.MontajeProceso] = "Montaje en proceso",
        [Etapa.MontajeTerminado] = "Montaje terminado", [Etapa.MontajeEntregado] = "Montaje entregado",
        [Etapa.TendidoProceso] = "Tendido en proceso", [Etapa.TendidoTerminado] = "Tendido terminado",
    };

    public static Etapa? EtapaDeDescripcion(string? d) =>
        NombresEtapa.Where(x => Clave(x.Value) == Clave(d)).Select(x => (Etapa?)x.Key).FirstOrDefault();

    // Etapa de un estado: la elegida en el catálogo (Descripcion) o, si no, por su nombre.
    public static Etapa EtapaDe(string? nombre, IEnumerable<ItemCatalogo>? catalogo)
    {
        if (catalogo?.FirstOrDefault(c => Clave(c.Nombre) == Clave(nombre)) is { } c && EtapaDeDescripcion(c.Descripcion) is Etapa e) return e;
        return EtapaDe(nombre);
    }

    // Orden de los estados (ConfiguracionCostos.OrdenEstadosTorre = ids separados por coma); los demás, por Id.
    public static List<ItemCatalogo> Ordenar(IEnumerable<ItemCatalogo> items, string? orden)
    {
        var pos = (orden ?? "").Split(',', StringSplitOptions.RemoveEmptyEntries).Select((x, i) => (Ok: int.TryParse(x, out var id), id, i))
            .Where(x => x.Ok).GroupBy(x => x.id).ToDictionary(g => g.Key, g => g.First().i);
        return items.OrderBy(c => pos.TryGetValue(c.Id, out var i) ? i : int.MaxValue).ThenBy(c => c.Id).ToList();
    }

    // Etapa de un estado por su nombre (los de fábrica; uno nuevo del catálogo se deduce por palabras).
    public static Etapa EtapaDe(string? nombre)
    {
        var k = Clave(nombre);
        var f = Fabrica.FirstOrDefault(d => Clave(d.Nombre) == k);
        if (f is not null) return f.Etapa;
        bool proc = k.Contains("PROCESO");
        if (k.Contains("TENDIDO")) return proc ? Etapa.TendidoProceso : Etapa.TendidoTerminado;
        if (k.Contains("ENTREGAD")) return Etapa.MontajeEntregado;
        if (k.Contains("MONTAJE")) return proc ? Etapa.MontajeProceso : Etapa.MontajeTerminado;
        if (k.Contains("CIMENTACION")) return Etapa.CambioCimentacion;
        if (k.Contains("CIVIL")) return proc ? Etapa.CivilProceso : Etapa.CivilTerminado;
        if (k.Contains("RESTRIC")) return Etapa.Restriccion;
        if (k.Contains("DISPONIBLE")) return Etapa.Disponible;
        return Etapa.Pendiente;
    }

    public static bool EnProceso(Etapa e) => e is Etapa.CivilProceso or Etapa.MontajeProceso or Etapa.TendidoProceso;
    public static bool CivilTerminada(Etapa e) => e >= Etapa.CivilTerminado && e != Etapa.CambioCimentacion;
    public static bool Montada(Etapa e) => e >= Etapa.MontajeTerminado;

    // km del tiro guardados en Ajustes ("1=6.5;2=9", por número del tiro).
    public static Dictionary<int, decimal> Longitudes(string? texto)
    {
        var d = new Dictionary<int, decimal>();
        foreach (var p in (texto ?? "").Split(';', StringSplitOptions.RemoveEmptyEntries))
        {
            var kv = p.Split('=');
            if (kv.Length == 2 && int.TryParse(kv[0].Trim(), out var n)
                && decimal.TryParse(kv[1].Trim().Replace(',', '.'), NumberStyles.Float, CultureInfo.InvariantCulture, out var km)) d[n] = km;
        }
        return d;
    }

    public static string EscribirLongitudes(Dictionary<int, decimal> d) =>
        string.Join(";", d.OrderBy(x => x.Key).Select(x => $"{x.Key}={x.Value.ToString(CultureInfo.InvariantCulture)}"));

    // Número de un tiro por su nombre ("TIRO 3: 38 A 50" → 3).
    public static int? NumeroTiro(string? nombre)
    {
        var m = System.Text.RegularExpressions.Regex.Match(nombre ?? "", @"\d+");
        return m.Success && int.TryParse(m.Value, out var n) ? n : null;
    }

    // ---------- Carga inicial (01/11/2026) ----------
    // Códigos: R restricción · D disponible · I27 inicio 2027 · CP/CT civil en proceso/terminado · CC cambio de cimentación ·
    // MP/MT/ME montaje en proceso/terminado/entregado. "!" = texto rojo en la hoja (sin leyenda: se guarda como marca).
    // SUPUESTO: las celdas naranja con texto rojo en negrita se cargan como CIVIL EN PROCESO (falta confirmar su significado).
    private static readonly (int Tiro, decimal Km, string Torres)[] Imagen =
    {
        (1, 6.5m, "3:CT 5N:CT 6NNN:D 7:D 8:MT 9NN:MT 10:D 11:MT 12:MT 13N:MT 15:MT 15A:MT"),
        (2, 9m, "16N:MT 17:D 18N:MT 19N:CP 20:MT 21:MT 22:MT 23N:MT 24N:CP 25:CT 26:MT 27:CP 28:CT 29:CT 30:MT 31:MT 32N:I27 33:MT 34N:I27 35N:MT 36:MT 37:MT"),
        (3, 6.2m, "38:MT 39NN:I27 40N:CT 41:MT 42N:D 43N:MT 44:MT 45:MT 46N:D 47:MT 48:D 49NN:MT 50:MT"),
        (4, 6.3m, "51N:MT 52:MT 53N:MT 54:MT 55N:MT 57N:CT 57AN:CP 58:D 59N:R 60N:R 62:MT"),
        (5, 8m, "63:ME 64:ME 64AN:ME 65:ME 67N:ME 68:ME 69N:MT 70N:CP 71:ME 72N:ME 73N:ME 74NNN:I27 76:ME 77:ME 78N:ME 79:MT 80:ME"),
        (6, 1.1m, "81:ME 82:ME 83:ME"),
        (7, 7.8m, "84N:ME 85N:ME 86:ME 87N:ME 89N:ME 90:MT 91:MT 92NN:ME 93:ME 94N:ME 95N:ME 96N:ME 97:MT 98:MT 99:MT 100N:MT"),
        (8, 7.9m, "101NN:CP! 102NN:MT 103N:D 104N:D 105N:CP! 106:D 107N:D! 108:MT 109N:MT 110:MT 111:MT 112:CT 113:MT 114:CP! 115:R 116:CP! 117:CP! 118N:MP 119:MT"),
        (9, 7.8m, "120NNN:MT 121N:CC 122N:MT 123N:I27 124:MT 125:CT 126:D 127:CT 128:D 129NN:I27 130:CT 131N:CT 132N:CT 133:CT 134:CT 135:ME 136:ME 137N:ME"),
        (10, 5.4m, "138NN:ME 139:I27 141NN:I27 142N:I27 143N:MT 144:R 145N:MT 146N:CT 147:CT 148:MT 149N:MT"),
        (11, 6.01m, "150:MT 152:CT 153N:MT 154:MP 155:CP! 156:MT 157:MT 158:CT"),
    };

    private static readonly Dictionary<string, string> Codigos = new()
    {
        ["R"] = "RESTRICCIÓN", ["D"] = "DISPONIBLE", ["I27"] = "INICIO 2027", ["CP"] = "CIVIL EN PROCESO", ["CT"] = "CIVIL TERMINADO",
        ["CC"] = "CAMBIO DE CIMENTACIÓN", ["MP"] = "MONTAJE EN PROCESO", ["MT"] = "MONTAJE TERMINADO", ["ME"] = "MONTAJE ENTREGADO",
    };

    // Seguro en cada arranque: crea el catálogo y sus ítems si faltan; la carga inicial corre una sola vez
    // (marca = NotaCarga) y solo en torres sin ningún estado; las longitudes, si Ajustes no tiene ninguna.
    public static void Sembrar(AppDbContext db)
    {
        if (!db.DefinicionesCatalogo.IgnoreQueryFilters().Any(d => d.Nombre == Catalogo))
            db.DefinicionesCatalogo.Add(new DefinicionCatalogo
            {
                Nombre = Catalogo, EntidadAsociada = "Costos y presupuestos — estado de las torres (colores de la vista)", EsPredeterminado = true,
                Orden = db.DefinicionesCatalogo.IgnoreQueryFilters().Select(d => (int?)d.Orden).Max() + 1 ?? 0
            });
        var items = db.Catalogos.Where(c => c.Tipo == Catalogo).ToList();
        if (items.Count == 0)
            foreach (var d in Fabrica)
                db.Catalogos.Add(new ItemCatalogo
                {
                    Tipo = Catalogo, Nombre = d.Nombre, Estilo = "personalizado", ColorFondo = d.Fondo, ColorTexto = d.Texto,
                    Forma = "celda", Negrita = true, Descripcion = NombresEtapa[d.Etapa]
                });
        foreach (var i in items.Where(i => string.IsNullOrWhiteSpace(i.Descripcion)))     // etapa explícita para poder renombrarlos
            i.Descripcion = NombresEtapa[EtapaDe(i.Nombre)];
        db.SaveChanges();

        if (db.EstadosTorreManual.Any(e => e.Nota == NotaCarga)) return;

        var torres = db.ElementosTrazado.AsNoTracking().Select(t => new { t.Id, t.Nombre, t.Codigo }).ToList();
        var porClave = new Dictionary<string, int>();
        var porRaiz = new Dictionary<string, List<int>>();
        foreach (var t in torres)
            foreach (var n in new[] { t.Nombre, t.Codigo }.Where(x => !string.IsNullOrWhiteSpace(x)))
            {
                var k = ImportadorF054.ClaveTorre(n!);
                porClave.TryAdd(k, t.Id);
                var r = k.TrimEnd('N');
                if (!porRaiz.TryGetValue(r, out var l)) porRaiz[r] = l = new();
                if (!l.Contains(t.Id)) l.Add(t.Id);
            }
        var conEstado = db.EstadosTorreManual.Select(e => e.ElementoTrazadoId).Distinct().ToHashSet();
        var fecha = new DateTime(2026, 11, 1);
        foreach (var (_, _, lista) in Imagen)
            foreach (var par in lista.Split(' ', StringSplitOptions.RemoveEmptyEntries))
            {
                var i = par.LastIndexOf(':');
                var nombre = par[..i]; var cod = par[(i + 1)..];
                var alerta = cod.EndsWith('!'); cod = cod.TrimEnd('!');
                var k = ImportadorF054.ClaveTorre(nombre);
                int? id = porClave.TryGetValue(k, out var x) ? x
                    : porRaiz.TryGetValue(k.TrimEnd('N'), out var ids) && ids.Count == 1 ? ids[0] : null;
                if (id is not int tid || !conEstado.Add(tid)) continue;
                db.EstadosTorreManual.Add(new EstadoTorreManual { ElementoTrazadoId = tid, Estado = Codigos[cod], Desde = fecha, Alerta = alerta, Nota = NotaCarga });
            }

        var cfg = db.ConfiguracionCostos.OrderBy(c => c.Id).FirstOrDefault();
        if (cfg is null) db.ConfiguracionCostos.Add(cfg = new ConfiguracionCostos());
        if (string.IsNullOrWhiteSpace(cfg.LongitudTiros))
            cfg.LongitudTiros = EscribirLongitudes(Imagen.ToDictionary(t => t.Tiro, t => t.Km));
        db.SaveChanges();
        db.ChangeTracker.Clear();
    }
}
