using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;

namespace SigWeb.Data.Seguridad;

public sealed class InicioSesionSig(UserManager<UsuarioSig> usuarios, IHttpContextAccessor contexto,
    IUserClaimsPrincipalFactory<UsuarioSig> claims, IOptions<IdentityOptions> opciones,
    ILogger<SignInManager<UsuarioSig>> log, IAuthenticationSchemeProvider esquemas,
    IUserConfirmation<UsuarioSig> confirmacion, IConfiguration cfg)
    : SignInManager<UsuarioSig>(usuarios, contexto, claims, opciones, log, esquemas, confirmacion)
{
    public override async Task<bool> IsTwoFactorClientRememberedAsync(UsuarioSig usuario) =>
        !(cfg.GetValue<bool>("Seguridad:MfaObligatoriaAdministradores") &&
          await UserManager.IsInRoleAsync(usuario, RolesFabrica.AdministradorGlobal)) &&
        await base.IsTwoFactorClientRememberedAsync(usuario);

    // Los caracteres de control pueden cambiar cómo el navegador interpreta el destino.
    public static string DestinoLocal(string? ruta) =>
        ruta is { Length: > 0 } && ruta[0] == '/' &&
        (ruta.Length == 1 || ruta[1] is not ('/' or '\\')) && !ruta.Any(char.IsControl) ? ruta : "/";
}

// Claims de la cookie: nombre visible y proyecto activo (además de id, correo, roles y sello de seguridad).
public sealed class FabricaClaimsSig : UserClaimsPrincipalFactory<UsuarioSig, IdentityRole>
{
    public FabricaClaimsSig(UserManager<UsuarioSig> um, RoleManager<IdentityRole> rm, IOptions<IdentityOptions> o) : base(um, rm, o) { }

    protected override async Task<ClaimsIdentity> GenerateClaimsAsync(UsuarioSig user)
    {
        var id = await base.GenerateClaimsAsync(user);
        id.AddClaim(new Claim(ClaimsSig.Nombre, string.IsNullOrWhiteSpace(user.NombreCompleto) ? user.Email ?? "" : user.NombreCompleto));
        if (user.ProyectoActivoId is int p) id.AddClaim(new Claim(ClaimsSig.Proyecto, p.ToString()));
        if (user.FotoVersion is long f) id.AddClaim(new Claim(ClaimsSig.Foto, f.ToString()));
        return id;
    }
}

// Contraseñas: largo antes que símbolos (NIST SP 800-63B §5.1.1.2): mínimo 12, no puede ser una de las más
// comunes ni contener el correo o el nombre.
public sealed class ValidadorContrasenaSig : IPasswordValidator<UsuarioSig>
{
    private static readonly HashSet<string> Comunes = new(StringComparer.OrdinalIgnoreCase)
    {
        "123456789012", "1234567890123", "contraseña123", "contrasena123", "password1234", "password12345", "qwertyuiop12",
        "administrador", "administrador1", "sigweb123456", "empresa12345", "colombia1234", "bogota123456", "123456789abc",
        "abc123456789", "qwerty123456", "iloveyou1234", "passw0rd1234", "1q2w3e4r5t6y", "000000000000", "111111111111",
        "aaaaaaaaaaaa", "zaq12wsxcde3", "torres123456", "proyecto1234", "ingenieros12", "bienvenido12", "bienvenido123",
    };

    public Task<IdentityResult> ValidateAsync(UserManager<UsuarioSig> manager, UsuarioSig user, string? password)
    {
        var errores = new List<IdentityError>();
        var p = password ?? "";
        if (Comunes.Contains(p) || p.Distinct().Count() <= 3)
            errores.Add(new IdentityError { Code = "Comun", Description = "Esa contraseña es demasiado común. Use una frase larga que solo usted conozca." });
        var local = (user.Email ?? "").Split('@')[0];
        if (local.Length >= 4 && p.Contains(local, StringComparison.OrdinalIgnoreCase))
            errores.Add(new IdentityError { Code = "Correo", Description = "La contraseña no puede contener su correo." });
        foreach (var parte in (user.NombreCompleto ?? "").Split(' ', StringSplitOptions.RemoveEmptyEntries).Where(x => x.Length >= 4))
            if (p.Contains(parte, StringComparison.OrdinalIgnoreCase))
            {
                errores.Add(new IdentityError { Code = "Nombre", Description = "La contraseña no puede contener su nombre." });
                break;
            }
        return Task.FromResult(errores.Count == 0 ? IdentityResult.Success : IdentityResult.Failed(errores.ToArray()));
    }
}

// Mensajes de Identity en español (los que ve el usuario).
public sealed class ErroresIdentidadEs : IdentityErrorDescriber
{
    public override IdentityError PasswordTooShort(int length) => new() { Code = nameof(PasswordTooShort), Description = $"La contraseña debe tener al menos {length} caracteres." };
    public override IdentityError PasswordRequiresUniqueChars(int n) => new() { Code = nameof(PasswordRequiresUniqueChars), Description = $"La contraseña debe tener al menos {n} caracteres distintos." };
    public override IdentityError PasswordRequiresDigit() => new() { Code = nameof(PasswordRequiresDigit), Description = "La contraseña debe tener al menos un número." };
    public override IdentityError PasswordRequiresLower() => new() { Code = nameof(PasswordRequiresLower), Description = "La contraseña debe tener al menos una minúscula." };
    public override IdentityError PasswordRequiresUpper() => new() { Code = nameof(PasswordRequiresUpper), Description = "La contraseña debe tener al menos una mayúscula." };
    public override IdentityError PasswordRequiresNonAlphanumeric() => new() { Code = nameof(PasswordRequiresNonAlphanumeric), Description = "La contraseña debe tener al menos un símbolo." };
    public override IdentityError DuplicateEmail(string email) => new() { Code = nameof(DuplicateEmail), Description = "Ya existe una cuenta con ese correo." };
    public override IdentityError DuplicateUserName(string userName) => new() { Code = nameof(DuplicateUserName), Description = "Ya existe una cuenta con ese correo." };
    public override IdentityError InvalidEmail(string? email) => new() { Code = nameof(InvalidEmail), Description = "Correo no válido." };
    public override IdentityError InvalidToken() => new() { Code = nameof(InvalidToken), Description = "El enlace no es válido o ya venció." };
    public override IdentityError PasswordMismatch() => new() { Code = nameof(PasswordMismatch), Description = "La contraseña actual no es correcta." };
    public override IdentityError UserLockoutNotEnabled() => new() { Code = nameof(UserLockoutNotEnabled), Description = "El bloqueo no está habilitado para esta cuenta." };
}

// Versión de la política de tratamiento de datos que el usuario autoriza al crear su cuenta.
public static class Privacidad
{
    public const string Version = "2026-11";
}
