using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace SigWeb.Data.Seguridad;

public sealed partial class ServicioProyectos
{
    // Todas las transiciones de estado de Administración comparten el bloqueo, incluso entre instancias.
    public static async Task<IDbContextTransaction> BloquearCambiosAsync(SeguridadDbContext db)
    {
        var tx = await db.Database.BeginTransactionAsync();
        try
        {
            await db.Database.ExecuteSqlAsync($"SELECT pg_advisory_xact_lock({71823}, {9})");
            return tx;
        }
        catch
        {
            await tx.DisposeAsync();
            throw;
        }
    }

    public static async Task<string?> ImpedimentoEliminacionAsync(SeguridadDbContext db, Proyecto p, int? proyectoActual)
    {
        if (p.EsEmpresa) return "El proyecto Empresa está protegido.";
        if (p.Id == proyectoActual) return "Cambie a otro proyecto antes de eliminar el proyecto actual.";
        if (p.Estado == EstadoProyecto.Activo &&
            !await db.Proyectos.AnyAsync(x => x.Id != p.Id && !x.EsEmpresa && x.Estado == EstadoProyecto.Activo))
            return "Debe quedar al menos un proyecto activo además de Empresa.";
        return null;
    }

    public sealed record ImpactoProyecto(int Membresias, int UsuariosConAcceso, int Invitaciones, int InvitacionesVigentes,
        int ReglasAvisos, int ReglasActivas, IReadOnlyList<(string Nombre, long? Total)> Registros, long? Bytes);

    public async Task<ImpactoProyecto> ImpactoAsync(SeguridadDbContext central, Proyecto p)
    {
        var ahora = DateTime.UtcNow;
        var miembros = central.Miembros.Where(m => m.ProyectoId == p.Id);
        var invitaciones = central.Invitaciones.Where(i => i.ProyectoId == p.Id);
        var reglas = central.ReglasNotificacion.Where(r => r.ProyectoId == p.Id);
        var roles = from ur in central.UserRoles
                    join r in central.Roles on ur.RoleId equals r.Id
                    where r.Name == RolesFabrica.AdministradorGlobal ||
                          (p.Estado == EstadoProyecto.Activo && r.Name == RolesFabrica.AuditorGlobal)
                    select ur.UserId;
        var usuarios = await central.Users.CountAsync(u => u.Activo && (u.LockoutEnd == null || u.LockoutEnd <= ahora)
            && (roles.Contains(u.Id) || (p.Estado == EstadoProyecto.Activo &&
                miembros.Any(m => m.UsuarioId == u.Id && m.Activo && (m.Vence == null || m.Vence > ahora)))));

        var registros = new List<(string Nombre, long? Total)>();
        long? bytes = null;
        await using var db = CrearContexto(new ProyectoActivo(p.Id, p.Codigo, p.Nombre, p.BaseDatos,
            CadenaDe(p.CodigoConexion ?? p.Codigo, p.BaseDatos), p.EsEmpresa, p.EsPiloto, p.Color));
        db.Database.SetCommandTimeout(3);
        using var plazo = new CancellationTokenSource(TimeSpan.FromSeconds(10));

        async Task<long?> Consultar(Func<Task<long>> consulta)
        {
            if (plazo.IsCancellationRequested) return null;
            try { return await consulta(); }
            catch (Exception ex)
            {
                _log.LogDebug(ex, "No se pudo consultar una cifra de impacto del proyecto {Id}", p.Id);
                return null;
            }
        }

        try { await db.Database.OpenConnectionAsync(plazo.Token); }
        catch (Exception ex)
        {
            _log.LogWarning(ex, "Sin acceso a la base para calcular el impacto del proyecto {Id}", p.Id);
            plazo.Cancel();
        }

        async Task Contar<T>(string nombre, DbSet<T> tabla) where T : class =>
            registros.Add((nombre, await Consultar(() => tabla.IgnoreQueryFilters().LongCountAsync(plazo.Token))));

        await Contar("Torres", db.ElementosTrazado);
        await Contar("Planes de acción", db.PlanesAccion);
        await Contar("Documentos maestros", db.DocumentosMaestro);
        await Contar("Resultados de ensayos", db.ResultadosEnsayo);
        await Contar("Planos maestros", db.PlanosMaestro);
        await Contar("Equipos", db.Equipos);
        await Contar("Informes diarios", db.InformesDiarios);
        await Contar("Auditorías", db.Auditorias);
        bytes = await Consultar(() => db.Database
            .SqlQuery<long>($"SELECT pg_database_size(current_database()) AS \"Value\"").SingleAsync(plazo.Token));

        return new(await miembros.CountAsync(), usuarios, await invitaciones.CountAsync(),
            await invitaciones.CountAsync(i => i.Estado == EstadoInvitacion.Pendiente && i.Vence > ahora),
            await reglas.CountAsync(), await reglas.CountAsync(r => r.Activa), registros, bytes);
    }
}

