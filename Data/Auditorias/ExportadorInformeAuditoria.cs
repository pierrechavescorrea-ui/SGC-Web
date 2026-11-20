using ClosedXML.Excel;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using SigWeb.Models;
using A = DocumentFormat.OpenXml.Drawing;
using DW = DocumentFormat.OpenXml.Drawing.Wordprocessing;
using PIC = DocumentFormat.OpenXml.Drawing.Pictures;

namespace SigWeb.Data.Auditorias;

// Informe de auditoría en Word (.docx, mismas secciones que la vista imprimible, con fotos) y en
// Excel (.xlsx, una hoja por tema para filtrar: datos, hallazgos, planes y acciones, archivos, cambios).
// Lo sirve GET /api/auditorias/{id}/informe.{docx|xlsx} (no pasa por el circuito de Blazor).
public static class ExportadorInformeAuditoria
{
    private const string Gris = "5B6675", Borde = "C9D0D9", Fondo = "F1F3F6", Rojo = "EC1C24";

    private static string L(string? t) { var l = ServicioAuditorias.Lista(t); return l.Count == 0 ? "—" : string.Join(", ", l); }
    private static string T(string? t) => string.IsNullOrWhiteSpace(t) ? "—" : t.Trim();
    private static string TipoGrado(HallazgoAuditoria h) => h.Tipo + (string.IsNullOrWhiteSpace(h.Grado) ? "" : " " + h.Grado.ToLowerInvariant());
    private static string Resultado(PlanAccion p) => p.Eficaz is null ? "Sin verificar" : p.Eficaz.Value ? "Eficaz" : "No eficaz";

    public static string NombreArchivo(InformeAuditoria d, string ext) => $"Informe {d.Auditoria.Codigo}.{ext}";

