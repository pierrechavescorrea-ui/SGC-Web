using System.Globalization;
using System.Linq.Expressions;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using SigWeb.Models;

namespace SigWeb.Data.Busqueda;

// El modelo EF es el inventario: también se buscan campos importados, anexos y registros hijos.
// Las consultas filtran en PostgreSQL, sin cargar tablas completas ni necesitar unaccent instalado.
public static class BuscadorGlobal
{
    public sealed record Resultado(string Tipo, string Titulo, string? Detalle, string Url, string Icono = "Documento");
    private const int PorTipo = 6;
    private static readonly MethodInfo ConsultarMetodo = typeof(BuscadorGlobal).GetMethod(nameof(Consultar), BindingFlags.NonPublic | BindingFlags.Static)!;
    private static readonly MethodInfo ObtenerMetodo = typeof(BuscadorGlobal).GetMethod(nameof(Obtener), BindingFlags.NonPublic | BindingFlags.Static)!;
    private static readonly string[] Preferidos = ["Codigo", "Consecutivo", "Nombre", "Titulo", "NombreMaterial", "NumeroOrden", "Numero", "Norma", "Descripcion", "DescripcionHallazgo", "Declaracion", "NombreOriginal", "Concepto", "Localizacion", "ElementoTexto", "Valor", "Observaciones", "Observacion", "Nota", "Motivo"];
    // Apariencia, cachés y relaciones puras no representan contenido consultable del usuario.
    private static readonly HashSet<string> Tecnicas = ["AsignacionIcono", "IconoCatalogo", "ConfiguracionCampoExtraTorre", "MapeoColumnaEnsayo", "FormatoCondicional"];
    private static readonly HashSet<string> CamposTecnicos = ["Color", "Fondo", "Forma", "Icono", "IconoMapa", "ColorMapa", "GeometriaJson", "ConfiguracionJson", "DiasLaborables", "Festivos", "OrdenEstadosTorre", "LongitudTiros"];
    private static readonly Dictionary<string, string> Propietarios = new()
    {
        ["Componente"] = "ElementoTrazadoId", ["CampoExtraComponente"] = "ComponenteId",
        ["ArchivoComponente"] = "ComponenteId", ["RegistroParametro"] = "ComponenteId",
        ["ArchivoRegistroParametro"] = "RegistroParametroId", ["RegistroParametroTiro"] = "TiroId",
        ["ParametroActividad"] = "ActividadId", ["EstadoParametro"] = "ParametroActividadId",
        ["TipoEnsayo"] = "GrupoEnsayoId", ["Auditoria"] = "ProgramaAuditoriaId",
        ["ResultadoEnsayo"] = "TipoEnsayoId", ["ValorCampoEnsayo"] = "ResultadoEnsayoId",
        ["CampoEnsayo"] = "TipoEnsayoId", ["Informe"] = "TipoEnsayoId", ["ArchivoInforme"] = "InformeId",
        ["ValorCampoEquipo"] = "EquipoId", ["FotoEquipo"] = "EquipoId", ["IntervencionEquipo"] = "EquipoId", ["ArchivoEquipo"] = "EquipoId",
        ["AccionPlan"] = "PlanAccionId", ["ProductoAccion"] = "AccionPlanId", ["SeguimientoAccion"] = "AccionPlanId",
        ["ArchivoAccionPlan"] = "AccionPlanId", ["CarpetaPlan"] = "PlanAccionId", ["ArchivoCarpetaPlan"] = "CarpetaPlanId",
        ["ArchivoEficaciaPlan"] = "PlanAccionId", ["ActividadPrograma"] = "ProgramaTrabajoId", ["CambioPrograma"] = "ProgramaTrabajoId", ["MetaProduccionDia"] = "MetaPeriodoId",
        ["Certificacion"] = "DocumentoMaestroId", ["DocumentoDireccionamiento"] = "DocumentoMaestroId", ["HallazgoRiesgo"] = "SeguimientoRiesgoId",
        ["SeguimientoRiesgo"] = "RiesgoId", ["ElementoRiesgo"] = "RiesgoId", ["ArchivoRiesgo"] = "SeguimientoRiesgoId",
        ["HallazgoAuditoria"] = "AuditoriaId", ["ArchivoHallazgoAuditoria"] = "HallazgoAuditoriaId", ["CambioAuditoria"] = "AuditoriaId",
        ["MedicionIndicador"] = "IndicadorId", ["ArchivoMedicionIndicador"] = "MedicionIndicadorId",
        ["VersionDocumento"] = "DocumentoMaestroId", ["RevisionDocumento"] = "DocumentoMaestroId", ["ControlCambioVersion"] = "VersionDocumentoId",
        ["ArchivoPlanoMaestro"] = "PlanoMaestroId", ["AdjuntoMaterial"] = "MaterialId", ["FotoMaterial"] = "MaterialId",
        ["AdjuntoComunicado"] = "ComunicadoId", ["ValorCampoCatalogoItem"] = "ItemCatalogoId", ["CampoCatalogoDefinicion"] = "DefinicionCatalogoId",
        ["CampoExtraTorre"] = "ElementoTrazadoId", ["AliasTorre"] = "ElementoTrazadoId",
        ["NotaInformeArea"] = "InformeDiarioId", ["RegistroAvance"] = "ElementoTrazadoId", ["RegistroAvanceTiro"] = "TiroId", ["FotoAvance"] = "InformeDiarioId"
    };
    private static readonly Dictionary<string, (string Ruta, string Icono)> Listas = new()
    {
        ["EstadoParametro"] = ("actividades", "Actividad"), ["AsignacionPiezaModelo"] = ("ifc", "Torre"),
        ["CapaTrazado"] = ("trazado", "Torre"), ["GrupoEnsayo"] = ("ensayos", "Ensayo"), ["AdjuntoEnsayo"] = ("ensayos", "Ensayo"),
        ["GrupoPlano"] = ("planos", "Plano"), ["CampoEquipo"] = ("equipos", "Equipo"),
        ["CargaEquipos"] = ("equipos", "Equipo"), ["CargaMateriales"] = ("materiales", "Material"),
        ["CargaPlanos"] = ("planos", "Plano"), ["CargaArchivosPlanos"] = ("planos", "Plano"),
        ["CargaDocumentos"] = ("documentos", "Documento"), ["CargaArchivosDocumentos"] = ("documentos", "Documento"),
        ["CargaComunicados"] = ("comunicados", "Comunicado"), ["CargaPlanesAccion"] = ("planes-accion", "Plan de acción"),
        ["ActividadObra"] = ("avance-obra?vista=ajustes", "Actividad"), ["InformeDiario"] = ("avance-obra?vista=informe", "Actividad"),
        ["ConfiguracionEmpresa"] = ("/", "modulo"),
        ["ItemPago"] = ("costos?vista=precios", "Costo"), ["AjustePrecio"] = ("costos?vista=precios", "Costo"),
        ["ComponenteAjustePrecio"] = ("costos?vista=precios", "Costo"), ["PrecioManual"] = ("costos?vista=precios", "Costo"),
        ["ModificacionContrato"] = ("costos?vista=precios", "Costo"), ["CantidadTorre"] = ("costos?vista=torres", "Costo"),
        ["RegistroCantidad"] = ("costos?vista=avance", "Costo"), ["ActaFacturacion"] = ("costos?vista=actas", "Costo"),
        ["CodigoPresupuesto"] = ("costos?vista=presupuesto", "Costo"), ["ValorPresupuesto"] = ("costos?vista=presupuesto", "Costo"),
        ["PorcentajeIndirecto"] = ("costos?vista=ajustes", "Costo"), ["CostoOrden"] = ("costos?vista=costos", "Costo"),
        ["Persona"] = ("costos?vista=personal", "Cargo"), ["MetaPeriodo"] = ("costos?vista=metas", "Costo"),
        ["MetaProduccionDia"] = ("costos?vista=metas", "Costo"), ["AsignacionCostoActividad"] = ("costos?vista=costo-actividad", "Costo"),
        ["ProgramaTrabajo"] = ("costos?vista=programa", "Actividad"), ["ActividadPrograma"] = ("costos?vista=programa", "Actividad"),
        ["CambioPrograma"] = ("costos?vista=programa", "Actividad"), ["EstadoTorreManual"] = ("avance-obra?vista=estado", "Torre"),
        ["ConfiguracionCostos"] = ("costos?vista=ajustes", "Costo")
    };

