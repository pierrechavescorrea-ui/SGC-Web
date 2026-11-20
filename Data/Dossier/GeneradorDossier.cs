using System.Globalization;
using System.IO.Compression;
using ClosedXML.Excel;
using SigWeb.Data.Tablas;
using SigWeb.Models;

namespace SigWeb.Data.Dossier;

// Escribe el ZIP directamente en la respuesta HTTP (sin armarlo en memoria ni en disco): cada archivo
// se copia por partes, se respeta la cancelación (el usuario cancela la descarga o cierra la pestaña) y
// los archivos que no están en disco se anotan en la hoja "Faltantes" del índice en vez de detener todo.
public static class GeneradorDossier
{
    // Máximo de dossieres generándose a la vez (cada uno lee muchos archivos del disco).
    private static readonly SemaphoreSlim Turnos = new(2);

    private static readonly HashSet<string> YaComprimidos = new(StringComparer.OrdinalIgnoreCase)
    {
        ".pdf", ".jpg", ".jpeg", ".png", ".gif", ".webp", ".heic", ".zip", ".rar", ".7z", ".gz",
        ".xlsx", ".xlsm", ".docx", ".pptx", ".dwg", ".kmz", ".mp4", ".mov", ".mp3", ".ifc"
    };

    public sealed record Faltante(string Modulo, string Elemento, string Clase, string Nombre, string Ruta);

