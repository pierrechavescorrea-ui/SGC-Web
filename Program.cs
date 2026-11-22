using SigWeb.Data.Seguridad;
using Microsoft.EntityFrameworkCore;
using SigWeb.Components;

AppContext.SetSwitch("Npgsql.EnableLegacyTimestampBehavior", true);
// Fechas en toda la app como día/mes/año (16/10/2026): se clona la cultura del equipo (números
// y separadores siguen igual) y solo se cambian los patrones de fecha.
{
    var cultura = (System.Globalization.CultureInfo)System.Globalization.CultureInfo.CurrentCulture.Clone();
    cultura.DateTimeFormat.ShortDatePattern = "dd/MM/yyyy";
    cultura.DateTimeFormat.DateSeparator = "/";
    cultura.DateTimeFormat.ShortTimePattern = "HH:mm";
    cultura.DateTimeFormat.LongTimePattern = "HH:mm:ss";
    System.Globalization.CultureInfo.DefaultThreadCurrentCulture = cultura;
    System.Globalization.CultureInfo.DefaultThreadCurrentUICulture = cultura;
    System.Globalization.CultureInfo.CurrentCulture = cultura;
    System.Globalization.CultureInfo.CurrentUICulture = cultura;
}

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

builder.Services.AddSingleton<SigWeb.Data.Iconos.IconosApp>();
builder.Services.AddSingleton<SigWeb.Data.Catalogos.EstilosCatalogo>();
// Dossier: solicitudes de descarga pendientes (la pantalla las deja, /api/dossier/{token} las genera).
builder.Services.AddSingleton<SigWeb.Data.Dossier.AlmacenDossier>();
builder.Services.AddSingleton<SigWeb.Data.CreadorModulos.ServicioVinculos>();
builder.Services.AddSingleton<SigWeb.Data.CreadorModulos.RegistroModulosCreados>();   // creador de módulos: publicados (menú, permisos)
builder.Services.AddScoped<SigWeb.Data.CreadorModulos.ServicioRegistrosModulo>();
// Trazado › Bases de datos: entrega un archivo ya leído a otra pantalla (Tabla de torres) sin volver a elegirlo.
builder.Services.AddSingleton<SigWeb.Data.Trazado.AlmacenArchivosCarga>();
// Bot de Telegram (Data/Bot): long polling en segundo plano; sin Telegram:Token no hace nada.
builder.Services.AddSingleton<SigWeb.Data.Bot.ServicioBotTelegram>();
builder.Services.AddHostedService(sp => sp.GetRequiredService<SigWeb.Data.Bot.ServicioBotTelegram>());
// Multiproyecto y seguridad (Data/Seguridad): base central de usuarios/permisos, una base por proyecto (AppDbContext
// apunta a la del proyecto activo), Identity, permisos por módulo, auditoría, límites de peticiones, Data Protection.
builder.AddSeguridadSigWeb();
// Avisos por correo (Data/Notificaciones): reglas por proyecto, cola de envíos y trabajador cada 5 min (Notificaciones:Activas).
builder.Services.AddSingleton<SigWeb.Data.Notificaciones.ServicioNotificaciones>();
builder.Services.AddHostedService<SigWeb.Data.Notificaciones.TrabajadorNotificaciones>();