    public static async Task<List<Resultado>> BuscarAsync(AppDbContext db, string? texto, CancellationToken cancellationToken = default)
    {
        var q = Normalizar((texto ?? "").Trim());
        if (q.Length < 2) return [];
        q = q[..Math.Min(q.Length, 120)];
        var patron = $"%{q.Replace("\\", "\\\\").Replace("%", "\\%").Replace("_", "\\_")}%";
        var vigentes = new Dictionary<string, int>
        {
            [nameof(VersionMatrizRiesgos)] = await db.VersionesMatrizRiesgos.AsNoTracking().Where(x => !x.Eliminado)
                .OrderByDescending(x => x.Estado == EstadoVersionMatriz.Vigente).ThenByDescending(x => x.Id).Select(x => x.Id).FirstOrDefaultAsync(cancellationToken),
            [nameof(VersionOrganigrama)] = await db.VersionesOrganigrama.AsNoTracking().Where(x => !x.Eliminado)
                .OrderByDescending(x => x.Estado == EstadoOrganigrama.Vigente).ThenByDescending(x => x.Numero).Select(x => x.Id).FirstOrDefaultAsync(cancellationToken),
            [nameof(VersionPartesInteresadas)] = await db.VersionesPartesInteresadas.AsNoTracking().Where(x => !x.Eliminado)
                .OrderByDescending(x => x.Estado == EstadoVersionPartes.Vigente).ThenByDescending(x => x.Numero).Select(x => x.Id).FirstOrDefaultAsync(cancellationToken)
        };
        var resultados = new List<Resultado>();
        var cache = new Dictionary<(Type, int), object?>();
        foreach (var tipo in db.Model.GetEntityTypes().OrderBy(x => x.ClrType.Name))
        {
            cancellationToken.ThrowIfCancellationRequested();
            var campos = Campos(tipo);
            if (Tecnicas.Contains(tipo.ClrType.Name) || campos.Length == 0 || tipo.FindProperty("Id")?.ClrType != typeof(int)) continue;
            var filas = await (Task<List<object>>)ConsultarMetodo.MakeGenericMethod(tipo.ClrType).Invoke(null, [db, tipo, campos, patron, vigentes, cancellationToken])!;
            foreach (var fila in filas)
            {
                var destino = await Destino(db, fila, cache, cancellationToken);
                if (destino is null) continue;
                var textos = campos.Select(c => (Nombre: c.Name, Valor: Texto(fila, c.Name))).Where(c => !string.IsNullOrWhiteSpace(c.Valor)).ToList();
                var titulo = Preferidos.Select(p => Texto(fila, p)).FirstOrDefault(s => !string.IsNullOrWhiteSpace(s)) ?? $"{Etiqueta(tipo.ClrType.Name)} #{Id(fila)}";
                var coincidencia = textos.FirstOrDefault(c => Normalizar(c.Valor).Contains(q));
                var detalle = coincidencia.Valor != titulo && !string.IsNullOrEmpty(coincidencia.Valor)
                    ? $"{Etiqueta(coincidencia.Nombre)}: {coincidencia.Valor}"
                    : string.Join(" · ", textos.Where(c => c.Valor != titulo).Take(2).Select(c => c.Valor));
                if (fila is ValorCampoTabla valor && cache.TryGetValue((typeof(CampoTabla), valor.CampoTablaId), out var definicion) && definicion is CampoTabla columna)
                    detalle = string.IsNullOrWhiteSpace(detalle) ? columna.Nombre : $"{columna.Nombre} · {detalle}";
                if (string.IsNullOrWhiteSpace(detalle))
                {
                    detalle = Texto(fila, "Estado");
                    var fecha = new[] { "Fecha", "FechaEjecucion", "FechaCarga", "FechaCreacion" }
                        .Select(n => fila.GetType().GetProperty(n)?.GetValue(fila)).OfType<DateTime>().FirstOrDefault();
                    if (fecha != default) detalle = string.IsNullOrWhiteSpace(detalle) ? $"{fecha:dd/MM/yyyy}" : $"{detalle} · {fecha:dd/MM/yyyy}";
                }
                var secundario = string.IsNullOrWhiteSpace(detalle) ? $"#{Id(fila)}" : $"#{Id(fila)} · {detalle}";
                resultados.Add(new(Etiqueta(tipo.ClrType.Name), Corto(titulo, 120), Corto(secundario, 180), destino.Value.Url, destino.Value.Icono));
            }
        }
        return resultados;
    }

