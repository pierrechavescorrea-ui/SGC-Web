using Microsoft.EntityFrameworkCore;
using SigWeb.Data.CostosNoCalidad;
using SigWeb.Data.Equipos;
using SigWeb.Models;

namespace SigWeb.Data.Inicio;

// Widgets de resumen del panel de inicio (fase 3): todos con la misma forma (fichas de cifras + barras), pintados por
// Components/Inicio/WidgetResumen. Cada uno se lee con su propio contexto tras el primer render (Home.razor.cs LeerDatosAsync).
public static class ResumenesInicio
{
    // Tono: rojo | ambar | verde | gris (clases ini-* de css/inicio.css).
    public sealed record Ficha(string Etiqueta, string Valor, string Tono = "gris", string? Href = null, string? Titulo = null);
    public sealed record Barra(string Etiqueta, double Valor, string Texto, string Tono = "gris", string? Href = null);
    public sealed record Resumen(List<Ficha> Fichas, List<Barra> Barras, string? Nota = null, double? MaxBarra = null)
    {
        public bool Vacio => Fichas.Count == 0 && Barras.Count == 0;
    }

    // Widgets: id → (título, ícono, ruta del módulo para permisos y vínculo, ancho, alto).
    public sealed record Def(string Id, string Titulo, string Icono, string Ruta, int W, int H);
    public static readonly IReadOnlyList<Def> Lista = new Def[]
    {
        new("cnc", "Costos de no calidad", "costos-no-calidad", "costos-no-calidad", 4, 6),
        new("planes", "Planes de acción y eficacia", "plan-accion", "planes-accion", 4, 6),
        new("auditorias", "Programa de auditoría", "auditoria-interna", "auditoria-interna", 4, 6),
        new("ensayos", "Ensayos recientes", "ensayos", "ensayos", 4, 6),
        new("equipos", "Estado de equipos", "equipos", "equipos", 3, 5),
        new("comunicados", "Comunicados del mes", "comunicados", "comunicados", 3, 5),
    };

    public static async Task<Resumen> CargarAsync(string id, AppDbContext db) => id switch
    {
        "cnc" => await CostosNoCalidad(db),
        "planes" => await Planes(db),
        "auditorias" => await Auditorias(db),
        "ensayos" => await Ensayos(db),
        "equipos" => await EquiposEstado(db),
        "comunicados" => await Comunicados(db),
        _ => new(new(), new()),
    };

    private static string Pesos(double v) => Math.Abs(v) >= 1e6 ? (v / 1e6).ToString("#,0.0") + " M" : v.ToString("#,0");

    private static async Task<Resumen> CostosNoCalidad(AppDbContext db)
    {
        var inst = await ServicioCostosNoCalidad.CargarAsync(db);
        if (!inst.Ligado) return new(new(), new(), "Sin paso de costos ligado en Planes de acción.");
        var hoy = DateTime.Today;
        var anio = inst.Planes.Where(p => p.Fecha.Year == hoy.Year && p.Total != 0).ToList();
        var mes = anio.Where(p => p.Fecha.Month == hoy.Month).Sum(p => p.Total);
        var mesAnt = inst.Planes.Where(p => p.Fecha.Year == hoy.AddMonths(-1).Year && p.Fecha.Month == hoy.AddMonths(-1).Month).Sum(p => p.Total);
        var fichas = new List<Ficha>
        {
            new($"Año {hoy.Year}", Pesos(anio.Sum(p => p.Total)), "gris", "costos-no-calidad", "Costo de no calidad del año"),
            new("Este mes", Pesos(mes), mes > mesAnt ? "rojo" : "verde", "costos-no-calidad", $"Mes anterior: {Pesos(mesAnt)}"),
            new("Planes con costo", anio.Count.ToString(), "gris", "costos-no-calidad"),
        };
        var barras = inst.Rubros.Select(r => (Rubro: r, V: inst.TotalRubro(anio, r))).Where(x => x.V != 0)
            .OrderByDescending(x => x.V).Take(5).Select(x => new Barra(x.Rubro, x.V, Pesos(x.V), "rojo", "costos-no-calidad")).ToList();
        return new(fichas, barras);
    }