    // =====================================================================================
    // Word
    // =====================================================================================
    public static byte[] Word(InformeAuditoria d, string raizWeb)
    {
        using var ms = new MemoryStream();
        using (var doc = WordprocessingDocument.Create(ms, WordprocessingDocumentType.Document))
        {
            var main = doc.AddMainDocumentPart();
            main.Document = new Document(new Body());
            Estilos(main);
            var w = new Escritor(main, raizWeb);
            var a = d.Auditoria;

            w.Titulo("INFORME DE AUDITORÍA INTERNA");
            w.Parrafo(string.Join(" · ", new[] { d.Programa?.Titulo, a.Proyecto ?? d.Programa?.Proyecto }.Where(x => !string.IsNullOrWhiteSpace(x))), color: Gris);
            w.Ficha(new[] {
                ("Código", a.Codigo), ("Estado", EscalaAuditoria.Nombre(a.Estado)), ("Fecha del informe", a.FechaInforme?.ToString("dd/MM/yyyy") ?? "—") });

            w.Seccion("1. Datos generales");
            var ficha = new List<(string, string)> {
                ("Tipo de auditoría", T(a.Tipo)), ("Modalidad", T(a.Modalidad)), ("Fecha(s)", d.Fechas),
                ("Programa", d.Programa is null ? "—" : $"{d.Programa.Titulo} ({d.Programa.Anio})"),
                ("Normas", L(a.Normas)), ("Procesos auditados", L(a.Procesos)) };
            if (d.Lugares.Count > 0) ficha.Add(("Lugares", string.Join(", ", d.Lugares)));
            ficha.Add(("Auditor líder", T(a.AuditorLider)));
            ficha.Add(("Auditores", L(a.Auditores)));
            if (!string.IsNullOrWhiteSpace(a.ExpertoTecnico)) ficha.Add(("Experto técnico", a.ExpertoTecnico!));
            if (!string.IsNullOrWhiteSpace(a.Observadores)) ficha.Add(("Observadores", a.Observadores!));
            ficha.Add(("Auditados", T(a.Auditados)));
            w.Ficha(ficha);

            w.Seccion("2. Objetivo, alcance y criterios");
            w.Campo("Objetivo", T(a.Objetivo));
            w.Campo("Alcance", T(a.Alcance));
            w.Campo("Criterios de auditoría", T(a.Criterios));

            w.Seccion("3. Resumen de resultados");
            w.Tabla(new[] { "Hallazgos", "No conformidades", "Sin cerrar", "Planes de acción", "Eficaces / verificados", "Por verificar" },
                new[] { new[] { $"{d.Hallazgos.Count}", $"{d.NoConformidades}", $"{d.Abiertos}", $"{d.Planes.Count}", $"{d.Eficaces} / {d.Verificados}", $"{d.PorVerificar}" } });
            if (d.Hallazgos.Count == 0) w.Parrafo("No se registraron hallazgos.", cursiva: true);
            else
            {
                w.Tabla(new[] { "Tipo de hallazgo", "N.º" }, d.PorTipo().Select(x => new[] { x.Clave, x.N.ToString() }));
                w.Tabla(new[] { "Proceso", "N.º" }, d.PorProceso().Select(x => new[] { x.Clave, x.N.ToString() }));
                w.Tabla(new[] { "Hallazgo", "Tipo", "Proceso", "Declaración", "Planes", "Estado" },
                    d.Hallazgos.Select(h => new[] { h.H.Codigo, TipoGrado(h.H), L(h.H.Procesos), h.H.Descripcion,
                        string.Join(", ", h.Planes.Select(id => d.Plan(id)?.P.Codigo).Where(c => c is not null)), EscalaAuditoria.Nombre(h.Estado) }));

                w.Seccion("4. Detalle de los hallazgos");
                foreach (var h in d.Hallazgos)
                {
                    var x = h.H;
                    w.Subtitulo($"{x.Codigo} · {TipoGrado(x)} · {EscalaAuditoria.Nombre(h.Estado)}", h.EsNc ? Rojo : "1F2A3A");
                    var meta = new List<(string, string)> { ("Fecha", x.Fecha.ToString("dd/MM/yyyy")) };
                    if (!string.IsNullOrWhiteSpace(x.Auditor)) meta.Add(("Auditor", x.Auditor!));
                    if (!string.IsNullOrWhiteSpace(x.Procesos)) meta.Add(("Proceso", L(x.Procesos)));
                    if (h.Lugares.Count > 0) meta.Add(("Lugar", string.Join(", ", h.Lugares)));
                    meta.Add(("Aceptación del auditado", EscalaAuditoria.Nombre(x.Aceptacion) + (string.IsNullOrWhiteSpace(x.NotaAceptacion) ? "" : ": " + x.NotaAceptacion)));
                    w.Ficha(meta);
                    w.Campo("Declaración del hallazgo", x.Descripcion);
                    var req = string.Join(" · ", new[] { L(x.Normas) == "—" ? null : L(x.Normas), string.IsNullOrWhiteSpace(x.Numerales) ? null : $"numeral {x.Numerales}", x.Documento }.Where(t => !string.IsNullOrWhiteSpace(t)));
                    if (req.Length > 0 || !string.IsNullOrWhiteSpace(x.Requisito))
                        w.Campo("Requisito", string.Join("\n", new[] { req, x.Requisito }.Where(t => !string.IsNullOrWhiteSpace(t))));
                    w.Campo("Evidencia objetiva", T(x.Evidencia));
                    if (h.Fotos.Count + h.Anexos.Count > 0)
                    {
                        w.Etiqueta("Registro fotográfico y anexos");
                        w.Archivos(h.Fotos.Concat(h.Anexos).Cast<IAdjuntoDescrito>().ToList(), x.Codigo);
                    }
                    if (h.Planes.Count > 0)
                    {
                        w.Etiqueta("Planes de acción");
                        foreach (var id in h.Planes) if (d.Plan(id) is { } pl) Plan(w, pl, false);
                    }
                    else if (h.EsNc) w.Parrafo("Sin plan de acción.", cursiva: true);
                    if (x.FechaCierre is not null)
                        w.Campo("Cierre", $"{x.FechaCierre:dd/MM/yyyy}{(string.IsNullOrWhiteSpace(x.CerradoPor) ? "" : " · " + x.CerradoPor)}{(string.IsNullOrWhiteSpace(x.ObservacionCierre) ? "" : "\n" + x.ObservacionCierre)}");
                }
            }

            var n = d.Hallazgos.Count > 0 ? 5 : 4;
            var sobre = d.Planes.Where(p => p.SobreAuditoria).ToList();
            if (sobre.Count > 0)
            {
                w.Seccion($"{n++}. Planes de acción sobre la auditoría");
                foreach (var pl in sobre) Plan(w, pl, true);
            }
            w.Seccion($"{n++}. Fortalezas y conclusiones");
            w.Campo("Fortalezas", T(a.Fortalezas));
            w.Campo("Conclusiones", T(a.Conclusiones));
            if (d.Cambios.Count > 0)
            {
                w.Seccion($"{n++}. Cambios en la auditoría");
                w.Tabla(new[] { "Fecha", "Cambio", "Anterior", "Nuevo", "Motivo" },
                    d.Cambios.Select(c => new[] { c.Fecha.ToString("dd/MM/yyyy"), c.Campo, c.Anterior ?? "", c.Nuevo ?? "", c.Motivo ?? "" }));
            }
            w.Firmas(new[] { (a.AuditorLider ?? "", "Auditor líder"), ("", "Responsable del proceso auditado"), (d.Programa?.Responsable ?? "", "Responsable del programa de auditoría") });
            w.Parrafo($"{a.Codigo} · generado el {d.Generado:dd/MM/yyyy HH:mm}", color: Gris, tam: 16);

            main.Document.Body!.Append(new SectionProperties(
                new PageSize { Width = 11906, Height = 16838 },
                new PageMargin { Top = 1000, Bottom = 1000, Left = 1000, Right = 1000, Header = 500, Footer = 500 }));
            main.Document.Save();
        }
        return ms.ToArray();
    }

