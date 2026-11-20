using System.Globalization;
using System.Text;
using ClosedXML.Excel;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using SigWeb.Data;
using SigWeb.Data.Ensayos;
using SigWeb.Data.Navegacion;

namespace SigWeb.Components.Pages;

// Ensayos › Resumen: resultados de todas las torres con las columnas marcadas «Resumen» en cada ensayo.
// Vistas: por ensayo (pestañas + tabla) y por torre (tarjetas). Filtros y orden en memoria.
public partial class EnsayosResumen
{
    [Inject] private IServiceScopeFactory Scopes { get; set; } = default!;
    [Inject] private IJSRuntime JS { get; set; } = default!;
    [Inject] private NavigationManager Nav { get; set; } = default!;

    [SupplyParameterFromQuery(Name = "vista")] public string? VistaQ { get; set; }
    [SupplyParameterFromQuery(Name = "tipo")] public int? TipoQ { get; set; }
    [SupplyParameterFromQuery(Name = "buscar")] public string? BuscarQ { get; set; }
    [SupplyParameterFromQuery(Name = "grupo")] public string? GrupoQ { get; set; }
    [SupplyParameterFromQuery(Name = "torre")] public string? TorreQ { get; set; }

    private List<TipoResumenEnsayo>? tipos;
    private string? error;
    private string vista = "ensayo";
    private bool pantallaCompleta;
    private int? tipoSel;
    private string buscar = "", fGrupo = "", fTorre = "";
    private string? orden; private bool ordenDesc;
    private int limite = 200, limiteTorres = 60;

