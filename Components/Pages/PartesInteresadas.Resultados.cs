using SigWeb.Components.Layout;
using SigWeb.Data.PartesInteresadas;
using SigWeb.Models;

namespace SigWeb.Components.Pages;

// Vista "Resultados de los seguimientos": compilado de todas las partes y vista rápida de cada una
// (como Indicador por indicador). Solo arma datos en memoria con lo que ya cargó Cargar().
public partial class PartesInteresadas
{
    private int anioRes = DateTime.Today.Year;
    private string? selRes;

    private static string ClaseDe(CumplimientoParte c) => c switch
    {
        CumplimientoParte.Cumple => ClasesRes.Ok,
        CumplimientoParte.Parcial => ClasesRes.Medio,
        CumplimientoParte.NoCumple => ClasesRes.Mal,
        _ => ClasesRes.Gris
    };

    private static string Corto(CumplimientoParte c) => c switch
    {
        CumplimientoParte.Cumple => "Cumple",
        CumplimientoParte.Parcial => "Parcial",
        CumplimientoParte.NoCumple => "No cumple",
        _ => "Sin medir"
    };

    private static readonly List<ChipRes> LeyendaPartes = new()
    {
        new("Cumple", ClasesRes.Ok), new("Parcial", ClasesRes.Medio), new("No cumple", ClasesRes.Mal), new("Sin medir", ClasesRes.Gris)
    };

    private List<ObjetoRes> ItemsResultados()
    {
        var hoy = DateTime.Today;
        var res = new List<ObjetoRes>();
        foreach (var p in Mostradas.OrderBy(p => EsInactiva(p.Clave)).ThenBy(p => p.Orden).ThenBy(p => p.Id))
        {
            var segs = SeguimientosDe(p.Clave).ToList();      // más reciente primero
            var inactiva = EsInactiva(p.Clave);
            var meses = new MesRes[12];
            for (var m = 1; m <= 12; m++)
            {
                var s = segs.FirstOrDefault(x => x.Fecha.Year == anioRes && x.Fecha.Month == m);
                var pasado = new DateTime(anioRes, m, 1) <= hoy;
                meses[m - 1] = s is null
                    ? new MesRes(pasado && !inactiva ? ClasesRes.Falta : "", "", pasado ? "Sin seguimiento" : "")
                    : new MesRes(ClaseDe(s.Cumplimiento), string.IsNullOrWhiteSpace(s.Resultado) ? Corto(s.Cumplimiento) : s.Resultado!,
                        $"{s.Fecha:dd/MM/yyyy} · {ServicioPartesInteresadas.Nombre(s.Cumplimiento)}" + (string.IsNullOrWhiteSpace(s.Resultado) ? "" : $" · {s.Resultado}"));
            }
            var ult = segs.FirstOrDefault();
            var delAnio = segs.Where(s => s.Fecha.Year == anioRes).ToList();
            var medidos = delAnio.Count(s => s.Cumplimiento != CumplimientoParte.SinMedir);
            var cumplen = delAnio.Count(s => s.Cumplimiento == CumplimientoParte.Cumple);
            var sit = situacion.GetValueOrDefault(p.Clave);
            var prog = programaciones.GetValueOrDefault(p.Clave);
            var cuad = ServicioPartesInteresadas.Cuadrante(p.Impacto, p.Influencia);

            var chips = new List<ChipRes>
            {
                new(ServicioPartesInteresadas.Nombre(cuad), Color: ServicioPartesInteresadas.Color(cuad), Titulo: $"IMP {p.Impacto:0.#} · INF {p.Influencia:0.#}"),
                new(inactiva ? "Inactiva" : ServicioPartesInteresadas.Nombre(EstadoDe(p.Clave)), Titulo: "Seguimiento")
            };
            if (!string.IsNullOrWhiteSpace(p.Clasificacion)) chips.Add(new(p.Clasificacion!, Titulo: "Clasificación"));
            if (prog?.Cada is int cada) chips.Add(new($"Cada {cada} {ServicioPartesInteresadas.NombreUnidad(prog.Unidad, cada)}", Titulo: "Programación"));
            else if (!string.IsNullOrWhiteSpace(p.Periodicidad)) chips.Add(new(p.Periodicidad!, Titulo: "Periodicidad"));
            if (!string.IsNullOrWhiteSpace(prog?.Responsable)) chips.Add(new(prog!.Responsable!, Titulo: "Responsable"));

            res.Add(new ObjetoRes
            {
                Clave = p.Clave.ToString(),
                Codigo = p.Orden.ToString(),
                Nombre = p.Nombre,
                Apagado = inactiva,
                Ultimo = ult is null ? null : new ChipRes(Corto(ult.Cumplimiento), ClaseDe(ult.Cumplimiento), Titulo: $"{ult.Fecha:dd/MM/yyyy} · {ult.Resultado}"),
                Chips = chips,
                Meses = meses,
                Kpis = new()
                {
                    new(ult is null ? "Sin seguimiento" : $"Último · {ult.Fecha:dd/MM/yyyy}",
                        ult is null ? "—" : string.IsNullOrWhiteSpace(ult.Resultado) ? Corto(ult.Cumplimiento) : ult.Resultado!,
                        ult is null ? null : ServicioPartesInteresadas.Nombre(ult.Cumplimiento), ult is null ? null : ClaseDe(ult.Cumplimiento), Grande: true),
                    new("Meta", string.IsNullOrWhiteSpace(p.Meta) ? "—" : p.Meta!, p.Medicion),
                    new("Seguimientos que cumplen", medidos == 0 ? "—" : $"{cumplen} / {medidos}", $"{anioRes} · {delAnio.Count} seguimiento(s)"),
                    new("Próximo seguimiento", sit?.Proximo is DateTime px ? px.ToString("dd/MM/yyyy") : "—",
                        inactiva ? "Inactiva" : ServicioPartesInteresadas.Nombre(EstadoDe(p.Clave)),
                        EstadoDe(p.Clave) switch { EstadoSeguimientoParte.Vencido => ClasesRes.Mal, EstadoSeguimientoParte.PorVencer => ClasesRes.Medio, _ => null })
                },
                Ficha = new()
                {
                    ("Necesidades", p.Necesidades), ("Expectativas", p.Expectativas), ("Cómo afecta al SGC", p.AfectaSgc),
                    ("Estrategias", p.Estrategias), ("Medición", p.Medicion), ("Procesos", p.Procesos), ("Observaciones", p.Observaciones)
                },
                Filas = segs.Select(s => new FilaRes(s.Fecha, Corto(s.Cumplimiento), ClaseDe(s.Cumplimiento), s.Responsable,
                    string.Join(" · ", new[]
                    {
                        string.IsNullOrWhiteSpace(s.Resultado) ? null : $"Resultado: {s.Resultado}",
                        s.Impacto is null && s.Influencia is null ? null : $"IMP {s.Impacto:0.#} · INF {s.Influencia:0.#}"
                    }.Where(x => x is not null)), string.Join("\n", new[] { s.Observacion, s.DescripcionHallazgo }.Where(x => !string.IsNullOrWhiteSpace(x))))).ToList()
            });
        }
        return res;
    }

