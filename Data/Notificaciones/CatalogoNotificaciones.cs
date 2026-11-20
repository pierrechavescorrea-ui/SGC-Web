using Microsoft.EntityFrameworkCore;
using SigWeb.Data.Inicio;
using SigWeb.Data.Seguridad;
using SigWeb.Models;

namespace SigWeb.Data.Notificaciones;

// Un elemento de un aviso: lo que hay que gestionar (una calibración por vencer, una acción vencida…).
// Clave: identidad estable del elemento (para «solo nuevos»). Responsable: texto tal cual del registro.
public sealed record AvisoNotif(string Clave, string Titulo, string? Detalle, string Etiqueta, string Tono, string Href, string? Responsable);

// Tipos de aviso automático. Cada uno lee el proyecto (solo lectura) y devuelve sus elementos; la mayoría reutiliza los
// apartados del tablero de Inicio (misma regla que lo que se ve en pantalla). Tipo nuevo = una línea en Lista.
public static class CatalogoNotificaciones
{
    public sealed record Tipo(
        string Clave, string Nombre, string Modulo, string Descripcion,
        string? Seccion,                                                  // apartado de TableroInicio que lo alimenta
        Func<AppDbContext, DateTime, List<AvisoNotif>>? Propia,           // o una consulta propia
        FrecuenciaNotificacion Frecuencia, DestinoNotificacion Destino);

    private const DestinoNotificacion Resp = DestinoNotificacion.Responsables;
    private const DestinoNotificacion Editan = DestinoNotificacion.QuienesEditan;

    public static readonly IReadOnlyList<Tipo> Lista = new Tipo[]
    {
        new("calibraciones", "Calibraciones por vencer", "equipos",
            "Equipos con la calibración vencida o que vence en los próximos 30 días.", "calibracion", null, FrecuenciaNotificacion.Semanal, Editan),
        new("certificaciones", "Certificaciones por vencer", "certificaciones",
            "Certificados ISO vencidos o que vencen en los próximos 90 días.", "certificaciones", null, FrecuenciaNotificacion.Mensual, Editan),
        new("acciones-por-vencer", "Acciones por vencer", "planes-accion",
            "Acciones de planes sin cerrar cuya fecha llega en los próximos 7 días. Va a su responsable.", null, AccionesPorVencer,
            FrecuenciaNotificacion.Diaria, Resp),
        new("acciones-vencidas", "Acciones vencidas", "planes-accion",
            "Acciones de planes sin cerrar con la fecha ya pasada. Va a su responsable.", "acciones", null, FrecuenciaNotificacion.Semanal, Resp | Editan),
        new("eficacia", "Verificación de eficacia", "planes-accion",
            "Planes cerrados cuya verificación de eficacia está programada en los próximos 7 días o ya se pasó.", null, Eficacia,
            FrecuenciaNotificacion.Semanal, Resp | Editan),
        new("planes-abiertos", "Hallazgos y planes abiertos", "planes-accion",
            "Resumen de los planes de acción y hallazgos de riesgos sin cerrar.", "planes", null, FrecuenciaNotificacion.Semanal, Editan),
        new("auditorias", "Auditorías próximas", "auditoria-interna",
            "Auditorías en ejecución, atrasadas o que empiezan en los próximos 30 días.", "auditorias", null, FrecuenciaNotificacion.Semanal, Editan),
        new("indicadores", "Indicadores fuera de meta", "indicadores",
            "Indicadores cuyo último periodo medido no cumple la meta. Va a su responsable.", "indicadores", null, FrecuenciaNotificacion.Mensual, Resp | Editan),
        new("comunicados-vencidos", "Comunicados vencidos", "comunicados",
            "Comunicados que requieren respuesta y ya pasaron el plazo. Va al responsable de la respuesta.", "comunicados", null,
            FrecuenciaNotificacion.Diaria, Resp),
        new("comunicados-por-responder", "Comunicados por responder", "comunicados",
            "Comunicados que requieren respuesta y siguen dentro del plazo.", "comunicados-plazo", null, FrecuenciaNotificacion.Semanal, Resp),
        new("riesgos-materializados", "Riesgos materializados", "riesgos",
            "Riesgos que se materializaron en los últimos 30 días.", "materializados", null, FrecuenciaNotificacion.Semanal, Editan),
        new("riesgos-sin-seguimiento", "Riesgos sin seguimiento", "riesgos",
            "Riesgos activos de la matriz vigente sin seguimiento en los últimos 30 días.", "seguimiento", null, FrecuenciaNotificacion.Mensual, Editan),
        new("pie", "Pendientes de la Matriz PIE", "matriz-pie",
            "Torres con registros pendientes en la Matriz PIE.", "pie", null, FrecuenciaNotificacion.Semanal, Editan),
    };

    public const string Manual = "manual";

    public static Tipo? De(string? clave) => Lista.FirstOrDefault(t => t.Clave == clave);

