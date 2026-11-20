using System.Globalization;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using SigWeb.Data.Estadistica;
using SigWeb.Models;

namespace SigWeb.Components.Estadistica;

// Análisis estadístico de un campo numérico de los resultados de un ensayo (estilo MegaStat). Recibe las filas ya filtradas
// por el panel de gráficos (GraficoEnsayos) y calcula todo en memoria con Data/Estadistica; el JS solo dibuja.
public partial class AnalisisEstadistico : IAsyncDisposable
{
    [Inject] private IJSRuntime JS { get; set; } = default!;

    [Parameter] public string Titulo { get; set; } = "";
    [Parameter] public List<Dictionary<string, object>> Columnas { get; set; } = new();
    [Parameter] public List<Dictionary<string, object>> Filas { get; set; } = new();
    [Parameter] public Dictionary<string, TipoDatoCampoEnsayo?> Tipos { get; set; } = new();
    [Parameter] public int FilasTotales { get; set; }
    [Parameter] public RenderFragment? FiltrosBarra { get; set; }
    [Parameter] public RenderFragment? FiltrosDetalle { get; set; }

    private readonly string idHist = "est-h-" + Guid.NewGuid().ToString("N")[..8];
    private string IdCampana => idHist + "-c";
    private string IdQq => idHist + "-q";
    private DotNetObjectReference<AnalisisEstadistico>? yo;

    // Elecciones del usuario.
    private string campo = "";
    private string agrupar = "";
    private double confianza = 0.95;
    private ReglaClases regla = ReglaClases.Sturges;
    private int clasesManual = 10;
    private double? lie, lse;
    private ElementReference raiz;
    private bool editando, restaurar, guardarOcultos;
    private readonly HashSet<string> ocultos = new(StringComparer.Ordinal);

    private void MostrarWidget(string id)
    {
        if (!ocultos.Remove(id)) return;
        guardarOcultos = redibujar = true;
    }

    [JSInvokable]
    public Task SoltarWidget(string id) => InvokeAsync(() =>
    {
        if (!editando || !ocultos.Contains(id)) return;
        MostrarWidget(id);
        StateHasChanged();
    });

    private void QuitarWidget(string id)
    {
        if (!ocultos.Add(id)) return;
        guardarOcultos = true;
    }

    private void Restaurar()
    {
        ocultos.Clear();
        restaurar = guardarOcultos = redibujar = true;
    }

    // Opciones del histograma.
    private bool verCurva = true, verMedia = true, verMediana = true, verModa = true, verIc, verEspec = true, verLeyenda = true, verCuadricula = true;
    private int sigmas;
    private string ejeY = "frec";
    private const string ColorBase = "#5b7fa6";
    private string colorBarras = ColorBase;
    private string tituloGrafico = "";

    // Probabilidad de ocurrencia.
    private string tipoProb = "entre"; // menor | mayor | entre | percentil
    private double? probA, probB, probP = 95;

    // Resultado.
    private ResultadoEstadistico r = new();
    private List<Interpretacion.Parrafo> lectura = new();
    private List<(string Grupo, ResultadoEstadistico R)> grupos = new();
    private int sinValor;
    private string firma = "";
    private bool redibujar, montado;

    private List<(string Clave, string Titulo)> CamposNumericos => Columnas
        .Select(c => (Clave: c["field"]?.ToString() ?? "", Titulo: c["title"]?.ToString() ?? ""))
        .Where(c => Tipos.TryGetValue(c.Clave, out var t) && (t == TipoDatoCampoEnsayo.Numero || t == TipoDatoCampoEnsayo.Formula))
        .ToList();

    private List<(string Clave, string Titulo)> CamposGrupo => Columnas
        .Select(c => (Clave: c["field"]?.ToString() ?? "", Titulo: c["title"]?.ToString() ?? ""))
        .Where(c => !(Tipos.TryGetValue(c.Clave, out var t) && (t == TipoDatoCampoEnsayo.Numero || t == TipoDatoCampoEnsayo.Formula || t == TipoDatoCampoEnsayo.Fecha)))
        .ToList();

    private string NombreCampo => CamposNumericos.FirstOrDefault(c => c.Clave == campo).Titulo ?? campo;

    protected override void OnParametersSet()
    {
        if (!CamposNumericos.Any(c => c.Clave == campo)) campo = CamposNumericos.FirstOrDefault().Clave ?? "";
        Recalcular(soloSiCambia: true);
    }

    public static double? Numero(object? v) => v switch
    {
        null => null,
        double d => double.IsFinite(d) ? d : null,
        float f => f,
        decimal m => (double)m,
        int i => i,
        long l => l,
        _ => double.TryParse(v.ToString(), NumberStyles.Float, CultureInfo.InvariantCulture, out var n) && double.IsFinite(n) ? n : null,
    };

