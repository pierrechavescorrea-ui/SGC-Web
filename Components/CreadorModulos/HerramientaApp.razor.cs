
using System.Globalization;
using System.Text.Json;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using SigWeb.Data.CreadorModulos;
using SigWeb.Data.Dossier;
using SigWeb.Data.Estadistica;
using SigWeb.Data.Seguridad;

namespace SigWeb.Components.CreadorModulos;

public partial class HerramientaApp : IAsyncDisposable
{
    [Inject] private ServicioVinculos Vinculos { get; set; } = default!;
    [Inject] private NavigationManager Navegacion { get; set; } = default!;
    [Inject] private IJSRuntime Js { get; set; } = default!;

    [Parameter, EditorRequired] public CampoModulo Campo { get; set; } = default!;
    [Parameter] public IReadOnlyList<VinculoElegido>? Filtro { get; set; }
    [Parameter] public bool VistaPrevia { get; set; }
    [Parameter] public int AltoPx { get; set; }

    private static readonly CultureInfo Cultura = CultureInfo.GetCultureInfo("es-CO");
    private static readonly CultureInfo Invariante = CultureInfo.InvariantCulture;
    private static readonly string[] FormatosFecha = ["yyyy-MM-dd", "d/M/yyyy", "d/M/yyyy HH:mm", "yyyy-MM-dd HH:mm:ss"];
    private const int FilasIniciales = 200;

    private sealed record Adjunto(string Clase, string Carpeta, string Nombre, string Url, bool Actual);
    private sealed record PuntoMapa(double Lat, double Lon, string Etiqueta);
    private sealed record PuntoGrafico(string Etiqueta, double Valor);
    private sealed record Evento(DateTime Fecha, FilaVinculo Fila);

    private string? claveCarga;
    private CancellationTokenSource? carga;
    private bool cargando;
    private bool cerrado;
    private int limite = FilasIniciales;
    private int pdfSeleccionado;
    private IReadOnlyList<FilaVinculo> filas = [];
    private IReadOnlyList<ColumnaVinculo> columnas = [];
    private List<Adjunto> adjuntos = [];
    private ResultadoEstadistico? estadistica;
    private (double? Valor, string Detalle) indicador;
    private PuntoGrafico[] serie = [];
    private PuntoMapa[] puntos = [];
    private Evento[] eventos = [];
    private ElementReference mapa;
    private Task<IJSObjectReference>? moduloMapa;
    private bool mapaPendiente;
    private bool errorMapa;
    private object claveMapa = new();

    private string MensajeVacio => VistaPrevia ? "Vista previa" :
        !string.IsNullOrWhiteSpace(Campo.FiltroCampo) && Filtro is { Count: 0 }
            ? "Elija un registro arriba para ver sus datos."
            : "No hay datos disponibles.";
    private int Altura => AltoPx > 0 ? AltoPx : Math.Clamp(Campo.Alto, 1, 100) * 40;
    // UrlDe solo lee la instantánea en memoria y los permisos vigentes.
    private string? UrlFila(FilaVinculo fila) =>
        !VistaPrevia && Campo.Fuente is { } fuente ? Vinculos.UrlDe(fuente, fila.Id) : null;