    private static IProperty[] Campos(IEntityType tipo) => tipo.GetProperties().Where(p => p.ClrType == typeof(string)
        && !CamposTecnicos.Contains(p.Name) && !p.Name.StartsWith("Ruta") && !p.Name.StartsWith("Archivo")
        && !p.Name.EndsWith("Json") && !p.Name.EndsWith("Color") && !p.Name.EndsWith("Fondo")
        && p.Name is not "Tabla" and not "MimeType" and not "ContentType" and not "Hash").ToArray();

    private static async Task<List<object>> Consultar<T>(AppDbContext db, IEntityType tipo, IProperty[] campos, string patron,
        Dictionary<string, int> vigentes, CancellationToken ct) where T : class
    {
        var x = Expression.Parameter(typeof(T), "x");
        Expression filtro = Expression.Constant(false);
        Expression<Func<string>> valorPatron = () => patron;
        foreach (var campo in campos)
        {
            Expression texto = Expression.Coalesce(Expression.Property(x, campo.Name), Expression.Constant(""));
            texto = Expression.Call(texto, nameof(string.ToLower), Type.EmptyTypes);
            // Equivalente a NFD en el cliente para español, incluida ñ y las vocales descompuestas.
            foreach (var (de, a) in Sustituciones)
                texto = Expression.Call(texto, nameof(string.Replace), Type.EmptyTypes, Expression.Constant(de), Expression.Constant(a));
            var coincide = Expression.Call(typeof(NpgsqlDbFunctionsExtensions), nameof(NpgsqlDbFunctionsExtensions.ILike), Type.EmptyTypes,
                Expression.Property(null, typeof(EF), nameof(EF.Functions)), texto, valorPatron.Body);
            filtro = Expression.OrElse(filtro, coincide);
        }
        filtro = Expression.AndAlso(filtro, Visible(db, tipo, x, vigentes, []));
        var consulta = db.Set<T>().AsNoTracking().Where(Expression.Lambda<Func<T, bool>>(filtro, x));
        return (await consulta.OrderBy(x => EF.Property<int>(x, "Id")).Take(PorTipo).ToListAsync(ct)).Cast<object>().ToList();
    }

