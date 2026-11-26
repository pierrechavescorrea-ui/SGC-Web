using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.EntityFrameworkCore;
using Microsoft.JSInterop;
using SigWeb.Data;
using SigWeb.Data.Direccionamiento;
using SigWeb.Data.Documentos;
using SigWeb.Data.Impacto;
using SigWeb.Data.Navegacion;
using SigWeb.Models;

namespace SigWeb.Components.Pages;

// Direccionamiento estratégico: documentos (misión, visión, políticas…) clasificados por tipo, con las
// opciones documentales de Certificaciones (sus archivos y versiones son los de un documento del Listado
// maestro). La carga acepta uno o varios PDF; un PDF con varios documentos unidos se divide por rangos de
// páginas (sugeridos leyendo el rótulo de cada página) y cada parte se clasifica antes de guardar.
public partial class DireccionamientoEstrategico
{
    [Inject] private AppDbContext DbPropio { get; set; } = default!;
    [Inject] private SigWeb.Data.Seguridad.FabricaDbCompartida Compartida { get; set; } = default!;
    // Multiproyecto: en un proyecto que toma este módulo de la Empresa, se lee la base de la Empresa (solo lectura).
    private AppDbContext? _db;
    private AppDbContext Db => _db ??= Compartida.ParaModulo("direccionamiento") ?? DbPropio;
    private bool EsCompartido => !ReferenceEquals(Db, DbPropio);
    [Inject] private IWebHostEnvironment Env { get; set; } = default!;
    [Inject] private SigWeb.Data.Seguridad.IndiceArchivosProyecto IndiceArchivos { get; set; } = default!;
    [Inject] private NavigationManager Nav { get; set; } = default!;
    [Inject] private IJSRuntime JS { get; set; } = default!;
    [Inject] private AnalizadorImpacto Impacto { get; set; } = default!;

    [SupplyParameterFromQuery(Name = "vista")] public string? VistaQ { get; set; }

    private const string TipoDocumental = "Direccionamiento estratégico";
    private const long MaxArchivo = 100L * 1024 * 1024;
    private const int MaxArchivosCarga = 20;
    private const string CarpetaTmp = "direccionamiento-tmp";

    private List<DocumentoDireccionamiento> docs = new();
    private Dictionary<int, DocumentoMaestro> docPorId = new();
    private Dictionary<int, VersionDocumento> vigentePorDoc = new();     // documento → archivo vigente actual
    private Dictionary<int, RevisionDocumento> revisionPorDoc = new();   // documento → revisión más reciente
    private string vista = "mosaicos", filtro = "", mensaje = "";
    private bool error, guardando;
    private ElementReference raiz, sala;