    private static void Plan(Escritor w, InformeAuditoria.PlanInforme pl, bool descripcion)
    {
        var p = pl.P;
        w.Subtitulo($"Plan {p.Codigo} · {EscalaAccion.Nombre(pl.Estado)} · Eficacia: {EscalaEficacia.Nombre(pl.Eficacia, p.FechaProgramadaEficacia)}", "1F4E8C", 21);
        if (pl.Hallazgos.Count > 1) w.Parrafo("Trata los hallazgos " + string.Join(", ", pl.Hallazgos), color: Gris);
        if (descripcion && !string.IsNullOrWhiteSpace(p.DescripcionHallazgo)) w.Campo("Situación que trata", p.DescripcionHallazgo);
        w.Campo("Análisis de causa", string.IsNullOrWhiteSpace(p.AnalisisCausa) ? "No registrado." : p.AnalisisCausa);
        if (pl.Acciones.Count == 0) w.Parrafo("El plan aún no tiene acciones.", cursiva: true);
        else w.Tabla(new[] { "#", "Tipo", "Acción", "Producto / evidencia", "Responsable", "Fecha", "Estado" },
            pl.Acciones.Select((a, i) => new[] { (i + 1).ToString(), EscalaAccion.Nombre(a.Tipo), a.Descripcion, a.Producto, a.Responsable, a.Fecha.ToString("dd/MM/yyyy"), EscalaAccion.Nombre(a.Estado) }));
        var ef = new List<(string, string)> {
            ("Criterio de eficacia", string.IsNullOrWhiteSpace(p.CriterioEficacia) ? "No definido." : p.CriterioEficacia!),
            ("Fecha programada", p.FechaProgramadaEficacia?.ToString("dd/MM/yyyy") ?? "—"), ("Resultado", Resultado(p)) };
        if (p.Eficaz is not null)
        {
            ef.Add(("Fecha de verificación", p.FechaVerificacionEficacia?.ToString("dd/MM/yyyy") ?? "—"));
            ef.Add(("Verificó", T(p.VerificoEficacia)));
            ef.Add(("Evidencia de la verificación", T(p.EvidenciaEficacia)));
        }
        w.Etiqueta("Verificación de eficacia (ISO 9001 10.2.1 d)");
        w.Ficha(ef);
        if (pl.Soportes.Count > 0) w.Archivos(pl.Soportes.Cast<IAdjuntoDescrito>().ToList(), $"Soporte {p.Codigo}");
    }

    private static void Estilos(MainDocumentPart main)
    {
        var st = main.AddNewPart<StyleDefinitionsPart>();
        st.Styles = new Styles(new DocDefaults(
            new RunPropertiesDefault(new RunPropertiesBaseStyle(new RunFonts { Ascii = "Calibri", HighAnsi = "Calibri", ComplexScript = "Calibri" },
                new FontSize { Val = "20" }, new Languages { Val = "es-CO" })),
            new ParagraphPropertiesDefault(new ParagraphPropertiesBaseStyle(new SpacingBetweenLines { After = "60", Line = "264", LineRule = LineSpacingRuleValues.Auto }))));
        st.Styles.Save();
    }

