using Microsoft.EntityFrameworkCore;
using SigWeb.Data.CostosNoCalidad;
using SigWeb.Data.Seguridad;

namespace SigWeb.Data.Inicio;

// Tablero de proyectos (administrador): el mismo resumen del Inicio de cada proyecto (TableroInicio) + costo de no calidad,
// leído de la base de cada uno con su propio contexto y su propio ambiente (AsyncLocal) para que los módulos compartidos
// se resuelvan contra ese proyecto. Solo lectura. Un proyecto cuya base no responde sale como "no disponible" con el motivo.
public sealed class ResumenProyecto
{
    public required Proyecto P { get; init; }
    public TableroInicio.Datos? Datos { get; init; }
    public double CostoNoCalidad { get; init; }
    public int PlanesConCosto { get; init; }
    public int Miembros { get; init; }
    public string? Error { get; init; }
    public bool Disponible => Datos is not null;

    public TableroInicio.Kpi? Kpi(string clave) => Datos?.Kpis.FirstOrDefault(k => k.Clave == clave);
    public TableroInicio.Seccion? Seccion(string clave) => Datos?.Secciones.FirstOrDefault(s => s.Clave == clave);
    public int Total(string clave) => Seccion(clave)?.Total ?? 0;
    // Pendientes en rojo (vencidos, materializados, fuera de meta): la "alarma" del proyecto.
    public int Alertas => Datos?.Secciones.Where(s => s.Tono == "rojo").Sum(s => s.Total) ?? 0;
}

public static class TableroProyectosDatos
{
    public static async Task<List<ResumenProyecto>> CargarAsync(ServicioProyectos proyectos, IReadOnlyDictionary<int, int> miembros, SesionSig? sesion)
    {
        var lista = proyectos.Todos().Where(p => p.Estado is EstadoProyecto.Activo or EstadoProyecto.Suspendido).ToList();
        using var limite = new SemaphoreSlim(4);
        var tareas = lista.Select(async p =>
        {
            await limite.WaitAsync();
            try { return await Task.Run(() => UnoAsync(proyectos, p, miembros.GetValueOrDefault(p.Id), sesion)); }
            finally { limite.Release(); }
        });
        var r = await Task.WhenAll(tareas);
        return r.OrderByDescending(x => x.P.EsEmpresa).ThenByDescending(x => x.Alertas).ThenBy(x => x.P.Nombre).ToList();
    }

    private static async Task<ResumenProyecto> UnoAsync(ServicioProyectos proyectos, Proyecto p, int miembros, SesionSig? sesion)
    {
        var activo = proyectos.Activo(p.Id);
        if (activo is null) return new ResumenProyecto { P = p, Miembros = miembros, Error = "Proyecto sin configuración." };
        // Ambiente de ESTE proyecto mientras se lee (módulos compartidos, interceptores): solo lectura.
        AmbienteSig.Actual = new SesionSig
        {
            Proyecto = activo, Usuario = sesion?.Usuario, UsuarioId = sesion?.UsuarioId, Email = sesion?.Email,
            Permisos = sesion?.Permisos ?? PermisosEfectivos.Ninguno, Sistema = true, Ip = sesion?.Ip,
        };
        try
        {
            await using var db = proyectos.CrearContexto(activo);
            if (!await db.Database.CanConnectAsync()) return new ResumenProyecto { P = p, Miembros = miembros, Error = "La base del proyecto aún no se ha creado (ábralo una vez)." };
            var datos = TableroInicio.Cargar(db);
            double costo = 0; int conCosto = 0;
            try
            {
                var cnc = await ServicioCostosNoCalidad.CargarAsync(db);
                costo = cnc.Planes.Sum(x => x.Total); conCosto = cnc.Planes.Count(x => x.Total != 0);
            }
            catch { /* sin paso de costos o tablas: 0 */ }
            return new ResumenProyecto { P = p, Datos = datos, CostoNoCalidad = costo, PlanesConCosto = conCosto, Miembros = miembros };
        }
        catch (Exception ex)
        {
            return new ResumenProyecto { P = p, Miembros = miembros, Error = "No se pudo leer: " + (ex.InnerException?.Message ?? ex.Message) };
        }
    }
}
