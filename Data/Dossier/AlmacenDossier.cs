using System.Collections.Concurrent;
using SigWeb.Data.Seguridad;

namespace SigWeb.Data.Dossier;

// La pantalla (circuito Blazor) deja aquí la solicitud y el navegador la descarga por
// GET /api/dossier/{token}: así el ZIP sale por HTTP normal (streaming, barra de descarga del
// navegador, cancelable) y no por el circuito, que se colgaría con archivos grandes.
public sealed class AlmacenDossier
{
    private static readonly TimeSpan Vigencia = TimeSpan.FromMinutes(30);
    // Cada solicitud queda atada al usuario y al proyecto que la pidió: otro usuario con el enlace no la descarga.
    private readonly ConcurrentDictionary<string, (SolicitudDossier S, DateTime Vence, string? Usuario, int? Proyecto)> pendientes = new();

    public string Guardar(SolicitudDossier s)
    {
        var ahora = DateTime.UtcNow;
        foreach (var kv in pendientes) if (kv.Value.Vence < ahora) pendientes.TryRemove(kv.Key, out _);
        var token = Convert.ToHexString(System.Security.Cryptography.RandomNumberGenerator.GetBytes(16));
        pendientes[token] = (s, ahora + Vigencia, SigWeb.Data.Seguridad.AmbienteSig.Actual?.UsuarioId, SigWeb.Data.Seguridad.AmbienteSig.ProyectoId);
        return token;
    }

    // No se borra al usarla: el navegador puede reintentar la descarga mientras siga vigente.
    public SolicitudDossier? Obtener(string token)
    {
        var sesion = AmbienteSig.Actual;
        if (sesion is null || !pendientes.TryGetValue(token, out var p) || p.Vence < DateTime.UtcNow
            || p.Usuario != sesion.UsuarioId || p.Proyecto != AmbienteSig.ProyectoId)
            return null;

        bool Puede(string modulo, Accion accion) => sesion.Sistema || sesion.Permisos.Puede(modulo, accion);
        var modulos = p.S.Modulos.Where(m => FuentesDossier.Permitida(m.Clave, Accion.Descargar, Puede)).ToList();
        return modulos.Count == 0 ? null : p.S with { Modulos = modulos };
    }
}
