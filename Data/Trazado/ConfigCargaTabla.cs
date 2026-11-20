using System.Text.Json;

namespace SigWeb.Data.Trazado;

// Configuración con la que se cargó una revisión de la tabla de torres (hoja, filas de encabezado, destino de cada
// columna, sistema de coordenadas y observaciones por torre). Se guarda junto a la copia del Excel
// (uploads/tabla-torres/{archivo}.mapeo.json) para poder "Corregir columnas": deshacer la revisión y reabrirla igual.
public sealed class ConfigCargaTabla
{
    public string? Hoja { get; set; }
    public int FilasEncabezado { get; set; } = 1;
    public string? Sistema { get; set; }
    public List<Columna> Columnas { get; set; } = new();
    public Dictionary<string, string> Observaciones { get; set; } = new();

    public sealed class Columna
    {
        public int Indice { get; set; }
        public string Destino { get; set; } = "Ignorar";
        public string? NombreCampo { get; set; }
        public string? PataLetra { get; set; }
        public string? EjeCoordenada { get; set; }
    }

    private static string RutaDe(string rutaExcel) => rutaExcel + ".mapeo.json";

    public static void Guardar(string rutaExcel, ConfigCargaTabla config)
    {
        try { File.WriteAllText(RutaDe(rutaExcel), JsonSerializer.Serialize(config)); }
        catch (Exception ex) { Console.Error.WriteLine("Tabla de torres: no se pudo guardar la configuración de columnas: " + ex.Message); }
    }

    public static ConfigCargaTabla? Leer(string rutaExcel)
    {
        try { var r = RutaDe(rutaExcel); return File.Exists(r) ? JsonSerializer.Deserialize<ConfigCargaTabla>(File.ReadAllText(r)) : null; }
        catch { return null; }
    }
}
