using System.Collections.Concurrent;
using System.Security.Cryptography;

namespace SigWeb.Data.Trazado;

// Archivo ya leído en memoria (Trazado › Bases de datos). Se lee UNA vez al elegirlo, mientras el InputFile sigue en
// pantalla (un IBrowserFile no se puede leer después de que su <InputFile> desaparece), y se entrega al flujo de carga.
public sealed record ArchivoCarga(string Nombre, byte[] Bytes)
{
    public string Extension => Path.GetExtension(Nombre).ToLowerInvariant();
    public MemoryStream Abrir() => new(Bytes, writable: false);
}

// Qué se va a cargar en Trazado › Bases de datos.
public enum TipoCargaTrazado { TrazadoKmz, TablaTorres, ElementosCad, InspeccionCad }

// Entrega un archivo ya leído a otra pantalla (p. ej. Bases de datos → Tabla de torres) sin volver a elegirlo.
// Vence a los 15 minutos.
public sealed class AlmacenArchivosCarga
{
    private readonly ConcurrentDictionary<string, (ArchivoCarga Archivo, DateTime Vence)> _archivos = new();

    public string Guardar(ArchivoCarga archivo)
    {
        foreach (var k in _archivos.Where(k => k.Value.Vence < DateTime.Now).Select(k => k.Key).ToList()) _archivos.TryRemove(k, out _);
        var token = Convert.ToHexString(RandomNumberGenerator.GetBytes(12)).ToLowerInvariant();
        _archivos[token] = (archivo, DateTime.Now.AddMinutes(15));
        return token;
    }

    // Sin consumirlo: la página se inicializa dos veces (pre-render e interactiva) y ambas deben verlo. Vence solo.
    public ArchivoCarga? Ver(string? token) =>
        token is not null && _archivos.TryGetValue(token, out var a) && a.Vence >= DateTime.Now ? a.Archivo : null;
}
