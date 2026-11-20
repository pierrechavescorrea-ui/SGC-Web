using Microsoft.EntityFrameworkCore;
using SigWeb.Components.Layout;
using SigWeb.Models;

namespace SigWeb.Components.Pages;

// Vista "Resultados de los seguimientos" de la matriz de riesgos: compilado de todos los riesgos de la
// revisión mostrada y vista rápida de cada uno (como Indicador por indicador). Los seguimientos se
// juntan por código de riesgo, así un riesgo conserva su historia aunque haya cambiado de revisión.
public partial class Riesgos
{
    private int anioRes = DateTime.Today.Year;
    private string? selRes;
    private Dictionary<string, List<SeguimientoRiesgo>> segsPorCodigo = new(StringComparer.OrdinalIgnoreCase);

    // Se llama desde CargarAsync (nunca desde el render).
    private void CargarResultados()
    {
        var codigos = riesgos.Select(r => r.Codigo.Trim()).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        var filas = (from s in Db.SeguimientosRiesgo.AsNoTracking()
                     join r in Db.Riesgos.AsNoTracking() on s.RiesgoId equals r.Id
                     where codigos.Contains(r.Codigo)
                     select new { r.Codigo, S = s }).ToList();
        segsPorCodigo = filas.GroupBy(x => x.Codigo.Trim(), StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.Select(x => x.S).OrderByDescending(s => s.Fecha).ThenByDescending(s => s.Id).ToList(), StringComparer.OrdinalIgnoreCase);
    }

    private static string ClaseDe(ResultadoSeguimiento r) => r switch
    {
        ResultadoSeguimiento.Materializado => ClasesRes.Mal,
        ResultadoSeguimiento.EnObservacion => ClasesRes.Medio,
        _ => ClasesRes.Ok
    };

    private static string Corto(ResultadoSeguimiento r) => r switch
    {
        ResultadoSeguimiento.Materializado => "Materializado",
        ResultadoSeguimiento.EnObservacion => "Observación",
        _ => "No mat."
    };

    private static string ClaseNivel(NivelRiesgo n) => n switch
    {
        NivelRiesgo.Extremo or NivelRiesgo.Alto => ClasesRes.Mal,
        NivelRiesgo.Moderado => ClasesRes.Medio,
        _ => ClasesRes.Ok
    };

    private static readonly List<ChipRes> LeyendaRiesgos = new()
    {
        new("No materializado", ClasesRes.Ok), new("En observación", ClasesRes.Medio), new("Materializado", ClasesRes.Mal)
    };

