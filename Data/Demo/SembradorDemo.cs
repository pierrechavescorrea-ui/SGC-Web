using Microsoft.EntityFrameworkCore;
using SigWeb.Data.Seguridad;

namespace SigWeb.Data.Demo;

// Datos de demostración (solo con Demo:Sembrar = true; repositorio SGC-Web).
// Deja la app lista para probar: Empresa (SGC administrador) + 2 proyectos inventados, cada uno con su base,
// el IFC BP6 cargado, 10 torres BP6 sobre un trazado de ejemplo, PDF en hojas de vida y adjuntos, y un ejemplo por módulo.
// Idempotente: cada módulo solo siembra si su tabla está vacía; se puede dejar encendido sin duplicar nada.
public static class SembradorDemo
{
    public sealed record DatosProyectoDemo(string Codigo, string Nombre, string Cliente, string Ubicacion, string Descripcion, string Color);

    public static readonly DatosProyectoDemo Primero = new("LTN230", "LT Norte 230 kV (demo)", "Transmisora Demo S.A. E.S.P.",
        "Municipio Norte Demo", "Proyecto de demostración: línea de transmisión 230 kV, 10 torres BP6 (datos ficticios).", "#EC1C24");
    public static readonly DatosProyectoDemo Segundo = new("LTS500", "LT Sur 500 kV (demo)", "Energía Andina Demo S.A.S.",
        "Municipio Sur Demo", "Proyecto de demostración: línea de transmisión 500 kV, 10 torres BP6 (datos ficticios).", "#2563eb");

    public static async Task EjecutarAsync(WebApplication app, CancellationToken ct = default)
    {
        if (!app.Configuration.GetValue<bool>("Demo:Sembrar")) return;
        var log = app.Services.GetRequiredService<ILoggerFactory>().CreateLogger("SigWeb.Demo");
        var servicio = app.Services.GetRequiredService<ServicioProyectos>();
        try
        {
            await AsegurarProyectosAsync(app.Services, servicio, log, ct);
        }
        catch (Exception ex) { log.LogError(ex, "Demo: no se pudieron crear los proyectos de demostración."); return; }

        var modulos = typeof(IModuloDemo).Assembly.GetTypes()
            .Where(t => t is { IsClass: true, IsAbstract: false } && typeof(IModuloDemo).IsAssignableFrom(t))
            .Select(t => (IModuloDemo)Activator.CreateInstance(t)!)
            .OrderBy(m => m.Orden).ThenBy(m => m.Clave).ToList();

        var webRoot = app.Environment.WebRootPath;
        foreach (var p in servicio.Todos().Where(p => p.Estado == EstadoProyecto.Activo))
        {
            var activo = servicio.Activo(p.Id);
            if (activo is null) continue;
            var error = await servicio.PrepararAsync(p.Id, ct);
            if (error is not null) { log.LogError("Demo: {Codigo}: {Error}", p.Codigo, error); continue; }
            var indice = p.EsEmpresa ? -1 : p.Codigo == Segundo.Codigo ? 1 : 0;
            var aplican = modulos.Where(m => p.EsEmpresa ? m.EnEmpresa : m.EnProyecto).ToList();
            // Los módulos son síncronos o asíncronos; se corren fuera de cualquier contexto de sincronización.
            await Task.Run(async () =>
            {
                using (AmbienteSig.Usar(new SesionSig { Proyecto = activo, Sistema = true }))
                {
                    foreach (var m in aplican)
                    {
                        using var scope = app.Services.CreateScope();
                        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                        var c = new ContextoDemo
                        {
                            Db = db, Proyecto = activo, Servicios = scope.ServiceProvider, WebRoot = webRoot, Log = log, Indice = indice,
                        };
                        try
                        {
                            await m.SembrarAsync(c, ct);
                        }
                        catch (Exception ex)
                        {
                            log.LogError(ex, "Demo: {Codigo} › módulo {Modulo} falló (los demás siguen).", p.Codigo, m.Clave);
                        }
                    }
                }
            }, ct);
            log.LogInformation("Demo: {Codigo} listo ({N} módulos revisados).", p.Codigo, aplican.Count);
        }
    }

    // Proyecto 1 = el piloto (lo crea ArranqueSeguridad con Multiproyecto:CodigoPiloto/NombrePiloto); aquí se completan sus datos.
    // Proyecto 2 = se da de alta con su propia base (<base piloto>_lts500), igual que Administración › Proyectos › Guardar.
    private static async Task AsegurarProyectosAsync(IServiceProvider sp, ServicioProyectos servicio, ILogger log, CancellationToken ct)
    {
        using var scope = sp.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SeguridadDbContext>();
        var cambios = false;

        var piloto = await db.Proyectos.FirstOrDefaultAsync(p => p.EsPiloto, ct);
        if (piloto is not null && string.IsNullOrWhiteSpace(piloto.Cliente))
        {
            piloto.Cliente = Primero.Cliente; piloto.Ubicacion = Primero.Ubicacion; piloto.Descripcion = Primero.Descripcion;
            piloto.Color = Primero.Color; piloto.FechaInicio ??= new DateTime(2026, 2, 1, 0, 0, 0, DateTimeKind.Utc);
            cambios = true;
        }

        if (!await db.Proyectos.IgnoreQueryFilters().AnyAsync(p => p.Codigo == Segundo.Codigo, ct))
        {
            var baseDatos = (servicio.BasePiloto + "_" + Segundo.Codigo).ToLowerInvariant();
            if (!ServicioProyectos.NombreBaseValido(baseDatos)) baseDatos = "sigweb_" + Segundo.Codigo.ToLowerInvariant();
            db.Proyectos.Add(new Proyecto
            {
                Codigo = Segundo.Codigo, Nombre = Segundo.Nombre, Cliente = Segundo.Cliente, Ubicacion = Segundo.Ubicacion,
                Descripcion = Segundo.Descripcion, Color = Segundo.Color, BaseDatos = baseDatos,
                FechaInicio = new DateTime(2026, 4, 1, 0, 0, 0, DateTimeKind.Utc),
            });
            cambios = true;
            log.LogInformation("Demo: proyecto {Codigo} creado (base {Base}).", Segundo.Codigo, baseDatos);
        }

        var empresa = await db.Proyectos.FirstOrDefaultAsync(p => p.EsEmpresa, ct);
        if (empresa is not null && string.IsNullOrWhiteSpace(empresa.Cliente))
        {
            empresa.Cliente = "Ingeniería Demo S.A.S."; empresa.Ubicacion = "Ciudad Demo"; cambios = true;
        }

        if (cambios) await db.SaveChangesAsync(ct);
        servicio.Invalidar();
    }
}