    // Escribe párrafos, tablas e imágenes en el cuerpo del documento.
    private sealed class Escritor
    {
        private readonly MainDocumentPart _main;
        private readonly Body _body;
        private readonly string _raiz;
        private uint _img = 1;
        public Escritor(MainDocumentPart main, string raiz) { _main = main; _body = main.Document.Body!; _raiz = raiz; }

        private static Run R(string t, bool negrita = false, string? color = null, int tam = 0, bool cursiva = false)
        {
            var rp = new RunProperties();
            if (negrita) rp.Append(new Bold());
            if (cursiva) rp.Append(new Italic());
            if (color is not null) rp.Append(new Color { Val = color });
            if (tam > 0) rp.Append(new FontSize { Val = tam.ToString() });
            var run = new Run(rp);
            var lineas = (t ?? "").Replace("\r", "").Split('\n');
            for (int i = 0; i < lineas.Length; i++)
            {
                if (i > 0) run.Append(new Break());
                run.Append(new Text(lineas[i]) { Space = SpaceProcessingModeValues.Preserve });
            }
            return run;
        }

        private static Paragraph P(params OpenXmlElement[] hijos) => new(hijos);

        public void Titulo(string t) => _body.Append(P(new ParagraphProperties(
            new ParagraphBorders(new BottomBorder { Val = BorderValues.Single, Size = 18, Color = Rojo }), new SpacingBetweenLines { After = "80" }),
            R(t, true, "1F2A3A", 34)));

        public void Seccion(string t) => _body.Append(P(new ParagraphProperties(new KeepNext(),
            new ParagraphBorders(new BottomBorder { Val = BorderValues.Single, Size = 6, Color = Borde }), new SpacingBetweenLines { Before = "280", After = "100" }),
            R(t, true, "1F2A3A", 26)));

        public void Subtitulo(string t, string color, int tam = 23) => _body.Append(P(new ParagraphProperties(new KeepNext(), new SpacingBetweenLines { Before = "200", After = "60" }),
            R(t, true, color, tam)));

        public void Etiqueta(string t) => _body.Append(P(new ParagraphProperties(new KeepNext(), new SpacingBetweenLines { Before = "120", After = "40" }),
            R(t.ToUpperInvariant(), true, Gris, 17)));

        public void Parrafo(string t, string? color = null, bool cursiva = false, int tam = 0) =>
            _body.Append(P(R(t, color: color, cursiva: cursiva, tam: tam)));

        public void Campo(string etiqueta, string valor) => _body.Append(P(new ParagraphProperties(new SpacingBetweenLines { After = "80" }),
            R(etiqueta + ": ", true, Gris), R(valor)));

        private static TableCell Celda(string t, bool cab = false, int ancho = 0, bool negrita = false)
        {
            var tcp = new TableCellProperties(new TableCellMargin(new LeftMargin { Width = "70", Type = TableWidthUnitValues.Dxa }, new RightMargin { Width = "70", Type = TableWidthUnitValues.Dxa }));
            if (ancho > 0) tcp.Append(new TableCellWidth { Width = ancho.ToString(), Type = TableWidthUnitValues.Dxa });
            if (cab) tcp.Append(new Shading { Val = ShadingPatternValues.Clear, Fill = Fondo });
            return new TableCell(tcp, new Paragraph(new ParagraphProperties(new SpacingBetweenLines { After = "0" }), R(t, cab || negrita, tam: 18)));
        }

        private static Table NuevaTabla() => new(new TableProperties(
            new TableWidth { Width = "5000", Type = TableWidthUnitValues.Pct },
            new TableBorders(
                new TopBorder { Val = BorderValues.Single, Size = 4, Color = Borde }, new BottomBorder { Val = BorderValues.Single, Size = 4, Color = Borde },
                new LeftBorder { Val = BorderValues.Single, Size = 4, Color = Borde }, new RightBorder { Val = BorderValues.Single, Size = 4, Color = Borde },
                new InsideHorizontalBorder { Val = BorderValues.Single, Size = 4, Color = Borde }, new InsideVerticalBorder { Val = BorderValues.Single, Size = 4, Color = Borde })));

