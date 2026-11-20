using System.Globalization;
using ClosedXML.Excel;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using SigWeb.Data;
using SigWeb.Data.CostosNoCalidad;
using SigWeb.Components.Layout;
using SigWeb.Data.Navegacion;
using SigWeb.Models;

namespace SigWeb.Components.Pages;

// Costos de no calidad (Evaluación y mejora): lee el paso de Planes de acción ligado a este módulo
// (ModuloCostosNoCalidad) y suma los campos numéricos marcados «Resumen». Vistas: plan · tabla · gráficos.
public partial class CostosNoCalidad : IDisposable
{
    [Inject] private IServiceScopeFactory Scopes { get; set; } = default!;
    [Inject] private IJSRuntime JS { get; set; } = default!;
    [Inject] private NavigationManager Nav { get; set; } = default!;

    [SupplyParameterFromQuery(Name = "vista")] public string? VistaQ { get; set; }
    [SupplyParameterFromQuery(Name = "plan")] public int? PlanQ { get; set; }
    [SupplyParameterFromQuery(Name = "buscar")] public string? BuscarQ { get; set; }
    [SupplyParameterFromQuery(Name = "anio")] public int? AnioQ { get; set; }
    [SupplyParameterFromQuery(Name = "origen")] public string? OrigenQ { get; set; }
    [SupplyParameterFromQuery(Name = "tipo")] public string? TipoQ { get; set; }
    [SupplyParameterFromQuery(Name = "proceso")] public string? ProcesoQ { get; set; }
    [SupplyParameterFromQuery(Name = "estado")] public string? EstadoQ { get; set; }
    [SupplyParameterFromQuery(Name = "todos")] public bool? TodosQ { get; set; }
    [SupplyParameterFromQuery(Name = "agrupar")] public string? AgruparQ { get; set; }
    [SupplyParameterFromQuery(Name = "disenarVista")] public string? DisenarQ { get; set; }

    private VistaWidgets? vw;
    private bool GraficosEnPanel => vw?.Presentes.Any(w => w.Tipo == "grafico") == true;
    private Task AlCambiarVista() { firmaGraficos = null; StateHasChanged(); return Task.CompletedTask; }

    private static readonly CultureInfo Co = CultureInfo.GetCultureInfo("es-CO");
    private InstantaneaCostosNoCalidad? datos;
    private string vista = "plan";
    private bool pantallaCompleta, verFiltros = true, ordenCosto = true, soloConCosto = true;
    private int? selId;
    private string buscar = "", fOrigen = "", fTipo = "", fProceso = "", fEstado = "";
    private int fAnio;
    private List<int> anios = new();
    private List<string> origenes = new(), tipos = new(), procesos = new();
    private bool cargando;

    private string Subtitulo => datos is null ? "" : datos.Ligado ? $"Paso «{datos.PasoTitulo}» de Planes de acción" : "Sin paso ligado";