    private List<ObjetoRes> ItemsResultados()
    {
        var hoy = DateTime.Today;
        var res = new List<ObjetoRes>();
        foreach (var r in Mostrados)
        {
            var segs = segsPorCodigo.GetValueOrDefault(r.Codigo.Trim()) ?? new();
            var cerrado = string.Equals(r.EstadoActual, "Cerrado", StringComparison.OrdinalIgnoreCase);
            var meses = new MesRes[12];
            for (var m = 1; m <= 12; m++)
            {
                var delMes = segs.Where(x => x.Fecha.Year == anioRes && x.Fecha.Month == m).ToList();
                // En el mes manda el peor resultado (un materializado no se esconde tras un seguimiento posterior).
                var s = delMes.OrderByDescending(x => (int)x.Resultado).FirstOrDefault();
                var pasado = new DateTime(anioRes, m, 1) <= hoy;
                meses[m - 1] = s is null
                    ? new MesRes(pasado && !cerrado ? ClasesRes.Falta : "", "", pasado ? "Sin seguimiento" : "")
                    : new MesRes(ClaseDe(s.Resultado), s.Resultado switch { ResultadoSeguimiento.Materializado => "Mat.", ResultadoSeguimiento.EnObservacion => "Obs.", _ => "No mat." },
                        string.Join(" · ", delMes.Select(x => $"{x.Fecha:dd/MM/yyyy} {NombreResultado(x.Resultado)}")));
            }
            var ult = segs.FirstOrDefault();
            var delAnio = segs.Where(s => s.Fecha.Year == anioRes).ToList();
            var mat = delAnio.Count(s => s.Resultado == ResultadoSeguimiento.Materializado);
            var planes = PlanesDe(r);
            var hall = hallazgosAbiertos.GetValueOrDefault(r.Id);

            var chips = new List<ChipRes>
            {
                new(EscalaRiesgo.Nombre(r.Nivel), ClasesRes.Ok == ClaseNivel(r.Nivel) ? "res-ley res-ok" : ClaseNivel(r.Nivel) == ClasesRes.Medio ? "res-ley res-medio" : "res-ley res-mal", Titulo: "Nivel"),
                new(r.Tipo == TipoRiesgo.Oportunidad ? "Oportunidad" : "Riesgo")
            };
            foreach (var (t, v) in new[] { ("Estado", r.EstadoActual), ("Categoría", r.Categoria), ("Área", r.Area), ("Estrategia", r.Estrategia), ("Responsable del plan", r.ResponsablePlan) })
                if (!string.IsNullOrWhiteSpace(v)) chips.Add(new(v, Titulo: t));

            res.Add(new ObjetoRes
            {
                Clave = r.Id.ToString(),
                Codigo = r.Codigo,
                Nombre = r.Descripcion,
                Apagado = cerrado,
                Ultimo = ult is null ? null : new ChipRes(Corto(ult.Resultado), ClaseDe(ult.Resultado), Titulo: $"{ult.Fecha:dd/MM/yyyy} · {NombreResultado(ult.Resultado)}"),
                Chips = chips,
                Meses = meses,
                Kpis = new()
                {
                    new(ult is null ? "Sin seguimiento" : $"Último · {ult.Fecha:dd/MM/yyyy}", ult is null ? "—" : NombreResultado(ult.Resultado),
                        ult?.Responsable, ult is null ? null : ClaseDe(ult.Resultado), Grande: true),
                    new("Nivel actual", EscalaRiesgo.Nombre(r.Nivel), $"{EscalaRiesgo.Nombre(r.Probabilidad)} × {EscalaRiesgo.Nombre(r.Impacto)}", ClaseNivel(r.Nivel)),
                    new("Materializado en el año", delAnio.Count == 0 ? "—" : $"{mat} / {delAnio.Count}", $"{anioRes} · seguimientos", mat > 0 ? ClasesRes.Mal : null),
                    new("Hallazgos abiertos · planes", $"{hall} · {planes.Count}", planes.Count == 0 ? "sin planes" : string.Join(", ", planes.Select(p => p.Codigo)), hall > 0 ? ClasesRes.Medio : null)
                },
                Ficha = new()
                {
                    ("Causas", r.Causa), ("Consecuencias", r.Consecuencia), ("Disparadores", r.Disparadores),
                    ("Plan de respuesta", r.PlanRespuesta), ("Cómo se hace el seguimiento", r.ComoSeHaceSeguimiento)
                },
                Filas = segs.Select(s => new FilaRes(s.Fecha, NombreResultado(s.Resultado), ClaseDe(s.Resultado), s.Responsable,
                    s.Probabilidad is ProbabilidadRiesgo p && s.Impacto is ImpactoRiesgo i
                        ? $"Valoración: {EscalaRiesgo.Nombre(p)} × {EscalaRiesgo.Nombre(i)} = {EscalaRiesgo.Nombre(EscalaRiesgo.Nivel(p, i))}" : null,
                    s.Observacion)).ToList()
            });
        }
        return res;
    }

    private List<KpiRes> KpisResultados(List<ObjetoRes> items)
    {
        var codigos = items.Select(i => i.Codigo ?? "").ToList();
        var delAnio = codigos.SelectMany(c => (segsPorCodigo.GetValueOrDefault(c.Trim()) ?? new()).Where(s => s.Fecha.Year == anioRes).Select(s => (c, s))).ToList();
        var matRiesgos = delAnio.Where(x => x.s.Resultado == ResultadoSeguimiento.Materializado).Select(x => x.c).Distinct().Count();
        var obs = delAnio.Count(x => x.s.Resultado == ResultadoSeguimiento.EnObservacion);
        var activos = items.Where(i => !i.Apagado).ToList();
        var sinSeg = activos.Count(i => !delAnio.Any(x => x.c == i.Codigo));
        return new()
        {
            new("Riesgos materializados", matRiesgos.ToString(), $"{anioRes} · de {items.Count} riesgo(s)", matRiesgos > 0 ? ClasesRes.Mal : ClasesRes.Ok, Grande: true),
            new("Seguimientos", delAnio.Count.ToString(), $"{anioRes}"),
            new("En observación", obs.ToString(), "seguimientos del año", obs > 0 ? ClasesRes.Medio : null),
            new("Sin seguimiento en el año", sinSeg.ToString(), $"de {activos.Count} riesgo(s) activos")
        };
    }

    private void CambiarAnioRes(int a) => anioRes = Math.Min(a, DateTime.Today.Year);

    private string TituloEliminarRiesgo(Riesgo r) =>
        EsBorradorId(r.VersionMatrizRiesgosId) ? "Eliminar riesgo (va a la Papelera)"
        : borradorV is not null ? $"Eliminar del borrador {borradorV.Codigo}" : "Eliminar (crea un borrador)";

    private async Task EliminarRiesgoRes(string clave)
    {
        if (int.TryParse(clave, out var id) && riesgos.FirstOrDefault(x => x.Id == id) is Riesgo r) await PedirEliminarRiesgo(r);
    }

    private void AbrirRiesgoRes(string clave)
    {
        if (int.TryParse(clave, out var id) && riesgos.FirstOrDefault(x => x.Id == id) is Riesgo r) AbrirRiesgo(r);
    }
}
