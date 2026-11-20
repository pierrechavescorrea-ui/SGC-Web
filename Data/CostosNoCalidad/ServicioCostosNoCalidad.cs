using System.Globalization;
using Microsoft.EntityFrameworkCore;
using SigWeb.Data.Formularios;
using SigWeb.Data.Tablas;
using SigWeb.Models;

namespace SigWeb.Data.CostosNoCalidad;

// Costos de no calidad: se alimenta del paso del formulario de Planes de acción ligado a este módulo
// (SeccionD.Modulo = Clave en el diseño vigente; "Costos asociados" en el proyecto piloto).
// Lo que cuenta = campos de ese paso marcados "Resumen" (CampoTabla.EnResumen):
//   - numéricos → métricas (rubros). Un campo por opción ("Valor - Consultoría") = rubro "Consultoría".
//   - texto / lista / sí-no / fecha → dimensiones (chips y agrupación de los gráficos).
// No escribe nada; todo se lee con su propio DbContext (nunca en el render).
public static class ModuloCostosNoCalidad
{
    public const string Clave = "costos-no-calidad";
    public const string Nombre = "Costos de no calidad";
    public const string Formulario = "planes-accion";
    public const string Tabla = "planes-accion";
    public const string TituloSugerido = "Costos asociados";

    // Módulos que se pueden ligar a un paso nuevo de un formulario (diseñador): formulario → (clave, nombre).
    public static readonly IReadOnlyList<(string Formulario, string Clave, string Nombre)> Ligables = new[]
    {
        (Formulario, Clave, Nombre),
    };

    public static string? NombreModulo(string? clave) => Ligables.FirstOrDefault(l => l.Clave == clave).Nombre;

    // Arranque (idempotente): si el diseño vigente de Planes no tiene paso ligado y tiene un paso nuevo llamado
    // "Costos asociados", lo liga en la misma versión (no crea versión: solo marca el paso existente).
    public static async Task LigarPasoPorNombreAsync(AppDbContext db)
    {
        try
        {
            var reg = await db.DisenosFormulario.Where(x => x.Clave == Formulario && x.Vigente).OrderByDescending(x => x.Version).FirstOrDefaultAsync();
            var d = DisenoModelo.DeJson<Diseno>(reg?.Json);
            if (reg is null || d is null || d.Original || d.Secciones.Any(s => s.Modulo == Clave)) return;
            var paso = d.Secciones.FirstOrDefault(s => s.Nueva && string.Equals(DisenoModelo.Normal(s.Titulo), TituloSugerido, StringComparison.OrdinalIgnoreCase));
            if (paso is null) return;
            paso.Modulo = Clave;
            reg.Json = DisenoModelo.AJson(d);
            await db.SaveChangesAsync();
        }
        catch { /* sin tablas del diseñador: el módulo avisa que no hay paso ligado */ }
    }
}

public sealed record CampoCosto(int Id, string Nombre, string Rubro, string Grupo, int? Decimales);
public sealed record CampoDimension(int Id, string Nombre, TipoCampoTabla Tipo);

public sealed class PlanCosto
{
    public int Id { get; init; }
    public string Codigo { get; init; } = "";
    public string Descripcion { get; init; } = "";
    public DateTime Fecha { get; init; }
    public string Origen { get; init; } = "";
    public string Tipo { get; init; } = "";
    public string Proceso { get; init; } = "";
    public EstadoAccionPlan Estado { get; init; }
    public int? AuditoriaId { get; init; }
    public bool? Eficaz { get; init; }
    public Dictionary<int, double> Valores { get; } = new();      // CampoCosto.Id → valor
    public Dictionary<int, string> Dimensiones { get; } = new();  // CampoDimension.Id → texto
    public double Total => Valores.Values.Sum();
}

public sealed class InstantaneaCostosNoCalidad
{
    public bool Ligado { get; init; }
    public string? PasoClave { get; init; }
    public string PasoTitulo { get; init; } = ModuloCostosNoCalidad.TituloSugerido;
    public List<CampoCosto> Campos { get; init; } = new();
    public List<CampoDimension> Dimensiones { get; init; } = new();
    public int CamposEnPaso { get; init; }                         // todos los del paso (marcados o no): para el aviso
    public List<PlanCosto> Planes { get; init; } = new();          // todos los planes (con o sin costo)

    public IEnumerable<string> Rubros => Campos.Select(c => c.Rubro).Distinct();
    public double TotalRubro(IEnumerable<PlanCosto> planes, string rubro)
    {
        var ids = Campos.Where(c => c.Rubro == rubro).Select(c => c.Id).ToHashSet();
        return planes.Sum(p => p.Valores.Where(v => ids.Contains(v.Key)).Sum(v => v.Value));
    }
    public double ValorRubro(PlanCosto p, string rubro)
    {
        var ids = Campos.Where(c => c.Rubro == rubro).Select(c => c.Id).ToHashSet();
        return p.Valores.Where(v => ids.Contains(v.Key)).Sum(v => v.Value);
    }
}