        public void Tabla(string[] cab, IEnumerable<string[]> filas)
        {
            var t = NuevaTabla();
            t.Append(new TableGrid(cab.Select(_ => new GridColumn { Width = (9900 / cab.Length).ToString() })));
            t.Append(new TableRow(new OpenXmlElement[] { new TableRowProperties(new TableHeader()) }.Concat(cab.Select(c => Celda(c, cab: true)))));
            foreach (var f in filas) t.Append(new TableRow(new OpenXmlElement[] { new TableRowProperties(new CantSplit()) }.Concat(f.Select(c => Celda(c ?? "")))));
            _body.Append(t);
            _body.Append(P(new ParagraphProperties(new SpacingBetweenLines { After = "40" })));
        }

        // Etiqueta | valor en filas (ficha).
        public void Ficha(IEnumerable<(string Etiqueta, string Valor)> filas)
        {
            var t = NuevaTabla();
            t.Append(new TableGrid(new GridColumn { Width = "2600" }, new GridColumn { Width = "7300" }));
            foreach (var (e, v) in filas)
                t.Append(new TableRow(new TableRowProperties(new CantSplit()), Celda(e, cab: true, ancho: 2600), Celda(v, ancho: 7300)));
            _body.Append(t);
            _body.Append(P(new ParagraphProperties(new SpacingBetweenLines { After = "40" })));
        }

        public void Firmas(IEnumerable<(string Nombre, string Cargo)> firmas)
        {
            _body.Append(P(new ParagraphProperties(new SpacingBetweenLines { Before = "900" })));
            var t = new Table(new TableProperties(new TableWidth { Width = "5000", Type = TableWidthUnitValues.Pct }));
            var fila = new TableRow(new TableRowProperties(new CantSplit()));
            foreach (var (n, c) in firmas)
                fila.Append(new TableCell(new TableCellProperties(new TableCellMargin(new LeftMargin { Width = "200", Type = TableWidthUnitValues.Dxa }, new RightMargin { Width = "200", Type = TableWidthUnitValues.Dxa })),
                    new Paragraph(new ParagraphProperties(new Justification { Val = JustificationValues.Center },
                        new ParagraphBorders(new TopBorder { Val = BorderValues.Single, Size = 6, Color = "1F2A3A" }), new SpacingBetweenLines { After = "0" }), R(n.Length == 0 ? " " : n, true)),
                    new Paragraph(new ParagraphProperties(new Justification { Val = JustificationValues.Center }), R(c, color: Gris, tam: 17))));
            t.Append(fila);
            _body.Append(t);
        }

        // Fotos (con número y descripción) y anexos (nombre + descripción).
        public void Archivos(List<IAdjuntoDescrito> archivos, string prefijo)
        {
            var fotos = archivos.Where(x => SigWeb.Data.Planes.AlmacenAnexos.EsImagen(x.RutaArchivo)).ToList();
            for (int i = 0; i < fotos.Count; i++)
            {
                var f = fotos[i];
                var ruta = Path.Combine(_raiz, f.RutaArchivo.TrimStart('/').Replace('/', Path.DirectorySeparatorChar));
                var pie = $"{prefijo} · foto{(fotos.Count > 1 ? $" {i + 1}" : "")}. ";
                if (File.Exists(ruta) && Imagen(ruta) is { } dib)
                    _body.Append(P(new ParagraphProperties(new KeepNext(), new SpacingBetweenLines { Before = "80", After = "20" }), dib));
                else pie += "(archivo no encontrado) ";
                _body.Append(P(new ParagraphProperties(new SpacingBetweenLines { After = "120" }),
                    R(pie, true, Gris, 17), R(string.IsNullOrWhiteSpace(f.Descripcion) ? f.NombreOriginal : f.Descripcion!, color: Gris, tam: 17)));
            }
            var otros = archivos.Where(x => !SigWeb.Data.Planes.AlmacenAnexos.EsImagen(x.RutaArchivo)).ToList();
            if (otros.Count > 0)
                Tabla(new[] { "Anexo", "Descripción", "Fecha" }, otros.Select(x => new[] { x.NombreOriginal, x.Descripcion ?? "", x.FechaRegistro.ToString("dd/MM/yyyy") }));
        }