    // Lee todos los tipos pedidos de una vez (el tablero se carga una sola vez).
    public static Dictionary<string, List<AvisoNotif>> Leer(AppDbContext db, DateTime hoy, IEnumerable<string> tipos)
    {
        var pedidos = tipos.Select(De).Where(t => t is not null).Select(t => t!).ToList();
        var r = new Dictionary<string, List<AvisoNotif>>();
        TableroInicio.Datos? tablero = null;
        foreach (var t in pedidos)
        {
            try
            {
                if (t.Propia is not null) { r[t.Clave] = t.Propia(db, hoy); continue; }
                tablero ??= TableroInicio.Cargar(db, hoy, todosLosResponsables: true);
                var sec = tablero.Secciones.FirstOrDefault(s => s.Clave == t.Seccion);
                r[t.Clave] = sec is null ? new() : sec.Items
                    .Select(i => new AvisoNotif($"{i.Href}|{i.Titulo}", i.Titulo, i.Detalle, i.Etiqueta, i.Tono, i.Href, i.Responsable)).ToList();
            }
            catch
            {
                r[t.Clave] = new();   // una tabla que aún no existe (migración pendiente) no tumba los demás avisos
            }
        }
        return r;
    }

    // ---------- Consultas propias ----------

    private static List<AvisoNotif> AccionesPorVencer(AppDbContext db, DateTime hoy)
    {
        var hasta = hoy.AddDays(7);
        var filas = (from a in db.AccionesPlan.AsNoTracking()
                     join p in db.PlanesAccion.AsNoTracking() on a.PlanAccionId equals p.Id
                     where a.Estado != EstadoAccionPlan.Cerrada && a.Fecha >= hoy && a.Fecha <= hasta
                     orderby a.Fecha
                     select new { a.Id, a.PlanAccionId, a.Descripcion, a.Fecha, a.Responsable, a.Tipo, p.Codigo }).ToList();
        return filas.Select(a =>
        {
            var dias = (a.Fecha.Date - hoy).Days;
            var plan = string.IsNullOrWhiteSpace(a.Codigo) ? $"Plan {a.PlanAccionId}" : a.Codigo;
            return new AvisoNotif($"accion:{a.Id}:{a.Fecha:yyyyMMdd}", $"{plan} · {EscalaAccion.Nombre(a.Tipo)}", Corto(a.Descripcion),
                dias == 0 ? "Hoy" : $"{dias} d", dias <= 2 ? "ambar" : "gris", $"planes-accion/{a.PlanAccionId}?acciones={a.Id}", a.Responsable);
        }).ToList();
    }

    private static List<AvisoNotif> Eficacia(AppDbContext db, DateTime hoy)
    {
        var hasta = hoy.AddDays(7);
        var planes = db.PlanesAccion.AsNoTracking()
            .Where(p => p.Eficaz == null && p.FechaProgramadaEficacia != null && p.FechaProgramadaEficacia <= hasta)
            .Select(p => new { p.Id, p.Codigo, p.DescripcionHallazgo, p.FechaProgramadaEficacia, p.VerificoEficacia, p.CriterioEficacia }).ToList();
        if (planes.Count == 0) return new();
        var ids = planes.Select(p => p.Id).ToList();
        var abiertas = db.AccionesPlan.AsNoTracking().Where(a => ids.Contains(a.PlanAccionId))
            .GroupBy(a => a.PlanAccionId).Select(g => new { g.Key, Abiertas = g.Count(a => a.Estado != EstadoAccionPlan.Cerrada), Total = g.Count() })
            .ToDictionary(x => x.Key);
        return planes
            .Where(p => abiertas.TryGetValue(p.Id, out var x) && x.Total > 0 && x.Abiertas == 0)   // solo planes cerrados
            .OrderBy(p => p.FechaProgramadaEficacia)
            .Select(p =>
            {
                var dias = (p.FechaProgramadaEficacia!.Value.Date - hoy).Days;
                return new AvisoNotif($"eficacia:{p.Id}:{p.FechaProgramadaEficacia:yyyyMMdd}",
                    string.IsNullOrWhiteSpace(p.Codigo) ? $"Plan {p.Id}" : p.Codigo,
                    Corto(string.IsNullOrWhiteSpace(p.CriterioEficacia) ? p.DescripcionHallazgo : "Criterio: " + p.CriterioEficacia),
                    dias < 0 ? $"Vencida {-dias} d" : dias == 0 ? "Hoy" : $"{dias} d", dias < 0 ? "rojo" : "ambar",
                    $"planes-accion/{p.Id}", p.VerificoEficacia);
            }).ToList();
    }

    private static string Corto(string? s, int n = 140)
    {
        s = (s ?? "").Replace('\n', ' ').Replace('\r', ' ').Trim();
        return s.Length <= n ? s : s[..(n - 1)].TrimEnd() + "…";
    }
}