    private IEnumerable<double> ValoresDe(IEnumerable<Dictionary<string, object>> filas) =>
        filas.Select(f => f.TryGetValue(campo, out var v) ? Numero(v) : null).Where(v => v is not null).Select(v => v!.Value);

    private void Recalcular(bool soloSiCambia = false)
    {
        var valores = ValoresDe(Filas).ToList();
        var nueva = $"{campo}|{agrupar}|{confianza}|{regla}|{clasesManual}|{lie}|{lse}|{valores.Count}|{(valores.Count > 0 ? valores.Sum() : 0):R}|{(valores.Count > 0 ? valores.Sum(v => v * v) : 0):R}";
        if (soloSiCambia && nueva == firma) return;
        firma = nueva;
        sinValor = Filas.Count - valores.Count;
        r = Descriptiva.Analizar(valores, confianza, regla, clasesManual, lie, lse);
        lectura = Interpretacion.Leer(r, NombreCampo);
        grupos = string.IsNullOrEmpty(agrupar) ? new() : Filas
            .GroupBy(f => f.TryGetValue(agrupar, out var g) && !string.IsNullOrWhiteSpace(g?.ToString()) ? g!.ToString()! : "(vacío)")
            .Select(g => (g.Key, Descriptiva.Analizar(ValoresDe(g), confianza)))
            .Where(g => g.Item2.N > 0)
            .OrderBy(g => g.Key, StringComparer.CurrentCultureIgnoreCase)
            .Take(60)
            .ToList();
        if (probA is null && probB is null && r.N > 1) { probA = Redondear(r.Media - r.Desviacion); probB = Redondear(r.Media + r.Desviacion); }
        redibujar = true;
    }

    private double Redondear(double v)
    {
        var escala = Math.Max(Math.Abs(r.Max), Math.Abs(r.Min));
        return Math.Round(v, escala >= 1000 ? 0 : escala >= 100 ? 1 : escala >= 1 ? 2 : 4);
    }

    private void Cambio(Action a) { a(); Recalcular(); }
    private void CambioGrafico(Action a) { a(); redibujar = true; }

    // Probabilidad elegida: (desde, hasta) del intervalo, para normal y observada.
    private (double? A, double? B) Intervalo() => tipoProb switch
    {
        "menor" => (null, probA),
        "mayor" => (probA, null),
        "percentil" => (null, ValorPercentil()),
        _ => (probA is double a && probB is double b && b < a ? (probB, probA) : (probA, probB)),
    };

    private double? ValorPercentil() => probP is double p && p > 0 && p < 100 && r.Desviacion > 0
        ? Distribuciones.NormalInv(p / 100, r.Media, r.Desviacion) : null;

    private (double Normal, double Empirica) Prob()
    {
        var (a, b) = Intervalo();
        return Descriptiva.Probabilidad(r, a, b);
    }