// Papelera de reciclaje: aquí se registra cada módulo que tenga función de
// eliminar. Reciclaje.razor pide "dame todos los IProveedorPapelera
// registrados" y el contenedor de servicios le entrega esta lista.
builder.Services.AddScoped<SigWeb.Data.Impacto.AnalizadorImpacto>();
builder.Services.AddScoped<SigWeb.Data.Riesgos.MatrizRiesgosServicio>();
builder.Services.AddScoped<SigWeb.Data.Planes.PlanesAccionServicio>();
// Diseñador de formularios: diseños (singleton con caché) y valores de los campos nuevos por circuito.
builder.Services.AddSingleton<SigWeb.Data.Formularios.ServicioFormularios>();
builder.Services.AddScoped<SigWeb.Data.Formularios.ValoresFormulario>();
builder.Services.AddSingleton<SigWeb.Data.Seguridad.LogoApp>();   // logo único de la app (lo elige el administrador; base Empresa)
builder.Services.AddSingleton<SigWeb.Data.Asistente.ManualAyuda>();   // asistente de ayuda interno: índice del manual (docs/manual)
builder.Services.AddHostedService<SigWeb.Data.Asistente.PrepararIndice>();
builder.Services.AddScoped<SigWeb.Data.Asistente.AsistenteAyuda>();  // conversación del asistente (una por pestaña)
builder.Services.AddScoped<SigWeb.Data.Navegacion.MenuIcono>();   // menú del ícono del encabezado (vistas / proyectos)
// Plantillas de proyecto (Configuración › Catálogos): un IPlantillaModulo por módulo; ServicioPlantillas orquesta.
builder.Services.AddScoped<SigWeb.Data.Plantillas.IPlantillaModulo, SigWeb.Data.Plantillas.PlantillaTiros>();
builder.Services.AddScoped<SigWeb.Data.Plantillas.IPlantillaModulo, SigWeb.Data.Plantillas.PlantillaCatalogos>();
builder.Services.AddScoped<SigWeb.Data.Plantillas.IPlantillaModulo, SigWeb.Data.Plantillas.PlantillaTorres>();
builder.Services.AddScoped<SigWeb.Data.Plantillas.IPlantillaModulo, SigWeb.Data.Plantillas.PlantillaItemsPago>();
builder.Services.AddScoped<SigWeb.Data.Plantillas.IPlantillaModulo, SigWeb.Data.Plantillas.PlantillaCodigosPresupuesto>();
builder.Services.AddScoped<SigWeb.Data.Plantillas.IPlantillaModulo, SigWeb.Data.Plantillas.PlantillaCantidadesPrevistas>();
builder.Services.AddScoped<SigWeb.Data.Plantillas.IPlantillaModulo, SigWeb.Data.Plantillas.PlantillaProduccion>();
builder.Services.AddScoped<SigWeb.Data.Plantillas.ServicioPlantillas>();
builder.Services.AddScoped<SigWeb.Data.Papelera.IProveedorPapelera, SigWeb.Data.Papelera.ProveedorPapeleraMateriales>();
builder.Services.AddScoped<SigWeb.Data.Papelera.IProveedorPapelera, SigWeb.Data.Papelera.ProveedorPapeleraComponentes>();
builder.Services.AddScoped<SigWeb.Data.Papelera.IProveedorPapelera, SigWeb.Data.Papelera.ProveedorPapeleraElementosTrazado>();
builder.Services.AddScoped<SigWeb.Data.Papelera.IProveedorPapelera, SigWeb.Data.Papelera.ProveedorPapeleraArchivosRegistroParametro>();
builder.Services.AddScoped<SigWeb.Data.Papelera.IProveedorPapelera, SigWeb.Data.Papelera.ProveedorPapeleraArchivosComponente>();
builder.Services.AddScoped<SigWeb.Data.Papelera.IProveedorPapelera, SigWeb.Data.Papelera.ProveedorPapeleraCarpetasHojaVida>();
builder.Services.AddScoped<SigWeb.Data.Papelera.IProveedorPapelera, SigWeb.Data.Papelera.ProveedorPapeleraModulosCreados>();
builder.Services.AddScoped<SigWeb.Data.Papelera.IProveedorPapelera, SigWeb.Data.Papelera.ProveedorPapeleraArchivosModulosCreados>();
builder.Services.AddScoped<SigWeb.Data.Papelera.IProveedorPapelera, SigWeb.Data.Papelera.ProveedorPapeleraArchivosHojaVida>();
builder.Services.AddScoped<SigWeb.Data.Papelera.IProveedorPapelera, SigWeb.Data.Papelera.ProveedorPapeleraPlanoMaestro>();
builder.Services.AddScoped<SigWeb.Data.Papelera.IProveedorPapelera, SigWeb.Data.Papelera.ProveedorPapeleraArchivosPlanoMaestro>();
builder.Services.AddScoped<SigWeb.Data.Papelera.IProveedorPapelera, SigWeb.Data.Papelera.ProveedorPapeleraResultadosEnsayo>();
builder.Services.AddScoped<SigWeb.Data.Papelera.IProveedorPapelera, SigWeb.Data.Papelera.ProveedorPapeleraCamposExtraTorre>();
builder.Services.AddScoped<SigWeb.Data.Papelera.IProveedorPapelera, SigWeb.Data.Papelera.ProveedorPapeleraCamposExtraComponente>();
builder.Services.AddScoped<SigWeb.Data.Papelera.IProveedorPapelera, SigWeb.Data.Papelera.ProveedorPapeleraGruposEnsayo>();
builder.Services.AddScoped<SigWeb.Data.Papelera.IProveedorPapelera, SigWeb.Data.Papelera.ProveedorPapeleraTiposEnsayo>();
builder.Services.AddScoped<SigWeb.Data.Papelera.IProveedorPapelera, SigWeb.Data.Papelera.ProveedorPapeleraDefinicionesCatalogo>();
builder.Services.AddScoped<SigWeb.Data.Papelera.IProveedorPapelera, SigWeb.Data.Papelera.ProveedorPapeleraGruposPlano>();
builder.Services.AddScoped<SigWeb.Data.Papelera.IProveedorPapelera, SigWeb.Data.Papelera.ProveedorPapeleraGruposDocumento>();
builder.Services.AddScoped<SigWeb.Data.Papelera.IProveedorPapelera, SigWeb.Data.Papelera.ProveedorPapeleraIconos>();
builder.Services.AddScoped<SigWeb.Data.Papelera.IProveedorPapelera, SigWeb.Data.Papelera.ProveedorPapeleraGruposEquipo>();
builder.Services.AddScoped<SigWeb.Data.Papelera.IProveedorPapelera, SigWeb.Data.Papelera.ProveedorPapeleraGruposMaterial>();
builder.Services.AddScoped<SigWeb.Data.Papelera.IProveedorPapelera, SigWeb.Data.Papelera.ProveedorPapeleraGruposResultadoEnsayo>();
builder.Services.AddScoped<SigWeb.Data.Papelera.IProveedorPapelera, SigWeb.Data.Papelera.ProveedorPapeleraActividades>();
builder.Services.AddScoped<SigWeb.Data.Papelera.IProveedorPapelera, SigWeb.Data.Papelera.ProveedorPapeleraTiros>();
builder.Services.AddScoped<SigWeb.Data.Papelera.IProveedorPapelera, SigWeb.Data.Papelera.ProveedorPapeleraParametros>();
builder.Services.AddScoped<SigWeb.Data.Papelera.IProveedorPapelera, SigWeb.Data.Papelera.ProveedorPapeleraVersionesMatrizRiesgos>();
builder.Services.AddScoped<SigWeb.Data.Papelera.IProveedorPapelera, SigWeb.Data.Papelera.ProveedorPapeleraRiesgos>();
builder.Services.AddScoped<SigWeb.Data.Papelera.IProveedorPapelera, SigWeb.Data.Papelera.ProveedorPapeleraSeguimientosRiesgo>();
builder.Services.AddScoped<SigWeb.Data.Papelera.IProveedorPapelera, SigWeb.Data.Papelera.ProveedorPapeleraPlanesAccion>();
builder.Services.AddScoped<SigWeb.Data.Papelera.IProveedorPapelera, SigWeb.Data.Papelera.ProveedorPapeleraAccionesPlan>();
builder.Services.AddScoped<SigWeb.Data.Papelera.IProveedorPapelera, SigWeb.Data.Papelera.ProveedorPapeleraAnexosAccion>();
builder.Services.AddScoped<SigWeb.Data.Papelera.IProveedorPapelera, SigWeb.Data.Papelera.ProveedorPapeleraArchivosRiesgo>();
builder.Services.AddScoped<SigWeb.Data.Papelera.IProveedorPapelera, SigWeb.Data.Papelera.ProveedorPapeleraCamposTabla>();
builder.Services.AddScoped<SigWeb.Data.Papelera.IProveedorPapelera, SigWeb.Data.Papelera.ProveedorPapeleraDocumentosMaestro>();
builder.Services.AddScoped<SigWeb.Data.Papelera.IProveedorPapelera, SigWeb.Data.Papelera.ProveedorPapeleraCargasDocumentos>();
builder.Services.AddScoped<SigWeb.Data.Papelera.IProveedorPapelera, SigWeb.Data.Papelera.ProveedorPapeleraCargasArchivosDocumentos>();
builder.Services.AddScoped<SigWeb.Data.Papelera.IProveedorPapelera, SigWeb.Data.Papelera.ProveedorPapeleraCertificaciones>();
builder.Services.AddScoped<SigWeb.Data.Papelera.IProveedorPapelera, SigWeb.Data.Papelera.ProveedorPapeleraDireccionamiento>();
builder.Services.AddScoped<SigWeb.Data.Papelera.IProveedorPapelera, SigWeb.Data.Papelera.ProveedorPapeleraVersionesOrganigrama>();
builder.Services.AddScoped<SigWeb.Data.Papelera.IProveedorPapelera, SigWeb.Data.Papelera.ProveedorPapeleraCargosOrganigrama>();
builder.Services.AddScoped<SigWeb.Data.Papelera.IProveedorPapelera, SigWeb.Data.Papelera.ProveedorPapeleraVersionesPartes>();
builder.Services.AddScoped<SigWeb.Data.Papelera.IProveedorPapelera, SigWeb.Data.Papelera.ProveedorPapeleraPartes>();
builder.Services.AddScoped<SigWeb.Data.Papelera.IProveedorPapelera, SigWeb.Data.Papelera.ProveedorPapeleraSeguimientosParte>();
builder.Services.AddScoped<SigWeb.Data.Papelera.IProveedorPapelera, SigWeb.Data.Papelera.ProveedorPapeleraCargasPlanos>();
builder.Services.AddScoped<SigWeb.Data.Papelera.IProveedorPapelera, SigWeb.Data.Papelera.ProveedorPapeleraCargasArchivosPlanos>();
builder.Services.AddScoped<SigWeb.Data.Papelera.IProveedorPapelera, SigWeb.Data.Papelera.ProveedorPapeleraVersionesDocumento>();
builder.Services.AddScoped<SigWeb.Data.Papelera.IProveedorPapelera, SigWeb.Data.Papelera.ProveedorPapeleraAdjuntosMaterial>();
builder.Services.AddScoped<SigWeb.Data.Papelera.IProveedorPapelera, SigWeb.Data.Papelera.ProveedorPapeleraCargasMateriales>();
builder.Services.AddScoped<SigWeb.Data.Papelera.IProveedorPapelera, SigWeb.Data.Papelera.ProveedorPapeleraCargasPlanesAccion>();
builder.Services.AddScoped<SigWeb.Data.Papelera.IProveedorPapelera, SigWeb.Data.Papelera.ProveedorPapeleraSeguimientosAccion>();
builder.Services.AddScoped<SigWeb.Data.Papelera.IProveedorPapelera, SigWeb.Data.Papelera.ProveedorPapeleraArchivosEquipo>();
builder.Services.AddScoped<SigWeb.Data.Papelera.IProveedorPapelera, SigWeb.Data.Papelera.ProveedorPapeleraCarpetasPlan>();
builder.Services.AddScoped<SigWeb.Data.Papelera.IProveedorPapelera, SigWeb.Data.Papelera.ProveedorPapeleraArchivosCarpetaPlan>();
builder.Services.AddScoped<SigWeb.Data.Papelera.IProveedorPapelera, SigWeb.Data.Papelera.ProveedorPapeleraComunicados>();
builder.Services.AddScoped<SigWeb.Data.Papelera.IProveedorPapelera, SigWeb.Data.Papelera.ProveedorPapeleraAdjuntosComunicado>();
builder.Services.AddScoped<SigWeb.Data.Papelera.IProveedorPapelera, SigWeb.Data.Papelera.ProveedorPapeleraCargasComunicados>();
builder.Services.AddScoped<SigWeb.Data.Papelera.IProveedorPapelera, SigWeb.Data.Papelera.ProveedorPapeleraIndicadores>();
builder.Services.AddScoped<SigWeb.Data.Papelera.IProveedorPapelera, SigWeb.Data.Papelera.ProveedorPapeleraArchivosIndicador>();
builder.Services.AddScoped<SigWeb.Data.Papelera.IProveedorPapelera, SigWeb.Data.Papelera.ProveedorPapeleraCargasEquipos>();
builder.Services.AddScoped<SigWeb.Data.Papelera.IProveedorPapelera, SigWeb.Data.Papelera.ProveedorPapeleraInformesDiarios>();
builder.Services.AddScoped<SigWeb.Data.Papelera.IProveedorPapelera, SigWeb.Data.Papelera.ProveedorPapeleraRegistrosAvance>();
builder.Services.AddScoped<SigWeb.Data.Papelera.IProveedorPapelera, SigWeb.Data.Papelera.ProveedorPapeleraRegistrosAvanceTiro>();
builder.Services.AddScoped<SigWeb.Data.Papelera.IProveedorPapelera, SigWeb.Data.Papelera.ProveedorPapeleraFotosAvance>();
builder.Services.AddScoped<SigWeb.Data.Papelera.IProveedorPapelera, SigWeb.Data.Papelera.ProveedorPapeleraActividadesObra>();
// Auditoría interna (Data/Papelera/ProveedorPapeleraAuditorias.cs)
builder.Services.AddScoped<SigWeb.Data.Papelera.IProveedorPapelera, SigWeb.Data.Papelera.ProveedorPapeleraProgramasAuditoria>();
builder.Services.AddScoped<SigWeb.Data.Papelera.IProveedorPapelera, SigWeb.Data.Papelera.ProveedorPapeleraAuditorias>();
builder.Services.AddScoped<SigWeb.Data.Papelera.IProveedorPapelera, SigWeb.Data.Papelera.ProveedorPapeleraHallazgosAuditoria>();
builder.Services.AddScoped<SigWeb.Data.Papelera.IProveedorPapelera, SigWeb.Data.Papelera.ProveedorPapeleraAnexosHallazgo>();
builder.Services.AddScoped<SigWeb.Data.Papelera.IProveedorPapelera, SigWeb.Data.Papelera.ProveedorPapeleraAnexosEficacia>();
builder.Services.AddScoped<SigWeb.Data.Papelera.IProveedorPapelera, SigWeb.Data.Papelera.ProveedorPapeleraArchivosCampoFormulario>();
builder.Services.AddScoped<SigWeb.Data.Papelera.IProveedorPapelera, SigWeb.Data.Papelera.ProveedorPapeleraVistasRegistro>();
// Costos y presupuestos (Data/Papelera/ProveedorPapeleraCostos.cs)
builder.Services.AddScoped<SigWeb.Data.Papelera.IProveedorPapelera, SigWeb.Data.Papelera.ProveedorPapeleraItemsPago>();
builder.Services.AddScoped<SigWeb.Data.Papelera.IProveedorPapelera, SigWeb.Data.Papelera.ProveedorPapeleraRegistrosCantidad>();
builder.Services.AddScoped<SigWeb.Data.Papelera.IProveedorPapelera, SigWeb.Data.Papelera.ProveedorPapeleraCostosOrden>();
builder.Services.AddScoped<SigWeb.Data.Papelera.IProveedorPapelera, SigWeb.Data.Papelera.ProveedorPapeleraPersonas>();
builder.Services.AddScoped<SigWeb.Data.Papelera.IProveedorPapelera, SigWeb.Data.Papelera.ProveedorPapeleraCodigosPresupuesto>();
builder.Services.AddScoped<SigWeb.Data.Papelera.IProveedorPapelera, SigWeb.Data.Papelera.ProveedorPapeleraModificacionesContrato>();
builder.Services.AddScoped<SigWeb.Data.Papelera.IProveedorPapelera, SigWeb.Data.Papelera.ProveedorPapeleraAjustesPrecio>();
builder.Services.AddScoped<SigWeb.Data.Papelera.IProveedorPapelera, SigWeb.Data.Papelera.ProveedorPapeleraActasFacturacion>();
builder.Services.AddScoped<SigWeb.Data.Papelera.IProveedorPapelera, SigWeb.Data.Papelera.ProveedorPapeleraMetasPeriodo>();
builder.Services.AddScoped<SigWeb.Data.Papelera.IProveedorPapelera, SigWeb.Data.Papelera.ProveedorPapeleraProgramasTrabajo>();
builder.Services.AddScoped<SigWeb.Data.Papelera.IProveedorPapelera, SigWeb.Data.Papelera.ProveedorPapeleraModelosIfc>();

