using System.Collections.Concurrent;
using System.Security.Cryptography;
using Microsoft.AspNetCore.WebUtilities;

namespace SigWeb.Data.Seguridad;

// Atajo para las pantallas: quién es, en qué proyecto está y qué puede hacer (lee el AmbienteSig vigente).
public sealed class UsuarioActual
{
    public SesionSig? Sesion => AmbienteSig.Actual;
    public bool Autenticado => Sesion?.UsuarioId is not null;
    public string? Id => Sesion?.UsuarioId;
    public string Nombre => Sesion?.Usuario?.FindFirst(ClaimsSig.Nombre)?.Value ?? Sesion?.Email ?? "";
    public string? Email => Sesion?.Email;
    public string? FotoUrl => Sesion?.Usuario?.FindFirst(ClaimsSig.Foto)?.Value is string v && Id is not null ? $"cuenta/foto/{Id}?v={v}" : null;
    public ProyectoActivo? Proyecto => Sesion?.Proyecto;
    public PermisosEfectivos Permisos => Sesion?.Permisos ?? PermisosEfectivos.Ninguno;
    public bool EsAdministrador => Permisos.AdminGlobal;
    public bool EsAuditor => Permisos.AuditorGlobal;
    public bool VeComoOtro => Sesion?.Impersonador is not null;
    public bool Desarrollo => Sesion?.Usuario?.HasClaim(c => c.Type == ClaimsSig.Desarrollo) == true;

    public bool Puede(string modulo, Accion accion) => Sesion is { } s && (s.Sistema || s.Permisos.Puede(modulo, accion));
    public bool PuedeAqui(Accion accion) => Puede(Sesion?.Modulo ?? "inicio", accion);
}

// "Ver como otro usuario" (para revisar permisos): la pantalla de administración pide un token de un solo uso
// y el navegador lo canjea por HTTP (la cookie solo se puede cambiar fuera del circuito). Vence en 1 minuto.
public sealed class VerComoTokens
{
    private readonly ConcurrentDictionary<string, (string Admin, string Objetivo, DateTime Vence)> _t = new();

    public string Crear(string adminId, string objetivoId)
    {
        foreach (var k in _t.Where(k => k.Value.Vence < DateTime.UtcNow).Select(k => k.Key).ToList()) _t.TryRemove(k, out _);
        var token = WebEncoders.Base64UrlEncode(RandomNumberGenerator.GetBytes(24));
        _t[token] = (adminId, objetivoId, DateTime.UtcNow.AddMinutes(1));
        return token;
    }

    public (string Admin, string Objetivo)? Canjear(string? token) =>
        token is not null && _t.TryRemove(token, out var v) && v.Vence >= DateTime.UtcNow ? (v.Admin, v.Objetivo) : null;
}
