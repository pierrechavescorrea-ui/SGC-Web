using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace SigWeb.Data.Seguridad;

// Control de escritura en el servidor (no depende de que la pantalla oculte botones):
// antes de guardar, cada cambio se compara con los permisos del usuario en el módulo de la pantalla.
//   alta → Crear, Colaborar o Importar · cambio → Editar (o Aprobar/Administrar) · Eliminado = true → Eliminar ·
//   borrado físico → Editar o Eliminar · Campos/formatos → Configurar campos · diseños → Editar formularios ·
//   vistas → Editar vistas.
// Seguridad:ControlEscritura = "Bloquear" (por defecto) | "Registrar" (solo anota en la auditoría) | "Apagado".
public sealed class InterceptorEscritura : SaveChangesInterceptor
{
    private readonly IConfiguration _cfg;
    private readonly AuditoriaAccesos _auditoria;
    private readonly ILogger<InterceptorEscritura> _log;

    public InterceptorEscritura(IConfiguration cfg, AuditoriaAccesos auditoria, ILogger<InterceptorEscritura> log)
    {
        _cfg = cfg; _auditoria = auditoria; _log = log;
    }

    // Escrituras internas que la app hace sola al pintar (no son decisiones del usuario).
    private static readonly HashSet<string> Internas = new(StringComparer.Ordinal)
    {
        "EstructuraFormulario",   // el formulario registra su estructura al pintarse
        "UsuarioBot",             // último uso del bot
    };

    private static readonly Dictionary<string, Accion> PorTipo = new(StringComparer.Ordinal)
    {
        ["CampoTabla"] = Accion.ConfigurarCampos,
        ["FormatoCondicional"] = Accion.ConfigurarCampos,
        ["DisenoFormulario"] = Accion.EditarFormularios,
        ["VistaRegistro"] = Accion.EditarVistas,
    };

    public override InterceptionResult<int> SavingChanges(DbContextEventData e, InterceptionResult<int> r)
    {
        Revisar(e.Context);
        return r;
    }

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData e, InterceptionResult<int> r, CancellationToken ct = default)
    {
        Revisar(e.Context);
        return ValueTask.FromResult(r);
    }

    private void Revisar(DbContext? ctx)
    {
        var s = AmbienteSig.Actual;
        if (ctx is null || s is null || s.Sistema) return;
        // "Ver como otro usuario" es solo para revisar permisos: nunca escribe.
        if (s.Impersonador is not null && ctx.ChangeTracker.Entries().Any(en => en.State is not (EntityState.Unchanged or EntityState.Detached)
                && !Internas.Contains(en.Metadata.ClrType.Name)))
        {
            _auditoria.Registrar("Escritura sin permiso", false, "Intento de cambio en modo «ver como otro usuario».", s);
            throw new PermisoDenegadoException("Está viendo la aplicación como otro usuario: modo solo lectura.");
        }
        if (s.Permisos.AdminGlobal) return;
        var modo = _cfg["Seguridad:ControlEscritura"] ?? "Bloquear";
        if (modo.Equals("Apagado", StringComparison.OrdinalIgnoreCase)) return;

        var modulo = s.Modulo ?? "inicio";
        var tiene = s.Permisos.De(modulo);
        foreach (var en in ctx.ChangeTracker.Entries())
        {
            if (en.State is EntityState.Unchanged or EntityState.Detached) continue;
            var tipo = en.Metadata.ClrType.Name;
            if (Internas.Contains(tipo)) continue;
            var requiere = Requiere(en, tipo);
            if ((tiene & requiere) != 0) continue;

            var detalle = $"{modulo}: {en.State} {tipo} sin permiso ({Acciones.Texto(requiere)}).";
            _auditoria.Registrar("Escritura sin permiso", false, detalle, s);
            if (modo.Equals("Registrar", StringComparison.OrdinalIgnoreCase)) { _log.LogWarning("{Detalle}", detalle); continue; }
            throw new PermisoDenegadoException("No tiene permiso para hacer este cambio en " +
                (CatalogoModulos.De(modulo)?.Nombre ?? modulo) + ".");
        }
    }

    private static Accion Requiere(EntityEntry en, string tipo)
    {
        if (PorTipo.TryGetValue(tipo, out var especial)) return especial | Accion.Administrar;
        switch (en.State)
        {
            case EntityState.Added:
                return Accion.Crear | Accion.Comentar | Accion.Importar | Accion.Editar | Accion.Administrar;
            case EntityState.Deleted:
                return Accion.Editar | Accion.Eliminar | Accion.Administrar;
            default:
                var eliminado = en.Metadata.FindProperty("Eliminado");
                if (eliminado is not null)
                {
                    var p = en.Property("Eliminado");
                    if (p.IsModified && !Equals(p.OriginalValue, p.CurrentValue))
                        return (p.CurrentValue as bool? == true ? Accion.Eliminar : Accion.Administrar) | Accion.Administrar;
                }
                return Accion.Editar | Accion.Aprobar | Accion.Administrar;
        }
    }
}
