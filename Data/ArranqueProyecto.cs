using SigWeb.Data.Seguridad;

namespace SigWeb.Data;

// Sembradores y migraciones de datos idempotentes de cada base de proyecto (antes vivían en Program.cs).
// ServicioProyectos.PrepararAsync lo llama una vez por proceso y por base, con el AmbienteSig del proyecto.
// Los datos de ejemplo (Organigrama, Partes interesadas, Indicadores, F-054) solo van al proyecto piloto.
public static class ArranqueProyecto
{
    public static void Ejecutar(AppDbContext db, IConfiguration cfg, IWebHostEnvironment env, ProyectoActivo proyecto)
    {
    // Los 6 catálogos de fábrica (antes una lista fija en el código):
    // debe ir primero porque Catalogos.razor ahora lee la lista de la BD.
    SigWeb.Data.Catalogos.SembradorDefinicionesCatalogo.SembrarSiHaceFalta(db);
    try { SigWeb.Data.Catalogos.ModulosCatalogo.ClasificarPendientes(db); }   // módulo de cada catálogo (panel de Catálogos)
    catch (Exception ex) { Console.Error.WriteLine("Módulo de catálogos (¿falta la migración ModuloCatalogos?): " + ex.Message); }
    SigWeb.Data.Riesgos.SembradorCatalogosRiesgo.SembrarDefiniciones(db);
    // Separa en elementos las causas, consecuencias, etc. numeradas de los
    // riesgos cargados antes de esta función. Solo toca los que no tienen.
    SigWeb.Data.Riesgos.ElementosRiesgo.ConvertirPendientes(db);
    // Las listas desplegables de riesgos dependen de Catálogos: aquí se
    // suben a su catálogo los valores ya usados (incluidos los cargos
    // responsables, uno por línea). Seguro de repetir.
    SigWeb.Data.Riesgos.SembradorCatalogosRiesgo.SincronizarConRiesgos(db);
    // Planes de acción: el lugar único de antes pasa a la lista de lugares.
    SigWeb.Data.Planes.PlanesAccionServicio.MigrarLugarUnico(db);
    SigWeb.Data.Planes.PlanesAccionServicio.MigrarRiesgoUnico(db);
    // Hallazgos: el lugar único de antes pasa a la lista de lugares del hallazgo.
    SigWeb.Data.Planes.PlanesAccionServicio.MigrarLugarHallazgos(db);
    // Costos de no calidad: liga el paso "Costos asociados" de Planes de acción si aún no hay paso ligado.
    SigWeb.Data.CostosNoCalidad.ModuloCostosNoCalidad.LigarPasoPorNombreAsync(db).GetAwaiter().GetResult();
    // Resumen de ensayos: la primera vez marca en «Resumen» lo mismo que lleva «Información Bot».
    SigWeb.Data.Ensayos.ResumenEnsayos.SembrarVistaResumen(db);
    SigWeb.Data.Trazado.SembradorActividades.SembrarSiVacio(db);
    SigWeb.Data.Trazado.SembradorActividadesPata.SembrarSiHaceFalta(db);
    SigWeb.Data.Trazado.SembradorCapaInicial.SembrarSiHaceFalta(db);
    SigWeb.Data.Trazado.SembradorHojaDeVida.SembrarSiHaceFalta(db);
    // Galería de íconos y módulo Ensayos: la de íconos debe ir primero,
    // el sembrador de Ensayos busca sus íconos por Clave.
    SigWeb.Data.Ensayos.SembradorIconosCatalogo.SembrarSiHaceFalta(db);
    SigWeb.Data.Ensayos.SembradorEnsayos.SembrarSiHaceFalta(db);
    SigWeb.Data.Planos.SembradorGruposPlano.SembrarSiHaceFalta(db);
    SigWeb.Data.Equipos.SembradorCamposEquipo.SembrarSiHaceFalta(db);
    // Deja el catálogo "Localización" al día con las torres ya cargadas
    // (incluye las 147 anteriores a esta función). Segura de repetir en
    // cada arranque: solo crea o actualiza lo que falte.
    SigWeb.Data.Trazado.SincronizadorCatalogoLocalizacion.SincronizarTodas(db);
    // Enlaza (o adopta/backfillea) la Actividad de cada Tipo de Ensayo en
    // Trazado. Debe ir después de SembradorActividadesPata (para poder
    // adoptar las 2 actividades de fábrica) y de SembradorEnsayos (para
    // que ya existan los Tipos de Ensayo). Segura de repetir en cada
    // arranque.
    SigWeb.Data.Trazado.SincronizadorActividadesEnsayo.SincronizarTodos(db);
    // Listado maestro de documentos: catálogos de tipo documental/estado, cargas de las
    // importaciones anteriores (sus filas quedan como "Formatos") y estado traído del Excel.
    SigWeb.Data.Documentos.MigracionDocumentos.Ejecutar(db);
    // Listado maestro de planos: importaciones y archivos anteriores quedan agrupados en cargas.
    SigWeb.Data.Planos.MigracionPlanos.Ejecutar(db);
    // Materiales: PDF/fotos → adjuntos clasificados y columnas fijas → Campos.
    SigWeb.Data.ListadoMateriales.MigracionMateriales.Ejecutar(db);
    // Datos de un proyecto concreto (no catálogos genéricos): solo con Arranque:DatosDeEjemploDelProyecto = true.
    // La app se entrega desnuda (appsettings.json en false): el usuario carga lo suyo desde el día 0. En el entorno
    // de desarrollo (appsettings.Development.json) queda en true. Cada sembrador solo actúa si su tabla está vacía.
    var datosDeEjemplo = cfg.GetValue<bool>("Arranque:DatosDeEjemploDelProyecto") && proyecto.EsPiloto;
    if (datosDeEjemplo)
    {
        // Indicadores: la primera vez, los de la matriz de indicadores del SGI (con ene/feb-26 congelados).
        SigWeb.Data.Indicadores.SembradorIndicadores.Ejecutar(db);
        // Avance de obra: la primera vez, las actividades del informe diario F-054 con sus pesos.
        SigWeb.Data.AvanceObra.SembradorActividadesObra.SembrarSiVacio(db);
        SigWeb.Data.AvanceObra.SembradorActividadesObra.SembrarTendidoSiFalta(db);
    }
    // Actividades importadas antes sin efecto 3D (importador genérico): se les deduce por nombre.
    SigWeb.Data.AvanceObra.SembradorActividadesObra.AsignarEfectosFaltantes(db);
    SigWeb.Data.Costos.EstadoTorres.Sembrar(db);
    if (datosDeEjemplo)
    {
        // Organigrama: la primera vez, la versión 1 con el organigrama actual.
        SigWeb.Data.Organigrama.SembradorOrganigrama.SembrarSiVacio(db);
        // Partes interesadas: la primera vez, la versión 1 con la matriz del Excel.
        SigWeb.Data.PartesInteresadas.SembradorPartesInteresadas.SembrarSiVacio(db);
    }
    // Auditoría interna: catálogos de tipo, modalidad, grado de NC, normas y formato de estados.
    SigWeb.Data.Auditorias.ServicioAuditorias.SembrarCatalogos(db);
    }
}