    protected override void OnInitialized()
    {
        if (VistaQ is "lista" or "mosaicos") vista = VistaQ;
        LimpiarTemporales();
        Cargar();
    }

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        try { await JS.InvokeVoidAsync("sigVitrina.miniaturas", raiz); } catch { }
        // Mosaicos: el tamaño de las hojas se ajusta a la cantidad para que todo quepa sin scroll vertical.
        if (vista == "mosaicos") { try { await JS.InvokeVoidAsync("sigVitrina.ajustarMosaico", sala); } catch { } }
    }

    private void Cargar()
    {
        Db.ChangeTracker.Clear();
        docs = Db.DocumentosDireccionamiento.AsNoTracking().ToList()
            .OrderBy(d => PdfDireccionamiento.OrdenTipo(d.Tipo)).ThenBy(d => d.Tipo).ThenBy(d => d.Orden).ThenBy(d => d.Id).ToList();
        var idsDoc = docs.Where(d => d.DocumentoMaestroId != null).Select(d => d.DocumentoMaestroId!.Value).Distinct().ToList();
        docPorId = Db.DocumentosMaestro.AsNoTracking().Where(d => idsDoc.Contains(d.Id)).ToDictionary(d => d.Id);

        // Vigente actual = el de la revisión más reciente (mayor fecha; a igual fecha, la última creada), igual que
        // Documentos y Certificaciones; sin revisiones, el vigente cargado más reciente.
        var revs = Db.RevisionesDocumento.AsNoTracking().Where(r => idsDoc.Contains(r.DocumentoMaestroId)).ToList()
            .GroupBy(r => r.DocumentoMaestroId)
            .ToDictionary(g => g.Key, g => g.OrderByDescending(r => r.Fecha ?? DateTime.MinValue).ThenByDescending(r => r.Creada).ThenByDescending(r => r.Id).ToList());
        revisionPorDoc = revs.ToDictionary(kv => kv.Key, kv => kv.Value[0]);
        var rango = revs.ToDictionary(kv => kv.Key, kv => kv.Value.Select((r, i) => (r.Id, i)).ToDictionary(x => x.Id, x => x.i));
        vigentePorDoc = Db.VersionesDocumento.AsNoTracking()
            .Where(v => idsDoc.Contains(v.DocumentoMaestroId) && v.Clase == ClaseArchivoDocumento.Vigente).ToList()
            .GroupBy(v => v.DocumentoMaestroId)
            .ToDictionary(g => g.Key, g => g
                .OrderBy(v => v.RevisionDocumentoId is int r && rango.TryGetValue(g.Key, out var rg) && rg.TryGetValue(r, out var i) ? i : int.MaxValue)
                .ThenByDescending(v => v.FechaCarga).ThenByDescending(v => v.Id).First());
        if (filtro.Length > 0 && docs.All(d => d.Tipo != filtro)) filtro = "";
    }

    private async Task CambiarVista(string v)
    {
        vista = v;
        await JS.RecordarVistaAsync("/direccionamiento", new Dictionary<string, object?> { ["vista"] = v == "mosaicos" ? null : v });
    }

    private async Task PantallaCompleta()
    {
        try { await JS.InvokeVoidAsync("sigVitrina.pantallaCompleta", sala); } catch { }
    }

    // ---------- Consultas para el render ----------
    private List<string> TiposPresentes() =>
        docs.Select(d => d.Tipo).Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(PdfDireccionamiento.OrdenTipo).ThenBy(t => t).ToList();

    private List<DocumentoDireccionamiento> Visibles() =>
        filtro.Length == 0 ? docs : docs.Where(d => string.Equals(d.Tipo, filtro, StringComparison.OrdinalIgnoreCase)).ToList();

    private IEnumerable<string> TiposSugeridos() =>
        PdfDireccionamiento.Tipos.Concat(docs.Select(d => d.Tipo)).Where(t => !string.IsNullOrWhiteSpace(t)).Distinct(StringComparer.OrdinalIgnoreCase);

    private VersionDocumento? VigenteDe(DocumentoDireccionamiento d) =>
        d.DocumentoMaestroId is int id && docPorId.ContainsKey(id) && vigentePorDoc.TryGetValue(id, out var v) ? v : null;

    private RevisionDocumento? RevisionDe(DocumentoDireccionamiento d) =>
        d.DocumentoMaestroId is int id && revisionPorDoc.TryGetValue(id, out var r) ? r : null;

    private bool TieneDoc(DocumentoDireccionamiento d) => d.DocumentoMaestroId is int id && docPorId.ContainsKey(id);

    private string? CodigoDoc(DocumentoDireccionamiento d) =>
        d.DocumentoMaestroId is int id && docPorId.TryGetValue(id, out var doc) ? (string.IsNullOrWhiteSpace(doc.Codigo) ? $"#{doc.Id}" : doc.Codigo) : null;

    // "F03-RESPON-01 · v09 · Revisión 01"
    private string LineaRotulo(DocumentoDireccionamiento d)
    {
        var r = RevisionDe(d);
        var ver = r?.Version ?? VigenteDe(d)?.Version;
        return string.Join(" · ", new[] { CodigoDoc(d), string.IsNullOrWhiteSpace(ver) ? null : "v" + ver, r?.Cambio }
            .Where(t => !string.IsNullOrWhiteSpace(t)));
    }

    private DateTime? FechaDe(DocumentoDireccionamiento d) => RevisionDe(d)?.Fecha ?? VigenteDe(d)?.Fecha;

    private static bool EsPdf(string nombre) => Path.GetExtension(nombre).Equals(".pdf", StringComparison.OrdinalIgnoreCase);
    private static bool EsImagen(string nombre) => Path.GetExtension(nombre).ToLowerInvariant() is ".png" or ".jpg" or ".jpeg" or ".webp" or ".gif";

    private void AbrirDocumento(DocumentoDireccionamiento d, string abrir)
    {
        if (EsCompartido) return;   // el documento vive en la base de la Empresa, no en Documentos de este proyecto
        if (d.DocumentoMaestroId is int id) Nav.NavigateTo($"/documentos?doc={id}&abrir={abrir}");
    }

    // ---------- Compilado: une los vigentes en PDF, en el orden de la pantalla ----------
    private async Task DescargarCompilado()
    {
        mensaje = "";
        var pdfs = new List<byte[]>();
        foreach (var d in Visibles())
        {
            var v = VigenteDe(d);
            if (v is null || !EsPdf(v.NombreOriginal)) continue;
            var ruta = RutaFisica(v.RutaArchivo);
            if (ruta is not null && File.Exists(ruta)) pdfs.Add(await File.ReadAllBytesAsync(ruta));
        }
        if (pdfs.Count == 0) { mensaje = "No hay documentos vigentes en PDF para unir."; error = true; return; }
        try
        {
            var unido = PdfDireccionamiento.Unir(pdfs);
            var nombre = $"compilado-{Guid.NewGuid():N}.pdf";
            await File.WriteAllBytesAsync(Path.Combine(CarpetaTemporal(), nombre), unido);
            IndiceArchivos.PermitirTemporal($"/uploads/{CarpetaTmp}/{nombre}");
            await JS.InvokeVoidAsync("sigDescargarUrl", $"/uploads/{CarpetaTmp}/{nombre}");
        }
        catch (Exception ex) { mensaje = "No se pudo unir los documentos: " + ex.Message; error = true; }
    }

    // ---------- Carga de PDF (unidos o separados) ----------
    private sealed class ParteCarga
    {
        public bool Incluir { get; set; }
        public int Desde { get; set; }
        public int Hasta { get; set; }
        public string? Tipo { get; set; }
        public string? Titulo { get; set; }
        public string? Codigo { get; set; }
        public string? Version { get; set; }
        public string? Revision { get; set; }
        public DateTime? Fecha { get; set; }
        public int Destino { get; set; }          // 0 = documento nuevo; si no, Id del DocumentoDireccionamiento a actualizar
        public bool YaCargada { get; set; }       // el destino ya tiene esa versión y fecha
    }

    private sealed class ArchivoCarga
    {
        public string Nombre = "";
        public string Url = "";          // /uploads/direccionamiento-tmp/… (miniaturas de páginas en el navegador)
        public string Ruta = "";         // ruta física del temporal
        public int Paginas;
        public bool SinTexto;
        public List<ParteCarga> Partes = new();
    }

    private sealed class Carga
    {
        public List<ArchivoCarga> Archivos = new();
        public bool Leyendo;
    }

    private Carga? carga;

    private void AbrirCarga()
    {
        if (carga is not null) { CancelarCarga(); return; }
        form = null; elim = null; mensaje = "";
        carga = new Carga();
    }

    private void CancelarCarga()
    {
        if (carga is not null) foreach (var a in carga.Archivos) BorrarTemporal(a.Ruta);
        carga = null;
    }

    private async Task ElegirArchivos(InputFileChangeEventArgs e)
    {
        if (carga is null) return;
        carga.Leyendo = true; mensaje = "";
        var errores = new List<string>();
        try
        {
            foreach (var f in e.GetMultipleFiles(MaxArchivosCarga))
            {
                if (!EsPdf(f.Name)) { errores.Add($"{f.Name}: no es PDF"); continue; }
                try
                {
                    byte[] bytes;
                    await using (var s = f.OpenReadStream(MaxArchivo))
                    using (var ms = new MemoryStream())
                    { await s.CopyToAsync(ms); bytes = ms.ToArray(); }
                    var nombre = $"{Guid.NewGuid():N}.pdf";
                    var ruta = Path.Combine(CarpetaTemporal(), nombre);
                    await File.WriteAllBytesAsync(ruta, bytes);
                    var an = PdfDireccionamiento.Analizar(bytes);
                    IndiceArchivos.PermitirTemporal($"/uploads/{CarpetaTmp}/{nombre}");
                    var arch = new ArchivoCarga { Nombre = f.Name, Url = $"/uploads/{CarpetaTmp}/{nombre}", Ruta = ruta, Paginas = an.Paginas, SinTexto = an.SinTexto };
                    foreach (var p in an.Partes) arch.Partes.Add(Sugerir(p, f.Name, an.Partes.Count == 1));
                    carga.Archivos.Add(arch);
                }
                catch (Exception ex) { errores.Add($"{f.Name}: {ex.Message}"); }
            }
        }
        finally { carga.Leyendo = false; }
        if (errores.Count > 0) { mensaje = "No se pudo leer: " + string.Join("; ", errores); error = true; }
    }

    // Parte sugerida → fila editable; si el código ya está en el Direccionamiento, propone actualizar ese documento.
    private ParteCarga Sugerir(PdfDireccionamiento.Parte p, string nombreArchivo, bool unica)
    {
        var x = new ParteCarga
        {
            Incluir = p.Incluir, Desde = p.Desde, Hasta = p.Hasta, Tipo = p.Tipo, Titulo = p.Titulo, Codigo = p.Codigo,
            Version = p.Version, Revision = p.Revision, Fecha = p.Fecha
        };
        if (unica && string.IsNullOrWhiteSpace(x.Titulo)) x.Titulo = Path.GetFileNameWithoutExtension(nombreArchivo);
        AjustarDestino(x);
        return x;
    }

    private void AjustarDestino(ParteCarga x)
    {
        x.Destino = 0; x.YaCargada = false;
        if (string.IsNullOrWhiteSpace(x.Codigo)) return;
        var d = docs.FirstOrDefault(d => string.Equals(CodigoDoc(d), x.Codigo.Trim(), StringComparison.OrdinalIgnoreCase));
        if (d is null) return;
        x.Destino = d.Id;
        x.YaCargada = YaTiene(d, x);
        if (x.YaCargada) x.Incluir = false;
    }

    private bool YaTiene(DocumentoDireccionamiento d, ParteCarga x)
    {
        if (d.DocumentoMaestroId is not int id || string.IsNullOrWhiteSpace(x.Version)) return false;
        var v = x.Version.Trim().TrimStart('0');
        return Db.RevisionesDocumento.AsNoTracking().Where(r => r.DocumentoMaestroId == id).ToList()
            .Any(r => (r.Version ?? "").Trim().TrimStart('0') == v && r.Fecha?.Date == x.Fecha?.Date);
    }

    private void CambiarDestino(ParteCarga x, ChangeEventArgs e) => CambiarDestino(x, int.TryParse(e.Value?.ToString(), out var v) ? v : 0);

    private void CambiarDestino(ParteCarga x, int destino)
    {
        x.Destino = destino;
        var d = docs.FirstOrDefault(d => d.Id == destino);
        x.YaCargada = d is not null && YaTiene(d, x);
        if (d is not null)
        {
            if (string.IsNullOrWhiteSpace(x.Tipo)) x.Tipo = d.Tipo;
            if (string.IsNullOrWhiteSpace(x.Titulo)) x.Titulo = d.Titulo;
        }
    }

    // Partir: la parte que contiene la página n se corta y n empieza una parte nueva (misma clasificación vacía).
    private void PartirEn(ArchivoCarga a, int n)
    {
        var i = a.Partes.FindIndex(p => p.Desde < n && p.Hasta >= n);
        if (i < 0) return;
        var p = a.Partes[i];
        var nueva = new ParteCarga { Incluir = true, Desde = n, Hasta = p.Hasta };
        p.Hasta = n - 1;
        a.Partes.Insert(i + 1, nueva);
    }

    // Unir con la siguiente: la parte absorbe el rango de la siguiente (conserva su clasificación).
    private void UnirConSiguiente(ArchivoCarga a, ParteCarga p)
    {
        var i = a.Partes.IndexOf(p);
        if (i < 0 || i + 1 >= a.Partes.Count) return;
        var s = a.Partes[i + 1];
        p.Hasta = Math.Max(p.Hasta, s.Hasta);
        p.Version ??= s.Version; p.Revision ??= s.Revision; p.Fecha ??= s.Fecha; p.Codigo ??= s.Codigo;
        a.Partes.RemoveAt(i + 1);
    }

    private void QuitarArchivo(ArchivoCarga a)
    {
        BorrarTemporal(a.Ruta);
        carga?.Archivos.Remove(a);
    }

    // Índice de la parte (marcada) a la que pertenece una página: colorea la tira de páginas.
    private static int ParteDePagina(ArchivoCarga a, int n)
    {
        for (int i = 0; i < a.Partes.Count; i++)
            if (a.Partes[i].Desde <= n && n <= a.Partes[i].Hasta) return i;
        return -1;
    }

    private static string? ErrorParte(ArchivoCarga a, ParteCarga p)
    {
        if (!p.Incluir) return null;
        if (p.Desde < 1 || p.Hasta > a.Paginas || p.Desde > p.Hasta) return $"Páginas fuera de 1–{a.Paginas}";
        if (string.IsNullOrWhiteSpace(p.Tipo)) return "Falta el tipo";
        if (string.IsNullOrWhiteSpace(p.Titulo)) return "Falta el título";
        return null;
    }

    private bool CargaValida() =>
        carga is not null && !carga.Leyendo && carga.Archivos.Any(a => a.Partes.Any(p => p.Incluir))
        && carga.Archivos.All(a => a.Partes.All(p => ErrorParte(a, p) is null));

    private async Task GuardarCarga()
    {
        if (carga is null || !CargaValida()) return;
        guardando = true; mensaje = "";
        int nuevos = 0, actualizados = 0;
        try
        {
            Db.ChangeTracker.Clear();
            var ahora = DateTime.Now;
            var incluidas = carga.Archivos.SelectMany(a => a.Partes.Where(p => p.Incluir).Select(p => (a, p))).ToList();
            var cargaArch = new CargaArchivosDocumentos
            {
                Fecha = ahora, Origen = "Direccionamiento", Archivos = incluidas.Count,
                Descripcion = Recortar(string.Join(", ", carga.Archivos.Select(a => a.Nombre)), 500)
            };
            Db.CargasArchivosDocumentos.Add(cargaArch);
            await Db.SaveChangesAsync();
            await AsegurarTipoDocumental();

            var orden = docs.Count == 0 ? 0 : docs.Max(d => d.Orden) + 1;
            var momento = ahora;
            foreach (var grupo in incluidas.GroupBy(x => x.a))
            {
                var bytes = await File.ReadAllBytesAsync(grupo.Key.Ruta);
                foreach (var (_, p) in grupo)
                {
                    var tipo = p.Tipo!.Trim();
                    var titulo = p.Titulo!.Trim();
                    var codigo = Limpio(p.Codigo);

                    DocumentoDireccionamiento? dir = p.Destino == 0 ? null : await Db.DocumentosDireccionamiento.FirstOrDefaultAsync(d => d.Id == p.Destino);
                    int? docId = dir?.DocumentoMaestroId;
                    if (docId is int existente && !await Db.DocumentosMaestro.AnyAsync(d => d.Id == existente)) docId = null;
                    if (docId is null)
                    {
                        // Si el código ya existe en el Listado maestro, se usa ese documento (no se duplica).
                        var cod = codigo ?? $"DIR {titulo}";
                        var doc = await Db.DocumentosMaestro.FirstOrDefaultAsync(d => d.Codigo != null && d.Codigo.ToLower() == cod.ToLower());
                        if (doc is null)
                        {
                            doc = new DocumentoMaestro { Codigo = cod, TipoDocumental = TipoDocumental };
                            Db.DocumentosMaestro.Add(doc);
                            await Db.SaveChangesAsync();
                        }
                        docId = doc.Id;
                    }
                    if (dir is null)
                    {
                        dir = new DocumentoDireccionamiento { Orden = orden++, FechaCreacion = ahora };
                        Db.DocumentosDireccionamiento.Add(dir);
                        nuevos++;
                    }
                    else actualizados++;
                    dir.Tipo = tipo; dir.Titulo = titulo; dir.DocumentoMaestroId = docId;

                    momento = momento.AddMilliseconds(10);
                    var rev = new RevisionDocumento
                    {
                        DocumentoMaestroId = docId.Value, Version = Limpio(p.Version), Fecha = p.Fecha, Creada = momento,
                        Cambio = Limpio(p.Revision) is string r ? $"Revisión {r}" : null
                    };
                    Db.RevisionesDocumento.Add(rev);
                    await Db.SaveChangesAsync();

                    var parte = PdfDireccionamiento.Extraer(bytes, p.Desde, p.Hasta);
                    Db.VersionesDocumento.Add(new VersionDocumento
                    {
                        DocumentoMaestroId = docId.Value, Clase = ClaseArchivoDocumento.Vigente, RevisionDocumentoId = rev.Id,
                        Version = rev.Version, Fecha = rev.Fecha, CargaArchivosDocumentosId = cargaArch.Id, FechaCarga = momento,
                        NombreOriginal = NombreArchivo(codigo, titulo), RutaArchivo = await GuardarArchivo(parte),
                        Observaciones = $"{grupo.Key.Nombre}, páginas {p.Desde}–{p.Hasta}"
                    });
                    await Db.SaveChangesAsync();
                }
            }
            CancelarCarga();
            mensaje = $"{nuevos} documento(s) nuevo(s), {actualizados} actualizado(s)."; error = false;
            Cargar();
        }
        catch (Exception ex) { mensaje = "No se pudo guardar: " + ex.Message; error = true; Cargar(); }
        finally { guardando = false; }
    }

    private static string NombreArchivo(string? codigo, string titulo)
    {
        var n = string.Join(" ", new[] { codigo, titulo }.Where(t => !string.IsNullOrWhiteSpace(t)));
        foreach (var c in Path.GetInvalidFileNameChars()) n = n.Replace(c, '-');
        return Recortar(n, 150) + ".pdf";
    }

    private static string Recortar(string s, int max) => s.Length <= max ? s : s[..max];
    private static string? Limpio(string? s) => string.IsNullOrWhiteSpace(s) ? null : s.Trim();

    // El tipo documental debe existir en su catálogo (las listas de Documentos salen de él).
    private async Task AsegurarTipoDocumental()
    {
        var existe = await Db.Catalogos.AnyAsync(i => i.Tipo == MigracionDocumentos.CatTipo && i.Nombre.ToLower() == TipoDocumental.ToLower());
        if (!existe)
        {
            Db.Catalogos.Add(new ItemCatalogo { Tipo = MigracionDocumentos.CatTipo, Nombre = TipoDocumental });
            await Db.SaveChangesAsync();
        }
    }

    // Misma carpeta y nombre único que Documentos.
    private async Task<string> GuardarArchivo(byte[] contenido)
    {
        var carpeta = Path.Combine(Env.WebRootPath, "uploads", "documentos");
        Directory.CreateDirectory(carpeta);
        var nombre = $"{Guid.NewGuid()}.pdf";
        await File.WriteAllBytesAsync(Path.Combine(carpeta, nombre), contenido);
        return $"/uploads/documentos/{nombre}";
    }

    private string CarpetaTemporal()
    {
        var c = Path.Combine(Env.WebRootPath, "uploads", CarpetaTmp);
        Directory.CreateDirectory(c);
        return c;
    }

    private string? RutaFisica(string url)
    {
        if (string.IsNullOrWhiteSpace(url) || !url.StartsWith("/uploads/", StringComparison.Ordinal)) return null;
        var ruta = Path.GetFullPath(Path.Combine(Env.WebRootPath, url.TrimStart('/').Replace('/', Path.DirectorySeparatorChar)));
        return ruta.StartsWith(Path.GetFullPath(Env.WebRootPath), StringComparison.Ordinal) ? ruta : null;
    }

    private static void BorrarTemporal(string ruta)
    {
        try { if (File.Exists(ruta)) File.Delete(ruta); } catch { }
    }

    // Temporales (cargas sin guardar y compilados) de más de un día.
    private void LimpiarTemporales()
    {
        try
        {
            var c = Path.Combine(Env.WebRootPath, "uploads", CarpetaTmp);
            if (!Directory.Exists(c)) return;
            foreach (var f in Directory.GetFiles(c))
                if (File.GetLastWriteTime(f) < DateTime.Now.AddDays(-1)) BorrarTemporal(f);
        }
        catch { }
    }

    // ---------- Editar clasificación ----------
    private sealed class FormDoc
    {
        public int Id;
        public string Tipo { get; set; } = "";
        public string Titulo { get; set; } = "";
    }

    private FormDoc? form;

    private void Editar(DocumentoDireccionamiento d)
    {
        CancelarCarga(); elim = null; mensaje = "";
        form = new FormDoc { Id = d.Id, Tipo = d.Tipo, Titulo = d.Titulo };
    }

    private async Task GuardarForm()
    {
        if (form is null || string.IsNullOrWhiteSpace(form.Tipo) || string.IsNullOrWhiteSpace(form.Titulo)) return;
        guardando = true;
        try
        {
            Db.ChangeTracker.Clear();
            var d = await Db.DocumentosDireccionamiento.FirstAsync(x => x.Id == form.Id);
            d.Tipo = form.Tipo.Trim(); d.Titulo = form.Titulo.Trim();
            await Db.SaveChangesAsync();
            mensaje = $"{d.Titulo} guardado."; error = false;
            form = null;
            Cargar();
        }
        catch (Exception ex) { mensaje = "No se pudo guardar: " + ex.Message; error = true; }
        finally { guardando = false; }
    }

    // Mover dentro de su tipo (orden de la pantalla y del compilado).
    private async Task Mover(DocumentoDireccionamiento d, int paso)
    {
        var grupo = docs.Where(x => string.Equals(x.Tipo, d.Tipo, StringComparison.OrdinalIgnoreCase)).ToList();
        var i = grupo.FindIndex(x => x.Id == d.Id);
        var j = i + paso;
        if (i < 0 || j < 0 || j >= grupo.Count) return;
        (grupo[i], grupo[j]) = (grupo[j], grupo[i]);
        Db.ChangeTracker.Clear();
        var ids = grupo.Select(x => x.Id).ToList();
        var ents = await Db.DocumentosDireccionamiento.Where(x => ids.Contains(x.Id)).ToListAsync();
        var baseOrden = grupo.Min(x => x.Orden);
        for (int k = 0; k < grupo.Count; k++) ents.First(e => e.Id == grupo[k].Id).Orden = baseOrden + k;
        await Db.SaveChangesAsync();
        Cargar();
    }

    // ---------- Eliminar (a la Papelera, con impacto) ----------
    private sealed class Eliminacion
    {
        public int Id;
        public InformeImpacto Informe = new();
        public bool Entendido;
    }

    private Eliminacion? elim;

    private async Task PedirEliminar(DocumentoDireccionamiento d)
    {
        form = null; CancelarCarga(); mensaje = "";
        elim = new Eliminacion { Id = d.Id, Informe = await Impacto.DocumentoDireccionamiento(d.Id) };
    }

    private async Task ConfirmarEliminar()
    {
        if (elim is null) return;
        try
        {
            Db.ChangeTracker.Clear();
            var d = await Db.DocumentosDireccionamiento.FirstOrDefaultAsync(x => x.Id == elim.Id);
            if (d is not null)
            {
                d.Eliminado = true; d.FechaEliminacion = DateTime.Now;
                await Db.SaveChangesAsync();
                mensaje = $"{d.Titulo} enviado a la Papelera."; error = false;
            }
        }
        catch (Exception ex) { mensaje = "No se pudo eliminar: " + ex.Message; error = true; }
        elim = null;
        Cargar();
    }
}