        private Run? Imagen(string ruta)
        {
            var ext = Path.GetExtension(ruta).ToLowerInvariant();
            var tipo = ext switch { ".png" => ImagePartType.Png, ".gif" => ImagePartType.Gif, ".bmp" => ImagePartType.Bmp, ".jpg" or ".jpeg" => ImagePartType.Jpeg, _ => (PartTypeInfo?)null };
            if (tipo is null) return null;   // webp: Word no lo muestra
            var bytes = File.ReadAllBytes(ruta);
            var (pw, ph) = TamanoImagen(bytes);
            if (pw <= 0 || ph <= 0) { pw = 4; ph = 3; }
            // Máx. 12 cm de ancho y 9 cm de alto, sin deformar.
            const long emuCm = 360000;
            double esc = Math.Min(12.0 / pw, 9.0 / ph);
            long cx = (long)(pw * esc * emuCm), cy = (long)(ph * esc * emuCm);
            var part = _main.AddImagePart(tipo.Value);
            using (var s = new MemoryStream(bytes)) part.FeedData(s);
            var rid = _main.GetIdOfPart(part);
            var id = _img++;
            var nombre = $"Imagen{id}";
            return new Run(new Drawing(new DW.Inline(
                new DW.Extent { Cx = cx, Cy = cy },
                new DW.EffectExtent { LeftEdge = 0, TopEdge = 0, RightEdge = 0, BottomEdge = 0 },
                new DW.DocProperties { Id = id, Name = nombre },
                new DW.NonVisualGraphicFrameDrawingProperties(new A.GraphicFrameLocks { NoChangeAspect = true }),
                new A.Graphic(new A.GraphicData(new PIC.Picture(
                    new PIC.NonVisualPictureProperties(new PIC.NonVisualDrawingProperties { Id = id, Name = nombre }, new PIC.NonVisualPictureDrawingProperties()),
                    new PIC.BlipFill(new A.Blip { Embed = rid }, new A.Stretch(new A.FillRectangle())),
                    new PIC.ShapeProperties(new A.Transform2D(new A.Offset { X = 0, Y = 0 }, new A.Extents { Cx = cx, Cy = cy }),
                        new A.PresetGeometry(new A.AdjustValueList()) { Preset = A.ShapeTypeValues.Rectangle })))
                { Uri = "http://schemas.openxmlformats.org/drawingml/2006/picture" }))
            { DistanceFromTop = 0, DistanceFromBottom = 0, DistanceFromLeft = 0, DistanceFromRight = 0 }));
        }
    }

    // Ancho y alto en píxeles leídos de la cabecera (PNG, GIF, BMP, JPEG). 0 = no se pudo.
    internal static (int W, int H) TamanoImagen(byte[] b)
    {
        try
        {
            if (b.Length > 24 && b[0] == 0x89 && b[1] == 0x50)
                return ((b[16] << 24) | (b[17] << 16) | (b[18] << 8) | b[19], (b[20] << 24) | (b[21] << 16) | (b[22] << 8) | b[23]);
            if (b.Length > 10 && b[0] == 'G' && b[1] == 'I')
                return (b[6] | (b[7] << 8), b[8] | (b[9] << 8));
            if (b.Length > 26 && b[0] == 'B' && b[1] == 'M')
                return (BitConverter.ToInt32(b, 18), Math.Abs(BitConverter.ToInt32(b, 22)));
            if (b.Length > 4 && b[0] == 0xFF && b[1] == 0xD8)
            {
                int i = 2;
                while (i + 9 < b.Length)
                {
                    if (b[i] != 0xFF) { i++; continue; }
                    var m = b[i + 1];
                    if (m is >= 0xC0 and <= 0xCF && m is not (0xC4 or 0xC8 or 0xCC))
                        return ((b[i + 7] << 8) | b[i + 8], (b[i + 5] << 8) | b[i + 6]);
                    i += 2 + ((b[i + 2] << 8) | b[i + 3]);
                }
            }
        }
        catch { }
        return (0, 0);
    }

