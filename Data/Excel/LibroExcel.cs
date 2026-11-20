using System.Text;
using ClosedXML.Excel;

namespace SigWeb.Data.Excel;

// Apertura de libros con mensajes claros. ClosedXML solo lee .xlsx/.xlsm (Open XML); con un .xls de Excel 97-2003,
// un libro protegido con contraseña, un CSV o un archivo dañado respondía "File contains corrupted data." en inglés.
public sealed class FormatoArchivoException(string mensaje) : InvalidOperationException(mensaje);

public enum TipoArchivoTabla { Xlsx, Csv, XlsAntiguo, Desconocido }

public static class LibroExcel
{
    public static TipoArchivoTabla Tipo(ReadOnlySpan<byte> b)
    {
        if (b.Length >= 4 && b[0] == 0x50 && b[1] == 0x4B && b[2] == 0x03 && b[3] == 0x04) return TipoArchivoTabla.Xlsx;   // ZIP (Open XML)
        if (b.Length >= 8 && b[0] == 0xD0 && b[1] == 0xCF && b[2] == 0x11 && b[3] == 0xE0) return TipoArchivoTabla.XlsAntiguo; // CFB: .xls o .xlsx cifrado
        if (b.Length >= 4 && b[0] == (byte)'%' && b[1] == (byte)'P' && b[2] == (byte)'D' && b[3] == (byte)'F') return TipoArchivoTabla.Desconocido;
        var n = Math.Min(b.Length, 4096);
        if (n == 0) return TipoArchivoTabla.Desconocido;
        for (var i = 0; i < n; i++) if (b[i] == 0) return TipoArchivoTabla.Desconocido;   // binario
        return TipoArchivoTabla.Csv;
    }

    public static XLWorkbook Abrir(byte[] libro) => Abrir(new MemoryStream(libro));

    public static XLWorkbook Abrir(Stream s)
    {
        if (s.CanSeek)
        {
            var cab = new byte[8];
            var leidos = s.Read(cab, 0, cab.Length);
            s.Position = 0;
            switch (Tipo(cab.AsSpan(0, leidos)))
            {
                case TipoArchivoTabla.XlsAntiguo:
                    throw new FormatoArchivoException("El archivo es de Excel 97-2003 (.xls) o está protegido con contraseña. Ábralo en Excel, quite la contraseña si la tiene y guárdelo como «Libro de Excel (.xlsx)».");
                case TipoArchivoTabla.Csv:
                    throw new FormatoArchivoException("El archivo es de texto (CSV), no un libro de Excel. Ábralo en Excel y guárdelo como .xlsx.");
                case TipoArchivoTabla.Desconocido:
                    throw new FormatoArchivoException("El archivo no es un libro de Excel (.xlsx). Revise que no sea un PDF o una imagen con la extensión cambiada.");
            }
        }
        // Se trabaja sobre una copia en memoria: si ClosedXML falla, se repara la copia y se reintenta (el archivo original no se toca).
        byte[] bytes;
        using (var ms = new MemoryStream()) { s.CopyTo(ms); bytes = ms.ToArray(); }
        Exception primero;
        try { return new XLWorkbook(new MemoryStream(bytes)); }
        catch (Exception ex) when (ex is not FormatoArchivoException) { primero = ex; }

        // 1) Imágenes/dibujos con identificador repetido ("The picture ID '0' already exists"): Excel lo tolera, ClosedXML no.
        //    Se renumeran los id de los dibujos y se conservan las imágenes. 2) Si aún falla, se quitan los dibujos de la copia
        //    (las importaciones solo leen celdas).
        foreach (var reparar in new Func<byte[], byte[]?>[] { RenumerarDibujos, QuitarDibujos })
        {
            byte[]? reparado;
            try { reparado = reparar(bytes); } catch { reparado = null; }
            if (reparado is null) continue;
            try { return new XLWorkbook(new MemoryStream(reparado)); }
            catch (Exception) { /* siguiente reparación */ }
        }
        throw new FormatoArchivoException("No se pudo abrir el libro: está dañado o tiene un formato que la aplicación no lee. Ábralo en Excel y guárdelo de nuevo como .xlsx. (" + primero.GetBaseException().Message + ")");
    }

    // ---------- Reparación de libros que Excel abre pero ClosedXML no ----------

    private static readonly System.Text.RegularExpressions.Regex IdDibujo =
        new(@"(<(?:\w+:)?cNvPr\b[^>]*?\bid="")(\d+)("")", System.Text.RegularExpressions.RegexOptions.Compiled);

    // Renumera los id de los objetos de cada dibujo (xl/drawings/*.xml) para que no se repitan. null = no había dibujos.
    private static byte[]? RenumerarDibujos(byte[] libro) => ReescribirZip(libro, (nombre, texto) =>
    {
        if (!nombre.StartsWith("xl/drawings/", StringComparison.OrdinalIgnoreCase) || nombre.Contains("/_rels/") || !nombre.EndsWith(".xml", StringComparison.OrdinalIgnoreCase)) return null;
        int siguiente = 1;
        return IdDibujo.Replace(texto, m => m.Groups[1].Value + (siguiente++).ToString() + m.Groups[3].Value);
    });

