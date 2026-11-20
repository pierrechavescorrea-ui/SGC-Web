using Microsoft.EntityFrameworkCore;
using SigWeb.Data.AvanceObra;
using SigWeb.Data.Costos;
using SigWeb.Data.Equipos;
using SigWeb.Data.Indicadores;
using SigWeb.Data.PartesInteresadas;
using SigWeb.Models;

namespace SigWeb.Data.Inicio;

// Datos de los widgets "pesados" del panel de inicio (avance de obra, valor ganado, indicadores, calendario). Cada uno se
// lee con su propio contexto, después del primer render, para que el panel aparezca sin esperar (Home.razor.cs).
// Si un módulo falla (migración pendiente, sin datos) su widget queda vacío y los demás siguen.
public static class WidgetsInicio
{
    // ---------- Avance de obra (F-054 / cantidades): mismo % del encabezado de Avance de obra ----------
    public sealed record GrupoAvance(string Nombre, decimal? Pct);
    public sealed record ResumenAvance(decimal? Pct, List<GrupoAvance> Grupos, int Torres, int Iniciadas, int Terminadas, DateTime? UltimoRegistro);

    public static ResumenAvance Avance(AppDbContext db, string webRoot)
    {
        var s = ServicioAvanceObra.Cargar(db, webRoot);
        var hoy = DateTime.Today;
        var grupos = s.Actividades.Where(a => a.Peso > 0).Select(a => a.Grupo).Distinct().Select(g =>
        {
            var l = s.Torres.Select(t => s.PctGrupo(t, g, hoy)).Where(v => v is not null).Select(v => v!.Value).ToList();
            return new GrupoAvance(g, l.Count == 0 ? null : l.Average());
        }).ToList();
        var pcts = s.Torres.Select(t => s.PctTorre(t, hoy)).ToList();
        return new ResumenAvance(s.PctProyecto(hoy), grupos, s.Torres.Count,
            s.Torres.Count(t => s.Iniciada(t, null, hoy)), pcts.Count(p => p >= 100m),
            s.Fechas.Count == 0 ? null : s.Fechas[^1]);
    }

    // ---------- Valor ganado (Costos y presupuestos) ----------
    public sealed record ResumenEvm(decimal Bac, decimal AvanceFisico, decimal? AvanceProgramado, decimal Ev, decimal Ac,
        decimal? Cpi, decimal? Spi, decimal? Eac, decimal? Vac);

    public static async Task<ResumenEvm?> ValorGanado(AppDbContext db)
    {
        var s = await InstantaneaCostos.CargarAsync(db);
        var e = s.ValorGanado(DateTime.Today);
        if (e.Bac <= 0 && e.Ac <= 0) return null;
        return new ResumenEvm(e.Bac, e.AvanceFisico, e.AvanceProgramado, e.Ev, e.Ac, e.Cpi, e.Spi, e.Eac, e.Vac);
    }

    // ---------- Indicadores: los últimos 12 meses de cada uno (congelado/digitado manda; si no, cálculo en vivo) ----------
    public sealed record MesIndicador(string Etiqueta, decimal? Valor, bool? Cumple, bool Mide);
    public sealed record SerieIndicador(int Id, string Nombre, string Meta, string Unidad, List<MesIndicador> Meses, Indicador Indicador)
    {
        public MesIndicador? Ultimo => Meses.LastOrDefault(m => m.Valor is not null);
    }

    public static List<SerieIndicador> Indicadores(AppDbContext db)
    {
        var hoy = DateTime.Today;
        var ctx = ContextoIndicadores.Cargar(db);
        var indicadores = db.Indicadores.AsNoTracking().Where(i => !i.Inactivo && !i.Eliminado).OrderBy(i => i.Orden).ThenBy(i => i.Id).ToList();
        var mediciones = db.MedicionesIndicador.AsNoTracking().Where(m => !m.Eliminado).ToList()
            .GroupBy(m => (m.IndicadorId, m.Anio, m.Mes)).ToDictionary(g => g.Key, g => g.OrderByDescending(m => m.Id).First());
        var r = new List<SerieIndicador>();
        foreach (var ind in indicadores)
        {
            var meses = new List<MesIndicador>();
            for (var k = 11; k >= 0; k--)
            {
                var f = new DateTime(hoy.Year, hoy.Month, 1).AddMonths(-k);
                var mide = CalculoIndicadores.Mide(ind, f.Year, f.Month);
                decimal? valor = null;
                if (mide)
                {
                    mediciones.TryGetValue((ind.Id, f.Year, f.Month), out var m);
                    if (m?.Congelado == true || !CalculoIndicadores.Calculado(ind.Fuente))
                        valor = m is null || m.NoAplica ? null : m.Valor;
                    else
                    {
                        var res = CalculoIndicadores.Calcular(ind, f.Year, f.Month, ctx, m?.BaseCalculo);
                        valor = res is null || res.NoAplica ? null : res.Valor;
                    }
                }
                meses.Add(new MesIndicador(CalculoIndicadores.Mes(f.Year, f.Month), valor, valor is null ? null : CalculoIndicadores.Cumple(ind, valor), mide));
            }
            var meta = ind.Meta is decimal mt ? $"{ind.OperadorMeta} {CalculoIndicadores.Formato(ind, mt)}" : ind.Objetivo;
            r.Add(new SerieIndicador(ind.Id, ind.Nombre, meta, ind.Unidad, meses, ind));
        }
        return r;
    }