    // =====================================================================================
    // Excel
    // =====================================================================================
    public static byte[] Excel(InformeAuditoria d, string urlBase)
    {
        using var wb = new XLWorkbook();
        var a = d.Auditoria;
        string Url(string ruta) => urlBase.TrimEnd('/') + ruta;

        // Hoja Informe: ficha de la auditoría y resumen.
        var ws = wb.Worksheets.Add("Informe");
        ws.Cell(1, 1).Value = "INFORME DE AUDITORÍA INTERNA";
        ws.Cell(1, 1).Style.Font.SetBold().Font.SetFontSize(14);
        int r = 3;
        void Fila(string e, string? v)
        {
            ws.Cell(r, 1).Value = e; ws.Cell(r, 1).Style.Font.SetBold().Fill.SetBackgroundColor(XLColor.FromHtml("#" + Fondo));
            ws.Cell(r, 2).Value = v ?? ""; ws.Cell(r, 2).Style.Alignment.SetWrapText().Alignment.SetVertical(XLAlignmentVerticalValues.Top);
            r++;
        }
        Fila("Código", a.Codigo);
        Fila("Estado", EscalaAuditoria.Nombre(a.Estado));
        Fila("Programa", d.Programa is null ? "" : $"{d.Programa.Titulo} ({d.Programa.Anio})");
        Fila("Proyecto", a.Proyecto ?? d.Programa?.Proyecto);
        Fila("Tipo de auditoría", a.Tipo);
        Fila("Modalidad", a.Modalidad);
        Fila("Fecha(s)", d.Fechas);
        Fila("Fecha del informe", a.FechaInforme?.ToString("dd/MM/yyyy"));
        Fila("Normas", L(a.Normas));
        Fila("Procesos auditados", L(a.Procesos));
        Fila("Lugares", string.Join(", ", d.Lugares));
        Fila("Auditor líder", a.AuditorLider);
        Fila("Auditores", L(a.Auditores));
        Fila("Experto técnico", a.ExpertoTecnico);
        Fila("Observadores", a.Observadores);
        Fila("Auditados", a.Auditados);
        Fila("Objetivo", a.Objetivo);
        Fila("Alcance", a.Alcance);
        Fila("Criterios de auditoría", a.Criterios);
        Fila("Fortalezas", a.Fortalezas);
        Fila("Conclusiones", a.Conclusiones);
        r++;
        Fila("Hallazgos", d.Hallazgos.Count.ToString());
        Fila("No conformidades", d.NoConformidades.ToString());
        Fila("Hallazgos sin cerrar", d.Abiertos.ToString());
        Fila("Planes de acción", d.Planes.Count.ToString());
        Fila("Planes eficaces / verificados", $"{d.Eficaces} / {d.Verificados}");
        Fila("Planes por verificar", d.PorVerificar.ToString());
        Fila("Generado", d.Generado.ToString("dd/MM/yyyy HH:mm"));
        ws.Column(1).Width = 30; ws.Column(2).Width = 100;

        // Hallazgos: una fila por hallazgo.
        var hs = wb.Worksheets.Add("Hallazgos");
        Tabla(hs, new[] { "Código", "Fecha", "Tipo", "Grado", "Estado", "Procesos", "Lugares", "Auditor", "Normas", "Numerales", "Documento",
            "Requisito", "Evidencia objetiva", "Declaración", "Aceptación", "Nota de aceptación", "Planes de acción", "Fecha de cierre", "Cerrado por", "Observación de cierre", "Fotos y anexos" },
            d.Hallazgos.Select(h => new object?[] { h.H.Codigo, h.H.Fecha, h.H.Tipo, h.H.Grado, EscalaAuditoria.Nombre(h.Estado), L(h.H.Procesos), string.Join(", ", h.Lugares),
                h.H.Auditor, L(h.H.Normas), h.H.Numerales, h.H.Documento, h.H.Requisito, h.H.Evidencia, h.H.Descripcion, EscalaAuditoria.Nombre(h.H.Aceptacion), h.H.NotaAceptacion,
                string.Join(", ", h.Planes.Select(id => d.Plan(id)?.P.Codigo).Where(c => c is not null)), h.H.FechaCierre, h.H.CerradoPor, h.H.ObservacionCierre, h.Fotos.Count + h.Anexos.Count }));

        // Planes y acciones: una fila por acción (el plan sin acciones sale en una fila).
        var ps = wb.Worksheets.Add("Planes y acciones");
        var filas = new List<object?[]>();
        foreach (var pl in d.Planes)
        {
            var p = pl.P;
            object?[] Base() => new object?[] { p.Codigo, pl.Hallazgos.Count == 0 ? "(sobre la auditoría)" : string.Join(", ", pl.Hallazgos), p.Fecha, EscalaAccion.Nombre(pl.Estado),
                p.DescripcionHallazgo, p.AnalisisCausa, p.CriterioEficacia, p.FechaProgramadaEficacia, Resultado(p), p.FechaVerificacionEficacia, p.VerificoEficacia, p.EvidenciaEficacia,
                EscalaEficacia.Nombre(pl.Eficacia, p.FechaProgramadaEficacia) };
            if (pl.Acciones.Count == 0) filas.Add(Base().Concat(new object?[] { null, null, null, null, null, null, null }).ToArray());
            var i = 0;
            foreach (var ac in pl.Acciones)
                filas.Add(Base().Concat(new object?[] { ++i, EscalaAccion.Nombre(ac.Tipo), ac.Descripcion, ac.Producto, ac.Responsable, ac.Fecha, EscalaAccion.Nombre(ac.Estado) }).ToArray());
        }
        Tabla(ps, new[] { "Plan", "Hallazgos", "Fecha del plan", "Estado del plan", "Situación que trata", "Análisis de causa", "Criterio de eficacia", "Verificación programada",
            "Resultado de eficacia", "Fecha de verificación", "Verificó", "Evidencia de la verificación", "Estado de la eficacia",
            "N.º acción", "Tipo de acción", "Acción", "Producto / evidencia", "Responsable", "Fecha de la acción", "Estado de la acción" }, filas);

        // Archivos: fotos y anexos de hallazgos + soportes de eficacia, con su descripción y vínculo.
        var fs = wb.Worksheets.Add("Fotos y anexos");
        var arch = d.Hallazgos.SelectMany(h => h.Fotos.Concat(h.Anexos).Select(x => (Origen: "Hallazgo", Codigo: h.H.Codigo, X: (IAdjuntoDescrito)x)))
            .Concat(d.Planes.SelectMany(pl => pl.Soportes.Select(x => (Origen: "Eficacia del plan", Codigo: pl.P.Codigo, X: (IAdjuntoDescrito)x)))).ToList();
        Tabla(fs, new[] { "Origen", "Código", "Archivo", "Descripción", "Fecha", "Tipo" },
            arch.Select(x => new object?[] { x.Origen, x.Codigo, x.X.NombreOriginal, x.X.Descripcion, x.X.FechaRegistro, SigWeb.Data.Planes.AlmacenAnexos.EsImagen(x.X.RutaArchivo) ? "Foto" : "Anexo" }));
        for (int i = 0; i < arch.Count; i++)
            fs.Cell(i + 2, 3).SetHyperlink(new XLHyperlink(Url(arch[i].X.RutaArchivo)));

        if (d.Cambios.Count > 0)
        {
            var cs = wb.Worksheets.Add("Cambios");
            Tabla(cs, new[] { "Fecha", "Cambio", "Anterior", "Nuevo", "Motivo" }, d.Cambios.Select(c => new object?[] { c.Fecha, c.Campo, c.Anterior, c.Nuevo, c.Motivo }));
        }

        using var ms = new MemoryStream();
        wb.SaveAs(ms);
        return ms.ToArray();
    }