public static class ServicioCostosNoCalidad
{
    public static async Task<InstantaneaCostosNoCalidad> CargarAsync(AppDbContext db)
    {
        // Paso ligado en el diseño vigente; si se restableció o se trajo otra versión, la última versión que lo tenga.
        Diseno? d = null; SeccionD? paso = null;
        try
        {
            var jsons = await db.DisenosFormulario.AsNoTracking().Where(x => x.Clave == ModuloCostosNoCalidad.Formulario)
                .OrderByDescending(x => x.Vigente).ThenByDescending(x => x.Version).Select(x => x.Json).Take(30).ToListAsync();
            foreach (var j in jsons)
            {
                var dj = DisenoModelo.DeJson<Diseno>(j);
                var pj = dj?.Secciones.FirstOrDefault(s => s.Modulo == ModuloCostosNoCalidad.Clave);
                if (pj is not null) { d = dj; paso = pj; break; }
            }
        }
        catch { /* migración DisenoFormularios sin aplicar */ }

        var planesBd = await db.PlanesAccion.AsNoTracking()
            .Select(p => new { p.Id, p.Codigo, p.DescripcionHallazgo, p.Fecha, p.OrigenHallazgo, p.TipoHallazgo, p.Proceso, p.AuditoriaId, p.Eficaz })
            .ToListAsync();
        var planIds = planesBd.Select(p => p.Id).ToList();
        var acciones = (await db.AccionesPlan.AsNoTracking().Where(a => planIds.Contains(a.PlanAccionId)).ToListAsync())
            .ToLookup(a => a.PlanAccionId);

        var campos = new List<CampoCosto>(); var dims = new List<CampoDimension>(); int enPaso = 0;
        if (d is not null && paso is not null)
        {
            var todos = await db.CamposTabla.AsNoTracking().Where(c => c.Tabla == ModuloCostosNoCalidad.Tabla).ToListAsync();
            var ids = d.Campos.Where(c => c.Seccion == paso.Clave && c.CampoTablaId is int).Select(c => c.CampoTablaId!.Value).ToHashSet();
            // Hijos de las listas del paso (campos según la opción): son del paso aunque el diseño solo tenga la plantilla.
            foreach (var h in todos.Where(c => c.CampoPadreId is int pid && ids.Contains(pid)).ToList()) ids.Add(h.Id);
            var delPaso = todos.Where(c => ids.Contains(c.Id) && !CamposPorOpcion.EsPlantilla(c)).OrderBy(c => c.Orden).ThenBy(c => c.Id).ToList();
            enPaso = delPaso.Count;
            foreach (var c in delPaso.Where(c => c.EnResumen))
            {
                if (c.Tipo == TipoCampoTabla.Numero)
                {
                    var asociado = c.CampoPadreId is not null && !string.IsNullOrWhiteSpace(c.OpcionPadre);
                    campos.Add(new CampoCosto(c.Id, MotorTabla.EtiquetaCampo(c), asociado ? c.OpcionPadre! : MotorTabla.EtiquetaCampo(c),
                        asociado ? CamposPorOpcion.Base(c) : MotorTabla.EtiquetaCampo(c), c.Decimales));
                }
                else if (c.Tipo is TipoCampoTabla.Texto or TipoCampoTabla.Lista or TipoCampoTabla.SiNo or TipoCampoTabla.Fecha)
                    dims.Add(new CampoDimension(c.Id, MotorTabla.EtiquetaCampo(c), c.Tipo));
            }
        }

        var leer = campos.Select(c => c.Id).Concat(dims.Select(x => x.Id)).ToList();
        var valores = leer.Count == 0 ? new List<ValorCampoTabla>()
            : await db.ValoresCampoTabla.AsNoTracking().Where(v => leer.Contains(v.CampoTablaId) && planIds.Contains(v.FilaId)).ToListAsync();
        var porFila = valores.ToLookup(v => v.FilaId);
        var numericos = campos.Select(c => c.Id).ToHashSet();
        var planes = new List<PlanCosto>();
        foreach (var p in planesBd)
        {
            var pc = new PlanCosto
            {
                Id = p.Id, Codigo = p.Codigo, Descripcion = p.DescripcionHallazgo, Fecha = p.Fecha, Origen = p.OrigenHallazgo,
                Tipo = p.TipoHallazgo, Proceso = p.Proceso, AuditoriaId = p.AuditoriaId, Eficaz = p.Eficaz,
                Estado = EscalaAccion.EstadoPlan(acciones[p.Id]),
            };
            foreach (var v in porFila[p.Id])
            {
                if (string.IsNullOrWhiteSpace(v.Valor)) continue;
                if (numericos.Contains(v.CampoTablaId)) { if (Numero(v.Valor) is double n) pc.Valores[v.CampoTablaId] = n; }
                else pc.Dimensiones[v.CampoTablaId] = v.Valor!;
            }
            planes.Add(pc);
        }

        return new InstantaneaCostosNoCalidad
        {
            Ligado = paso is not null, PasoClave = paso?.Clave, PasoTitulo = paso?.Titulo ?? ModuloCostosNoCalidad.TituloSugerido,
            Campos = campos, Dimensiones = dims, CamposEnPaso = enPaso, Planes = planes,
        };
    }

    // Mismo criterio que MotorTabla.Tipar (invariante y luego es-CO).
    public static double? Numero(string? s)
    {
        if (string.IsNullOrWhiteSpace(s)) return null;
        s = s.Trim().Replace("$", "").Replace(" ", "");
        if (double.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out var n)) return n;
        if (double.TryParse(s, NumberStyles.Float | NumberStyles.AllowThousands, FormulaExcel.Co, out n)) return n;
        return null;
    }

    public static string Moneda(double v) => v.ToString("$ #,##0", FormulaExcel.Co);
    public static string Corto(double v) =>
        Math.Abs(v) >= 1e9 ? (v / 1e9).ToString("0.#", FormulaExcel.Co) + " mil M"
        : Math.Abs(v) >= 1e6 ? (v / 1e6).ToString("0.#", FormulaExcel.Co) + " M"
        : Math.Abs(v) >= 1e3 ? (v / 1e3).ToString("0.#", FormulaExcel.Co) + " mil"
        : v.ToString("0", FormulaExcel.Co);
}
