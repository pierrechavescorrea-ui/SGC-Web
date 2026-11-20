using Microsoft.EntityFrameworkCore;
using SigWeb.Models;

namespace SigWeb.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<Material> Materiales { get; set; }
    public DbSet<ItemCatalogo> Catalogos { get; set; }
    public DbSet<FotoMaterial> FotosMateriales { get; set; }
    public DbSet<ConfiguracionEmpresa> Configuracion { get; set; }
    public DbSet<ElementoTrazado> ElementosTrazado { get; set; }
    public DbSet<CapaTrazado> Capas { get; set; }
    public DbSet<Componente> Componentes { get; set; }
    public DbSet<Actividad> Actividades { get; set; }
    public DbSet<ParametroActividad> ParametrosActividad { get; set; }
    public DbSet<RegistroParametro> RegistrosParametro { get; set; }
    public DbSet<EstadoParametro> EstadosParametro { get; set; }
    public DbSet<ArchivoRegistroParametro> ArchivosRegistroParametro { get; set; }
    public DbSet<Tiro> Tiros { get; set; }
    public DbSet<CambioTiro> CambiosTiro { get; set; }
    public DbSet<AsignacionIcono> AsignacionesIcono { get; set; }
    public DbSet<CampoTabla> CamposTabla { get; set; }
    public DbSet<ValorCampoTabla> ValoresCampoTabla { get; set; }
    // Creador de módulos: registros de los módulos creados por el administrador (la definición está en la base central).
    public DbSet<SigWeb.Data.CreadorModulos.RegistroModulo> RegistrosModulo => Set<SigWeb.Data.CreadorModulos.RegistroModulo>();
    public DbSet<SigWeb.Data.CreadorModulos.VinculoRegistroModulo> VinculosRegistroModulo => Set<SigWeb.Data.CreadorModulos.VinculoRegistroModulo>();
    public DbSet<SigWeb.Data.CreadorModulos.ArchivoRegistroModulo> ArchivosRegistroModulo => Set<SigWeb.Data.CreadorModulos.ArchivoRegistroModulo>();
    public DbSet<DisenoFormulario> DisenosFormulario { get; set; }
    public DbSet<EstructuraFormulario> EstructurasFormulario { get; set; }
    public DbSet<ArchivoCampoFormulario> ArchivosCampoFormulario { get; set; }
    public DbSet<PasoNoAplica> PasosNoAplica { get; set; }
    public DbSet<VistaRegistro> VistasRegistro { get; set; }
    public DbSet<FormatoCondicional> FormatosCondicionales { get; set; }
    public DbSet<RegistroParametroTiro> RegistrosParametroTiro { get; set; }
    public DbSet<CampoExtraTorre> CamposExtraTorre { get; set; }
    public DbSet<ConfiguracionCampoExtraTorre> ConfiguracionesCampoExtraTorre { get; set; }
    public DbSet<CampoExtraComponente> CamposExtraComponente { get; set; }
    public DbSet<CarpetaHojaVida> CarpetasHojaVida { get; set; }
    public DbSet<ArchivoCarpetaHojaVida> ArchivosCarpetaHojaVida { get; set; }
    public DbSet<ArchivoComponente> ArchivosComponente { get; set; }
    public DbSet<ComponenteActividad> ComponenteActividades { get; set; }
    public DbSet<PlanoMaestro> PlanosMaestro { get; set; }
    public DbSet<ComponentePlano> ComponentesPlano { get; set; }
    public DbSet<ArchivoPlanoMaestro> ArchivosPlanoMaestro { get; set; }
    public DbSet<IconoCatalogo> IconosCatalogo { get; set; }
    public DbSet<GrupoEnsayo> GruposEnsayo { get; set; }
    public DbSet<GrupoPlano> GruposPlano { get; set; }
    public DbSet<GrupoDocumento> GruposDocumento { get; set; }
    public DbSet<GrupoEquipo> GruposEquipo { get; set; }
    public DbSet<GrupoMaterial> GruposMaterial { get; set; }
    public DbSet<GrupoResultadoEnsayo> GruposResultadoEnsayo { get; set; }
    public DbSet<TipoEnsayo> TiposEnsayo { get; set; }
    public DbSet<CampoEnsayo> CamposEnsayo { get; set; }
    public DbSet<ResultadoEnsayo> ResultadosEnsayo { get; set; }
    public DbSet<ResultadoEnsayoComponente> ResultadosEnsayoComponente { get; set; }
    public DbSet<ValorCampoEnsayo> ValoresCampoEnsayo { get; set; }
    public DbSet<Informe> Informes { get; set; }
    public DbSet<ArchivoInforme> ArchivosInforme { get; set; }
    public DbSet<MapeoColumnaEnsayo> MapeosColumnaEnsayo { get; set; }
    public DbSet<AliasTorre> AliasTorres { get; set; }
    public DbSet<DefinicionCatalogo> DefinicionesCatalogo { get; set; }
    // Cargas desde la plantilla Excel de proyecto (Data/Plantillas): agrupan los cambios para deshacerlos.
    public DbSet<CargaProyecto> CargasProyecto { get; set; }
    public DbSet<CambioCargaProyecto> CambiosCargaProyecto { get; set; }
    public DbSet<CampoCatalogoDefinicion> CamposCatalogoDefinicion { get; set; }
    public DbSet<ValorCampoCatalogoItem> ValoresCampoCatalogoItem { get; set; }
    public DbSet<Equipo> Equipos { get; set; }
    public DbSet<CampoEquipo> CamposEquipo { get; set; }
    public DbSet<ValorCampoEquipo> ValoresCampoEquipo { get; set; }
    public DbSet<FotoEquipo> FotosEquipo { get; set; }
    public DbSet<IntervencionEquipo> IntervencionesEquipo { get; set; }
    public DbSet<CargaEquipos> CargasEquipos { get; set; }
    public DbSet<AdjuntoEnsayo> AdjuntosEnsayo { get; set; }
    public DbSet<VersionMatrizRiesgos> VersionesMatrizRiesgos { get; set; }
    public DbSet<Riesgo> Riesgos { get; set; }
    public DbSet<AlcanceRiesgo> AlcancesRiesgo { get; set; }
    public DbSet<SeguimientoRiesgo> SeguimientosRiesgo { get; set; }
    public DbSet<HallazgoRiesgo> HallazgosRiesgo { get; set; }
    public DbSet<ArchivoRiesgo> ArchivosRiesgo { get; set; }
    public DbSet<ElementoRiesgo> ElementosRiesgo { get; set; }
    public DbSet<PlanAccion> PlanesAccion { get; set; }
    public DbSet<AccionPlan> AccionesPlan { get; set; }
    public DbSet<LugarPlan> LugaresPlan { get; set; }
    public DbSet<LugarHallazgo> LugaresHallazgo { get; set; }
    public DbSet<ArchivoAccionPlan> ArchivosAccionPlan { get; set; }
    public DbSet<ProductoAccion> ProductosAccion { get; set; }
    public DbSet<SeguimientoAccion> SeguimientosAccion { get; set; }
    public DbSet<ArchivoEquipo> ArchivosEquipo { get; set; }
    public DbSet<RiesgoPlan> RiesgosPlan { get; set; }
    public DbSet<Comunicado> Comunicados { get; set; }
    public DbSet<ActividadObra> ActividadesObra { get; set; }
    public DbSet<InformeDiario> InformesDiarios { get; set; }
    public DbSet<NotaInformeArea> NotasInformeArea { get; set; }
    public DbSet<RegistroAvance> RegistrosAvance { get; set; }
    public DbSet<RegistroAvanceTiro> RegistrosAvanceTiro { get; set; }
    public DbSet<FotoAvance> FotosAvance { get; set; }
    public DbSet<AdjuntoComunicado> AdjuntosComunicado { get; set; }
    public DbSet<CargaComunicados> CargasComunicados { get; set; }
    public DbSet<Indicador> Indicadores { get; set; }
    public DbSet<MedicionIndicador> MedicionesIndicador { get; set; }
    public DbSet<ArchivoMedicionIndicador> ArchivosMedicionIndicador { get; set; }
    public DbSet<CarpetaPlan> CarpetasPlan { get; set; }
    public DbSet<ArchivoCarpetaPlan> ArchivosCarpetaPlan { get; set; }
    public DbSet<DocumentoMaestro> DocumentosMaestro { get; set; }
    public DbSet<CargaDocumentos> CargasDocumentos { get; set; }
    public DbSet<VersionDocumento> VersionesDocumento { get; set; }
    public DbSet<ControlCambioVersion> ControlesCambioVersion { get; set; }
    public DbSet<RevisionDocumento> RevisionesDocumento { get; set; }
    public DbSet<CargaArchivosDocumentos> CargasArchivosDocumentos { get; set; }
    public DbSet<Certificacion> Certificaciones { get; set; }
    public DbSet<DocumentoDireccionamiento> DocumentosDireccionamiento { get; set; }
    public DbSet<VersionOrganigrama> VersionesOrganigrama { get; set; }
    public DbSet<CargoOrganigrama> CargosOrganigrama { get; set; }
    public DbSet<VersionPartesInteresadas> VersionesPartesInteresadas { get; set; }
    public DbSet<ParteInteresada> PartesInteresadas { get; set; }
    public DbSet<ProgramacionSeguimientoParte> ProgramacionesSeguimientoParte { get; set; }
    public DbSet<SeguimientoParteInteresada> SeguimientosParteInteresada { get; set; }
    public DbSet<CargaPlanos> CargasPlanos { get; set; }
    public DbSet<CargaArchivosPlanos> CargasArchivosPlanos { get; set; }
    public DbSet<CargaMateriales> CargasMateriales { get; set; }
    public DbSet<AsignacionPiezaModelo> AsignacionesPiezaModelo { get; set; }
    public DbSet<ModeloIfcCargado> ModelosIfcCargados { get; set; }
    public DbSet<AdjuntoMaterial> AdjuntosMaterial { get; set; }
    public DbSet<UbicacionMaterial> UbicacionesMaterial { get; set; }
    public DbSet<CargaPlanesAccion> CargasPlanesAccion { get; set; }
    public DbSet<ProgramaAuditoria> ProgramasAuditoria { get; set; }
    public DbSet<Auditoria> Auditorias { get; set; }
    public DbSet<CambioAuditoria> CambiosAuditoria { get; set; }
    public DbSet<LugarAuditoria> LugaresAuditoria { get; set; }
    public DbSet<HallazgoAuditoria> HallazgosAuditoria { get; set; }
    public DbSet<LugarHallazgoAuditoria> LugaresHallazgoAuditoria { get; set; }
    public DbSet<ArchivoHallazgoAuditoria> ArchivosHallazgoAuditoria { get; set; }
    public DbSet<ArchivoEficaciaPlan> ArchivosEficaciaPlan { get; set; }
    public DbSet<HallazgoPlan> HallazgosPlan { get; set; }

    // ---------- Costos y presupuestos (configuración en Data/Costos/ModeloCostos.cs) ----------
    public DbSet<ItemPago> ItemsPago { get; set; }
    public DbSet<AjustePrecio> AjustesPrecio { get; set; }
    public DbSet<ComponenteAjustePrecio> ComponentesAjustePrecio { get; set; }
    public DbSet<PrecioManual> PreciosManuales { get; set; }
    public DbSet<ModificacionContrato> ModificacionesContrato { get; set; }
    public DbSet<LineaModificacionContrato> LineasModificacionContrato { get; set; }
    public DbSet<CantidadTorre> CantidadesTorre { get; set; }
    public DbSet<RegistroCantidad> RegistrosCantidad { get; set; }
    public DbSet<ActaFacturacion> ActasFacturacion { get; set; }
    public DbSet<LineaActaFacturacion> LineasActaFacturacion { get; set; }
    public DbSet<CodigoPresupuesto> CodigosPresupuesto { get; set; }
    public DbSet<ValorPresupuesto> ValoresPresupuesto { get; set; }
    public DbSet<PorcentajeIndirecto> PorcentajesIndirectos { get; set; }
    public DbSet<CostoOrden> CostosOrden { get; set; }
    public DbSet<Persona> Personas { get; set; }
    public DbSet<MetaPeriodo> MetasPeriodo { get; set; }
    public DbSet<MetaProduccionDia> MetasProduccionDia { get; set; }
    public DbSet<AsignacionCostoActividad> AsignacionesCostoActividad { get; set; }
    public DbSet<ProgramaTrabajo> ProgramasTrabajo { get; set; }
    public DbSet<ActividadPrograma> ActividadesPrograma { get; set; }
    public DbSet<CambioPrograma> CambiosPrograma { get; set; }
    public DbSet<EstadoTorreManual> EstadosTorreManual { get; set; }
    public DbSet<ConfiguracionCostos> ConfiguracionCostos { get; set; }
    // Bot de Telegram (Administración › Bot de Telegram): chats autorizados.
    public DbSet<UsuarioBot> UsuariosBot { get; set; }
    // Consecutivos de comunicados de salida (Comunicados › Consecutivos, /consecutivo en Telegram).
    public DbSet<PlantillaConsecutivo> PlantillasConsecutivo { get; set; }
    public DbSet<ReservaConsecutivo> ReservasConsecutivo { get; set; }
    // Control de revisiones de la tabla de torres (cambios por torre, permanentes, deshacibles).
    public DbSet<RevisionTablaTorres> RevisionesTablaTorres { get; set; }
    public DbSet<CambioTablaTorres> CambiosTablaTorres { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // Creador de módulos (Data/CreadorModulos/ModelosModulo.cs).
        modelBuilder.Entity<SigWeb.Data.CreadorModulos.RegistroModulo>(e =>
        {
            e.HasQueryFilter(x => !x.Eliminado);
            e.HasIndex(x => new { x.Modulo, x.Consecutivo }).IsUnique();
            e.HasIndex(x => new { x.Modulo, x.Eliminado });
        });
        modelBuilder.Entity<SigWeb.Data.CreadorModulos.VinculoRegistroModulo>(e =>
        {
            e.HasQueryFilter(x => !x.Registro!.Eliminado);
            e.HasOne(x => x.Registro).WithMany().HasForeignKey(x => x.RegistroModuloId).OnDelete(DeleteBehavior.Cascade);
            e.HasIndex(x => new { x.RegistroModuloId, x.Campo });
            e.HasIndex(x => new { x.Fuente, x.ElementoId });
        });
        modelBuilder.Entity<SigWeb.Data.CreadorModulos.ArchivoRegistroModulo>(e =>
        {
            e.HasQueryFilter(x => !x.Eliminado && !x.Registro!.Eliminado);
            e.HasOne(x => x.Registro).WithMany().HasForeignKey(x => x.RegistroModuloId).OnDelete(DeleteBehavior.Cascade);
            e.HasIndex(x => new { x.RegistroModuloId, x.Campo });
        });
        modelBuilder.Entity<CambioCargaProyecto>().HasOne<CargaProyecto>().WithMany().HasForeignKey(c => c.CargaProyectoId).OnDelete(DeleteBehavior.Cascade);
        modelBuilder.Entity<CambioCargaProyecto>().HasIndex(c => new { c.CargaProyectoId, c.Orden });
        // Eliminar = mandar a la Papelera (Eliminado = true): estos filtros hacen que
        // lo eliminado desaparezca de todas las consultas sin tocar cada pantalla.
        // La Papelera y las revisiones de impacto usan IgnoreQueryFilters().
        modelBuilder.Entity<CarpetaHojaVida>().HasQueryFilter(e => !e.Eliminado);
        modelBuilder.Entity<CarpetaHojaVida>().HasIndex(e => e.NombreNormalizado).IsUnique().HasFilter("\"Eliminado\" = false AND \"ClaveElemento\" IS NULL");
        modelBuilder.Entity<CarpetaHojaVida>().HasIndex(e => new { e.ClaveElemento, e.NombreNormalizado }).IsUnique().HasFilter("\"Eliminado\" = false AND \"ClaveElemento\" IS NOT NULL");
        modelBuilder.Entity<ArchivoCarpetaHojaVida>().HasQueryFilter(e => !e.Eliminado && !e.Carpeta.Eliminado && e.Carpeta.Activo && (e.Componente == null || !e.Componente.Eliminado));
        modelBuilder.Entity<ArchivoCarpetaHojaVida>().HasOne(e => e.Carpeta).WithMany().HasForeignKey(e => e.CarpetaHojaVidaId).OnDelete(DeleteBehavior.Restrict);
        modelBuilder.Entity<ArchivoCarpetaHojaVida>().HasOne(e => e.Componente).WithMany().HasForeignKey(e => e.ComponenteId).OnDelete(DeleteBehavior.Restrict);
        modelBuilder.Entity<ArchivoCarpetaHojaVida>().HasOne<ElementoTrazado>().WithMany().HasForeignKey(e => e.ElementoTrazadoId).OnDelete(DeleteBehavior.Restrict);
        modelBuilder.Entity<GrupoEnsayo>().HasQueryFilter(e => !e.Eliminado);
        modelBuilder.Entity<TipoEnsayo>().HasQueryFilter(e => !e.Eliminado);
        modelBuilder.Entity<DefinicionCatalogo>().HasQueryFilter(e => !e.Eliminado);
        modelBuilder.Entity<GrupoPlano>().HasQueryFilter(e => !e.Eliminado);
        modelBuilder.Entity<GrupoDocumento>().HasQueryFilter(e => !e.Eliminado);
        modelBuilder.Entity<GrupoDocumento>().HasIndex(e => e.Nombre).IsUnique();
        modelBuilder.Entity<GrupoDocumento>().HasOne<IconoCatalogo>().WithMany().HasForeignKey(e => e.IconoCatalogoId).OnDelete(DeleteBehavior.SetNull);
        modelBuilder.Entity<DocumentoMaestro>().HasOne<GrupoDocumento>().WithMany().HasForeignKey(e => e.GrupoDocumentoId).OnDelete(DeleteBehavior.SetNull);
        modelBuilder.Entity<GrupoEquipo>().HasQueryFilter(e => !e.Eliminado);
        modelBuilder.Entity<GrupoEquipo>().HasIndex(e => e.Nombre).IsUnique();
        modelBuilder.Entity<GrupoEquipo>().HasOne<IconoCatalogo>().WithMany().HasForeignKey(e => e.IconoCatalogoId).OnDelete(DeleteBehavior.SetNull);
        modelBuilder.Entity<Equipo>().HasOne<GrupoEquipo>().WithMany().HasForeignKey(e => e.GrupoEquipoId).OnDelete(DeleteBehavior.SetNull);
        // Categorías de Materiales y de los resultados de cada tipo de ensayo (pestañas, 2026-11-16).
        modelBuilder.Entity<GrupoMaterial>().HasQueryFilter(e => !e.Eliminado);
        modelBuilder.Entity<GrupoMaterial>().HasIndex(e => e.Nombre).IsUnique();
        modelBuilder.Entity<GrupoMaterial>().HasOne<IconoCatalogo>().WithMany().HasForeignKey(e => e.IconoCatalogoId).OnDelete(DeleteBehavior.SetNull);
        modelBuilder.Entity<Material>().HasOne<GrupoMaterial>().WithMany().HasForeignKey(e => e.GrupoMaterialId).OnDelete(DeleteBehavior.SetNull);
        modelBuilder.Entity<GrupoResultadoEnsayo>().HasQueryFilter(e => !e.Eliminado);
        modelBuilder.Entity<GrupoResultadoEnsayo>().HasIndex(e => new { e.TipoEnsayoId, e.Nombre }).IsUnique();
        modelBuilder.Entity<GrupoResultadoEnsayo>().HasOne<IconoCatalogo>().WithMany().HasForeignKey(e => e.IconoCatalogoId).OnDelete(DeleteBehavior.SetNull);
        modelBuilder.Entity<GrupoResultadoEnsayo>().HasOne<TipoEnsayo>().WithMany().HasForeignKey(e => e.TipoEnsayoId).OnDelete(DeleteBehavior.Cascade);
        modelBuilder.Entity<ResultadoEnsayo>().HasOne<GrupoResultadoEnsayo>().WithMany().HasForeignKey(e => e.GrupoResultadoEnsayoId).OnDelete(DeleteBehavior.SetNull);
        modelBuilder.Entity<DocumentoMaestro>().HasQueryFilter(e => !e.Eliminado);
        modelBuilder.Entity<DocumentoMaestro>().HasIndex(e => e.Codigo);
        modelBuilder.Entity<DocumentoMaestro>().HasOne<CargaDocumentos>().WithMany().HasForeignKey(e => e.CargaDocumentosId).OnDelete(DeleteBehavior.SetNull);
        modelBuilder.Entity<CargaDocumentos>().HasQueryFilter(e => !e.Eliminado);
        modelBuilder.Entity<Certificacion>().HasQueryFilter(e => !e.Eliminado);
        modelBuilder.Entity<Certificacion>().HasOne<DocumentoMaestro>().WithMany().HasForeignKey(e => e.DocumentoMaestroId).OnDelete(DeleteBehavior.SetNull);
        modelBuilder.Entity<DocumentoDireccionamiento>().HasQueryFilter(e => !e.Eliminado);
        modelBuilder.Entity<DocumentoDireccionamiento>().HasOne<DocumentoMaestro>().WithMany().HasForeignKey(e => e.DocumentoMaestroId).OnDelete(DeleteBehavior.SetNull);
        // Organigrama: versión = foto completa; los cargos se borran con su versión. Jefe dentro de la misma versión.
        modelBuilder.Entity<VersionOrganigrama>().HasQueryFilter(e => !e.Eliminado);
        modelBuilder.Entity<VersionOrganigrama>().HasOne<VersionOrganigrama>().WithMany().HasForeignKey(e => e.VersionBaseId).OnDelete(DeleteBehavior.SetNull);
        modelBuilder.Entity<CargoOrganigrama>().HasQueryFilter(e => !e.Eliminado);
        modelBuilder.Entity<CargoOrganigrama>().HasOne<VersionOrganigrama>().WithMany().HasForeignKey(e => e.VersionOrganigramaId).OnDelete(DeleteBehavior.Cascade);
        modelBuilder.Entity<CargoOrganigrama>().HasOne<CargoOrganigrama>().WithMany().HasForeignKey(e => e.JefeId).OnDelete(DeleteBehavior.NoAction);
        modelBuilder.Entity<CargoOrganigrama>().HasIndex(e => e.VersionOrganigramaId);
        modelBuilder.Entity<CargoOrganigrama>().HasIndex(e => e.Clave);
        // Partes interesadas: versión = foto completa (las partes se borran con su versión). Seguimientos y
        // programación van por Clave (sobreviven a las versiones); la fila con que se hizo el seguimiento es SetNull.
        modelBuilder.Entity<VersionPartesInteresadas>().HasQueryFilter(e => !e.Eliminado);
        modelBuilder.Entity<VersionPartesInteresadas>().HasOne<VersionPartesInteresadas>().WithMany().HasForeignKey(e => e.VersionBaseId).OnDelete(DeleteBehavior.SetNull);
        modelBuilder.Entity<ParteInteresada>().HasQueryFilter(e => !e.Eliminado);
        modelBuilder.Entity<ParteInteresada>().HasOne<VersionPartesInteresadas>().WithMany().HasForeignKey(e => e.VersionPartesInteresadasId).OnDelete(DeleteBehavior.Cascade);
        modelBuilder.Entity<ParteInteresada>().HasIndex(e => e.VersionPartesInteresadasId);
        modelBuilder.Entity<ParteInteresada>().HasIndex(e => e.Clave);
        modelBuilder.Entity<ParteInteresada>().Property(e => e.Impacto).HasPrecision(5, 2);
        modelBuilder.Entity<ParteInteresada>().Property(e => e.Influencia).HasPrecision(5, 2);
        modelBuilder.Entity<ProgramacionSeguimientoParte>().HasIndex(e => e.ParteClave).IsUnique();
        modelBuilder.Entity<UsuarioBot>().HasIndex(e => e.ChatId).IsUnique();
        modelBuilder.Entity<PlantillaConsecutivo>().Property(e => e.CifrasNumero).HasDefaultValue(4);   // 0001
        modelBuilder.Entity<ReservaConsecutivo>().HasIndex(e => e.Codigo).IsUnique();
        modelBuilder.Entity<RevisionTablaTorres>().HasIndex(e => e.Codigo);
        modelBuilder.Entity<RevisionTablaTorres>().Property(e => e.Origen).HasDefaultValue(RevisionTablaTorres.OrigenTabla);
        modelBuilder.Entity<CambioTablaTorres>().HasIndex(e => e.RevisionTablaTorresId);
        modelBuilder.Entity<CambioTablaTorres>().HasIndex(e => e.ElementoTrazadoId);
        modelBuilder.Entity<CambioTablaTorres>().HasOne<RevisionTablaTorres>().WithMany().HasForeignKey(e => e.RevisionTablaTorresId).OnDelete(DeleteBehavior.Cascade);
        modelBuilder.Entity<ReservaConsecutivo>().HasIndex(e => new { e.PlantillaConsecutivoId, e.Anio, e.Numero }).IsUnique();
        modelBuilder.Entity<ReservaConsecutivo>().HasIndex(e => e.Estado);
        modelBuilder.Entity<ReservaConsecutivo>().HasOne<PlantillaConsecutivo>().WithMany().HasForeignKey(e => e.PlantillaConsecutivoId).OnDelete(DeleteBehavior.Restrict);
        modelBuilder.Entity<ReservaConsecutivo>().HasOne<Comunicado>().WithMany().HasForeignKey(e => e.ComunicadoId).OnDelete(DeleteBehavior.SetNull);
        modelBuilder.Entity<SeguimientoParteInteresada>().HasQueryFilter(e => !e.Eliminado);
        modelBuilder.Entity<SeguimientoParteInteresada>().HasOne<ParteInteresada>().WithMany().HasForeignKey(e => e.ParteInteresadaId).OnDelete(DeleteBehavior.SetNull);
        modelBuilder.Entity<SeguimientoParteInteresada>().HasIndex(e => e.ParteClave);
        modelBuilder.Entity<SeguimientoParteInteresada>().Property(e => e.Impacto).HasPrecision(5, 2);
        modelBuilder.Entity<SeguimientoParteInteresada>().Property(e => e.Influencia).HasPrecision(5, 2);
        modelBuilder.Entity<VersionDocumento>().HasQueryFilter(e => !e.Eliminado);
        modelBuilder.Entity<AdjuntoMaterial>().HasQueryFilter(e => !e.Eliminado);
        modelBuilder.Entity<AdjuntoMaterial>().HasOne<Material>().WithMany().HasForeignKey(e => e.MaterialId).OnDelete(DeleteBehavior.Cascade);
        modelBuilder.Entity<AdjuntoMaterial>().HasIndex(e => e.MaterialId);
        modelBuilder.Entity<UbicacionMaterial>().HasOne<Material>().WithMany().HasForeignKey(e => e.MaterialId).OnDelete(DeleteBehavior.Cascade);
        modelBuilder.Entity<UbicacionMaterial>().HasOne<ElementoTrazado>().WithMany().HasForeignKey(e => e.ElementoTrazadoId).OnDelete(DeleteBehavior.Cascade);
        modelBuilder.Entity<UbicacionMaterial>().HasOne<Componente>().WithMany().HasForeignKey(e => e.ComponenteId).OnDelete(DeleteBehavior.Cascade);
        modelBuilder.Entity<UbicacionMaterial>().HasOne<Tiro>().WithMany().HasForeignKey(e => e.TiroId).OnDelete(DeleteBehavior.Cascade);
        modelBuilder.Entity<UbicacionMaterial>().HasIndex(e => e.MaterialId);
        modelBuilder.Entity<UbicacionMaterial>().HasIndex(e => e.ElementoTrazadoId);
        modelBuilder.Entity<VersionDocumento>().HasOne<DocumentoMaestro>().WithMany().HasForeignKey(e => e.DocumentoMaestroId).OnDelete(DeleteBehavior.Cascade);
        modelBuilder.Entity<CargaArchivosDocumentos>().HasQueryFilter(e => !e.Eliminado);
        modelBuilder.Entity<CargaPlanos>().HasQueryFilter(e => !e.Eliminado);
        modelBuilder.Entity<CargaArchivosPlanos>().HasQueryFilter(e => !e.Eliminado);
        modelBuilder.Entity<CargaMateriales>().HasQueryFilter(e => !e.Eliminado);
        modelBuilder.Entity<CargaPlanesAccion>().HasQueryFilter(e => !e.Eliminado);
        modelBuilder.Entity<PlanAccion>().HasOne<CargaPlanesAccion>().WithMany().HasForeignKey(e => e.CargaPlanesAccionId).OnDelete(DeleteBehavior.SetNull);
        modelBuilder.Entity<AsignacionPiezaModelo>().HasIndex(e => new { e.Modelo, e.Pieza }).IsUnique();
        modelBuilder.Entity<AsignacionPiezaModelo>().HasOne<ArchivoPlanoMaestro>().WithMany().HasForeignKey(e => e.ArchivoPlanoMaestroId).OnDelete(DeleteBehavior.SetNull);
        modelBuilder.Entity<ModeloIfcCargado>().HasIndex(e => new { e.Codigo, e.Version });
        modelBuilder.Entity<ModeloIfcCargado>().HasQueryFilter(e => !e.Eliminado);
        modelBuilder.Entity<Material>().HasOne<CargaMateriales>().WithMany().HasForeignKey(e => e.CargaMaterialesId).OnDelete(DeleteBehavior.SetNull);
        modelBuilder.Entity<CargaEquipos>().HasQueryFilter(e => !e.Eliminado);
        modelBuilder.Entity<IntervencionEquipo>().HasQueryFilter(e => !e.Eliminado);
        modelBuilder.Entity<PlanoMaestro>().HasOne<CargaPlanos>().WithMany().HasForeignKey(e => e.CargaPlanosId).OnDelete(DeleteBehavior.SetNull);
        modelBuilder.Entity<ArchivoPlanoMaestro>().HasOne<CargaArchivosPlanos>().WithMany().HasForeignKey(e => e.CargaArchivosPlanosId).OnDelete(DeleteBehavior.SetNull);
        modelBuilder.Entity<VersionDocumento>().HasOne<CargaArchivosDocumentos>().WithMany().HasForeignKey(e => e.CargaArchivosDocumentosId).OnDelete(DeleteBehavior.SetNull);
        modelBuilder.Entity<ControlCambioVersion>().HasOne<VersionDocumento>().WithMany().HasForeignKey(e => e.VersionDocumentoId).OnDelete(DeleteBehavior.Cascade);
        modelBuilder.Entity<RevisionDocumento>().HasOne<DocumentoMaestro>().WithMany().HasForeignKey(e => e.DocumentoMaestroId).OnDelete(DeleteBehavior.Cascade);
        // Quitar una fila del control de cambios no borra sus archivos: van a la Papelera y quedan sin revisión.
        modelBuilder.Entity<VersionDocumento>().HasOne<RevisionDocumento>().WithMany().HasForeignKey(e => e.RevisionDocumentoId).OnDelete(DeleteBehavior.SetNull);
        modelBuilder.Entity<ParametroActividad>().HasQueryFilter(e => !e.Eliminado);
        modelBuilder.Entity<VersionMatrizRiesgos>().HasQueryFilter(e => !e.Eliminado);
        modelBuilder.Entity<Riesgo>().HasQueryFilter(e => !e.Eliminado);
        modelBuilder.Entity<SeguimientoRiesgo>().HasQueryFilter(e => !e.Eliminado);
        modelBuilder.Entity<HallazgoRiesgo>().HasQueryFilter(e => !e.Eliminado);
        modelBuilder.Entity<ArchivoRiesgo>().HasQueryFilter(e => !e.Eliminado);
        modelBuilder.Entity<PlanAccion>().HasQueryFilter(e => !e.Eliminado);
        modelBuilder.Entity<AccionPlan>().HasQueryFilter(e => !e.Eliminado);
        modelBuilder.Entity<ArchivoAccionPlan>().HasQueryFilter(e => !e.Eliminado);

        // Equipos: campos configurables, valores, fotos e historial de intervenciones
        modelBuilder.Entity<CampoEquipo>().HasIndex(c => c.Clave).IsUnique();
        modelBuilder.Entity<ValorCampoEquipo>().HasIndex(v => new { v.EquipoId, v.CampoEquipoId }).IsUnique();
        modelBuilder.Entity<ValorCampoEquipo>().HasOne<Equipo>().WithMany().HasForeignKey(v => v.EquipoId).OnDelete(DeleteBehavior.Cascade);
        modelBuilder.Entity<ValorCampoEquipo>().HasOne<CampoEquipo>().WithMany().HasForeignKey(v => v.CampoEquipoId).OnDelete(DeleteBehavior.Cascade);
        modelBuilder.Entity<FotoEquipo>().HasOne<Equipo>().WithMany().HasForeignKey(f => f.EquipoId).OnDelete(DeleteBehavior.Cascade);
        modelBuilder.Entity<ArchivoEquipo>().HasOne<Equipo>().WithMany().HasForeignKey(f => f.EquipoId).OnDelete(DeleteBehavior.Cascade);
        modelBuilder.Entity<ArchivoEquipo>().HasQueryFilter(e => !e.Eliminado);
        // Riesgos del plan: si el riesgo se borra definitivamente, se quita la relación (el plan queda).
        modelBuilder.Entity<RiesgoPlan>().HasOne<PlanAccion>().WithMany().HasForeignKey(e => e.PlanAccionId).OnDelete(DeleteBehavior.Cascade);
        modelBuilder.Entity<RiesgoPlan>().HasOne<Riesgo>().WithMany().HasForeignKey(e => e.RiesgoId).OnDelete(DeleteBehavior.Cascade);
        modelBuilder.Entity<RiesgoPlan>().HasIndex(e => new { e.PlanAccionId, e.RiesgoId }).IsUnique();
        modelBuilder.Entity<Comunicado>().HasQueryFilter(e => !e.Eliminado);
        modelBuilder.Entity<Comunicado>().HasIndex(e => e.Consecutivo);
        modelBuilder.Entity<Comunicado>().HasOne<CargaComunicados>().WithMany().HasForeignKey(e => e.CargaComunicadosId).OnDelete(DeleteBehavior.SetNull);
        modelBuilder.Entity<AdjuntoComunicado>().HasQueryFilter(e => !e.Eliminado);
        modelBuilder.Entity<AdjuntoComunicado>().HasOne<Comunicado>().WithMany().HasForeignKey(e => e.ComunicadoId).OnDelete(DeleteBehavior.Cascade);
        modelBuilder.Entity<CargaComunicados>().HasQueryFilter(e => !e.Eliminado);
        // Avance de obra (informe diario F-054)
        modelBuilder.Entity<ActividadObra>().HasQueryFilter(e => !e.Eliminado);
        modelBuilder.Entity<ActividadObra>().Property(e => e.Peso).HasPrecision(9, 5);
        modelBuilder.Entity<InformeDiario>().HasQueryFilter(e => !e.Eliminado);
        modelBuilder.Entity<InformeDiario>().HasIndex(e => e.Fecha);
        modelBuilder.Entity<NotaInformeArea>().HasOne<InformeDiario>().WithMany().HasForeignKey(e => e.InformeDiarioId).OnDelete(DeleteBehavior.Cascade);
        modelBuilder.Entity<RegistroAvance>().HasQueryFilter(e => !e.Eliminado);
        modelBuilder.Entity<RegistroAvance>().HasOne<InformeDiario>().WithMany().HasForeignKey(e => e.InformeDiarioId).OnDelete(DeleteBehavior.Cascade);
        modelBuilder.Entity<RegistroAvance>().HasOne<ActividadObra>().WithMany().HasForeignKey(e => e.ActividadObraId).OnDelete(DeleteBehavior.Restrict);
        modelBuilder.Entity<RegistroAvance>().HasOne<ElementoTrazado>().WithMany().HasForeignKey(e => e.ElementoTrazadoId).OnDelete(DeleteBehavior.Cascade);
        modelBuilder.Entity<RegistroAvance>().HasIndex(e => new { e.ElementoTrazadoId, e.Fecha });
        modelBuilder.Entity<RegistroAvance>().Property(e => e.Cantidad).HasPrecision(14, 3);
        modelBuilder.Entity<RegistroAvance>().Property(e => e.AvanceAcumulado).HasPrecision(6, 2);
        modelBuilder.Entity<RegistroAvanceTiro>().HasQueryFilter(e => !e.Eliminado);
        modelBuilder.Entity<RegistroAvanceTiro>().HasOne<InformeDiario>().WithMany().HasForeignKey(e => e.InformeDiarioId).OnDelete(DeleteBehavior.Cascade);
        modelBuilder.Entity<RegistroAvanceTiro>().HasOne<ActividadObra>().WithMany().HasForeignKey(e => e.ActividadObraId).OnDelete(DeleteBehavior.Restrict);
        modelBuilder.Entity<RegistroAvanceTiro>().HasOne<Tiro>().WithMany().HasForeignKey(e => e.TiroId).OnDelete(DeleteBehavior.Cascade);
        modelBuilder.Entity<RegistroAvanceTiro>().HasIndex(e => new { e.TiroId, e.Fecha });
        modelBuilder.Entity<RegistroAvanceTiro>().Property(e => e.Cantidad).HasPrecision(14, 3);
        modelBuilder.Entity<RegistroAvanceTiro>().Property(e => e.AvanceAcumulado).HasPrecision(6, 2);
        modelBuilder.Entity<FotoAvance>().HasQueryFilter(e => !e.Eliminado);
        modelBuilder.Entity<FotoAvance>().HasOne<InformeDiario>().WithMany().HasForeignKey(e => e.InformeDiarioId).OnDelete(DeleteBehavior.Cascade);
        SigWeb.Data.Costos.ModeloCostos.Configurar(modelBuilder);
        modelBuilder.Entity<Indicador>().HasQueryFilter(e => !e.Eliminado);
        modelBuilder.Entity<MedicionIndicador>().HasQueryFilter(e => !e.Eliminado);
        modelBuilder.Entity<MedicionIndicador>().HasOne<Indicador>().WithMany().HasForeignKey(e => e.IndicadorId).OnDelete(DeleteBehavior.Cascade);
        modelBuilder.Entity<MedicionIndicador>().HasIndex(e => new { e.IndicadorId, e.Anio, e.Mes });
        modelBuilder.Entity<ArchivoMedicionIndicador>().HasQueryFilter(e => !e.Eliminado);
        modelBuilder.Entity<ArchivoMedicionIndicador>().HasOne<MedicionIndicador>().WithMany().HasForeignKey(e => e.MedicionIndicadorId).OnDelete(DeleteBehavior.Cascade);
        modelBuilder.Entity<CarpetaPlan>().HasOne<PlanAccion>().WithMany().HasForeignKey(e => e.PlanAccionId).OnDelete(DeleteBehavior.Cascade);
        modelBuilder.Entity<CarpetaPlan>().HasQueryFilter(e => !e.Eliminado);
        modelBuilder.Entity<ArchivoCarpetaPlan>().HasOne<CarpetaPlan>().WithMany().HasForeignKey(e => e.CarpetaPlanId).OnDelete(DeleteBehavior.Cascade);
        modelBuilder.Entity<ArchivoCarpetaPlan>().HasQueryFilter(e => !e.Eliminado);
        modelBuilder.Entity<IntervencionEquipo>().HasOne<Equipo>().WithMany().HasForeignKey(i => i.EquipoId).OnDelete(DeleteBehavior.Cascade);
        modelBuilder.Entity<IntervencionEquipo>().HasIndex(i => new { i.EquipoId, i.Fecha });
        modelBuilder.Entity<AdjuntoEnsayo>().HasIndex(a => a.Hash).IsUnique();
        modelBuilder.Entity<ValorCampoEnsayo>().HasOne<AdjuntoEnsayo>().WithMany().HasForeignKey(v => v.AdjuntoEnsayoId).OnDelete(DeleteBehavior.SetNull);
        modelBuilder.Entity<Equipo>().HasOne<CargaEquipos>().WithMany().HasForeignKey(e => e.CargaEquiposId).OnDelete(DeleteBehavior.SetNull);
        modelBuilder.Entity<IntervencionEquipo>().HasOne<CargaEquipos>().WithMany().HasForeignKey(i => i.CargaEquiposId).OnDelete(DeleteBehavior.SetNull);

        // No se permite repetir el mismo nombre dentro de un mismo catálogo
        modelBuilder.Entity<ItemCatalogo>()
            .HasIndex(c => new { c.Tipo, c.Nombre })
            .IsUnique();

        // Cada foto pertenece a un material. Sin propiedad de navegación en
        // Material (a propósito): así Update(materialActual) nunca puede
        // interpretar "sin fotos cargadas en este objeto" como "bórralas todas".
        modelBuilder.Entity<FotoMaterial>()
            .HasOne<Material>()
            .WithMany()
            .HasForeignKey(f => f.MaterialId)
            .OnDelete(DeleteBehavior.Cascade);

        // Un componente (pata, trincho, etc.) pertenece a una torre. Si se
        // borra la torre, sus componentes se borran con ella (no tienen
        // sentido sin su torre). Nota: "eliminar" una torre desde el mapa
        // solo marca Eliminado = true (ver ElementoTrazado); este Cascade
        // solo aplica al borrado físico definitivo, desde la Papelera.
        modelBuilder.Entity<Componente>()
            .HasOne<ElementoTrazado>()
            .WithMany()
            .HasForeignKey(c => c.ElementoTrazadoId)
            .OnDelete(DeleteBehavior.Cascade);

        // Una torre pertenece a una capa (la carga de KMZ/Excel que la
        // trajo). Si esa capa se borrara alguna vez, la torre no se borra
        // con ella: simplemente queda sin capa (SetNull).
        modelBuilder.Entity<ElementoTrazado>()
            .HasOne<CapaTrazado>()
            .WithMany()
            .HasForeignKey(e => e.CapaTrazadoId)
            .OnDelete(DeleteBehavior.SetNull);

        // Un ítem del catálogo "Localización" puede provenir de una torre
        // del trazado. Si esa torre se borrara físicamente, el ítem del
        // catálogo no se borra: simplemente queda sin vínculo (SetNull).
        modelBuilder.Entity<ItemCatalogo>()
            .HasOne<ElementoTrazado>()
            .WithMany()
            .HasForeignKey(c => c.ElementoTrazadoId)
            .OnDelete(DeleteBehavior.SetNull);

        // Un parámetro pertenece a una actividad; si se borra la actividad,
        // sus parámetros se van con ella.
        modelBuilder.Entity<ParametroActividad>()
            .HasOne<Actividad>()
            .WithMany()
            .HasForeignKey(p => p.ActividadId)
            .OnDelete(DeleteBehavior.Cascade);

        // Un registro (el "check" de un parámetro para un componente) se
        // borra si se borra el componente o el parámetro al que pertenece.
        modelBuilder.Entity<RegistroParametro>()
            .HasOne<Componente>()
            .WithMany()
            .HasForeignKey(r => r.ComponenteId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<RegistroParametro>()
            .HasOne<EstadoParametro>()
            .WithMany()
            .HasForeignKey(r => r.EstadoParametroId)
            .OnDelete(DeleteBehavior.SetNull);

        modelBuilder.Entity<RegistroParametro>()
            .HasOne<ParametroActividad>()
            .WithMany()
            .HasForeignKey(r => r.ParametroActividadId)
            .OnDelete(DeleteBehavior.Cascade);

        // Íconos del catálogo asignados a puntos de la app: uno por punto; si se borra el ícono, vuelve el de por defecto.
        modelBuilder.Entity<IconoCatalogo>().HasQueryFilter(i => !i.Eliminado);
        modelBuilder.Entity<AsignacionIcono>().HasIndex(a => a.Punto).IsUnique();
        modelBuilder.Entity<AsignacionIcono>()
            .HasOne<IconoCatalogo>().WithMany().HasForeignKey(a => a.IconoCatalogoId).OnDelete(DeleteBehavior.SetNull);

        // Campos de tablas (ver Models/CamposTabla.cs)
        modelBuilder.Entity<CampoTabla>().HasIndex(c => new { c.Tabla, c.Clave }).IsUnique();
        // Diseñador de formularios: versiones por formulario y la estructura que declara el código.
        modelBuilder.Entity<DisenoFormulario>().HasIndex(d => new { d.Clave, d.Version }).IsUnique();
        modelBuilder.Entity<EstructuraFormulario>().HasIndex(e => e.Clave).IsUnique();
        modelBuilder.Entity<ArchivoCampoFormulario>().HasIndex(a => new { a.CampoTablaId, a.FilaId });
        modelBuilder.Entity<ArchivoCampoFormulario>().HasQueryFilter(a => !a.Eliminado);
        modelBuilder.Entity<PasoNoAplica>().HasIndex(p => new { p.Formulario, p.FilaId, p.Paso }).IsUnique();
        modelBuilder.Entity<VistaRegistro>().HasIndex(v => v.Modulo);
        modelBuilder.Entity<VistaRegistro>().HasQueryFilter(v => !v.Eliminado);
        modelBuilder.Entity<ValorCampoTabla>()
            .HasOne<CampoTabla>().WithMany().HasForeignKey(v => v.CampoTablaId).OnDelete(DeleteBehavior.Cascade);
        modelBuilder.Entity<ValorCampoTabla>().HasIndex(v => new { v.CampoTablaId, v.FilaId }).IsUnique();
        modelBuilder.Entity<FormatoCondicional>()
            .HasOne<CampoTabla>().WithMany().HasForeignKey(f => f.CampoTablaId).OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<CambioTiro>().HasIndex(c => c.Fecha);
        modelBuilder.Entity<CambioTiro>().HasIndex(c => c.Lote);

        // Tiros de tendido: al borrar definitivamente un tiro, sus torres
        // quedan sin tiro y sus registros se borran.
        modelBuilder.Entity<ElementoTrazado>()
            .HasOne<Tiro>()
            .WithMany()
            .HasForeignKey(e => e.TiroId)
            .OnDelete(DeleteBehavior.SetNull);

        modelBuilder.Entity<RegistroParametroTiro>()
            .HasOne<Tiro>()
            .WithMany()
            .HasForeignKey(r => r.TiroId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<RegistroParametroTiro>()
            .HasOne<ParametroActividad>()
            .WithMany()
            .HasForeignKey(r => r.ParametroActividadId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<RegistroParametroTiro>()
            .HasOne<EstadoParametro>()
            .WithMany()
            .HasForeignKey(r => r.EstadoParametroId)
            .OnDelete(DeleteBehavior.SetNull);

        modelBuilder.Entity<RegistroParametroTiro>()
            .HasIndex(r => new { r.TiroId, r.ParametroActividadId })
            .IsUnique();

        // Un adjunto se borra si se borra el registro (parámetro marcado)
        // al que pertenece.
        modelBuilder.Entity<ArchivoRegistroParametro>()
            .HasOne<RegistroParametro>()
            .WithMany()
            .HasForeignKey(a => a.RegistroParametroId)
            .OnDelete(DeleteBehavior.Cascade);

        // Si se borra el componente (pata) al que apuntaba un material,
        // el material NO se borra: simplemente queda sin ubicación (SetNull).
        modelBuilder.Entity<Material>()
            .HasOne<Componente>()
            .WithMany()
            .HasForeignKey(m => m.ComponenteId)
            .OnDelete(DeleteBehavior.SetNull);

        // Datos adicionales importados desde la "tabla de torres" en Excel:
        // se borran junto con la torre o el componente al que pertenecen.
        modelBuilder.Entity<CampoExtraTorre>()
            .HasOne<ElementoTrazado>()
            .WithMany()
            .HasForeignKey(c => c.ElementoTrazadoId)
            .OnDelete(DeleteBehavior.Cascade);

        // Qué columnas de "datos adicionales" se ven o se ocultan (ver
        // ConfiguracionCampoExtraTorre): una sola fila por Clave.
        modelBuilder.Entity<ConfiguracionCampoExtraTorre>()
            .HasIndex(c => c.Clave)
            .IsUnique();

        modelBuilder.Entity<CampoExtraComponente>()
            .HasOne<Componente>()
            .WithMany()
            .HasForeignKey(c => c.ComponenteId)
            .OnDelete(DeleteBehavior.Cascade);

        // Un plano/archivo adjunto directo a un componente (pata) se borra
        // si se borra el componente al que pertenece.
        modelBuilder.Entity<ArchivoComponente>()
            .HasOne<Componente>()
            .WithMany()
            .HasForeignKey(a => a.ComponenteId)
            .OnDelete(DeleteBehavior.Cascade);

        // Qué actividades se le habilitan a cada elemento (pata, trincho,
        // gavión...). Se borra junto con el componente o la actividad.
        modelBuilder.Entity<ComponenteActividad>()
            .HasOne<Componente>()
            .WithMany()
            .HasForeignKey(ca => ca.ComponenteId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<ComponenteActividad>()
            .HasOne<Actividad>()
            .WithMany()
            .HasForeignKey(ca => ca.ActividadId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<ComponenteActividad>()
            .HasIndex(ca => new { ca.ComponenteId, ca.ActividadId })
            .IsUnique();

        // Vínculo componente <-> UN archivo puntual del listado maestro (no
        // toda la fila/código, para poder elegir una sola hoja entre varias
        // del mismo plano): se borra si se borra el componente o el archivo
        // (borrado físico definitivo, no el "a la papelera"). No se permite
        // vincular el mismo archivo dos veces al mismo componente.
        modelBuilder.Entity<ComponentePlano>()
            .HasOne<Componente>()
            .WithMany()
            .HasForeignKey(cp => cp.ComponenteId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<ComponentePlano>()
            .HasOne<ArchivoPlanoMaestro>()
            .WithMany()
            .HasForeignKey(cp => cp.ArchivoPlanoMaestroId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<ComponentePlano>()
            .HasIndex(cp => new { cp.ComponenteId, cp.ArchivoPlanoMaestroId })
            .IsUnique();

        // Los PDF de una fila del listado maestro se borran si se borra
        // físicamente la fila (borrado definitivo desde Papelera).
        modelBuilder.Entity<ArchivoPlanoMaestro>()
            .HasOne<PlanoMaestro>()
            .WithMany()
            .HasForeignKey(a => a.PlanoMaestroId)
            .OnDelete(DeleteBehavior.Cascade);

        // Galería de íconos (Catálogos) y módulo Ensayos: un tipo de
        // ensayo pertenece a un grupo (Concretos, Suelos...) y se borra
        // con él; si un grupo o tipo de ensayo apunta a un ícono que se
        // llegara a borrar de la galería, simplemente queda sin ícono
        // propio (SetNull) y usa el genérico, no se rompe nada.
        modelBuilder.Entity<GrupoPlano>().HasIndex(g => g.Nombre).IsUnique();
        modelBuilder.Entity<Actividad>().HasOne<IconoCatalogo>().WithMany().HasForeignKey(a => a.IconoCatalogoId).OnDelete(DeleteBehavior.SetNull);
        modelBuilder.Entity<GrupoPlano>().HasOne<IconoCatalogo>().WithMany().HasForeignKey(g => g.IconoCatalogoId).OnDelete(DeleteBehavior.SetNull);
        modelBuilder.Entity<PlanoMaestro>().HasOne<GrupoPlano>().WithMany().HasForeignKey(p => p.GrupoPlanoId).OnDelete(DeleteBehavior.SetNull);
        modelBuilder.Entity<DefinicionCatalogo>().HasOne<IconoCatalogo>().WithMany().HasForeignKey(d => d.IconoCatalogoId).OnDelete(DeleteBehavior.SetNull);

        modelBuilder.Entity<IconoCatalogo>()
            .HasIndex(i => i.Clave)
            .IsUnique();

        // ---------- Matriz de riesgos ----------
        // Los riesgos cuelgan de su versión (revisión anual) y se borran con
        // ella; el encadenamiento con la revisión anterior es solo informativo,
        // por eso NoAction: borrar una versión vieja no debe arrastrar la nueva.
        modelBuilder.Entity<VersionMatrizRiesgos>().HasIndex(v => v.Codigo).IsUnique();
        modelBuilder.Entity<VersionMatrizRiesgos>()
            .HasOne<VersionMatrizRiesgos>().WithMany().HasForeignKey(v => v.VersionAnteriorId).OnDelete(DeleteBehavior.NoAction);

        modelBuilder.Entity<Riesgo>()
            .HasOne<VersionMatrizRiesgos>().WithMany().HasForeignKey(r => r.VersionMatrizRiesgosId).OnDelete(DeleteBehavior.Cascade);
        modelBuilder.Entity<Riesgo>()
            .HasOne<Riesgo>().WithMany().HasForeignKey(r => r.RiesgoAnteriorId).OnDelete(DeleteBehavior.NoAction);
        modelBuilder.Entity<Riesgo>().HasIndex(r => new { r.VersionMatrizRiesgosId, r.Codigo }).IsUnique();

        // Alcance: a qué torre / pata / localización / tiro aplica el riesgo.
        // Si el elemento desaparece, el alcance se va con él (el riesgo queda,
        // simplemente deja de estar acotado a ese elemento).
        modelBuilder.Entity<AlcanceRiesgo>()
            .HasOne<Riesgo>().WithMany().HasForeignKey(a => a.RiesgoId).OnDelete(DeleteBehavior.Cascade);
        modelBuilder.Entity<AlcanceRiesgo>()
            .HasOne<ElementoTrazado>().WithMany().HasForeignKey(a => a.ElementoTrazadoId).OnDelete(DeleteBehavior.Cascade);
        modelBuilder.Entity<AlcanceRiesgo>()
            .HasOne<Componente>().WithMany().HasForeignKey(a => a.ComponenteId).OnDelete(DeleteBehavior.Cascade);
        modelBuilder.Entity<AlcanceRiesgo>()
            .HasOne<ItemCatalogo>().WithMany().HasForeignKey(a => a.ItemCatalogoId).OnDelete(DeleteBehavior.Cascade);
        modelBuilder.Entity<AlcanceRiesgo>()
            .HasOne<Tiro>().WithMany().HasForeignKey(a => a.TiroId).OnDelete(DeleteBehavior.Cascade);
        modelBuilder.Entity<AlcanceRiesgo>().HasIndex(a => a.RiesgoId);

        modelBuilder.Entity<SeguimientoRiesgo>()
            .HasOne<Riesgo>().WithMany().HasForeignKey(s => s.RiesgoId).OnDelete(DeleteBehavior.Cascade);
        modelBuilder.Entity<SeguimientoRiesgo>().HasIndex(s => new { s.RiesgoId, s.Fecha });

        // Un hallazgo se borra con su seguimiento. Si la torre, la pata, la
        // localización o el tiro al que apunta se borran definitivamente, el
        // hallazgo NO se pierde: queda sin referencia (SetNull), porque es
        // evidencia de algo que sí ocurrió.
        modelBuilder.Entity<HallazgoRiesgo>()
            .HasOne<SeguimientoRiesgo>().WithMany().HasForeignKey(h => h.SeguimientoRiesgoId).OnDelete(DeleteBehavior.Cascade);
        modelBuilder.Entity<HallazgoRiesgo>()
            .HasOne<ElementoTrazado>().WithMany().HasForeignKey(h => h.ElementoTrazadoId).OnDelete(DeleteBehavior.SetNull);
        modelBuilder.Entity<HallazgoRiesgo>()
            .HasOne<Componente>().WithMany().HasForeignKey(h => h.ComponenteId).OnDelete(DeleteBehavior.SetNull);
        modelBuilder.Entity<HallazgoRiesgo>()
            .HasOne<ItemCatalogo>().WithMany().HasForeignKey(h => h.ItemCatalogoId).OnDelete(DeleteBehavior.SetNull);
        modelBuilder.Entity<HallazgoRiesgo>()
            .HasOne<Tiro>().WithMany().HasForeignKey(h => h.TiroId).OnDelete(DeleteBehavior.SetNull);
        modelBuilder.Entity<HallazgoRiesgo>().HasIndex(h => h.ElementoTrazadoId);

        modelBuilder.Entity<ArchivoRiesgo>()
            .HasOne<SeguimientoRiesgo>().WithMany().HasForeignKey(a => a.SeguimientoRiesgoId).OnDelete(DeleteBehavior.Cascade);
        modelBuilder.Entity<ArchivoRiesgo>()
            .HasOne<HallazgoRiesgo>().WithMany().HasForeignKey(a => a.HallazgoRiesgoId).OnDelete(DeleteBehavior.Cascade);

        // Causas, consecuencias, disparadores, plan de respuesta y forma de
        // seguimiento: listas propias del riesgo, se van con él.
        modelBuilder.Entity<ElementoRiesgo>()
            .HasOne<Riesgo>().WithMany().HasForeignKey(e => e.RiesgoId).OnDelete(DeleteBehavior.Cascade);
        modelBuilder.Entity<ElementoRiesgo>().HasIndex(e => new { e.RiesgoId, e.Tipo, e.Orden });

        // ---------- Auditoría interna ----------
        // Programa → auditorías → hallazgos en cascada (la Papelera los marca juntos con la misma
        // marca de tiempo). Los lugares van con su dueño y con el elemento al que apuntan. El vínculo
        // hallazgo ↔ plan se borra con cualquiera de los dos: el plan queda (evidencia de gestión).
        modelBuilder.Entity<ProgramaAuditoria>().HasQueryFilter(e => !e.Eliminado);
        modelBuilder.Entity<Auditoria>().HasQueryFilter(e => !e.Eliminado);
        modelBuilder.Entity<Auditoria>().HasOne<ProgramaAuditoria>().WithMany().HasForeignKey(e => e.ProgramaAuditoriaId).OnDelete(DeleteBehavior.Cascade);
        modelBuilder.Entity<Auditoria>().HasIndex(e => e.ProgramaAuditoriaId);
        modelBuilder.Entity<Auditoria>().HasIndex(e => e.Codigo);
        modelBuilder.Entity<CambioAuditoria>().HasOne<Auditoria>().WithMany().HasForeignKey(e => e.AuditoriaId).OnDelete(DeleteBehavior.Cascade);
        modelBuilder.Entity<CambioAuditoria>().HasIndex(e => e.AuditoriaId);
        modelBuilder.Entity<LugarAuditoria>().HasOne<Auditoria>().WithMany().HasForeignKey(e => e.AuditoriaId).OnDelete(DeleteBehavior.Cascade);
        modelBuilder.Entity<LugarAuditoria>().HasOne<ElementoTrazado>().WithMany().HasForeignKey(e => e.ElementoTrazadoId).OnDelete(DeleteBehavior.Cascade);
        modelBuilder.Entity<LugarAuditoria>().HasOne<Componente>().WithMany().HasForeignKey(e => e.ComponenteId).OnDelete(DeleteBehavior.Cascade);
        modelBuilder.Entity<LugarAuditoria>().HasOne<ItemCatalogo>().WithMany().HasForeignKey(e => e.ItemCatalogoId).OnDelete(DeleteBehavior.Cascade);
        modelBuilder.Entity<LugarAuditoria>().HasOne<Tiro>().WithMany().HasForeignKey(e => e.TiroId).OnDelete(DeleteBehavior.Cascade);
        modelBuilder.Entity<LugarAuditoria>().HasIndex(e => e.AuditoriaId);
        modelBuilder.Entity<HallazgoAuditoria>().HasQueryFilter(e => !e.Eliminado);
        modelBuilder.Entity<HallazgoAuditoria>().HasOne<Auditoria>().WithMany().HasForeignKey(e => e.AuditoriaId).OnDelete(DeleteBehavior.Cascade);
        modelBuilder.Entity<HallazgoAuditoria>().HasIndex(e => e.AuditoriaId);
        modelBuilder.Entity<LugarHallazgoAuditoria>().HasOne<HallazgoAuditoria>().WithMany().HasForeignKey(e => e.HallazgoAuditoriaId).OnDelete(DeleteBehavior.Cascade);
        modelBuilder.Entity<LugarHallazgoAuditoria>().HasOne<ElementoTrazado>().WithMany().HasForeignKey(e => e.ElementoTrazadoId).OnDelete(DeleteBehavior.Cascade);
        modelBuilder.Entity<LugarHallazgoAuditoria>().HasOne<Componente>().WithMany().HasForeignKey(e => e.ComponenteId).OnDelete(DeleteBehavior.Cascade);
        modelBuilder.Entity<LugarHallazgoAuditoria>().HasOne<ItemCatalogo>().WithMany().HasForeignKey(e => e.ItemCatalogoId).OnDelete(DeleteBehavior.Cascade);
        modelBuilder.Entity<LugarHallazgoAuditoria>().HasOne<Tiro>().WithMany().HasForeignKey(e => e.TiroId).OnDelete(DeleteBehavior.Cascade);
        modelBuilder.Entity<LugarHallazgoAuditoria>().HasIndex(e => e.HallazgoAuditoriaId);
        modelBuilder.Entity<ArchivoHallazgoAuditoria>().HasQueryFilter(e => !e.Eliminado);
        modelBuilder.Entity<ArchivoHallazgoAuditoria>().HasOne<HallazgoAuditoria>().WithMany().HasForeignKey(e => e.HallazgoAuditoriaId).OnDelete(DeleteBehavior.Cascade);
        modelBuilder.Entity<ArchivoHallazgoAuditoria>().HasIndex(e => e.HallazgoAuditoriaId);
        modelBuilder.Entity<ArchivoEficaciaPlan>().HasQueryFilter(e => !e.Eliminado);
        modelBuilder.Entity<ArchivoEficaciaPlan>().HasOne<PlanAccion>().WithMany().HasForeignKey(e => e.PlanAccionId).OnDelete(DeleteBehavior.Cascade);
        modelBuilder.Entity<ArchivoEficaciaPlan>().HasIndex(e => e.PlanAccionId);
        modelBuilder.Entity<HallazgoPlan>().HasOne<HallazgoAuditoria>().WithMany().HasForeignKey(e => e.HallazgoAuditoriaId).OnDelete(DeleteBehavior.Cascade);
        modelBuilder.Entity<HallazgoPlan>().HasOne<PlanAccion>().WithMany().HasForeignKey(e => e.PlanAccionId).OnDelete(DeleteBehavior.Cascade);
        modelBuilder.Entity<HallazgoPlan>().HasIndex(e => new { e.HallazgoAuditoriaId, e.PlanAccionId }).IsUnique();
        modelBuilder.Entity<HallazgoPlan>().HasIndex(e => e.PlanAccionId);
        modelBuilder.Entity<PlanAccion>().HasOne<Auditoria>().WithMany().HasForeignKey(e => e.AuditoriaId).OnDelete(DeleteBehavior.SetNull);

        // ---------- Planes de acción ----------
        // El plan es evidencia de gestión: si el riesgo, el seguimiento o la
        // localización de origen se borran definitivamente, queda sin
        // referencia (SetNull) en vez de perderse. Las acciones van con su plan.
        modelBuilder.Entity<PlanAccion>().HasIndex(p => p.Codigo).IsUnique();
        modelBuilder.Entity<PlanAccion>()
            .HasOne<Riesgo>().WithMany().HasForeignKey(p => p.RiesgoId).OnDelete(DeleteBehavior.SetNull);
        modelBuilder.Entity<PlanAccion>()
            .HasOne<SeguimientoRiesgo>().WithMany().HasForeignKey(p => p.SeguimientoRiesgoId).OnDelete(DeleteBehavior.SetNull);
        modelBuilder.Entity<PlanAccion>()
            .HasOne<SeguimientoParteInteresada>().WithMany().HasForeignKey(p => p.SeguimientoParteInteresadaId).OnDelete(DeleteBehavior.SetNull);
        modelBuilder.Entity<PlanAccion>()
            .HasOne<ItemCatalogo>().WithMany().HasForeignKey(p => p.ItemCatalogoId).OnDelete(DeleteBehavior.SetNull);
        modelBuilder.Entity<AccionPlan>()
            .HasOne<PlanAccion>().WithMany().HasForeignKey(a => a.PlanAccionId).OnDelete(DeleteBehavior.Cascade);
        modelBuilder.Entity<AccionPlan>().HasIndex(a => a.PlanAccionId);
        // Lugares del plan: igual que el alcance de un riesgo, se van con el
        // plan y con el elemento al que apuntan.
        modelBuilder.Entity<LugarPlan>()
            .HasOne<PlanAccion>().WithMany().HasForeignKey(l => l.PlanAccionId).OnDelete(DeleteBehavior.Cascade);
        modelBuilder.Entity<LugarPlan>()
            .HasOne<ElementoTrazado>().WithMany().HasForeignKey(l => l.ElementoTrazadoId).OnDelete(DeleteBehavior.Cascade);
        modelBuilder.Entity<LugarPlan>()
            .HasOne<Componente>().WithMany().HasForeignKey(l => l.ComponenteId).OnDelete(DeleteBehavior.Cascade);
        modelBuilder.Entity<LugarPlan>()
            .HasOne<ItemCatalogo>().WithMany().HasForeignKey(l => l.ItemCatalogoId).OnDelete(DeleteBehavior.Cascade);
        modelBuilder.Entity<LugarPlan>()
            .HasOne<Tiro>().WithMany().HasForeignKey(l => l.TiroId).OnDelete(DeleteBehavior.Cascade);
        modelBuilder.Entity<LugarPlan>().HasIndex(l => l.PlanAccionId);
        modelBuilder.Entity<LugarPlan>().HasIndex(l => l.ElementoTrazadoId);
        // Lugares de un hallazgo: van con el hallazgo. Si la torre, el elemento,
        // la localización o el tiro se borran definitivamente, el lugar se va
        // con ellos, pero el hallazgo (la evidencia) queda.
        modelBuilder.Entity<LugarHallazgo>()
            .HasOne<HallazgoRiesgo>().WithMany().HasForeignKey(l => l.HallazgoRiesgoId).OnDelete(DeleteBehavior.Cascade);
        modelBuilder.Entity<LugarHallazgo>()
            .HasOne<ElementoTrazado>().WithMany().HasForeignKey(l => l.ElementoTrazadoId).OnDelete(DeleteBehavior.Cascade);
        modelBuilder.Entity<LugarHallazgo>()
            .HasOne<Componente>().WithMany().HasForeignKey(l => l.ComponenteId).OnDelete(DeleteBehavior.Cascade);
        modelBuilder.Entity<LugarHallazgo>()
            .HasOne<ItemCatalogo>().WithMany().HasForeignKey(l => l.ItemCatalogoId).OnDelete(DeleteBehavior.Cascade);
        modelBuilder.Entity<LugarHallazgo>()
            .HasOne<Tiro>().WithMany().HasForeignKey(l => l.TiroId).OnDelete(DeleteBehavior.Cascade);
        modelBuilder.Entity<LugarHallazgo>().HasIndex(l => l.HallazgoRiesgoId);
        modelBuilder.Entity<LugarHallazgo>().HasIndex(l => l.ElementoTrazadoId);
        modelBuilder.Entity<ArchivoAccionPlan>()
            .HasOne<AccionPlan>().WithMany().HasForeignKey(a => a.AccionPlanId).OnDelete(DeleteBehavior.Cascade);
        modelBuilder.Entity<ProductoAccion>().HasOne<AccionPlan>().WithMany().HasForeignKey(p => p.AccionPlanId).OnDelete(DeleteBehavior.Cascade);
        modelBuilder.Entity<ProductoAccion>().HasIndex(p => p.AccionPlanId);
        modelBuilder.Entity<SeguimientoAccion>().HasQueryFilter(e => !e.Eliminado);
        modelBuilder.Entity<SeguimientoAccion>().HasOne<AccionPlan>().WithMany().HasForeignKey(e => e.AccionPlanId).OnDelete(DeleteBehavior.Cascade);
        modelBuilder.Entity<SeguimientoAccion>().HasIndex(e => e.AccionPlanId);
        modelBuilder.Entity<ArchivoAccionPlan>().HasOne<SeguimientoAccion>().WithMany().HasForeignKey(a => a.SeguimientoAccionId).OnDelete(DeleteBehavior.Cascade);
        modelBuilder.Entity<ArchivoAccionPlan>().HasOne<ProductoAccion>().WithMany().HasForeignKey(a => a.ProductoAccionId).OnDelete(DeleteBehavior.SetNull);

        modelBuilder.Entity<GrupoEnsayo>()
            .HasIndex(g => g.Nombre)
            .IsUnique();

        modelBuilder.Entity<GrupoEnsayo>()
            .HasOne<IconoCatalogo>()
            .WithMany()
            .HasForeignKey(g => g.IconoCatalogoId)
            .OnDelete(DeleteBehavior.SetNull);

        modelBuilder.Entity<TipoEnsayo>()
            .HasOne<GrupoEnsayo>()
            .WithMany()
            .HasForeignKey(t => t.GrupoEnsayoId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<TipoEnsayo>()
            .HasOne<IconoCatalogo>()
            .WithMany()
            .HasForeignKey(t => t.IconoCatalogoId)
            .OnDelete(DeleteBehavior.SetNull);

        modelBuilder.Entity<TipoEnsayo>()
            .HasIndex(t => new { t.GrupoEnsayoId, t.Nombre })
            .IsUnique();

        // Enlace real Actividad <-> Tipo de Ensayo (ver SincronizadorActividadesEnsayo):
        // si se borra el tipo de ensayo, la actividad NO se borra (quedaría
        // huérfano todo lo ya marcado/configurado en Trazado para ella) sino
        // que simplemente queda "libre" (SetNull), igual que un ícono borrado.
        modelBuilder.Entity<Actividad>()
            .HasOne(a => a.TipoEnsayo)
            .WithMany()
            .HasForeignKey(a => a.TipoEnsayoId)
            .OnDelete(DeleteBehavior.SetNull);

        modelBuilder.Entity<Actividad>()
            .HasIndex(a => a.TipoEnsayoId)
            .IsUnique();

        // ---------- Resultados de ensayos ----------
        // Plantilla de campos: pertenece a su Tipo de Ensayo y se borra
        // con él. Un campo no se puede repetir de clave dentro del
        // mismo tipo (es lo que referencian las fórmulas de otros campos).
        modelBuilder.Entity<CampoEnsayo>()
            .HasOne<TipoEnsayo>()
            .WithMany()
            .HasForeignKey(c => c.TipoEnsayoId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<CampoEnsayo>()
            .HasIndex(c => new { c.TipoEnsayoId, c.Clave })
            .IsUnique();

        // Un informe pertenece a un Tipo de Ensayo (numeración propia
        // por tipo, sin cruce entre tipos) y se borra con él.
        modelBuilder.Entity<Informe>()
            .HasOne<TipoEnsayo>()
            .WithMany()
            .HasForeignKey(i => i.TipoEnsayoId)
            .OnDelete(DeleteBehavior.Cascade);

        // Único por Tipo de Ensayo, pero solo cuando el Número no está en
        // blanco: puede haber varios informes "sin número" del mismo tipo
        // (ensayos que solo adjuntan un PDF sin diligenciar un No. de
        // Informe), uno por cada vez que se guarda sin indicarlo.
        modelBuilder.Entity<Informe>()
            .HasIndex(i => new { i.TipoEnsayoId, i.Numero })
            .IsUnique()
            .HasFilter("\"Numero\" <> ''");

        modelBuilder.Entity<ArchivoInforme>()
            .HasOne<Informe>()
            .WithMany()
            .HasForeignKey(a => a.InformeId)
            .OnDelete(DeleteBehavior.Cascade);

        // Un resultado pertenece a su Tipo de Ensayo (se borra con él).
        // La torre es opcional (SetNull): si la torre se borra
        // físicamente desde la Papelera, el resultado no desaparece,
        // solo queda sin torre asociada. Igual con el informe.
        modelBuilder.Entity<ResultadoEnsayo>()
            .HasOne<TipoEnsayo>()
            .WithMany()
            .HasForeignKey(r => r.TipoEnsayoId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<ResultadoEnsayo>()
            .HasOne<ElementoTrazado>()
            .WithMany()
            .HasForeignKey(r => r.ElementoTrazadoId)
            .OnDelete(DeleteBehavior.SetNull);

        modelBuilder.Entity<ResultadoEnsayo>()
            .HasOne<Informe>()
            .WithMany()
            .HasForeignKey(r => r.InformeId)
            .OnDelete(DeleteBehavior.SetNull);

        // Qué patas (Componente) cubre cada resultado. Se borra junto
        // con el resultado o junto con la pata, lo que ocurra primero.
        modelBuilder.Entity<ResultadoEnsayoComponente>()
            .HasOne<ResultadoEnsayo>()
            .WithMany()
            .HasForeignKey(rc => rc.ResultadoEnsayoId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<ResultadoEnsayoComponente>()
            .HasOne<Componente>()
            .WithMany()
            .HasForeignKey(rc => rc.ComponenteId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<ResultadoEnsayoComponente>()
            .HasIndex(rc => new { rc.ResultadoEnsayoId, rc.ComponenteId })
            .IsUnique();

        // Valores de la plantilla flexible para cada resultado.
        modelBuilder.Entity<ValorCampoEnsayo>()
            .HasOne<ResultadoEnsayo>()
            .WithMany()
            .HasForeignKey(v => v.ResultadoEnsayoId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<ValorCampoEnsayo>()
            .HasOne<CampoEnsayo>()
            .WithMany()
            .HasForeignKey(v => v.CampoEnsayoId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<ValorCampoEnsayo>()
            .HasIndex(v => new { v.ResultadoEnsayoId, v.CampoEnsayoId })
            .IsUnique();

        // Mapeo de columnas recordado por tipo de ensayo.
        modelBuilder.Entity<MapeoColumnaEnsayo>()
            .HasOne<TipoEnsayo>()
            .WithMany()
            .HasForeignKey(m => m.TipoEnsayoId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<MapeoColumnaEnsayo>()
            .HasIndex(m => new { m.TipoEnsayoId, m.EncabezadoTexto })
            .IsUnique();

        // Alias de texto de torre (ver AliasTorre): un mismo texto solo
        // puede apuntar a una torre, y se borra si esa torre se borra
        // físicamente desde la Papelera.
        modelBuilder.Entity<AliasTorre>()
            .HasOne<ElementoTrazado>()
            .WithMany()
            .HasForeignKey(a => a.ElementoTrazadoId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<AliasTorre>()
            .HasIndex(a => a.Texto)
            .IsUnique();

        // ---------- Catálogos dinámicos ----------
        // El nombre del catálogo es lo que ItemCatalogo.Tipo guarda como
        // texto plano (igual que antes, cuando la lista era fija en el
        // código); no hay FK entre ambos a propósito, para no romper
        // nada si algún día cambia cómo se identifican.
        modelBuilder.Entity<DefinicionCatalogo>()
            .HasIndex(d => d.Nombre)
            .IsUnique();

        // Los campos extra de un catálogo se borran junto con él; no se
        // puede repetir el nombre de un campo dentro del mismo catálogo.
        modelBuilder.Entity<CampoCatalogoDefinicion>()
            .HasOne<DefinicionCatalogo>()
            .WithMany()
            .HasForeignKey(c => c.DefinicionCatalogoId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<CampoCatalogoDefinicion>()
            .HasIndex(c => new { c.DefinicionCatalogoId, c.Nombre })
            .IsUnique();

        // El valor de un campo extra se borra junto con el ítem de
        // catálogo al que pertenece; un mismo ítem no puede tener dos
        // valores para la misma clave.
        modelBuilder.Entity<ValorCampoCatalogoItem>()
            .HasOne<ItemCatalogo>()
            .WithMany()
            .HasForeignKey(v => v.ItemCatalogoId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<ValorCampoCatalogoItem>()
            .HasIndex(v => new { v.ItemCatalogoId, v.Clave })
            .IsUnique();
    }
}