    protected override async Task OnParametersSetAsync()
    {
        var campo = Campo;
        var filtro = VistaPrevia ? null : Filtro?.ToArray();
        var previa = VistaPrevia;
        var fuente = campo.Fuente;
        // El diseñador puede editar la misma instancia del campo y el formulario puede mutar la lista.
        var clave = JsonSerializer.Serialize(new
        {
            Proyecto = AmbienteSig.ProyectoId, previa, fuente, filtro, campo.Tipo, campo.FiltroCampo,
            campo.ColumnaEnlace, campo.ColumnasFuente, campo.Calculo, campo.ColumnaValor, campo.ValorObjetivo,
            campo.ColumnaAgrupar, campo.ColumnaFecha, campo.ColumnaLat, campo.ColumnaLon
        });
        if (clave == claveCarga) return;
        claveCarga = clave;
        carga?.Cancel();
        using var nueva = new CancellationTokenSource();
        carga = nueva;
        var ct = nueva.Token;
        cargando = true;
        limite = FilasIniciales;
        pdfSeleccionado = 0;
        adjuntos = [];
        mapaPendiente = false;
        errorMapa = false;
        claveMapa = new();

        try
        {
            TablaVinculo? tabla = previa ? Muestra(campo) :
                !string.IsNullOrWhiteSpace(fuente) && (filtro is not { Length: 0 } || string.IsNullOrWhiteSpace(campo.FiltroCampo))
                    ? await Vinculos.TablaAsync(fuente, ct)
                    : null;
            ct.ThrowIfCancellationRequested();
            filas = tabla is null ? [] : ServicioVinculos.FiltrarFilas(tabla, campo, filtro);
            columnas = tabla is null ? [] : SeleccionarColumnas(tabla, campo);
            if (!previa && fuente is not null && filas.Count > 0 && campo.Tipo is TipoCampoModulo.Pdf or TipoCampoModulo.Carpetas)
            {
                var archivos = await CargarAdjuntosAsync(fuente, campo, filas, filtro, ct);
                ct.ThrowIfCancellationRequested();
                adjuntos = archivos;
            }
            ct.ThrowIfCancellationRequested();
            PrepararContenido(campo);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested) { }
        finally
        {
            if (ReferenceEquals(carga, nueva))
            {
                carga = null;
                cargando = false;
            }
        }
    }

    private static IReadOnlyList<ColumnaVinculo> SeleccionarColumnas(TablaVinculo tabla, CampoModulo campo) =>
        campo.ColumnasFuente.Count == 0 ? tabla.Columnas.Take(5).ToArray() :
        campo.ColumnasFuente.Distinct().Select(k => tabla.Columnas.FirstOrDefault(c => c.Clave == k))
            .OfType<ColumnaVinculo>().ToArray();