// Trazado (KMZ): cada módulo que permita vincular sus registros a una
// torre/pata se registra aquí como IProveedorUbicacion.
// Materiales ya no va aquí: la ficha lo muestra en su bloque "Materiales" (Archivos), con adjuntos.
// builder.Services.AddScoped<SigWeb.Data.Ubicacion.IProveedorUbicacion, SigWeb.Data.Ubicacion.ProveedorUbicacionMateriales>();

builder.Services.Configure<Microsoft.AspNetCore.SignalR.HubOptions>(options => { options.MaximumReceiveMessageSize = 20 * 1024 * 1024; });

var app = builder.Build();

// Bases de los proyectos: se preparan (sembradores idempotentes) al arrancar y al entrar a cada proyecto.
await SigWeb.Data.Seguridad.ArranqueSeguridad.InicializarAsync(app);
// Datos de demostración (Demo:Sembrar = true): 2 proyectos, torres BP6, IFC, PDF y un ejemplo por módulo. Idempotente.
await SigWeb.Data.Demo.SembradorDemo.EjecutarAsync(app);
// Módulos creados por el administrador: se suman a CatalogoModulos antes de atender peticiones (no lanza si falta la tabla).
await app.Services.GetRequiredService<SigWeb.Data.CreadorModulos.RegistroModulosCreados>().RecargarAsync();
// Delegados de lectura de la instantánea publicada: Dossier y navegación se actualizan sin depender del registro.
var modulosPublicados = app.Services.GetRequiredService<SigWeb.Data.CreadorModulos.RegistroModulosCreados>();
SigWeb.Data.Dossier.FuentesDossier.Creados = () => modulosPublicados.Todos
    .OrderBy(m => Array.IndexOf(SigWeb.Data.Navegacion.MapaNavegacion.OrdenGrupos, m.Grupo) is var i && i >= 0 ? i : 99)
    .ThenBy(m => m.Orden).ThenBy(m => m.Nombre)
    .Select(m => new SigWeb.Data.Dossier.Fuentes.FuenteModuloCreado(m));