    private static void Tabla(IXLWorksheet ws, string[] cab, IEnumerable<object?[]> filas)
    {
        for (int c = 0; c < cab.Length; c++) ws.Cell(1, c + 1).Value = cab[c];
        int r = 2;
        foreach (var f in filas)
        {
            for (int c = 0; c < f.Length; c++)
            {
                var cel = ws.Cell(r, c + 1);
                switch (f[c])
                {
                    case null: break;
                    case DateTime dt: cel.Value = dt; cel.Style.DateFormat.Format = dt.TimeOfDay == TimeSpan.Zero ? "dd/mm/yyyy" : "dd/mm/yyyy hh:mm"; break;
                    case int n: cel.Value = n; break;
                    default: cel.Value = f[c]!.ToString(); break;
                }
            }
            r++;
        }
        var rango = ws.Range(1, 1, Math.Max(1, r - 1), cab.Length);
        if (r > 2) rango.CreateTable().Theme = XLTableTheme.TableStyleLight9;
        else ws.Row(1).Style.Font.SetBold();
        ws.SheetView.FreezeRows(1);
        ws.Columns(1, cab.Length).AdjustToContents(1, Math.Min(r, 200));
        foreach (var col in ws.Columns(1, cab.Length)) if (col.Width > 60) { col.Width = 60; col.Style.Alignment.SetWrapText(); }
        ws.Rows(2, Math.Max(2, r)).Style.Alignment.SetVertical(XLAlignmentVerticalValues.Top);
    }
}