    protected override void OnInitialized()
    {
        if (VistaQ is "ensayo" or "torre") vista = VistaQ;
        tipoSel = TipoQ;
        buscar = BuscarQ ?? "";
        fGrupo = GrupoQ ?? "";
        fTorre = TorreQ ?? "";
    }

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (firstRender)
        {
            try
            {
                using var scope = Scopes.CreateScope();
                tipos = await ServicioResumenEnsayos.CargarAsync(scope.ServiceProvider.GetRequiredService<AppDbContext>());
            }
            catch (Exception ex) when (ex.Message.Contains("EnVistaResumen") || ex.InnerException?.Message.Contains("EnVistaResumen") == true)
            { error = "Falta aplicar la migración ResumenEnsayos (columna «Resumen» de los campos de ensayo)."; }
            StateHasChanged();
            return;
        }
        if (tipos is null) return;
        try { await JS.RecordarVistaAsync("/ensayos/resumen", new Dictionary<string, object?>
        {
            ["vista"] = vista, ["tipo"] = tipoSel, ["buscar"] = buscar,
            ["grupo"] = fGrupo, ["torre"] = fTorre
        }); } catch (JSDisconnectedException) { }
    }

    private Task AtrasCb() => JS.VolverAsync("/ensayos");
    private void AbrirEnsayo(int tipo, int resultado) => Nav.NavigateTo($"ensayos/{tipo}?resultado={resultado}");

    private IEnumerable<TipoResumenEnsayo> TiposVisibles =>
        (tipos ?? new()).Where(t => fGrupo == "" || t.Grupo == fGrupo);

    // Filas filtradas por la búsqueda (y la torre en la vista por torre), ordenadas por la columna elegida o por fecha.
    private IEnumerable<FilaResumenEnsayo> FilasDe(TipoResumenEnsayo t)
    {
        if (t.SinMarcas) return Enumerable.Empty<FilaResumenEnsayo>();
        IEnumerable<FilaResumenEnsayo> q = t.Filas;
        if (vista == "torre" && fTorre != "") q = q.Where(f => f.Lugar == fTorre);
        var b = Sin(buscar);
        if (b.Length > 0) q = q.Where(f => Sin(f.Lugar + " " + string.Join(" ", f.Valores.Values)).Contains(b));
        if (orden is not null && t.Columnas.Contains(orden))
        {
            object Clave(FilaResumenEnsayo f)
            {
                var v = f.Valores.GetValueOrDefault(orden) ?? "";
                if (double.TryParse(v, NumberStyles.Float, CultureInfo.InvariantCulture, out var n)) return (0, n, "");
                if (DateTime.TryParseExact(v, "dd/MM/yyyy", CultureInfo.InvariantCulture, DateTimeStyles.None, out var d)) return (0, (double)d.Ticks, "");
                return (1, 0d, v);
            }
            var cmp = Comparer<object>.Create((a, c) => ((ValueTuple<int, double, string>)a).CompareTo((ValueTuple<int, double, string>)c));
            return ordenDesc ? q.OrderByDescending(Clave, cmp) : q.OrderBy(Clave, cmp);
        }
        return q.OrderByDescending(f => f.Fecha).ThenByDescending(f => f.Id);
    }

    private void Ordenar(string col)
    {
        if (orden == col) ordenDesc = !ordenDesc; else { orden = col; ordenDesc = false; }
    }

    private IEnumerable<string> Lugares =>
        (tipos ?? new()).SelectMany(t => t.Filas).Select(f => f.Lugar).Where(l => l != "").Distinct().OrderBy(l => l, new OrdenNatural());

    private sealed record GrupoLugar(string Lugar, int? TorreId, List<(TipoResumenEnsayo Tipo, FilaResumenEnsayo[] Filas)> PorTipo)
    {
        public int Total => PorTipo.Sum(x => x.Filas.Length);
    }

    private IEnumerable<GrupoLugar> PorLugar()
    {
        var visibles = TiposVisibles.Where(t => !t.SinMarcas).ToList();
        var porTipo = visibles.Select(t => (Tipo: t, Filas: FilasDe(t).ToList())).ToList();
        return porTipo.SelectMany(x => x.Filas.Select(f => f.Lugar)).Distinct().OrderBy(l => l == "" ? "￿" : l, new OrdenNatural())
            .Select(l => new GrupoLugar(l,
                porTipo.SelectMany(x => x.Filas).FirstOrDefault(f => f.Lugar == l)?.TorreId,
                porTipo.Select(x => (x.Tipo, x.Filas.Where(f => f.Lugar == l).ToArray())).Where(x => x.Item2.Length > 0).ToList()));
    }

    private static string Sin(string? s)
    {
        var n = (s ?? "").Trim().ToLowerInvariant().Normalize(NormalizationForm.FormD);
        return new string(n.Where(c => CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark).ToArray());
    }

    // "57N" < "100N": compara los números como números.
    private sealed class OrdenNatural : IComparer<string>
    {
        public int Compare(string? a, string? b)
        {
            static (long, string) K(string? s)
            {
                s ??= ""; var i = 0; while (i < s.Length && char.IsDigit(s[i])) i++;
                return (i > 0 && long.TryParse(s[..i], out var n) ? n : long.MaxValue, s[i..]);
            }
            var (na, ra) = K(a); var (nb, rb) = K(b);
            var c = na.CompareTo(nb);
            return c != 0 ? c : string.Compare(ra, rb, StringComparison.OrdinalIgnoreCase);
        }
    }

    // ---------- Exportar: una hoja por ensayo con sus columnas «Resumen» (lo filtrado) ----------
    private async Task ExportarAsync()
    {
        if (tipos is null) return;
        using var wb = new XLWorkbook();
        var usados = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var t in TiposVisibles.Where(t => !t.SinMarcas))
        {
            var nombre = new string(t.Nombre.Where(ch => !"[]*?/\\:".Contains(ch)).ToArray()).Trim();
            if (nombre.Length > 28) nombre = nombre[..28];
            if (nombre == "") nombre = "Ensayo";
            var n = nombre; var k = 2; while (!usados.Add(n)) n = $"{nombre[..Math.Min(nombre.Length, 25)]} {k++}";
            var ws = wb.AddWorksheet(n);
            for (int c = 0; c < t.Columnas.Count; c++) ws.Cell(1, c + 1).Value = t.Columnas[c];
            int f = 2;
            foreach (var fila in FilasDe(t))
            {
                for (int c = 0; c < t.Columnas.Count; c++)
                {
                    var v = fila.Valores.GetValueOrDefault(t.Columnas[c]) ?? "";
                    if (double.TryParse(v, NumberStyles.Float, CultureInfo.InvariantCulture, out var num)) ws.Cell(f, c + 1).Value = num;
                    else ws.Cell(f, c + 1).Value = v;
                }
                f++;
            }
            var hdr = ws.Range(1, 1, 1, Math.Max(1, t.Columnas.Count));
            hdr.Style.Font.Bold = true; hdr.Style.Fill.BackgroundColor = XLColor.FromHtml("#EC1C24"); hdr.Style.Font.FontColor = XLColor.White;
            ws.Columns().AdjustToContents(8, 50);
            ws.SheetView.FreezeRows(1);
        }
        if (!wb.Worksheets.Any()) wb.AddWorksheet("Resumen").Cell(1, 1).Value = "Ningún ensayo tiene columnas marcadas «Resumen».";
        using var ms = new MemoryStream();
        wb.SaveAs(ms);
        await JS.InvokeVoidAsync("descargarArchivo", $"Resumen_ensayos_{DateTime.Now:yyyyMMdd_HHmm}.xlsx",
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", Convert.ToBase64String(ms.ToArray()));
    }
}
