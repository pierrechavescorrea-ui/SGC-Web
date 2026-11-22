namespace SigWeb.Data.Dossier;

// Estructura final del ZIP a partir de la solicitud: qué archivo va en qué ruta y dónde va cada tabla.
// La usan la vista previa de la pantalla y el generador, así lo que se ve es lo que se descarga.
public sealed class PlanDossier
{
    public const string NombreIndice = "00 Índice.xlsx", NombreLibroUnico = "00 Tablas.xlsx";

    public sealed record Entrada(string RutaZip, ArchivoDossier Archivo, string Modulo, int ElementoId, string Elemento, Func<string, byte[]>? Crear = null);

    public sealed class Tabla
    {
        public required FuenteCargada Fuente { get; init; }
        public required string Hoja { get; init; }
        public string? RutaZip { get; init; }                     // null = va en el libro único
        public List<int> Ids { get; } = new();
        public List<(string Nombre, HojaDossier Datos, List<FilaDossier> Filas)> Adicionales { get; } = new();
        // elemento → carpeta de sus archivos, relativa al Excel (para el vínculo de la tabla)
        public Dictionary<int, string> Vinculos { get; } = new();
    }

    public List<Entrada> Archivos { get; } = new();
    public List<Tabla> Tablas { get; } = new();
    public string? RutaLibroUnico { get; private set; }
    public bool ConIndice { get; private set; }

    public static PlanDossier Armar(SolicitudDossier s, IReadOnlyDictionary<string, FuenteCargada> fuentes)
    {
        var plan = new PlanDossier();
        var nombres = new NombresZip();
        plan.ConIndice = true;
        nombres.Unico("", NombreIndice, true);
        var modulos = s.Modulos.Where(m => (m.Tabla || m.Adjuntos) && m.Elementos.Count > 0 && fuentes.ContainsKey(m.Clave)).ToList();
        if (s.LibroUnico && modulos.Any(m => m.Tabla)) plan.RutaLibroUnico = nombres.Unico("", NombreLibroUnico, true);
        var hojas = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        for (var i = 0; i < modulos.Count; i++)
        {
            var m = modulos[i];
            var f = fuentes[m.Clave];
            var nombreMod = NombresZip.Limpiar(f.Fuente.Nombre, NombresZip.MaxCarpeta);
            var carpetaMod = nombres.Unico("", s.Numerar ? $"{i + 1:00} {nombreMod}" : nombreMod, false);
            var clases = new HashSet<string>(m.Clases, StringComparer.OrdinalIgnoreCase);
            var columnas = m.ColumnasNombre.Count > 0 ? m.ColumnasNombre : new List<string> { f.ColumnaNombre };

            Tabla? tabla = null;
            if (m.Tabla)
            {
                var hojasLibro = s.LibroUnico ? hojas : new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                var hoja = HojaUnica(hojasLibro, f.Fuente.Nombre);
                tabla = new Tabla
                {
                    Fuente = f, Hoja = hoja,
                    RutaZip = plan.RutaLibroUnico is null ? nombres.Unico(carpetaMod, NombresZip.LimpiarArchivo(f.Fuente.Nombre + ".xlsx"), true) : null
                };
                plan.Tablas.Add(tabla);
                var seleccionados = m.Elementos.ToHashSet();
                foreach (var adicional in f.Hojas)
                {
                    var filas = adicional.Filas.Where(r => seleccionados.Contains(r.ElementoId)).ToList();
                    if (filas.Count > 0)
                        tabla.Adicionales.Add((HojaUnica(hojasLibro, adicional.Nombre), adicional, filas));
                }
            }

            foreach (var id in m.Elementos)
            {
                var el = f.Elemento(id);
                if (el is null) continue;   // pasó a la Papelera después de elegirlo
                tabla?.Ids.Add(id);
                var archivos = m.Adjuntos
                    ? el.Archivos.Where(a => clases.Contains(a.Clase) && (!m.SoloActuales || a.Actual)).ToList()
                    : new List<ArchivoDossier>();
                if (archivos.Count == 0 && (tabla is null || el.ArchivosDeTabla.Count == 0)) continue;

                var nombreEl = NombreElemento(f, id, columnas);
                var carpetaGrupo = s.PorGrupo && f.NombreGrupo is not null
                    ? NombresZip.Unir(carpetaMod, NombresZip.Limpiar(string.IsNullOrWhiteSpace(el.Grupo) ? "Sin " + f.NombreGrupo.ToLowerInvariant() : el.Grupo, NombresZip.MaxCarpeta))
                    : carpetaMod;
                string carpetaEl, prefijo = "";
                if (s.CarpetaPorElemento) carpetaEl = nombres.Unico(carpetaGrupo, NombresZip.Limpiar(nombreEl, NombresZip.MaxCarpeta), false);
                else { carpetaEl = carpetaGrupo; prefijo = NombresZip.Limpiar(nombreEl, 40) + " - "; }

                foreach (var a in archivos)
                {
                    var sub = s.Subcarpetas
                        ? string.Join("/", (a.Carpeta ?? a.Clase).Split('/', StringSplitOptions.RemoveEmptyEntries).Select(x => NombresZip.Limpiar(x, NombresZip.MaxCarpeta)))
                        : "";
                    var dir = NombresZip.Unir(carpetaEl, sub);
                    var ruta = nombres.Unico(dir, NombresZip.LimpiarArchivo(prefijo + NombreArchivo(a)), true);
                    plan.Archivos.Add(new Entrada(ruta, a, f.Fuente.Nombre, id, nombreEl));
                }

                if (tabla is not null)
                    foreach (var generado in el.ArchivosDeTabla)
                    {
                        var ruta = nombres.Unico(carpetaEl, NombresZip.LimpiarArchivo(prefijo + generado.Nombre), true);
                        plan.Archivos.Add(new Entrada(ruta, new ArchivoDossier("Detalle", generado.Nombre, ""), f.Fuente.Nombre, id, nombreEl, generado.Crear));
                    }

                if (tabla is not null)
                {
                    // Excel en la carpeta del módulo → ruta relativa a ella; libro único → desde la raíz.
                    var rel = tabla.RutaZip is null ? carpetaEl
                        : carpetaEl.Length > carpetaMod.Length ? carpetaEl[(carpetaMod.Length + 1)..] : "";
                    if (rel.Length > 0 && s.CarpetaPorElemento) tabla.Vinculos[id] = rel;
                }
            }
        }
        return plan;
    }