    // Incluye al propietario en SQL: un anexo no reaparece si su registro padre está en Papelera.
    private static Expression Visible(AppDbContext db, IEntityType tipo, Expression x, Dictionary<string, int> vigentes, HashSet<Type> camino)
    {
        Expression filtro = Expression.Constant(true);
        if (tipo.FindProperty("Eliminado") is not null) filtro = Expression.Not(Expression.Property(x, "Eliminado"));
        if (tipo.FindProperty("Vigente")?.ClrType == typeof(bool)) filtro = Expression.AndAlso(filtro, Expression.Property(x, "Vigente"));
        filtro = Expression.AndAlso(filtro, Vigencia(db, tipo.ClrType, x, vigentes));
        if (tipo.ClrType == typeof(ValorCampoTabla)) filtro = Expression.AndAlso(filtro, FilaVisible(db, x, vigentes, camino));
        if (vigentes.TryGetValue(tipo.ClrType.Name, out var id))
            filtro = Expression.AndAlso(filtro, Expression.Equal(Expression.Property(x, "Id"), Expression.Constant(id)));
        foreach (var version in vigentes)
            if (tipo.FindProperty(version.Key + "Id") is not null)
                filtro = Expression.AndAlso(filtro, Expression.Equal(Expression.Property(x, version.Key + "Id"), Expression.Constant(version.Value)));
        if (!camino.Add(tipo.ClrType)) return filtro;
        var propietarios = new List<(IEntityType Tipo, string Campo)>();
        if (Propietario(db, tipo) is { } propietario) propietarios.Add(propietario);
        if (tipo.ClrType == typeof(ArchivoRiesgo)) propietarios.Add((db.Model.FindEntityType(typeof(HallazgoRiesgo))!, "HallazgoRiesgoId"));
        foreach (var p in propietarios.Where(p => !camino.Contains(p.Tipo.ClrType)))
        {
            var padre = Expression.Parameter(p.Tipo.ClrType, "padre");
            var fk = Expression.Property(x, p.Campo);
            Expression pk = Expression.Property(padre, "Id");
            if (fk.Type != pk.Type) pk = Expression.Convert(pk, fk.Type);
            var condicion = Expression.AndAlso(Expression.Equal(pk, fk), Visible(db, p.Tipo, padre, tipo.ClrType == typeof(SeguimientoRiesgo) ? vigentes.Where(v => v.Key != nameof(VersionMatrizRiesgos)).ToDictionary(v => v.Key, v => v.Value) : vigentes, new(camino)));
            var conjunto = Expression.Call(Expression.Constant(db), typeof(DbContext).GetMethod(nameof(DbContext.Set), Type.EmptyTypes)!.MakeGenericMethod(p.Tipo.ClrType));
            Expression existe = Expression.Call(typeof(Queryable), nameof(Queryable.Any), [p.Tipo.ClrType], conjunto, Expression.Lambda(condicion, padre));
            if (Nullable.GetUnderlyingType(fk.Type) is not null) existe = Expression.OrElse(Expression.Equal(fk, Expression.Constant(null, fk.Type)), existe);
            filtro = Expression.AndAlso(filtro, existe);
        }
        return filtro;
    }

    private static Expression FilaVisible(AppDbContext db, Expression valor, Dictionary<string, int> vigentes, HashSet<Type> camino)
    {
        var campo = Expression.Parameter(typeof(CampoTabla), "campo");
        Expression filas = Expression.Constant(false);
        foreach (var tabla in new[] { "torres", "trazado-torres", "componentes", "documentos", "planos", "materiales", "equipos", "comunicados", "planes-accion", "tiros", "actividades", "riesgos", "intervenciones-equipo", "ensayos:", "parametros:", "catalogo:" })
        {
            var tipo = tabla switch { "ensayos:" => typeof(ResultadoEnsayo), "parametros:" => typeof(ParametroActividad), "catalogo:" => typeof(ItemCatalogo), _ => TipoTabla(tabla)! };
            var fila = Expression.Parameter(tipo, "fila");
            Expression clave = Expression.Constant(tabla);
            if (tabla is "ensayos:" or "parametros:") clave = Expression.Call(typeof(string).GetMethod(nameof(string.Concat), [typeof(string), typeof(string)])!, clave,
                Expression.Call(Expression.Property(fila, tabla == "ensayos:" ? "TipoEnsayoId" : "ActividadId"), nameof(int.ToString), Type.EmptyTypes));
            if (tabla == "catalogo:") clave = Expression.Call(typeof(string).GetMethod(nameof(string.Concat), [typeof(string), typeof(string)])!, clave, Expression.Property(fila, "Tipo"));
            var coincide = Expression.AndAlso(Expression.Equal(Expression.Property(campo, "Tabla"), clave),
                Expression.Equal(Expression.Property(fila, "Id"), Expression.Property(valor, "FilaId")));
            var predicado = Expression.AndAlso(coincide, Visible(db, db.Model.FindEntityType(tipo)!, fila, vigentes, new(camino)));
            var conjunto = Expression.Call(Expression.Constant(db), typeof(DbContext).GetMethod(nameof(DbContext.Set), Type.EmptyTypes)!.MakeGenericMethod(tipo));
            filas = Expression.OrElse(filas, Expression.Call(typeof(Queryable), nameof(Queryable.Any), [tipo], conjunto, Expression.Lambda(predicado, fila)));
        }
        // BOM e IFC guardan filas de archivos externos, no entidades EF; se abre el visor existente.
        var tablaCampo = Expression.Property(campo, "Tabla");
        filas = Expression.OrElse(filas, Expression.OrElse(Expression.Call(tablaCampo, nameof(string.StartsWith), Type.EmptyTypes, Expression.Constant("bom:")),
            Expression.Equal(tablaCampo, Expression.Constant("ifc-modelos"))));
        var condicion = Expression.AndAlso(Expression.Equal(Expression.Property(campo, "Id"), Expression.Property(valor, "CampoTablaId")),
            Expression.AndAlso(Expression.Not(Expression.Property(campo, "Eliminado")), filas));
        return Expression.Call(typeof(Queryable), nameof(Queryable.Any), [typeof(CampoTabla)], ((IQueryable)db.CamposTabla).Expression, Expression.Lambda(condicion, campo));
    }