SigWeb.Data.Navegacion.MapaNavegacion.Creados = () => modulosPublicados.Todos
    .OrderBy(m => Array.IndexOf(SigWeb.Data.Navegacion.MapaNavegacion.OrdenGrupos, m.Grupo) is var i && i >= 0 ? i : 99)
    .ThenBy(m => m.Orden).ThenBy(m => m.Nombre)
    .Select(m => new SigWeb.Data.Navegacion.MapaNavegacion.Modulo(m.Grupo, m.Nombre, m.Ruta, "listados", m.Icono));
// Direccionamiento estratégico y Certificaciones son de la Empresa: lo cargado en el piloto antes de multiproyecto se copia
// una vez a la base de la Empresa (solo si allá no hay nada; no borra nada del piloto).
try
{
    var servProy = app.Services.GetRequiredService<SigWeb.Data.Seguridad.ServicioProyectos>();
    var logCopia = app.Services.GetRequiredService<ILoggerFactory>().CreateLogger("CopiaModulosEmpresa");
    logCopia.LogInformation("{Resultado}", await SigWeb.Data.Seguridad.CopiaModulosEmpresa.CopiarSiFaltaAsync(servProy, logCopia));
}
catch (Exception ex) { app.Logger.LogError(ex, "No se pudo copiar Direccionamiento/Certificaciones a la Empresa."); }