    private async Task<List<Adjunto>> CargarAdjuntosAsync(string fuente, CampoModulo campo,
        IReadOnlyList<FilaVinculo> datos, IReadOnlyList<VinculoElegido>? filtro, CancellationToken ct)
    {
        var ids = datos.Select(r => r.Id);
        if (campo.Tipo == TipoCampoModulo.Pdf)
        {
            var primero = string.IsNullOrWhiteSpace(campo.FiltroCampo) ? null : filtro?.FirstOrDefault();
            ids = primero?.Fuente == fuente ? ids.Where(id => id == primero.Id) : ids.Take(1);
        }
        var baseUri = new Uri(Navegacion.BaseUri);
        var resultado = new List<Adjunto>();
        foreach (var id in ids)
        {
            foreach (var archivo in await Vinculos.ArchivosAsync(fuente, id, ct))
            {
                if (string.IsNullOrWhiteSpace(archivo.Ruta) || archivo.Ruta.Contains('\\') ||
                    !Uri.TryCreate(baseUri, archivo.Ruta, out var uri)) continue;
                var local = uri.Scheme == baseUri.Scheme && uri.IdnHost == baseUri.IdnHost && uri.Port == baseUri.Port;
                if (!local && (!archivo.Enlace || uri.Scheme != Uri.UriSchemeHttps)) continue;
                if (campo.Tipo == TipoCampoModulo.Pdf && (!local || !archivo.Actual ||
                    !uri.AbsolutePath.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase))) continue;
                resultado.Add(new(archivo.Clase, archivo.Carpeta ?? "", archivo.Nombre, uri.AbsoluteUri, archivo.Actual));
            }
        }
        return resultado.Distinct().ToList();
    }

    private void PrepararContenido(CampoModulo campo)
    {
        switch (campo.Tipo)
        {
            case TipoCampoModulo.Kpi:
                indicador = Calcular(filas, campo);
                break;
            case TipoCampoModulo.Estadistica:
                estadistica = Descriptiva.Analizar(Numeros(filas, campo.ColumnaValor));
                break;
            case TipoCampoModulo.Grafico:
                serie = Agrupar(filas, campo);
                break;
            case TipoCampoModulo.Mapa:
                puntos = filas.Select(f => (Fila: f, Lat: Numero(f, campo.ColumnaLat), Lon: Numero(f, campo.ColumnaLon)))
                    .Where(p => p.Lat is >= -90 and <= 90 && p.Lon is >= -180 and <= 180)
                    .Select(p => new PuntoMapa(p.Lat!.Value, p.Lon!.Value, p.Fila.Etiqueta)).ToArray();
                mapaPendiente = puntos.Length > 0;
                break;
            case TipoCampoModulo.LineaTiempo:
                eventos = filas.Select(f => (Fila: f, Fecha: Fecha(Texto(f, campo.ColumnaFecha))))
                    .Where(e => e.Fecha.HasValue).OrderBy(e => e.Fecha)
                    .Select(e => new Evento(e.Fecha!.Value, e.Fila)).ToArray();
                break;
        }
    }

    private static string Texto(FilaVinculo fila, string? clave) =>
        clave is null ? "" : fila.Valores.GetValueOrDefault(clave) ?? "";

    private static double? Numero(FilaVinculo fila, string? columna)
    {
        if (columna is null) return null;
        if (fila.NumerosFormateados?.TryGetValue(columna, out var exacto) == true)
            return double.IsFinite(exacto) ? exacto : null;
        var texto = Texto(fila, columna);
        return (double.TryParse(texto, NumberStyles.Float, Invariante, out var n)
            || double.TryParse(texto, NumberStyles.Float, Cultura, out n)) && double.IsFinite(n) ? n : null;
    }

    private static DateTime? Fecha(string texto) => DateTime.TryParseExact(texto.Trim(), FormatosFecha,
        Invariante, DateTimeStyles.None, out var fecha) ? fecha : null;

    private static double[] Numeros(IEnumerable<FilaVinculo> datos, string? columna) =>
        datos.Select(f => Numero(f, columna)).OfType<double>().ToArray();

    private static (double? Valor, string Detalle) Calcular(IReadOnlyList<FilaVinculo> datos, CampoModulo campo)
    {
        if (campo.Calculo == CalculoCampo.Conteo) return (datos.Count, $"{datos.Count} de {datos.Count}");
        if (campo.Calculo == CalculoCampo.Porcentaje)
        {
            if (datos.Count == 0 || string.IsNullOrWhiteSpace(campo.ColumnaValor) || campo.ValorObjetivo is null)
                return (null, "");
            var objetivo = DefinicionModulo.Normalizar(campo.ValorObjetivo);
            int n = datos.Count(f => DefinicionModulo.Normalizar(Texto(f, campo.ColumnaValor)) == objetivo);
            return (100d * n / datos.Count, $"{n} de {datos.Count}");
        }
        var numeros = Numeros(datos, campo.ColumnaValor);
        if (numeros.Length == 0) return (null, "");
        double valor = campo.Calculo switch
        {
            CalculoCampo.Suma => numeros.Sum(),
            CalculoCampo.Promedio => numeros.Average(),
            CalculoCampo.Minimo => numeros.Min(),
            CalculoCampo.Maximo => numeros.Max(),
            _ => numeros.Length
        };
        return (double.IsFinite(valor) ? valor : null, campo.Calculo == CalculoCampo.Promedio
            ? $"promedio de {numeros.Length}" : $"{numeros.Length} de {datos.Count}");
    }

    private static PuntoGrafico[] Agrupar(IReadOnlyList<FilaVinculo> datos, CampoModulo campo)
    {
        var porMes = string.IsNullOrWhiteSpace(campo.ColumnaAgrupar) && !string.IsNullOrWhiteSpace(campo.ColumnaFecha);
        string? Grupo(FilaVinculo f)
        {
            if (porMes) return Fecha(Texto(f, campo.ColumnaFecha))?.ToString("yyyy-MM", Invariante);
            if (string.IsNullOrWhiteSpace(campo.ColumnaAgrupar)) return "Total";
            var grupo = Texto(f, campo.ColumnaAgrupar).Trim();
            return grupo.Length == 0 ? "Sin categoría" : grupo;
        }
        return datos.Select(f => (Fila: f, Grupo: Grupo(f))).Where(x => x.Grupo is not null)
            .GroupBy(x => x.Grupo!, StringComparer.Create(Cultura, true)).OrderBy(g => g.Key, StringComparer.Create(Cultura, true))
            .Select(g => (Etiqueta: porMes ? DateTime.ParseExact(g.Key, "yyyy-MM", Invariante).ToString("MMM yyyy", Cultura) : g.Key,
                Calculo: Calcular(g.Select(x => x.Fila).ToArray(), campo)))
            .Where(g => g.Calculo.Valor.HasValue)
            .Select(g => new PuntoGrafico(g.Etiqueta, g.Calculo.Valor!.Value)).ToArray();
    }

    private string Formatear(double valor, int decimales) => valor.ToString($"N{Math.Clamp(decimales, 0, 10)}", Cultura);
    private string Cifra(double valor) => Formatear(valor, Campo.Decimales ?? (Campo.Calculo == CalculoCampo.Conteo ? 0 : 2));
    private string Medida(double valor) => Formatear(valor, Campo.Decimales ?? 2);
    private string Unidad => Campo.Calculo == CalculoCampo.Porcentaje ? "%" : Campo.Unidad ?? "";
    private static string Svg(double valor) => valor.ToString("0.############", Invariante);

    private static TablaVinculo Muestra(CampoModulo campo)
    {
        var columnas = campo.ColumnasFuente.Concat(new[] { campo.ColumnaValor, campo.ColumnaAgrupar, campo.ColumnaFecha,
            campo.ColumnaLat, campo.ColumnaLon }).Where(c => !string.IsNullOrWhiteSpace(c)).OfType<string>().Distinct().ToArray();
        if (columnas.Length == 0) columnas = ["Categoría", "Valor"];
        string Valor(string columna, int i)
        {
            if (columna == campo.ColumnaFecha) return new DateTime(2026, i, 12).ToString("yyyy-MM-dd", Invariante);
            if (columna == campo.ColumnaLat) return Svg(4.62 + i * .012);
            if (columna == campo.ColumnaLon) return Svg(-74.08 + i * .008);
            if (columna == campo.ColumnaValor && campo.Calculo == CalculoCampo.Porcentaje)
                return i == 2 ? "Otro" : campo.ValorObjetivo ?? "Conforme";
            if (columna == campo.ColumnaValor || columna == "Valor") return Svg(6 + i * 2.5);
            return $"Grupo {(char)('A' + (i - 1) / 2)}";
        }
        return new(new(campo.Fuente ?? "", "", "", ""), columnas.Select(c => new ColumnaVinculo(c, c)).ToArray(),
            Enumerable.Range(1, 6).Select(i => new FilaVinculo(i, $"Registro {i}",
                columnas.ToDictionary(c => c, c => Valor(c, i)))).ToArray());
    }

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (cerrado || cargando || !mapaPendiente || Campo.Tipo != TipoCampoModulo.Mapa) return;
        mapaPendiente = false;
        var elemento = mapa;
        var datos = puntos;
        var clave = claveMapa;
        try
        {
            var modulo = await (moduloMapa ??= Js.InvokeAsync<IJSObjectReference>("import", "./js/herramientasApp.js").AsTask());
            if (!cerrado && clave == claveMapa)
                await modulo.InvokeVoidAsync("mostrarMapa", elemento, datos);
        }
        catch (JSDisconnectedException) { }
        catch (JSException)
        {
            if (!cerrado && clave == claveMapa)
            {
                errorMapa = true;
                StateHasChanged();
            }
        }
    }

    public async ValueTask DisposeAsync()
    {
        cerrado = true;
        carga?.Cancel();
        if (moduloMapa is null) return;
        try { await (await moduloMapa).DisposeAsync(); }
        catch (JSDisconnectedException) { }
        catch (JSException) { }
    }
}