    // Nombre de la carpeta del elemento: valor de 1 o 2 columnas de su tabla ("DIS-4025 - Planta").
    public static string NombreElemento(FuenteCargada f, int id, IEnumerable<string> columnas)
    {
        var partes = columnas.Select(c => f.Texto(id, c).Trim()).Where(t => t.Length > 0 && t != "#ERROR").Distinct().ToList();
        return partes.Count == 0 ? $"#{id}" : string.Join(" - ", partes);
    }

    // Nombre con extensión (si el nombre original no la trae, se toma de la ruta guardada). Enlaces → .url
    public static string NombreArchivo(ArchivoDossier a)
    {
        var n = string.IsNullOrWhiteSpace(a.Nombre) ? Path.GetFileName(a.Ruta.Split('?')[0]) : a.Nombre.Trim();
        if (a.Enlace) return n + ".url";
        if (Path.GetExtension(n).Length == 0) n += Path.GetExtension(a.Ruta.Split('?')[0]);
        return n;
    }

    private static string HojaUnica(HashSet<string> usadas, string nombre)
    {
        var b = new string(nombre.Where(ch => !char.IsControl(ch) && !"[]:*?/\\".Contains(ch)).ToArray()).Trim().Trim('\'');
        if (b.Length == 0) b = "Tabla";
        if (b.Length > 28) b = b[..28];
        var h = b;
        for (var i = 2; !usadas.Add(h); i++) h = $"{b} {i}";
        return h;
    }

    // Enlace a la nube que puede ir como acceso directo (.url): solo http/https.
    public static bool EnlaceValido(string ruta) =>
        Uri.TryCreate(ruta, UriKind.Absolute, out var u) && (u.Scheme == Uri.UriSchemeHttp || u.Scheme == Uri.UriSchemeHttps);

    // Ruta física de un adjunto guardado en uploads. Las rutas históricas también pueden venir codificadas.
    public static string? RutaFisica(string webRoot, string ruta)
    {
        var fisica = SigWeb.Data.Seguridad.ArchivosSeguros.RutaFisica(webRoot, ruta);
        if (fisica is null || File.Exists(fisica)) return fisica;
        var decodificada = SigWeb.Data.Seguridad.ArchivosSeguros.RutaFisica(webRoot, ruta, decodificar: true);
        return File.Exists(decodificada) ? decodificada : fisica;
    }
}