    // puede: módulo de permisos → ¿lo ve el usuario? (lo que no vea se omite aunque venga en la solicitud).
    // compartida: módulo → contexto de la base de la Empresa si ese módulo se comparte (null = la base del proyecto).
    public static async Task GenerarAsync(Stream salida, SolicitudDossier s, AppDbContext db, string webRoot, CancellationToken ct,
        Func<string, bool>? puede = null, Func<string, AppDbContext?>? compartida = null)
    {
        await Turnos.WaitAsync(ct);
        try
        {
            var fuentes = new Dictionary<string, FuenteCargada>();
            foreach (var m in s.Modulos)
            {
                ct.ThrowIfCancellationRequested();
                if (fuentes.ContainsKey(m.Clave) || FuentesDossier.De(m.Clave) is not { } f) continue;
                var modulo = FuentesDossier.ModuloDe(m.Clave);
                if (modulo == "inicio" || (puede is not null && !puede(modulo))) continue;
                var otra = compartida?.Invoke(modulo);
                try { fuentes[m.Clave] = await f.CargarAsync(otra ?? db, ct); }
                finally { if (otra is not null) await otra.DisposeAsync(); }
            }
            var plan = PlanDossier.Armar(s, fuentes);

            // Sin "using": si se cancela, no se intenta escribir el cierre del ZIP en una conexión cortada.
            var zip = new ZipArchive(salida, ZipArchiveMode.Create, leaveOpen: true);
            var faltantes = new List<Faltante>();
            var contenido = new List<(PlanDossier.Entrada E, long Bytes)>();

            // 1) Adjuntos
            var buffer = new byte[81920];
            foreach (var e in plan.Archivos)
            {
                ct.ThrowIfCancellationRequested();
                if (e.Crear is not null)
                {
                    var bytes = e.Crear(webRoot);
                    await using var destino = zip.CreateEntry(e.RutaZip, CompressionLevel.Fastest).Open();
                    await destino.WriteAsync(bytes, ct);
                    contenido.Add((e, bytes.Length));
                    continue;
                }
                if (e.Archivo.Enlace)
                {
                    if (!PlanDossier.EnlaceValido(e.Archivo.Ruta))
                    { faltantes.Add(new(e.Modulo, e.Elemento, e.Archivo.Clase, e.Archivo.Nombre, e.Archivo.Ruta)); continue; }
                    var uri = new Uri(e.Archivo.Ruta);
                    var entradaUrl = zip.CreateEntry(e.RutaZip, CompressionLevel.Fastest);
                    await using (var w = entradaUrl.Open())
                    {
                        var bytes = System.Text.Encoding.ASCII.GetBytes($"[InternetShortcut]\r\nURL={uri.AbsoluteUri}\r\n");
                        await w.WriteAsync(bytes, ct);
                    }
                    contenido.Add((e, 0));
                    continue;
                }

                var fisica = PlanDossier.RutaFisica(webRoot, e.Archivo.Ruta);
                FileStream? origen = null;
                try
                {
                    if (fisica is not null && File.Exists(fisica))
                        origen = new FileStream(fisica, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete, 81920, useAsync: true);
                }
                catch (IOException) { origen = null; }
                catch (UnauthorizedAccessException) { origen = null; }
                if (origen is null) { faltantes.Add(new(e.Modulo, e.Elemento, e.Archivo.Clase, e.Archivo.Nombre, e.Archivo.Ruta)); continue; }

                await using (origen)
                {
                    var nivel = YaComprimidos.Contains(Path.GetExtension(e.RutaZip)) ? CompressionLevel.NoCompression : CompressionLevel.Fastest;
                    var entrada = zip.CreateEntry(e.RutaZip, nivel);
                    try { entrada.LastWriteTime = File.GetLastWriteTime(fisica!); }
                    catch (ArgumentOutOfRangeException) { }   // ZIP solo admite fechas 1980–2107
                    await using var destino = entrada.Open();
                    int leidos;
                    while ((leidos = await origen.ReadAsync(buffer, ct)) > 0)
                        await destino.WriteAsync(buffer.AsMemory(0, leidos), ct);
                    contenido.Add((e, origen.Length));
                }
            }

            // 2) Tablas (después de los adjuntos: el vínculo "Abrir carpeta" solo queda si la carpeta llegó al ZIP)
            var conArchivos = contenido.Select(c => (c.E.Modulo, c.E.ElementoId)).ToHashSet();
            foreach (var t in plan.Tablas)
                foreach (var id in t.Vinculos.Keys.ToList())
                    if (!conArchivos.Contains((t.Fuente.Fuente.Nombre, id))) t.Vinculos.Remove(id);
            if (plan.RutaLibroUnico is not null)
            {
                using var wb = new XLWorkbook();
                foreach (var t in plan.Tablas) HojaTabla(wb, t, s.SoloVisibles);
                await EscribirLibroAsync(zip, plan.RutaLibroUnico, wb, ct);
            }
            else
                foreach (var t in plan.Tablas)
                {
                    using var wb = new XLWorkbook();
                    HojaTabla(wb, t, s.SoloVisibles);
                    await EscribirLibroAsync(zip, t.RutaZip!, wb, ct);
                }

            // 3) Índice (contenido con vínculos + faltantes)
            using (var wb = new XLWorkbook())
            {
                Indice(wb, plan, contenido, faltantes);
                await EscribirLibroAsync(zip, PlanDossier.NombreIndice, wb, ct);
            }
            zip.Dispose();   // escribe el directorio central (síncrono)
            await salida.FlushAsync(ct);
        }
        finally { Turnos.Release(); }
    }

    private static async Task EscribirLibroAsync(ZipArchive zip, string ruta, XLWorkbook wb, CancellationToken ct)
    {
        using var ms = new MemoryStream();
        wb.SaveAs(ms);
        ms.Position = 0;
        var entrada = zip.CreateEntry(ruta, CompressionLevel.Fastest);
        await using var w = entrada.Open();
        await ms.CopyToAsync(w, ct);
    }

    // ---------- Excel (mismo formato que BotonExportar: encabezado oscuro, filtro, fila fija) ----------