// ---------- Canal HTTP (el orden importa) ----------
app.UseRegistrarOrigenDesarrollo();
app.UseForwardedHeaders();            // IP y https reales detrás del balanceador de AWS
app.UseCabecerasSeguridad();
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    app.UseHsts();
}
app.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true);
app.UseHttpsRedirection();
app.UseAuthentication();
app.UseAccesoLibreDesarrollo();       // solo Development + Autenticacion:AccesoLibreDesarrollo + equipo local
app.UseAltaMfaAdministradores();
app.UseSesionSig();                   // proyecto activo y AmbienteSig; /uploads solo con sesión
app.UsePreferenciasUsuario();         // tema y opciones de vista del usuario para App.razor
app.UseRateLimiter();

// .ifc (modelos BIM) y .wasm (motor web-ifc) no están en la lista de tipos por defecto.
var tiposEstaticos = new Microsoft.AspNetCore.StaticFiles.FileExtensionContentTypeProvider();
tiposEstaticos.Mappings[".ifc"] = "application/x-step";
tiposEstaticos.Mappings[".wasm"] = "application/wasm";
tiposEstaticos.Mappings.TryAdd(".md", "text/markdown; charset=utf-8");   // informes de validación de Modelos 3D
app.UseStaticFiles(new StaticFileOptions { ContentTypeProvider = tiposEstaticos });