    private sealed class SustituirParametro(ParameterExpression origen, Expression destino) : ExpressionVisitor
    {
        protected override Expression VisitParameter(ParameterExpression node) => node == origen ? destino : base.VisitParameter(node);
    }
    private static Expression Aplicar<T>(Expression<Func<T, bool>> predicado, Expression x) => new SustituirParametro(predicado.Parameters[0], x).Visit(predicado.Body)!;

    private static Expression Vigencia(AppDbContext db, Type tipo, Expression x, Dictionary<string, int> vigentes)
    {
        var versionPartes = vigentes[nameof(VersionPartesInteresadas)];
        if (tipo == typeof(SeguimientoParteInteresada)) return Aplicar<SeguimientoParteInteresada>(s => db.PartesInteresadas.Any(p => !p.Eliminado && p.Clave == s.ParteClave && p.VersionPartesInteresadasId == versionPartes), x);
        if (tipo == typeof(ProgramacionSeguimientoParte)) return Aplicar<ProgramacionSeguimientoParte>(s => db.PartesInteresadas.Any(p => !p.Eliminado && p.Clave == s.ParteClave && p.VersionPartesInteresadasId == versionPartes), x);
        if (tipo == typeof(AdjuntoEnsayo)) return Aplicar<AdjuntoEnsayo>(a => db.ValoresCampoEnsayo.Any(v => v.AdjuntoEnsayoId == a.Id
            && db.ResultadosEnsayo.Any(r => r.Id == v.ResultadoEnsayoId && !r.Eliminado)), x);
        if (tipo == typeof(ModeloIfcCargado)) return Aplicar<ModeloIfcCargado>(m => !db.ModelosIfcCargados.Any(n => !n.Eliminado
            && n.Codigo.ToLower().Replace(" ", "").Replace("-", "") == m.Codigo.ToLower().Replace(" ", "").Replace("-", "") && n.Version > m.Version), x);
        if (tipo == typeof(ArchivoPlanoMaestro)) return Aplicar<ArchivoPlanoMaestro>(a => a.Clase != ClaseArchivoDocumento.Obsoleta
            && (a.Version ?? "").Trim().ToLower() == (db.ArchivosPlanoMaestro.Where(n => !n.Eliminado && n.PlanoMaestroId == a.PlanoMaestroId && n.Clase != ClaseArchivoDocumento.Obsoleta)
                .OrderByDescending(n => n.FechaCarga).ThenByDescending(n => n.Id).Select(n => n.Version).FirstOrDefault() ?? "").Trim().ToLower(), x);
        if (tipo == typeof(RevisionDocumento)) return Aplicar<RevisionDocumento>(a => a.Id == db.RevisionesDocumento.Where(n => n.DocumentoMaestroId == a.DocumentoMaestroId)
            .OrderByDescending(n => n.Fecha ?? DateTime.MinValue).ThenByDescending(n => n.Creada).ThenByDescending(n => n.Id).Select(n => n.Id).FirstOrDefault(), x);
        if (tipo == typeof(VersionDocumento)) return Aplicar<VersionDocumento>(a => a.Clase != ClaseArchivoDocumento.Obsoleta
            && (a.RevisionDocumentoId != null
                ? a.RevisionDocumentoId == db.VersionesDocumento.Where(n => !n.Eliminado && n.DocumentoMaestroId == a.DocumentoMaestroId && n.Clase == a.Clase && n.RevisionDocumentoId != null)
                    .OrderByDescending(n => db.RevisionesDocumento.Where(r => r.Id == n.RevisionDocumentoId).Select(r => r.Fecha ?? DateTime.MinValue).FirstOrDefault())
                    .ThenByDescending(n => db.RevisionesDocumento.Where(r => r.Id == n.RevisionDocumentoId).Select(r => r.Creada).FirstOrDefault())
                    .ThenByDescending(n => n.RevisionDocumentoId).Select(n => n.RevisionDocumentoId).FirstOrDefault()
                : !db.VersionesDocumento.Any(n => !n.Eliminado && n.DocumentoMaestroId == a.DocumentoMaestroId && n.Clase == a.Clase && n.RevisionDocumentoId != null)
                    && (a.Version ?? "").Trim().ToLower() == (db.VersionesDocumento.Where(n => !n.Eliminado && n.DocumentoMaestroId == a.DocumentoMaestroId && n.Clase == a.Clase)
                        .OrderByDescending(n => n.FechaCarga).ThenByDescending(n => n.Id).Select(n => n.Version).FirstOrDefault() ?? "").Trim().ToLower()), x);
        return Expression.Constant(true);
    }