    // Estado de un plan según sus acciones: todas cerradas = Cerrado; alguna en ejecución o cerrada = En ejecución; si no, Abierto.
    private static async Task<Resumen> Planes(AppDbContext db)
    {
        var planes = await db.PlanesAccion.AsNoTracking().Where(p => !p.Eliminado)
            .Select(p => new { p.Id, p.OrigenHallazgo, p.Eficaz }).ToListAsync();
        var acciones = (await db.AccionesPlan.AsNoTracking().Where(a => !a.Eliminado).Select(a => new { a.PlanAccionId, a.Estado, a.Fecha }).ToListAsync())
            .GroupBy(a => a.PlanAccionId).ToDictionary(g => g.Key, g => g.ToList());
        string Estado(int id) => !acciones.TryGetValue(id, out var l) || l.Count == 0 ? "Abierto"
            : l.All(a => a.Estado == EstadoAccionPlan.Cerrada) ? "Cerrado"
            : l.Any(a => a.Estado != EstadoAccionPlan.Abierta) ? "En ejecución" : "Abierto";
        var est = planes.Select(p => (p.Id, p.OrigenHallazgo, p.Eficaz, E: Estado(p.Id))).ToList();
        var vencidas = acciones.Values.SelectMany(l => l).Count(a => a.Estado != EstadoAccionPlan.Cerrada && a.Fecha.Date < DateTime.Today);
        var verificados = est.Count(p => p.Eficaz is not null);
        var eficaces = est.Count(p => p.Eficaz == true);
        var fichas = new List<Ficha>
        {
            new("Abiertos", est.Count(p => p.E == "Abierto").ToString(), "ambar", "planes-accion?vistaPlanes=tablero"),
            new("En ejecución", est.Count(p => p.E == "En ejecución").ToString(), "gris", "planes-accion?vistaPlanes=tablero"),
            new("Cerrados", est.Count(p => p.E == "Cerrado").ToString(), "verde", "planes-accion?vistaPlanes=tablero"),
            new("Acciones vencidas", vencidas.ToString(), vencidas > 0 ? "rojo" : "verde", "planes-accion"),
            new("Eficacia", verificados == 0 ? "—" : (100.0 * eficaces / verificados).ToString("0") + " %",
                verificados == 0 ? "gris" : eficaces * 1.0 / verificados >= 0.8 ? "verde" : "ambar", "planes-accion", $"{eficaces} eficaces de {verificados} verificados (ISO 9001 10.2.1 d)"),
        };
        var barras = est.Where(p => p.E != "Cerrado").GroupBy(p => string.IsNullOrWhiteSpace(p.OrigenHallazgo) ? "(sin origen)" : p.OrigenHallazgo)
            .Select(g => new Barra(g.Key, g.Count(), g.Count().ToString(), "ambar", "planes-accion")).OrderByDescending(b => b.Valor).Take(5).ToList();
        return new(fichas, barras, barras.Count > 0 ? "Planes abiertos por origen" : null);
    }

    private static async Task<Resumen> Auditorias(AppDbContext db)
    {
        var anio = DateTime.Today.Year;
        var programas = await db.ProgramasAuditoria.AsNoTracking().Where(p => !p.Eliminado && p.Anio == anio).Select(p => p.Id).ToListAsync();
        if (programas.Count == 0) return new(new(), new(), $"Sin programa de auditoría {anio}.");
        var auds = await db.Auditorias.AsNoTracking().Where(a => !a.Eliminado && programas.Contains(a.ProgramaAuditoriaId))
            .Select(a => new { a.Id, a.Estado, a.Mes }).ToListAsync();
        var ejecutadas = auds.Count(a => a.Estado is EstadoAuditoria.InformeEmitido or EstadoAuditoria.Cerrada);
        var validas = auds.Count(a => a.Estado != EstadoAuditoria.Cancelada);
        var atrasadas = auds.Count(a => a.Estado == EstadoAuditoria.Programada && a.Mes < DateTime.Today.Month);
        var ids = auds.Select(a => a.Id).ToList();
        var hallazgos = await db.HallazgosAuditoria.AsNoTracking().Where(h => !h.Eliminado && ids.Contains(h.AuditoriaId)).Select(h => h.Tipo).ToListAsync();
        var fichas = new List<Ficha>
        {
            new("Cumplimiento", validas == 0 ? "—" : (100.0 * ejecutadas / validas).ToString("0") + " %", "gris", "auditoria-interna", "Ejecutadas (informe emitido o cerradas) / programadas no canceladas"),
            new("Programadas", validas.ToString(), "gris", "auditoria-interna"),
            new("En ejecución", auds.Count(a => a.Estado == EstadoAuditoria.EnEjecucion).ToString(), "verde", "auditoria-interna"),
            new("Atrasadas", atrasadas.ToString(), atrasadas > 0 ? "rojo" : "verde", "auditoria-interna", "Programadas en un mes ya pasado sin iniciar"),
        };
        var barras = hallazgos.GroupBy(t => string.IsNullOrWhiteSpace(t) ? "(sin tipo)" : t)
            .Select(g => new Barra(g.Key, g.Count(), g.Count().ToString(), g.Key.Contains("No conformidad", StringComparison.OrdinalIgnoreCase) ? "rojo" : "gris", "auditoria-interna"))
            .OrderByDescending(b => b.Valor).ToList();
        return new(fichas, barras, barras.Count > 0 ? $"Hallazgos {anio} por tipo" : null);
    }