    private static readonly System.Text.RegularExpressions.Regex RelDibujo =
        new(@"<Relationship\b[^>]*?Type=""[^""]*/drawing""[^>]*/>", System.Text.RegularExpressions.RegexOptions.Compiled);
    private static readonly System.Text.RegularExpressions.Regex ElemDibujo =
        new(@"<(?:\w+:)?drawing\b[^>]*/>", System.Text.RegularExpressions.RegexOptions.Compiled);

    // Quita de cada hoja la referencia a sus dibujos (imágenes y formas). Las celdas quedan intactas. El VML (vmlDrawing /
    // legacyDrawing) se conserva: lo necesitan los comentarios de celda.
    private static byte[]? QuitarDibujos(byte[] libro) => ReescribirZip(libro, (nombre, texto) =>
    {
        if (nombre.StartsWith("xl/worksheets/_rels/", StringComparison.OrdinalIgnoreCase)) return RelDibujo.Replace(texto, "");
        if (nombre.StartsWith("xl/worksheets/", StringComparison.OrdinalIgnoreCase) && !nombre.Contains("/_rels/")) return ElemDibujo.Replace(texto, "");
        return null;
    });

    // Copia el ZIP cambiando el texto de las entradas que el transformador devuelva (null = sin cambio). null si nada cambió.
    private static byte[]? ReescribirZip(byte[] libro, Func<string, string, string?> transformar)
    {
        using var origen = new System.IO.Compression.ZipArchive(new MemoryStream(libro), System.IO.Compression.ZipArchiveMode.Read);
        using var salida = new MemoryStream();
        bool cambio = false;
        using (var destino = new System.IO.Compression.ZipArchive(salida, System.IO.Compression.ZipArchiveMode.Create, leaveOpen: true))
        {
            foreach (var e in origen.Entries)
            {
                var nueva = destino.CreateEntry(e.FullName, System.IO.Compression.CompressionLevel.Fastest);
                using var lee = e.Open();
                using var escribe = nueva.Open();
                if (e.FullName.EndsWith(".xml", StringComparison.OrdinalIgnoreCase) || e.FullName.EndsWith(".rels", StringComparison.OrdinalIgnoreCase))
                {
                    using var sr = new StreamReader(lee, Encoding.UTF8);
                    var texto = sr.ReadToEnd();
                    var t2 = transformar(e.FullName, texto);
                    if (t2 is not null && t2 != texto) cambio = true;
                    var datos = new UTF8Encoding(false).GetBytes(t2 ?? texto);
                    escribe.Write(datos, 0, datos.Length);
                }
                else lee.CopyTo(escribe);
            }
        }
        return cambio ? salida.ToArray() : null;
    }

    // ---------- CSV ----------

    // Texto separado por ; , o tabulador (se elige el que más aparece en la primera línea, fuera de comillas).
    // Codificación: UTF-8 (con o sin BOM) o, si no es UTF-8 válido, Windows-1252 (lo que guarda Excel en español).
    public static List<string[]> LeerCsv(byte[] datos, int maxFilas, int maxColumnas)
    {
        string texto;
        try { texto = new UTF8Encoding(false, true).GetString(datos); }
        catch (DecoderFallbackException) { texto = Encoding.Latin1.GetString(datos); }
        texto = texto.TrimStart('﻿');
        var primera = texto.Split('\n')[0];
        char sep = new[] { ';', ',', '\t' }.OrderByDescending(c => FueraDeComillas(primera, c)).First();

        var filas = new List<List<string>>();
        var fila = new List<string>();
        var celda = new StringBuilder();
        bool comillas = false;
        for (int i = 0; i < texto.Length && filas.Count < maxFilas; i++)
        {
            var ch = texto[i];
            if (comillas)
            {
                if (ch == '"') { if (i + 1 < texto.Length && texto[i + 1] == '"') { celda.Append('"'); i++; } else comillas = false; }
                else celda.Append(ch);
            }
            else if (ch == '"') comillas = true;
            else if (ch == sep) { fila.Add(celda.ToString().Trim()); celda.Clear(); }
            else if (ch == '\n' || ch == '\r')
            {
                if (ch == '\r' && i + 1 < texto.Length && texto[i + 1] == '\n') i++;
                fila.Add(celda.ToString().Trim()); celda.Clear();
                filas.Add(fila); fila = new();
            }
            else celda.Append(ch);
        }
        if (celda.Length > 0 || fila.Count > 0) { fila.Add(celda.ToString().Trim()); filas.Add(fila); }
        while (filas.Count > 0 && filas[^1].All(string.IsNullOrWhiteSpace)) filas.RemoveAt(filas.Count - 1);
        var ancho = Math.Min(maxColumnas, filas.Count == 0 ? 0 : filas.Max(f => f.Count));
        return filas.Select(f => Enumerable.Range(0, ancho).Select(c => c < f.Count ? f[c] : "").ToArray()).ToList();
    }

    private static int FueraDeComillas(string linea, char c)
    {
        int n = 0; bool q = false;
        foreach (var ch in linea) { if (ch == '"') q = !q; else if (ch == c && !q) n++; }
        return n;
    }
}