app.UseAuthorization();
app.UseGuardaModulos();               // permiso Ver del módulo de cada pantalla
app.UseAntiforgery();

app.MapStaticAssets().AllowAnonymous();
app.MapEndpointsCuenta();
app.MapEndpointsPreferencias();
SigWeb.Data.Seguridad.FiltroAuditoria.MapExportar(app);
app.MapHealthChecks("/salud", new Microsoft.AspNetCore.Diagnostics.HealthChecks.HealthCheckOptions { Predicate = _ => false }).AllowAnonymous().DisableRateLimiting();   // vivo (balanceador)
app.MapHealthChecks("/salud/listo", new Microsoft.AspNetCore.Diagnostics.HealthChecks.HealthCheckOptions
{ Predicate = c => c.Tags.Contains("listo") }).AllowAnonymous();
// Plantilla de proyecto: el libro se descarga por HTTP (no por el circuito). ?modulos=tiros,torres&datos=1
app.MapGet("/api/plantillas/proyecto.xlsx", async (string? modulos, int? datos, SigWeb.Data.Plantillas.ServicioPlantillas servicio, CancellationToken ct) =>
{
    var claves = (modulos ?? "").Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
    var permitidas = servicio.Modulos.Where(m => !SigWeb.Data.Plantillas.PlantillaCostos.EsDeCostos(m.Clave))
        .Select(m => m.Clave).ToHashSet(StringComparer.OrdinalIgnoreCase);
    if (claves.Any(c => !permitidas.Contains(c))) return Results.BadRequest();
    var elegidas = claves.Length == 0 ? permitidas.ToArray() : claves;
    if (datos == 1 && elegidas.Any(c => !servicio.PuedeUsar(c, Accion.Descargar)))
        return Results.StatusCode(StatusCodes.Status403Forbidden);
    var bytes = await servicio.GenerarAsync(elegidas, datos == 1, ct);
    return Results.File(bytes, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
        $"Plantilla proyecto SigWeb {DateTime.Now:yyyy-MM-dd}.xlsx");
}).RequiereModulo("catalogos", Accion.Ver | Accion.Descargar);
app.MapGet("/api/plantillas/costos.xlsx", async (string? modulo, int? datos, SigWeb.Data.Plantillas.ServicioPlantillas servicio, CancellationToken ct) =>
{
    if (modulo is null || !SigWeb.Data.Plantillas.PlantillaCostos.EsDeCostos(modulo)) return Results.BadRequest();
    var bytes = await servicio.GenerarAsync(new[] { modulo }, datos == 1, ct);
    return Results.File(bytes, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
        $"Plantilla {modulo}.xlsx");
}).RequiereModulo("costos", Accion.Ver | Accion.Descargar);
// Buscador rápido (Ctrl + K del menú): resultados en JSON para js/navegacion.js.
// Solo devuelve resultados de módulos que el usuario puede ver.
app.MapGet("/api/buscar", async (string? q, SigWeb.Data.AppDbContext db, CancellationToken cancellationToken) =>
{
    var permisos = AmbienteSig.Actual?.Permisos ?? PermisosEfectivos.Ninguno;
    var res = await SigWeb.Data.Busqueda.BuscadorGlobal.BuscarAsync(db, q, cancellationToken);
    return Results.Json(res.Where(r => permisos.Puede(CatalogoModulos.ClaveDeRuta(r.Url), Accion.Ver)).ToList());
}).RequiereModulo("inicio", Accion.Ver);
// Dossier: el ZIP se escribe directo en la respuesta (streaming); si el usuario cancela la descarga,
// RequestAborted detiene la generación. ZipArchive escribe su índice final de forma síncrona.
app.MapGet("/api/dossier/{token}", async (string token, HttpContext ctx, SigWeb.Data.Dossier.AlmacenDossier almacen,
    SigWeb.Data.AppDbContext db, IWebHostEnvironment env, ILogger<Program> log) =>
{
    var s = almacen.Obtener(token);
    if (s is null) { ctx.Response.StatusCode = StatusCodes.Status404NotFound; return; }
    var sync = ctx.Features.Get<Microsoft.AspNetCore.Http.Features.IHttpBodyControlFeature>();
    if (sync is not null) sync.AllowSynchronousIO = true;
    ctx.Features.Get<Microsoft.AspNetCore.Http.Features.IHttpResponseBodyFeature>()?.DisableBuffering();
    var nombre = SigWeb.Data.Dossier.GeneradorDossier.NombreZip(s.Nombre);
    ctx.Response.ContentType = "application/zip";
    ctx.Response.Headers.ContentDisposition = new System.Net.Http.Headers.ContentDispositionHeaderValue("attachment")
    { FileName = "\"" + new string(nombre.Select(ch => ch < 128 ? ch : '_').ToArray()) + "\"", FileNameStar = nombre }.ToString();
    var permisosDossier = SigWeb.Data.Seguridad.AmbienteSig.Actual?.Permisos ?? SigWeb.Data.Seguridad.PermisosEfectivos.Ninguno;
    var sistema = SigWeb.Data.Seguridad.AmbienteSig.Actual?.Sistema == true;
    var compartida = ctx.RequestServices.GetRequiredService<SigWeb.Data.Seguridad.FabricaDbCompartida>();
    try
    {
        await SigWeb.Data.Dossier.GeneradorDossier.GenerarAsync(ctx.Response.Body, s, db, env.WebRootPath, ctx.RequestAborted,
            m => sistema || permisosDossier.Puede(m, SigWeb.Data.Seguridad.Accion.Ver), compartida.ParaModulo);
    }
    catch (OperationCanceledException) { }
    catch (IOException) when (ctx.RequestAborted.IsCancellationRequested) { }   // el navegador cortó la descarga
    catch (Exception ex)
    {
        log.LogError(ex, "No se pudo generar el dossier");
        // Ya se enviaron bytes: se corta la conexión para que el navegador marque la descarga como fallida.
        if (ctx.Response.HasStarted) ctx.Abort();
        else { ctx.Response.Headers.ContentDisposition = default; ctx.Response.StatusCode = StatusCodes.Status500InternalServerError; }
    }
}).RequiereModulo("dossier", Accion.Ver | Accion.Descargar).RequireRateLimiting("dossier");
// Informe de auditoría interna en Word o Excel (fuera del circuito: puede llevar fotos).
app.MapGet("/api/auditorias/{id:int}/informe.{ext}", async (int id, string ext, HttpContext ctx, SigWeb.Data.AppDbContext db, IWebHostEnvironment env) =>
{
    ext = ext.ToLowerInvariant();
    if (ext is not ("docx" or "xlsx")) return Results.NotFound();
    var d = await SigWeb.Data.Auditorias.InformeAuditoria.CargarAsync(db, id, SigWeb.Data.Auditorias.OpcionesAuditoria.Cargar(db));
    if (d is null) return Results.NotFound();
    var nombre = SigWeb.Data.Auditorias.ExportadorInformeAuditoria.NombreArchivo(d, ext);
    return ext == "docx"
        ? Results.File(SigWeb.Data.Auditorias.ExportadorInformeAuditoria.Word(d, env.WebRootPath), "application/vnd.openxmlformats-officedocument.wordprocessingml.document", nombre)
        : Results.File(SigWeb.Data.Auditorias.ExportadorInformeAuditoria.Excel(d, $"{ctx.Request.Scheme}://{ctx.Request.Host}{ctx.Request.PathBase}"), "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", nombre);
}).RequiereModulo("auditoria-interna", Accion.Ver | Accion.Descargar);
SigWeb.Data.ArchivosHojaVidaEndpoints.MapArchivosHojaVida(app);

app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();