    private static async Task<Resumen> Ensayos(AppDbContext db)
    {
        var desde = DateTime.Today.AddDays(-90);
        var tipos = await db.TiposEnsayo.AsNoTracking().Where(t => !t.Eliminado).ToDictionaryAsync(t => t.Id, t => t.Nombre);
        var res = await db.ResultadosEnsayo.AsNoTracking().Where(r => !r.Eliminado)
            .Select(r => new { r.TipoEnsayoId, F = r.FechaEjecucion ?? r.FechaCreacion }).ToListAsync();
        res = res.Where(r => tipos.ContainsKey(r.TipoEnsayoId)).ToList();
        var d30 = res.Count(r => r.F >= DateTime.Today.AddDays(-30));
        var fichas = new List<Ficha>
        {
            new("Últimos 30 días", d30.ToString(), "gris", "ensayos"),
            new("Últimos 90 días", res.Count(r => r.F >= desde).ToString(), "gris", "ensayos"),
            new("Total", res.Count.ToString(), "gris", "ensayos/resumen"),
        };
        var barras = res.Where(r => r.F >= desde).GroupBy(r => r.TipoEnsayoId)
            .Select(g => new Barra(tipos[g.Key], g.Count(), g.Count().ToString(), "verde", $"ensayos/{g.Key}"))
            .OrderByDescending(b => b.Valor).Take(6).ToList();
        return new(fichas, barras, barras.Count > 0 ? "Resultados de los últimos 90 días por tipo" : null);
    }

    private static async Task<Resumen> EquiposEstado(AppDbContext db)
    {
        var hoy = DateTime.Today;
        var equipos = await db.Equipos.AsNoTracking().Where(e => !e.Eliminado).Select(e => e.Id).ToListAsync();
        var cal = (await db.IntervencionesEquipo.AsNoTracking().Where(i => !i.Eliminado && i.Tipo == TipoIntervencionEquipo.Calibracion).ToListAsync())
            .GroupBy(i => i.EquipoId).ToDictionary(g => g.Key, g => EstadoEquipo.Vigente(g)?.ProximaFecha);
        int venc = 0, porVencer = 0, vigentes = 0, sin = 0;
        foreach (var id in equipos)
        {
            if (!cal.TryGetValue(id, out var p) || p is null) sin++;
            else if (p.Value.Date < hoy) venc++;
            else if (p.Value.Date <= hoy.AddDays(30)) porVencer++;
            else vigentes++;
        }
        return new(new()
        {
            new("Vigentes", vigentes.ToString(), "verde", "equipos"),
            new("Por vencer (30 d)", porVencer.ToString(), porVencer > 0 ? "ambar" : "gris", "equipos?estado=porvencer"),
            new("Vencidos", venc.ToString(), venc > 0 ? "rojo" : "gris", "equipos?estado=vencido"),
            new("Sin calibración", sin.ToString(), "gris", "equipos"),
        }, new());
    }

    private static async Task<Resumen> Comunicados(AppDbContext db)
    {
        var hoy = DateTime.Today;
        var ini = new DateTime(hoy.Year, hoy.Month, 1);
        var l = await db.Comunicados.AsNoTracking().Where(c => !c.Eliminado)
            .Select(c => new { c.Fecha, c.Sentido, c.RequiereRespuesta, c.FechaRespuesta, c.ConsecutivoRespuesta }).ToListAsync();
        var mes = l.Where(c => c.Fecha >= ini).ToList();
        var pendientes = l.Count(c => c.RequiereRespuesta == true && c.FechaRespuesta is null && string.IsNullOrWhiteSpace(c.ConsecutivoRespuesta));
        return new(new()
        {
            new("Recibidos", mes.Count(c => string.Equals(c.Sentido, "Entrada", StringComparison.OrdinalIgnoreCase)).ToString(), "gris", "comunicados"),
            new("Enviados", mes.Count(c => string.Equals(c.Sentido, "Salida", StringComparison.OrdinalIgnoreCase)).ToString(), "gris", "comunicados"),
            new("Por responder", pendientes.ToString(), pendientes > 0 ? "ambar" : "verde", "comunicados?estado=Pendiente", "Requieren respuesta y aún no la tienen (todos los meses)"),
        }, new(), $"{hoy:MMMM yyyy}");
    }
}