    private List<KpiRes> KpisResultados(List<ObjetoRes> items)
    {
        var claves = items.Select(i => Guid.Parse(i.Clave)).ToHashSet();
        var delAnio = seguimientos.Where(s => claves.Contains(s.ParteClave) && s.Fecha.Year == anioRes).ToList();
        var medidos = delAnio.Count(s => s.Cumplimiento != CumplimientoParte.SinMedir);
        var cumplen = delAnio.Count(s => s.Cumplimiento == CumplimientoParte.Cumple);
        var activas = items.Where(i => !i.Apagado).Select(i => Guid.Parse(i.Clave)).ToList();
        var sinSeg = activas.Count(c => !delAnio.Any(s => s.ParteClave == c));
        var vencidas = activas.Count(c => EstadoDe(c) == EstadoSeguimientoParte.Vencido);
        var pct = medidos == 0 ? (decimal?)null : Math.Round(100m * cumplen / medidos, 0);
        return new()
        {
            new("Cumplimiento del año", pct is null ? "—" : $"{pct}%", medidos == 0 ? "sin mediciones" : $"{cumplen} de {medidos} seguimiento(s) medidos",
                pct is null ? null : pct >= 90 ? ClasesRes.Ok : pct >= 70 ? ClasesRes.Medio : ClasesRes.Mal, Grande: true),
            new("Seguimientos", delAnio.Count.ToString(), $"{anioRes}"),
            new("Seguimientos vencidos", vencidas.ToString(), $"de {activas.Count} parte(s) activas", vencidas > 0 ? ClasesRes.Mal : null),
            new("Sin seguimiento en el año", sinSeg.ToString(), "partes activas")
        };
    }

    private void CambiarAnioRes(int a) => anioRes = Math.Min(a, DateTime.Today.Year);

    private async Task AbrirParteEnMapa(string clave)
    {
        if (!Guid.TryParse(clave, out var g)) return;
        sel = g; vista = "mapa";
        await Recordar();
    }
}