    private static (IEntityType Tipo, string Campo)? Propietario(AppDbContext db, IEntityType tipo)
    {
        if (!Propietarios.TryGetValue(tipo.ClrType.Name, out var campo))
        {
            // Las entidades con una pantalla propia no se subordinan a enlaces opcionales (torre → tiro, plan → riesgo).
            if (Listas.ContainsKey(tipo.ClrType.Name) || Raices.Contains(tipo.ClrType.Name)) return null;
            campo = tipo.GetProperties().FirstOrDefault(p => p.Name.EndsWith("Id") && p.Name != "Id"
                && db.Model.GetEntityTypes().Any(t => t.ClrType.Name + "Id" == p.Name))?.Name;
        }
        if (campo is null || tipo.FindProperty(campo) is null) return null;
        var destino = tipo.GetForeignKeys().FirstOrDefault(f => f.Properties.Count == 1 && f.Properties[0].Name == campo)?.PrincipalEntityType
            ?? db.Model.GetEntityTypes().FirstOrDefault(t => t.ClrType.Name + "Id" == campo);
        return destino is null ? null : (destino, campo);
    }

    private static readonly HashSet<string> Raices = ["ElementoTrazado", "Tiro", "Actividad", "PlanAccion", "Riesgo", "PlanoMaestro", "DocumentoMaestro", "Material", "Equipo", "TipoEnsayo", "Comunicado", "Indicador", "Auditoria", "ProgramaAuditoria", "CargoOrganigrama", "ParteInteresada", "VersionOrganigrama", "VersionPartesInteresadas", "VersionMatrizRiesgos", "Certificacion", "DocumentoDireccionamiento", "DefinicionCatalogo", "ItemCatalogo", "ModeloIfcCargado", "CampoTabla", "ValorCampoTabla", "ProgramacionSeguimientoParte", "SeguimientoParteInteresada"];