    // ---------- Calendario de vencimientos ----------
    // Tipo = clave de ícono del módulo (EncabezadoPagina.Trazos); Tono = rojo (vencido), ambar (≤ 7 días), gris.
    public sealed record Evento(DateTime Fecha, string Titulo, string Detalle, string Tipo, string Tono, string Href);

    public static List<Evento> Calendario(AppDbContext db, DateTime desde, DateTime hasta)
    {
        var hoy = DateTime.Today;
        string Tono(DateTime f) => f.Date < hoy ? "rojo" : f.Date <= hoy.AddDays(7) ? "ambar" : "gris";
        var r = new List<Evento>();
        bool Dentro(DateTime f) => f.Date >= desde.Date && f.Date <= hasta.Date;
        void Intentar(Action a) { try { a(); } catch { /* módulo sin migrar o sin datos: se omite */ } }

        Intentar(() =>
        {
            var equipos = db.Equipos.AsNoTracking().Where(e => !e.Eliminado).ToDictionary(e => e.Id, e => e.Nombre);
            foreach (var g in db.IntervencionesEquipo.AsNoTracking().Where(i => !i.Eliminado && i.Tipo == TipoIntervencionEquipo.Calibracion).ToList().GroupBy(i => i.EquipoId))
                if (equipos.TryGetValue(g.Key, out var n) && EstadoEquipo.Vigente(g)?.ProximaFecha is DateTime p && Dentro(p))
                    r.Add(new Evento(p, n, "Calibración", "equipos", Tono(p), $"equipos/{g.Key}/hoja-de-vida"));
        });
        Intentar(() =>
        {
            foreach (var c in db.Certificaciones.AsNoTracking().Where(c => !c.Eliminado && c.Vencimiento != null).ToList())
                if (Dentro(c.Vencimiento!.Value))
                    r.Add(new Evento(c.Vencimiento.Value, c.Norma, "Vence certificado", "certificaciones", Tono(c.Vencimiento.Value), "certificaciones"));
        });
        Intentar(() =>
        {
            var programas = db.ProgramasAuditoria.AsNoTracking().Where(p => !p.Eliminado).ToDictionary(p => p.Id, p => p.Anio);
            foreach (var a in db.Auditorias.AsNoTracking().Where(a => !a.Eliminado && (a.Estado == EstadoAuditoria.Programada || a.Estado == EstadoAuditoria.EnEjecucion)).ToList())
            {
                if (!programas.TryGetValue(a.ProgramaAuditoriaId, out var anio)) continue;
                var f = a.FechaInicio ?? new DateTime(anio, Math.Clamp(a.Mes, 1, 12), 1);
                if (Dentro(f)) r.Add(new Evento(f, a.Codigo, "Auditoría · " + a.Tipo, "auditoria-interna", a.Estado == EstadoAuditoria.EnEjecucion ? "verde" : Tono(f), $"auditoria-interna/{a.Id}"));
            }
        });
        Intentar(() =>
        {
            var planes = db.PlanesAccion.AsNoTracking().Where(p => !p.Eliminado).ToDictionary(p => p.Id, p => p.Codigo);
            foreach (var a in db.AccionesPlan.AsNoTracking().Where(a => !a.Eliminado && a.Estado != EstadoAccionPlan.Cerrada).ToList())
                if (planes.TryGetValue(a.PlanAccionId, out var cod) && Dentro(a.Fecha))
                    r.Add(new Evento(a.Fecha, cod, a.Descripcion, "plan-accion", Tono(a.Fecha), $"planes-accion/{a.PlanAccionId}?acciones={a.Id}"));
        });
        Intentar(() =>
        {
            var vigente = db.VersionesPartesInteresadas.AsNoTracking().Where(v => !v.Eliminado && v.Estado == EstadoVersionPartes.Vigente).FirstOrDefault();
            if (vigente is null) return;
            var partes = db.PartesInteresadas.AsNoTracking().Where(p => p.VersionPartesInteresadasId == vigente.Id && !p.Eliminado).ToList();
            var progs = db.ProgramacionesSeguimientoParte.AsNoTracking().ToList().GroupBy(p => p.ParteClave).ToDictionary(g => g.Key, g => g.First());
            var segs = db.SeguimientosParteInteresada.AsNoTracking().Where(s => !s.Eliminado).ToList().GroupBy(s => s.ParteClave).ToDictionary(g => g.Key, g => (IReadOnlyList<SeguimientoParteInteresada>)g.ToList());
            foreach (var p in partes)
            {
                var sit = ServicioPartesInteresadas.Situacion(progs.GetValueOrDefault(p.Clave), segs.GetValueOrDefault(p.Clave) ?? Array.Empty<SeguimientoParteInteresada>(), vigente.VigenteDesde, hoy);
                if (sit.Proximo is DateTime f && Dentro(f))
                    r.Add(new Evento(f, p.Nombre, "Seguimiento de parte interesada", "partes-interesadas", Tono(f), "partes-interesadas?vista=seguimiento"));
            }
        });
        return r.OrderBy(e => e.Fecha).ThenBy(e => e.Titulo).ToList();
    }
}