    protected override void OnInitialized()
    {
        if (VistaQ is "plan" or "tabla" or "graficos") vista = VistaQ;
        buscar = BuscarQ ?? ""; fAnio = AnioQ ?? 0; fOrigen = OrigenQ ?? ""; fTipo = TipoQ ?? ""; fProceso = ProcesoQ ?? ""; fEstado = EstadoQ ?? "";
        soloConCosto = TodosQ != true;
        if (!string.IsNullOrEmpty(AgruparQ)) agrupar = AgruparQ;
        selId = PlanQ;
    }

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (firstRender) { await CargarAsync(); return; }
        if (datos is null) return;
        try { await JS.RecordarVistaAsync("/costos-no-calidad", new Dictionary<string, object?>
        {
            ["vista"] = vista, ["plan"] = selId, ["buscar"] = buscar == "" ? null : buscar, ["anio"] = fAnio == 0 ? null : fAnio,
            ["origen"] = fOrigen == "" ? null : fOrigen, ["tipo"] = fTipo == "" ? null : fTipo, ["proceso"] = fProceso == "" ? null : fProceso,
            ["estado"] = fEstado == "" ? null : fEstado, ["todos"] = soloConCosto ? null : true, ["agrupar"] = agrupar == AgOrigen ? null : agrupar
        }); } catch (JSDisconnectedException) { }
        if ((vista == "graficos" && vw?.Personalizada != true) || GraficosEnPanel) await DibujarGraficosAsync();
    }

    private async Task CargarAsync()
    {
        if (cargando) return;
        cargando = true;
        try
        {
            using var scope = Scopes.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            datos = await ServicioCostosNoCalidad.CargarAsync(db);
            anios = datos.Planes.Select(p => p.Fecha.Year).Distinct().OrderByDescending(a => a).ToList();
            origenes = datos.Planes.Select(p => p.Origen).Where(x => !string.IsNullOrWhiteSpace(x)).Distinct().OrderBy(x => x).ToList();
            tipos = datos.Planes.Select(p => p.Tipo).Where(x => !string.IsNullOrWhiteSpace(x)).Distinct().OrderBy(x => x).ToList();
            procesos = datos.Planes.SelectMany(p => SelectorMultiple.Leer(p.Proceso)).Distinct().OrderBy(x => x).ToList();
            AsignarColores();
            firmaGraficos = null;
        }
        finally { cargando = false; }
        StateHasChanged();
    }

    // ---------- Filtros (en memoria) ----------
    private IEnumerable<PlanCosto> Filtrados
    {
        get
        {
            var q = FiltradosSinAnio();
            if (fAnio != 0) q = q.Where(p => p.Fecha.Year == fAnio);
            return ordenCosto ? q.OrderByDescending(p => p.Total).ThenByDescending(p => p.Fecha) : q.OrderByDescending(p => p.Fecha).ThenByDescending(p => p.Id);
        }
    }

    // Todos los filtros salvo el año (la tendencia y la comparación con el periodo anterior lo necesitan).
    private IEnumerable<PlanCosto> FiltradosSinAnio()
    {
        {
            if (datos is null) return Enumerable.Empty<PlanCosto>();
            IEnumerable<PlanCosto> q = datos.Planes;
            if (soloConCosto) q = q.Where(p => p.Total != 0);
            if (fOrigen != "") q = q.Where(p => p.Origen == fOrigen);
            if (fTipo != "") q = q.Where(p => p.Tipo == fTipo);
            if (fProceso != "") q = q.Where(p => SelectorMultiple.Leer(p.Proceso).Contains(fProceso));
            if (fEstado != "" && Enum.TryParse<EstadoAccionPlan>(fEstado, out var est)) q = q.Where(p => p.Estado == est);
            var b = Sin(buscar);
            if (b.Length > 0)
                q = q.Where(p => Sin($"{p.Codigo} {p.Descripcion} {p.Origen} {p.Tipo} {p.Proceso} {string.Join(" ", p.Dimensiones.Values)}").Contains(b));
            return q;
        }
    }

    private bool HayFiltros => fAnio != 0 || fOrigen != "" || fTipo != "" || fProceso != "" || fEstado != "" || buscar != "" || !soloConCosto;
    private void QuitarFiltros() { fAnio = 0; fOrigen = fTipo = fProceso = fEstado = buscar = ""; soloConCosto = true; }

    private static string Sin(string? s)
    {
        var n = (s ?? "").Trim().ToLowerInvariant().Normalize(System.Text.NormalizationForm.FormD);
        return new string(n.Where(c => CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark).ToArray());
    }

    // ---------- Ayudantes de las vistas ----------
    private (string Rubro, double V)[] RubrosOrdenados(PlanCosto[] lista) =>
        datos is null ? Array.Empty<(string, double)>()
        : datos.Rubros.Select(r => (Rubro: r, V: datos.TotalRubro(lista, r))).OrderByDescending(x => x.V).ThenBy(x => x.Rubro).ToArray();

    private (string Rubro, double V)[] RubrosDePlan(PlanCosto p) =>
        datos is null ? Array.Empty<(string, double)>()
        : datos.Rubros.Select(r => (Rubro: r, V: datos.ValorRubro(p, r))).Where(x => x.V != 0).OrderByDescending(x => x.V).ToArray();

    private static double Maximo(PlanCosto[] lista) => lista.Length == 0 ? 0 : lista.Max(p => Math.Abs(p.Total));
    private static string Pct(double v) => v.ToString("0.#", CultureInfo.InvariantCulture);

    private static string TextoDimension(CampoDimension d, string v) => d.Tipo switch
    {
        TipoCampoTabla.SiNo => v is "1" or "true" or "True" or "Sí" ? "Sí" : "No",
        TipoCampoTabla.Fecha => DateTime.TryParseExact(v, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var f) ? f.ToString("dd/MM/yyyy") : v,
        _ => v
    };

    private void CambiarVista(string v) { vista = v; firmaGraficos = null; }
    private void VerPlan(int id) => Nav.NavigateTo($"planes-accion?plan={id}");
    private void AbrirPlan(int id) => Nav.NavigateTo($"planes-accion/{id}");
    private Task AtrasCb() => JS.VolverAsync("/");

    // ---------- Gráficos (Chart.js: sigwebRenderGrafico de js/graficoEnsayos.js) ----------
    private const string AgOrigen = "origen", AgTipo = "tipo", AgProceso = "proceso", AgEstado = "estado";
    private string agrupar = AgOrigen;
    private string? firmaGraficos;
    private bool porMes = true;
    private string TituloMes => porMes ? "Costo por mes" : "Costo por año";

    private string NombreAgrupacion => agrupar switch
    {
        AgTipo => "tipo de hallazgo",
        AgProceso => "proceso",
        AgEstado => "estado del plan",
        _ when agrupar.StartsWith("d:") && int.TryParse(agrupar[2..], out var id) => datos?.Dimensiones.FirstOrDefault(d => d.Id == id)?.Nombre.ToLowerInvariant() ?? "dato",
        _ => "origen"
    };

    // Reparte el costo del plan entre sus grupos sin repetirlo: si cada grupo es un rubro del plan (p. ej. Recursos =
    // Consultoría, Ensayos…), cada uno lleva el valor de su rubro y lo demás va a "Sin dato"; si no, partes iguales.
    private IEnumerable<(string G, double V)> Repartir(PlanCosto p)
    {
        var gs = GruposDe(p).Distinct().ToList();
        if (gs.Count <= 1) return gs.Select(g => (g, p.Total));
        if (datos is not null && gs.All(g => datos.Rubros.Contains(g)))
        {
            var porRubro = gs.Select(g => (G: g, V: datos.ValorRubro(p, g))).ToList();
            var sobra = p.Total - porRubro.Sum(x => x.V);
            return sobra == 0 ? porRubro : porRubro.Append(("Sin dato", sobra));
        }
        return gs.Select(g => (g, p.Total / gs.Count));
    }

    // Color fijo por grupo para la agrupación elegida: puesto por costo en TODOS los planes (sin filtros); un filtro no repinta.
    private Dictionary<string, string> ColoresDeGrupo()
    {
        if (datos is null) return new();
        var orden = datos.Planes.Where(p => p.Total != 0).SelectMany(Repartir).GroupBy(x => x.G)
            .Select(g => (G: g.Key, V: g.Sum(x => x.V))).Where(x => x.V != 0).OrderByDescending(x => x.V).ThenBy(x => x.G).Select(x => x.G).ToList();
        var r = new Dictionary<string, string>();
        for (int i = 0; i < orden.Count && i < Categorica.Length; i++) r[orden[i]] = Categorica[i];
        return r;
    }

    private void CambiarAgrupacion(ChangeEventArgs e) { agrupar = e.Value?.ToString() ?? AgOrigen; firmaGraficos = null; }

    // Un plan puede caer en varios grupos (procesos o listas de varios valores): su costo cuenta en cada uno.
    private IEnumerable<string> GruposDe(PlanCosto p)
    {
        IEnumerable<string> g = agrupar switch
        {
            AgTipo => new[] { p.Tipo },
            AgProceso => SelectorMultiple.Leer(p.Proceso),
            AgEstado => new[] { EscalaAccion.Nombre(p.Estado) },
            _ when agrupar.StartsWith("d:") && int.TryParse(agrupar[2..], out var id) && datos?.Dimensiones.FirstOrDefault(d => d.Id == id) is { } dm =>
                p.Dimensiones.TryGetValue(id, out var v) ? (dm.Tipo == TipoCampoTabla.Lista ? v.Split(';').Select(x => x.Trim()) : new[] { TextoDimension(dm, v) }) : Array.Empty<string>(),
            _ => new[] { p.Origen }
        };
        var l = g.Where(x => !string.IsNullOrWhiteSpace(x)).ToList();
        return l.Count == 0 ? new[] { "Sin dato" } : l;
    }


    private async Task DibujarGraficosAsync()
    {
        if (datos is null) return;
        var l = Filtrados.Where(p => p.Total != 0).ToArray();
        var firma = $"{l.Length}|{l.Sum(p => p.Total)}|{agrupar}|{fAnio}|{fOrigen}|{fTipo}|{fProceso}|{fEstado}|{buscar}|{soloConCosto}";
        if (firma == firmaGraficos) return;
        firmaGraficos = firma;
        try
        {
            // Costo por rubro (Pareto sin segundo eje): barras horizontales de mayor a menor, color del rubro;
            // el % acumulado va en la etiqueta del eje, no en otro eje.
            var comp = Composicion(l);
            double acum = 0;
            var etiquetas = comp.Select(c => { acum += c.Pct; return $"{c.Rubro}  ·  {c.Pct.ToString("P0", Co)} (acum. {acum.ToString("P0", Co)})"; }).ToArray();
            await JS.InvokeVoidAsync("sigwebRenderGrafico", "cnc-g-pareto", new
            {
                type = "bar",
                data = new { labels = etiquetas, datasets = new[] { new { label = "Costo", data = comp.Select(c => Math.Round(c.Valor, 2)).ToArray(), backgroundColor = comp.Select(c => c.Color).ToArray(), borderRadius = 4, maxBarThickness = 22 } } },
                options = new
                {
                    responsive = true, maintainAspectRatio = false, indexAxis = "y",
                    plugins = new { legend = new { display = false } },
                    scales = new { x = new { beginAtZero = true, grid = new { display = true } }, y = new { grid = new { display = false } } }
                }
            });

            // Por mes (o año si el periodo es largo), apilado por rubro con su color fijo; "Otros" al final.
            var fechas = l.Select(p => p.Fecha).ToList();
            var claves = new List<DateTime>();
            if (fechas.Count > 0)
            {
                var meses = (fechas.Max().Year - fechas.Min().Year) * 12 + fechas.Max().Month - fechas.Min().Month + 1;
                porMes = meses <= 36;
                var ini = porMes ? new DateTime(fechas.Min().Year, fechas.Min().Month, 1) : new DateTime(fechas.Min().Year, 1, 1);
                for (var p = ini; p <= fechas.Max(); p = porMes ? p.AddMonths(1) : p.AddYears(1)) claves.Add(p);
            }
            DateTime Grupo(DateTime f) => porMes ? new DateTime(f.Year, f.Month, 1) : new DateTime(f.Year, 1, 1);
            double ValorGrupo(PlanCosto p, string g) => datos.Rubros.Where(r => Agrupado(r) == g).Sum(r => datos.ValorRubro(p, r));
            await JS.InvokeVoidAsync("sigwebRenderGrafico", "cnc-g-mes", new
            {
                type = "bar",
                data = new
                {
                    labels = claves.Select(g => porMes ? g.ToString("MMM yy", Co) : g.Year.ToString()).ToArray(),
                    datasets = comp.Select(c => (object)new
                    {
                        label = c.Rubro, backgroundColor = c.Color, borderColor = "rgba(255,255,255,.9)", borderWidth = 1, borderRadius = 3, maxBarThickness = 40,
                        data = claves.Select(g => Math.Round(l.Where(p => Grupo(p.Fecha) == g).Sum(p => ValorGrupo(p, c.Rubro)), 2)).ToArray()
                    }).ToArray()
                },
                options = new
                {
                    responsive = true, maintainAspectRatio = false,
                    plugins = new { legend = new { position = "bottom", labels = new { boxWidth = 12 } } },
                    scales = new { x = new { stacked = true, grid = new { display = false } }, y = new { stacked = true, beginAtZero = true } }
                }
            });

            // Por la agrupación elegida (dona): 7 mayores + "Otros".
            var colorGrupo = ColoresDeGrupo();
            var grupos = l.SelectMany(Repartir).GroupBy(x => x.G)
                .Select(g => (G: g.Key, V: g.Sum(x => x.V))).Where(x => x.V != 0).OrderByDescending(x => x.V).ToList();
            var conColor = grupos.Where(g => colorGrupo.ContainsKey(g.G)).ToList();
            var resto = grupos.Where(g => !colorGrupo.ContainsKey(g.G)).Sum(g => g.V);
            grupos = resto == 0 ? conColor : conColor.Append((G: Otros, V: resto)).ToList();
            await JS.InvokeVoidAsync("sigwebRenderGrafico", "cnc-g-grupo", new
            {
                type = "doughnut",
                data = new { labels = grupos.Select(g => g.G).ToArray(), datasets = new[] { new { data = grupos.Select(g => Math.Round(g.V, 2)).ToArray(), backgroundColor = grupos.Select(g => colorGrupo.TryGetValue(g.G, out var cg) ? cg : ColorOtros).ToArray(), borderWidth = 2 } } },
                options = new { responsive = true, maintainAspectRatio = false, cutout = "62%", plugins = new { legend = new { position = "bottom", labels = new { boxWidth = 12 } } } }
            });

            // Los 10 planes con mayor costo (barras horizontales, un solo tono).
            var top = l.OrderByDescending(p => p.Total).Take(10).ToArray();
            await JS.InvokeVoidAsync("sigwebRenderGrafico", "cnc-g-planes", new
            {
                type = "bar",
                data = new { labels = top.Select(p => p.Codigo).ToArray(), datasets = new[] { new { label = "Costo", data = top.Select(p => Math.Round(p.Total, 2)).ToArray(), backgroundColor = Categorica[0], borderRadius = 4, maxBarThickness = 20 } } },
                options = new
                {
                    responsive = true, maintainAspectRatio = false, indexAxis = "y",
                    plugins = new { legend = new { display = false } },
                    scales = new { x = new { beginAtZero = true }, y = new { grid = new { display = false } } }
                }
            });
        }
        catch (JSDisconnectedException) { }
        catch (JSException) { }
    }

    // ---------- Exportar (lo filtrado) ----------
    private async Task ExportarAsync()
    {
        if (datos is null) return;
        var l = Filtrados.ToArray();
        var rubros = RubrosOrdenados(l).Select(r => r.Rubro).ToArray();
        using var wb = new XLWorkbook();
        var ws = wb.AddWorksheet("Costos de no calidad");
        var enc = new List<string> { "Código", "Fecha", "Hallazgo", "Origen", "Tipo", "Proceso", "Estado" };
        enc.AddRange(datos.Dimensiones.Select(d => d.Nombre));
        enc.AddRange(rubros);
        enc.Add("Total");
        for (int c = 0; c < enc.Count; c++) ws.Cell(1, c + 1).Value = enc[c];
        int f = 2;
        foreach (var p in l)
        {
            int c = 1;
            ws.Cell(f, c++).Value = p.Codigo;
            ws.Cell(f, c++).Value = p.Fecha; ws.Cell(f, c - 1).Style.DateFormat.Format = "dd/MM/yyyy";
            ws.Cell(f, c++).Value = p.Descripcion;
            ws.Cell(f, c++).Value = p.Origen;
            ws.Cell(f, c++).Value = p.Tipo;
            ws.Cell(f, c++).Value = p.Proceso;
            ws.Cell(f, c++).Value = EscalaAccion.Nombre(p.Estado);
            foreach (var d in datos.Dimensiones) ws.Cell(f, c++).Value = p.Dimensiones.TryGetValue(d.Id, out var v) ? TextoDimension(d, v) : "";
            foreach (var r in rubros) { var v = datos.ValorRubro(p, r); if (v != 0) ws.Cell(f, c).Value = v; ws.Cell(f, c++).Style.NumberFormat.Format = "#,##0"; }
            ws.Cell(f, c).Value = p.Total; ws.Cell(f, c).Style.NumberFormat.Format = "#,##0";
            f++;
        }
        var hdr = ws.Range(1, 1, 1, enc.Count);
        hdr.Style.Font.Bold = true; hdr.Style.Fill.BackgroundColor = XLColor.FromHtml("#EC1C24"); hdr.Style.Font.FontColor = XLColor.White;
        ws.Columns().AdjustToContents(8, 60);
        ws.SheetView.FreezeRows(1);
        using var ms = new MemoryStream();
        wb.SaveAs(ms);
        await JS.InvokeVoidAsync("descargarArchivo", $"Costos_no_calidad_{DateTime.Now:yyyyMMdd_HHmm}.xlsx",
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", Convert.ToBase64String(ms.ToArray()));
    }

    public void Dispose() { }
}