    private static async Task<(string Url, string Icono)?> Destino(AppDbContext db, object fila, Dictionary<(Type, int), object?> cache, CancellationToken ct, int profundidad = 0)
    {
        if (profundidad > 8) return null;
        var id = Id(fila);
        string F(string campo) => Texto(fila, campo);
        string Q(string campo) => Uri.EscapeDataString(F(campo));
        switch (fila.GetType().Name)
        {
            case "RegistroModulo": return ($"mod/{Q("Modulo")}?registro={id}", "Documento");
            case "ElementoTrazado": return ($"trazado?torre={id}&hdv=true", "Torre");
            case "Componente": return ($"trazado?torre={F("ElementoTrazadoId")}&hdv=true&comp={id}", "Torre");
            case "Tiro": return ($"tiros?tiro={id}", "Tiro");
            case "Actividad": return ($"actividades?actividad={id}&ambito={Convert.ToInt32(fila.GetType().GetProperty("Ambito")!.GetValue(fila))}", "Actividad");
            case "PlanAccion": return ($"planes-accion/{id}", "Plan de acción");
            case "AccionPlan": return ($"planes-accion/{F("PlanAccionId")}?acciones={id}", "Plan de acción");
            case "SeguimientoRiesgo":
                var riesgoOrigenId = Convert.ToInt32(F("RiesgoId"));
                var codigoOrigen = await db.Riesgos.AsNoTracking().Where(r => r.Id == riesgoOrigenId).Select(r => r.Codigo).FirstOrDefaultAsync(ct);
                var riesgoActualId = await db.Riesgos.AsNoTracking().Where(r => r.Codigo == codigoOrigen && !r.Eliminado
                    && db.VersionesMatrizRiesgos.Any(v => v.Id == r.VersionMatrizRiesgosId && !v.Eliminado && v.Estado == EstadoVersionMatriz.Vigente))
                    .Select(r => r.Id).FirstOrDefaultAsync(ct);
                return ($"riesgos/{(riesgoActualId == 0 ? riesgoOrigenId : riesgoActualId)}", "Riesgo");
            case "Riesgo": return ($"riesgos/{id}", "Riesgo");
            case "PlanoMaestro": return ($"planos?buscar={Q("Codigo")}", "Plano");
            case "DocumentoMaestro": return ($"documentos?doc={id}&abrir=todos", "Documento");
            case "Material": return ($"materiales?material={id}", "Material");
            case "Equipo": return ($"equipos/{id}/hoja-de-vida", "Equipo");
            case "ResultadoEnsayo": return ($"ensayos/{F("TipoEnsayoId")}?resultado={id}", "Ensayo");
            case "TipoEnsayo": return ($"ensayos/{id}", "Ensayo");
            case "Comunicado": return ($"comunicados?buscar={Q("Consecutivo")}", "Comunicado");
            case "Indicador": return ($"indicadores?indicador={id}", "Indicador");
            case "ProgramaAuditoria": return ($"auditoria-interna?programa={id}", "Auditoría");
            case "Auditoria": return ($"auditoria-interna/{id}", "Auditoría");
            case "HallazgoAuditoria": return ($"auditoria-interna/{F("AuditoriaId")}?hallazgo={id}", "Auditoría");
            case "CargoOrganigrama": return ($"organigrama?v={F("VersionOrganigramaId")}&cargo={F("Clave")}", "Cargo");
            case "ParteInteresada": return ($"partes-interesadas?v={F("VersionPartesInteresadasId")}&parte={F("Clave")}", "Parte interesada");
            case "SeguimientoParteInteresada": case "ProgramacionSeguimientoParte": return ($"partes-interesadas?parte={F("ParteClave")}&vista=seguimiento", "Parte interesada");
            case "VersionOrganigrama": return ($"organigrama?v={id}", "Cargo");
            case "VersionPartesInteresadas": return ($"partes-interesadas?v={id}", "Parte interesada");
            case "VersionMatrizRiesgos": return ($"riesgos?rev={id}", "Riesgo");
            case "Certificacion": return F("DocumentoMaestroId") is { Length: > 0 } doc ? ($"documentos?doc={doc}&abrir=todos", "Certificación") : ("certificaciones?vista=lista", "Certificación");
            case "DocumentoDireccionamiento": return F("DocumentoMaestroId") is { Length: > 0 } de ? ($"documentos?doc={de}&abrir=todos", "Documento") : ("direccionamiento?vista=lista", "Documento");
            case "DefinicionCatalogo": return ($"catalogos?catalogo={id}", "Catálogo");
            case "ItemCatalogo":
                var tipoCatalogo = F("Tipo");
                var cat = await db.DefinicionesCatalogo.AsNoTracking().Where(c => c.Nombre == tipoCatalogo).Select(c => c.Id).FirstOrDefaultAsync(ct);
                return ($"catalogos?catalogo={cat}", "Catálogo");
            case "ModeloIfcCargado": return ($"ifc?modelo={Uri.EscapeDataString(Path.GetFileName(F("ArchivoIfc")))}", "Torre");
            case "ArchivoRiesgo":
                var soporteTipo = int.TryParse(F("SeguimientoRiesgoId"), out var seguimientoId) ? typeof(SeguimientoRiesgo) : typeof(HallazgoRiesgo);
                var soporteId = seguimientoId != 0 ? seguimientoId : Convert.ToInt32(F("HallazgoRiesgoId"));
                var soporte = await Leer(db, soporteTipo, soporteId, cache, ct);
                return soporte is null ? null : await Destino(db, soporte, cache, ct, profundidad + 1);
            case "AdjuntoEnsayo":
                var muestraId = await db.ValoresCampoEnsayo.AsNoTracking().Where(v => v.AdjuntoEnsayoId == id && db.ResultadosEnsayo.Any(r => r.Id == v.ResultadoEnsayoId && !r.Eliminado)).Select(v => v.ResultadoEnsayoId).FirstOrDefaultAsync(ct);
                var muestra = await Leer(db, typeof(ResultadoEnsayo), muestraId, cache, ct);
                return muestra is null || Texto(muestra, "Eliminado") == "True" ? null : await Destino(db, muestra, cache, ct, profundidad + 1);
            case "CampoTabla": return Tabla(F("Tabla"));
            case "ValorCampoTabla":
                var campoId = Convert.ToInt32(F("CampoTablaId"));
                var campo = await db.CamposTabla.AsNoTracking().FirstOrDefaultAsync(c => c.Id == campoId, ct);
                if (campo is null || campo.Eliminado) return null;
                cache[(typeof(CampoTabla), campo.Id)] = campo;
                if (campo.Tabla.StartsWith("ensayos:") || campo.Tabla.StartsWith("parametros:") || campo.Tabla.StartsWith("catalogo:"))
                {
                    var tipoDinamico = campo.Tabla.StartsWith("ensayos:") ? typeof(ResultadoEnsayo) : campo.Tabla.StartsWith("parametros:") ? typeof(ParametroActividad) : typeof(ItemCatalogo);
                    var resultado = await Leer(db, tipoDinamico, Convert.ToInt32(F("FilaId")), cache, ct);
                    return resultado is null || Texto(resultado, "Eliminado") == "True" ? null : await Destino(db, resultado, cache, ct, profundidad + 1);
                }
                var tipoFila = TipoTabla(campo.Tabla);
                if (tipoFila is null) return Tabla(campo.Tabla);
                var registro = await Leer(db, tipoFila, Convert.ToInt32(F("FilaId")), cache, ct);
                return registro is null || Texto(registro, "Eliminado") == "True" ? null : await Destino(db, registro, cache, ct, profundidad + 1);
        }
        if (Listas.TryGetValue(fila.GetType().Name, out var lista)) return lista;
        var tipo = db.Model.FindEntityType(fila.GetType())!;
        if (Propietario(db, tipo) is { } p && int.TryParse(F(p.Campo), out var padreId))
        {
            var padre = await Leer(db, p.Tipo.ClrType, padreId, cache, ct);
            return padre is null || Texto(padre, "Eliminado") == "True" ? null : await Destino(db, padre, cache, ct, profundidad + 1);
        }
        return null;
    }

