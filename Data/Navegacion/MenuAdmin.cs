namespace SigWeb.Data.Navegacion;

// Menú de Administración agrupado (2026-11-18): pantallas agrupadas por módulo. Cada entrada es un módulo y sus
// pantallas son VISTAS (botones `pg-vistas` que EncabezadoPagina pinta solo en las rutas /admin…). Las rutas, permisos
// y enlaces de siempre no cambian: solo cómo se llega. Pantalla nueva de Administración = una vista en su grupo.
public static class MenuAdmin
{
    public sealed record Vista(string Ruta, string Texto, string Icono, bool SoloAdmin);
    public sealed record Grupo(string Texto, string Titulo, string Trazo, IReadOnlyList<Vista> Vistas, string? Icono = null);

    public static readonly IReadOnlyList<Grupo> Grupos = new Grupo[]
    {
        new("Proyectos", "Proyectos: tablero, datos, módulos de cada proyecto y creador de módulos", "M3 21h18M5 21V8l7-5 7 5v13M9 21v-6h6v6", new Vista[]
        {
            new("admin", "Tablero de proyectos", "tablero", true),
            new("admin/proyectos", "Proyectos y módulos", "carpeta", true),
            new("admin/modulos", "Creador de módulos", "formularios", true),
        }),
        new("Usuarios y acceso", "Usuarios, invitaciones, solicitudes y roles",
            "M9 8a3 3 0 1 0 0-.01M3.5 19a5.5 5.5 0 0 1 11 0M17 11a2.3 2.3 0 1 0 0-.01M15.5 15.2A4.5 4.5 0 0 1 21 19", new Vista[]
        {
            new("admin/usuarios", "Usuarios y permisos", "personal", true),
            new("admin/invitaciones", "Invitaciones y solicitudes", "correo", true),
            new("admin/roles", "Roles", "candado", true),
        }),
        new("Notificaciones", "Avisos por correo", "M6 8a6 6 0 0 1 12 0c0 7 3 9 3 9H3s3-2 3-9M10.3 21a1.94 1.94 0 0 0 3.4 0", new Vista[]
        {
            new("admin/notificaciones", "Avisos por correo", "correo", true),
        }),
        new("Bot de Telegram", "Bot de Telegram", "", new Vista[]
        {
            new("admin/telegram", "Bot de Telegram", "telegram", true),
        }, Icono: "telegram"),
        new("Sistema y seguridad", "Servidor, bases, correo saliente, logo y auditoría de accesos", "M12 3 4 6v6c0 4.5 3.4 8.2 8 9 4.6-.8 8-4.5 8-9V6zM9 12l2 2 4-4", new Vista[]
        {
            new("admin/sistema", "Sistema y servidor", "control", true),
            new("admin/auditoria", "Auditoría de accesos", "seguimiento", false),
        }),
    };

    private static string Norm(string? ruta) => (ruta ?? "").Split('?', '#')[0].Trim('/').ToLowerInvariant();

    // Grupo al que pertenece una ruta ("admin/usuarios/5" → Usuarios y acceso). null = no es de Administración.
    public static Grupo? GrupoDe(string? ruta)
    {
        var r = Norm(ruta);
        if (r != "admin" && !r.StartsWith("admin/")) return null;
        return Grupos.FirstOrDefault(g => g.Vistas.Any(v => r == v.Ruta || (v.Ruta != "admin" && r.StartsWith(v.Ruta + "/"))));
    }

    public static Vista? VistaDe(string? ruta)
    {
        var r = Norm(ruta);
        return GrupoDe(r)?.Vistas.FirstOrDefault(v => r == v.Ruta || (v.Ruta != "admin" && r.StartsWith(v.Ruta + "/")));
    }

    public static IEnumerable<Vista> Visibles(Grupo g, bool esAdmin) => g.Vistas.Where(v => esAdmin || !v.SoloAdmin);
}