    private static void HojaTabla(XLWorkbook wb, PlanDossier.Tabla t, bool soloVisibles)
    {
        var ws = wb.Worksheets.Add(t.Hoja);
        var f = t.Fuente;
        var cols = f.Columnas(soloVisibles);
        var conVinculo = t.Vinculos.Count > 0;
        var total = cols.Count + (conVinculo ? 1 : 0);
        for (var c = 0; c < cols.Count; c++) ws.Cell(1, c + 1).Value = cols[c].Nombre;
        if (conVinculo) ws.Cell(1, total).Value = "Archivos";
        for (var r = 0; r < t.Ids.Count; r++)
        {
            var id = t.Ids[r];
            for (var c = 0; c < cols.Count; c++)
            {
                object? v;
                try { v = f.Motor.Valor(id, cols[c]); }
                catch { v = "#ERROR"; }
                var celda = ws.Cell(r + 2, c + 1);
                Escribir(celda, cols[c], v);
                AplicarEstilo(celda, f.Motor.Estilo(id, cols[c]));
            }
            if (conVinculo && t.Vinculos.TryGetValue(id, out var rel)) Vinculo(ws.Cell(r + 2, total), rel, "Abrir carpeta");
        }
        Formato(ws, t.Ids.Count, Math.Max(total, 1));
        foreach (var (nombre, datos, filas) in t.Adicionales)
        {
            var extra = wb.Worksheets.Add(nombre);
            if (datos.Motor is { } motor)
            {
                var columnas = (soloVisibles ? motor.Visibles : motor.Ordenados).ToList();
                for (var c = 0; c < columnas.Count; c++) extra.Cell(1, c + 1).Value = columnas[c].Nombre;
                for (var r = 0; r < filas.Count; r++)
                    for (var c = 0; c < columnas.Count; c++)
                    {
                        var campo = columnas[c];
                        var id = filas[r].ElementoId;
                        var celda = extra.Cell(r + 2, c + 1);
                        object? valor;
                        try { valor = motor.Valor(id, campo); }
                        catch { valor = "#ERROR"; }
                        Escribir(celda, campo, valor);
                        AplicarEstilo(celda, motor.Estilo(id, campo));
                    }
                Formato(extra, filas.Count, Math.Max(1, columnas.Count));
                continue;
            }

            for (var c = 0; c < datos.Columnas.Count; c++) extra.Cell(1, c + 1).Value = datos.Columnas[c];
            for (var r = 0; r < filas.Count; r++)
                for (var c = 0; c < datos.Columnas.Count; c++)
                    Escribir(extra.Cell(r + 2, c + 1), null, filas[r].Valores[c]);
            Formato(extra, filas.Count, datos.Columnas.Count);
        }
    }

    private static void AplicarEstilo(IXLCell celda, EstiloCelda? estilo)
    {
        if (estilo is null) return;
        if (Color(estilo.ColorTexto) is { } tinta) celda.Style.Font.FontColor = tinta;
        if (estilo.Forma != "texto" && Color(estilo.ColorFondo) is { } fondo) celda.Style.Fill.BackgroundColor = fondo;
        celda.Style.Font.Bold = estilo.Negrita;
    }

    private static XLColor? Color(string valor) =>
        valor.Length == 7 && valor[0] == '#' && valor.Skip(1).All(Uri.IsHexDigit) ? XLColor.FromHtml(valor) : null;

    private static void Indice(XLWorkbook wb, PlanDossier plan, List<(PlanDossier.Entrada E, long Bytes)> contenido, List<Faltante> faltantes)
    {
        var ws = wb.Worksheets.Add("Contenido");
        string[] enc = { "Módulo", "Elemento", "Clase", "Archivo", "Nombre original", "Tamaño (KB)" };
        for (var c = 0; c < enc.Length; c++) ws.Cell(1, c + 1).Value = enc[c];
        var r = 2;
        foreach (var (e, bytes) in contenido)
        {
            ws.Cell(r, 1).Value = e.Modulo;
            ws.Cell(r, 2).Value = e.Elemento;
            ws.Cell(r, 3).Value = e.Archivo.Clase;
            Vinculo(ws.Cell(r, 4), e.RutaZip, e.RutaZip);
            ws.Cell(r, 5).Value = e.Archivo.Enlace ? e.Archivo.Ruta : e.Archivo.Nombre;
            if (!e.Archivo.Enlace) {
                ws.Cell(r, 6).Value = bytes / 1024.0;
                ws.Cell(r, 6).Style.NumberFormat.Format = "0.0";
            }
            r++;
        }
        Formato(ws, contenido.Count, enc.Length);

        var wt = wb.Worksheets.Add("Tablas");
        wt.Cell(1, 1).Value = "Módulo";
        wt.Cell(1, 2).Value = "Hoja";
        wt.Cell(1, 3).Value = "Libro";
        var filaTabla = 2;
        foreach (var tabla in plan.Tablas)
            foreach (var hoja in tabla.Adicionales.Select(a => a.Nombre).Prepend(tabla.Hoja))
            {
                wt.Cell(filaTabla, 1).Value = tabla.Fuente.Fuente.Nombre;
                wt.Cell(filaTabla, 2).Value = hoja;
                var ruta = tabla.RutaZip ?? plan.RutaLibroUnico!;
                Vinculo(wt.Cell(filaTabla++, 3), ruta, ruta);
            }
        Formato(wt, filaTabla - 2, 3);

        var wf = wb.Worksheets.Add("Faltantes");
        string[] encF = { "Módulo", "Elemento", "Clase", "Nombre", "Ruta guardada" };
        for (var c = 0; c < encF.Length; c++) wf.Cell(1, c + 1).Value = encF[c];
        r = 2;
        foreach (var x in faltantes)
        {
            wf.Cell(r, 1).Value = x.Modulo; wf.Cell(r, 2).Value = x.Elemento; wf.Cell(r, 3).Value = x.Clase;
            wf.Cell(r, 4).Value = x.Nombre; wf.Cell(r, 5).Value = x.Ruta;
            r++;
        }
        Formato(wf, faltantes.Count, encF.Length);
    }