    private static (string, string)? Tabla(string tabla) => tabla.StartsWith("ensayos:") && int.TryParse(tabla[8..], out var tipo) ? ($"ensayos/{tipo}", "Ensayo") : tabla switch
    {
        "torres" => ("trazado/tabla-torres", "Torre"), "trazado-torres" => ("trazado", "Torre"), "riesgos" => ("riesgos", "Riesgo"), "ifc-modelos" => ("ifc", "Torre"), "intervenciones-equipo" => ("equipos", "Equipo"), "componentes" => ("trazado", "Torre"),
        "documentos" or "planos" or "materiales" or "equipos" or "comunicados" or "planes-accion" or "tiros" or "actividades" => (tabla, "Documento"),
        _ when tabla.StartsWith("parametros:") => ($"actividades?actividad={tabla[11..]}", "Actividad"),
        _ when tabla.StartsWith("catalogo:") => ("catalogos", "Catálogo"),
        _ when tabla.StartsWith("bom:") => ($"modelos3d?modelo={Uri.EscapeDataString(tabla[4..])}", "Torre"),
        _ => null
    };
    private static Type? TipoTabla(string tabla) => tabla switch
    {
        "torres" or "trazado-torres" => typeof(ElementoTrazado), "componentes" => typeof(Componente), "documentos" => typeof(DocumentoMaestro),
        "planos" => typeof(PlanoMaestro), "materiales" => typeof(Material), "equipos" => typeof(Equipo),
        "comunicados" => typeof(Comunicado), "planes-accion" => typeof(PlanAccion), "tiros" => typeof(Tiro), "actividades" => typeof(Actividad), "riesgos" => typeof(Riesgo), "intervenciones-equipo" => typeof(IntervencionEquipo), _ => null
    };
    private static async Task<object?> Leer(AppDbContext db, Type tipo, int id, Dictionary<(Type, int), object?> cache, CancellationToken ct)
    {
        if (!cache.TryGetValue((tipo, id), out var fila))
            cache[(tipo, id)] = fila = await (Task<object?>)ObtenerMetodo.MakeGenericMethod(tipo).Invoke(null, [db, id, ct])!;
        return fila;
    }
    private static async Task<object?> Obtener<T>(AppDbContext db, int id, CancellationToken ct) where T : class => await db.Set<T>().AsNoTracking().FirstOrDefaultAsync(x => EF.Property<int>(x, "Id") == id, ct);
    private static int Id(object fila) => (int)fila.GetType().GetProperty("Id")!.GetValue(fila)!;
    private static string Texto(object fila, string campo) => Convert.ToString(fila.GetType().GetProperty(campo)?.GetValue(fila), CultureInfo.InvariantCulture) ?? "";
    private static readonly (string, string)[] Sustituciones = [("á", "a"), ("é", "e"), ("í", "i"), ("ó", "o"), ("ú", "u"), ("ü", "u"), ("ñ", "n"), ("à", "a"), ("è", "e"), ("ì", "i"), ("ò", "o"), ("ù", "u"), ("ç", "c"), ("\u0301", ""), ("\u0300", ""), ("\u0308", ""), ("\u0303", "")];
    private static string Normalizar(string texto) => string.Concat(texto.Normalize(NormalizationForm.FormD).Where(c => CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark)).ToLowerInvariant();
    private static string Corto(string texto, int largo) => texto.Length <= largo ? texto.Replace('\n', ' ').Trim() : texto[..(largo - 1)].Replace('\n', ' ').Trim() + "…";
    private static string Etiqueta(string nombre) => nombre switch
    {
        "ElementoTrazado" => "Torre", "PlanAccion" => "Plan de acción", "PlanoMaestro" => "Plano", "DocumentoMaestro" => "Documento",
        "TipoEnsayo" => "Ensayo", "ItemCatalogo" => "Ítem de catálogo", "DefinicionCatalogo" => "Catálogo", "CargoOrganigrama" => "Cargo",
        "ParteInteresada" => "Parte interesada", "Certificacion" => "Certificación", "Actividad" => "Actividad", "AccionPlan" => "Acción del plan",
        "Auditoria" => "Auditoría", "HallazgoAuditoria" => "Hallazgo de auditoría", "DocumentoDireccionamiento" => "Direccionamiento estratégico",
        "RegistroModulo" => "Registro de módulo creado", "ArchivoRegistroModulo" => "Adjunto de módulo creado", "VinculoRegistroModulo" => "Vínculo de módulo creado",
        _ => Tildes(Regex.Replace(nombre, "(?<=[a-záéíóú])(?=[A-Z])", " "))
    };

    // Nombres de tipo armados desde la clase (sin tildes): se corrigen las palabras frecuentes.
    private static readonly Dictionary<string, string> ConTilde = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Configuracion"] = "Configuración", ["Asignacion"] = "Asignación", ["Codigo"] = "Código", ["Item"] = "Ítem",
        ["Parametro"] = "Parámetro", ["Version"] = "Versión", ["Revision"] = "Revisión", ["Medicion"] = "Medición",
        ["Intervencion"] = "Intervención", ["Accion"] = "Acción", ["Modificacion"] = "Modificación", ["Facturacion"] = "Facturación",
        ["Produccion"] = "Producción", ["Programacion"] = "Programación", ["Definicion"] = "Definición", ["Ubicacion"] = "Ubicación",
        ["Organigrama"] = "Organigrama", ["Auditoria"] = "Auditoría", ["Certificacion"] = "Certificación", ["Catalogo"] = "Catálogo",
        ["Area"] = "Área", ["Diario"] = "Diario", ["Periodo"] = "Período", ["Linea"] = "Línea", ["Tecnica"] = "Técnica"
    };
    private static string Tildes(string texto)
    {
        var partes = texto.Split(' ');
        for (var i = 0; i < partes.Length; i++)
        {
            if (ConTilde.TryGetValue(partes[i], out var t)) partes[i] = t;
            if (i > 0) partes[i] = partes[i].ToLowerInvariant();
        }
        return string.Join(' ', partes);
    }
}