    [JSInvokable]
    public Task ElegirRango(double desde, double hasta)
    {
        tipoProb = "entre"; probA = Redondear(desde); probB = Redondear(hasta); redibujar = true;
        return InvokeAsync(StateHasChanged);
    }

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        try
        {
            if (firstRender)
            {
                yo = DotNetObjectReference.Create(this);
                montado = true;
                var guardados = await JS.InvokeAsync<string[]>("sigEstadistica.leerOcultos");
                ocultos.UnionWith(guardados.Where(EstWidget.Catalogo.ContainsKey));
                if (ocultos.Count > 0)
                {
                    StateHasChanged();
                    return;
                }
            }
            if (guardarOcultos)
            {
                guardarOcultos = false;
                await JS.InvokeVoidAsync("sigEstadistica.guardarOcultos", (object)ocultos.ToArray());
            }
            var reiniciar = restaurar;
            restaurar = false;
            await JS.InvokeVoidAsync("sigEstadistica.panelAplicar", raiz, editando, reiniciar, yo);
        }
        catch (JSDisconnectedException) { return; }
        catch (JSException) { }
        if (!redibujar || !montado) return;
        redibujar = false;
        try
        {
            if (r.N == 0) { await JS.InvokeVoidAsync("sigEstadistica.soltar", (object)new[] { idHist, IdCampana, IdQq }); return; }
            double? moda = r.Modas.Count == 1 ? r.Modas[0] : r.ModaAgrupada;
            await JS.InvokeVoidAsync("sigEstadistica.histograma", idHist, new
            {
                campo = NombreCampo,
                clases = r.Clases.Select(c => new { d = c.Desde, h = c.Hasta, f = c.Frecuencia }),
                n = r.N, media = r.Media, desv = r.Desviacion, mediana = r.Mediana, moda, lie, lse,
                icInf = r.IcMediaInf, icSup = r.IcMediaSup,
                op = new { curva = verCurva, media = verMedia, mediana = verMediana, moda = verModa, ic = verIc, espec = verEspec, sigmas, ejeY,
                    color = colorBarras, leyenda = verLeyenda, cuadricula = verCuadricula, titulo = tituloGrafico },
            }, yo);
            var (a, b) = Intervalo();
            await JS.InvokeVoidAsync("sigEstadistica.campana", IdCampana, new
            {
                media = r.Media, desv = r.Desviacion, a, b, campo = NombreCampo, pNormal = Prob().Normal,
                observados = r.Ordenados.Length <= 2000 ? r.Ordenados : Array.Empty<double>(),
            });
            if (r.N >= 3 && r.Desviacion > 0)
                await JS.InvokeVoidAsync("sigEstadistica.qq", IdQq, new { puntos = Descriptiva.PuntosQQ(r).Select(p => new { t = p.Teorico, o = p.Observado }), campo = NombreCampo });
        }
        catch (JSDisconnectedException) { }
        catch (JSException) { }
    }

    private async Task Png(string id, string sufijo)
    {
        try { await JS.InvokeVoidAsync("sigEstadistica.png", id, $"{Titulo} - {NombreCampo} - {sufijo}"); } catch (JSException) { }
    }

    public async ValueTask DisposeAsync()
    {
        try { await JS.InvokeVoidAsync("sigEstadistica.soltar", (object)new[] { idHist, IdCampana, IdQq }); } catch { }
        try { await JS.InvokeVoidAsync("sigEstadistica.panelSoltar", raiz); } catch { }
        yo?.Dispose();
    }

    // ---------- Formato ----------
    private string F(double? v) => v is double d && double.IsFinite(d) ? Interpretacion.Num(d, r) : "—";
    private string F(double? v, ResultadoEstadistico rr) => v is double d && double.IsFinite(d) ? Interpretacion.Num(d, rr) : "—";
    private static string D(double? v, string formato = "0.000") => v is double d && double.IsFinite(d) ? d.ToString(formato, CultureInfo.CurrentCulture) : "—";
    private static string Pct(double v) => (v * 100).ToString("0.00", CultureInfo.CurrentCulture) + " %";

    private static double? Leer(object? v) =>
        double.TryParse(v?.ToString()?.Replace(',', '.'), NumberStyles.Float, CultureInfo.InvariantCulture, out var d) ? d : null;

    private string ModaTexto => r.Modas.Count == 0 ? "—" : r.Modas.Count == 1 ? F(r.Modas[0]) : $"{F(r.Modas[0])} (+{r.Modas.Count - 1})";

    private string Normalidad => r.PAndersonDarling is null && r.PJarqueBera is null ? "—" : r.EsNormal ? "Normal" : "No normal";

    // ---------- Diagrama de caja (SVG) ----------
    private sealed record Caja(string Etiqueta, ResultadoEstadistico R);

    private List<Caja> Cajas() => grupos.Count > 0 ? grupos.Select(g => new Caja(g.Grupo, g.R)).ToList() : new() { new Caja(NombreCampo, r) };

    private static (double Min, double Max) Escala(List<Caja> cajas)
    {
        var min = cajas.Min(c => c.R.Min); var max = cajas.Max(c => c.R.Max);
        if (max - min < 1e-12) { min -= 1; max += 1; }
        var pad = (max - min) * 0.05;
        return (min - pad, max + pad);
    }

    private static string X(double v, (double Min, double Max) e) =>
        (120 + (v - e.Min) / (e.Max - e.Min) * 460).ToString("0.0", CultureInfo.InvariantCulture);

    private static string W(double a, double b, (double Min, double Max) e) =>
        Math.Max(1, (b - a) / (e.Max - e.Min) * 460).ToString("0.0", CultureInfo.InvariantCulture);

    private string Zs()
    {
        if (r.Desviacion <= 0) return "—";
        var (a, b) = Intervalo();
        string Z(double? v) => v is double d ? ((d - r.Media) / r.Desviacion).ToString("0.00", CultureInfo.CurrentCulture) : "∞";
        return tipoProb == "entre" ? $"{Z(a)} a {Z(b)}" : tipoProb == "menor" ? Z(b) : Z(a);
    }

    private static double BigoteInf(ResultadoEstadistico rr) => rr.Ordenados.Where(v => v >= rr.LimiteInfTukey).DefaultIfEmpty(rr.Min).Min();
    private static double BigoteSup(ResultadoEstadistico rr) => rr.Ordenados.Where(v => v <= rr.LimiteSupTukey).DefaultIfEmpty(rr.Max).Max();
}