    private static void Formato(IXLWorksheet ws, int filas, int columnas)
    {
        var cab = ws.Range(1, 1, 1, columnas);
        cab.Style.Font.Bold = true;
        cab.Style.Fill.BackgroundColor = XLColor.FromHtml("#1F2A3A");
        cab.Style.Font.FontColor = XLColor.White;
        ws.SheetView.FreezeRows(1);
        ws.Range(1, 1, Math.Max(1, filas + 1), columnas).SetAutoFilter();
        ws.Columns().AdjustToContents(1, Math.Min(filas + 1, 500));
        foreach (var col in ws.ColumnsUsed()) if (col.Width > 60) col.Width = 60;
    }

    // Vínculo relativo (funciona al abrir el Excel desde la carpeta descomprimida).
    private static void Vinculo(IXLCell celda, string ruta, string texto)
    {
        var r = ruta.Replace("\"", "\"\"");
        var t = texto.Replace("\"", "\"\"");
        if (r.Length > 250 || t.Length > 250) { celda.Value = texto; return; }   // límite de texto en fórmulas de Excel
        celda.FormulaA1 = $"HYPERLINK(\"{r}\",\"{t}\")";
        celda.Style.Font.FontColor = XLColor.FromHtml("#0563C1");
        celda.Style.Font.Underline = XLFontUnderlineValues.Single;
    }

    // Valor tipado del motor de Campos: números y fechas como tales; lo demás, texto.
    private static void Escribir(IXLCell celda, CampoTabla? c, object? v)
    {
        switch (v)
        {
            case null: return;
            case double d when double.IsFinite(d):
                celda.Value = d;
                celda.Style.NumberFormat.Format = c?.Decimales is int k
                    ? k == 0 ? "#,##0" : "#,##0." + new string('0', Math.Clamp(k, 0, 30))
                    : "0.##########";
                return;
            case decimal or int or long or float:
                celda.Value = Convert.ToDouble(v, CultureInfo.InvariantCulture);
                return;
            case DateTime f:
                celda.Value = f;
                celda.Style.DateFormat.Format = "dd/mm/yyyy";
                return;
            case bool b:
                celda.Value = b ? "Sí" : "No";
                return;
            default:
                var texto = c is null ? FormulaExcel.ATexto(v) : MotorTabla.Mostrar(c, v);
                celda.Value = texto.Length > 32767 ? texto[..32767] : texto;
                if (texto.Contains('\n')) celda.Style.Alignment.WrapText = true;
                return;
        }
    }

    // Nombre del archivo descargado: "Dossier_20261028_1530.zip".
    public static string NombreZip(string? nombre) =>
        NombresZip.Limpiar(string.IsNullOrWhiteSpace(nombre) ? "Dossier" : nombre, 80).Replace(' ', '_')
        + "_" + DateTime.Now.ToString("yyyyMMdd_HHmm", CultureInfo.InvariantCulture) + ".zip";
}
