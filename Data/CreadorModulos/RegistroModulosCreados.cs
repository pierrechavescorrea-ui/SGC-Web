using Microsoft.EntityFrameworkCore;
using SigWeb.Data.Seguridad;

namespace SigWeb.Data.CreadorModulos;

// Caché (singleton) de los módulos creados PUBLICADOS: los suma a CatalogoModulos (permisos, roles, guarda de rutas,
// habilitación por proyecto) y los entrega al menú y a la página /mod/{clave}. Se recarga al publicar/archivar.
// Si la base central aún no tiene las tablas (migración pendiente) o falla, la app sigue igual sin módulos creados.
public sealed class RegistroModulosCreados
{
    public sealed record Publicado(int Id, string Clave, string Nombre, string? Descripcion, string Icono, string Grupo,
        AmbitoModulo Ambito, int Version, int Orden, DefinicionModulo Definicion)
    {
        public string ClavePermiso => CatalogoModulos.PrefijoCreado + Clave;
        public string Ruta => "mod/" + Clave;
        public string Tabla => "mod:" + Clave;
    }

    private readonly IServiceScopeFactory _scopes;
    private readonly ILogger<RegistroModulosCreados> _log;
    private volatile IReadOnlyList<Publicado> _todos = Array.Empty<Publicado>();

    public RegistroModulosCreados(IServiceScopeFactory scopes, ILogger<RegistroModulosCreados> log) { _scopes = scopes; _log = log; }

    public event Action? Cambio;

    public IReadOnlyList<Publicado> Todos => _todos;
    public Publicado? De(string? clave) => clave is null ? null : _todos.FirstOrDefault(m => string.Equals(m.Clave, clave, StringComparison.OrdinalIgnoreCase));
    public IEnumerable<Publicado> DelGrupo(string grupo) => _todos.Where(m => m.Grupo == grupo).OrderBy(m => m.Orden).ThenBy(m => m.Nombre);

    public async Task RecargarAsync()
    {
        try
        {
            using var scope = _scopes.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<SeguridadDbContext>();
            var filas = await db.ModulosCreados.AsNoTracking()
                .Where(m => m.Estado == EstadoModuloCreado.Publicado && m.DefinicionJson != null)
                .OrderBy(m => m.Grupo).ThenBy(m => m.Orden).ThenBy(m => m.Nombre)
                .ToListAsync();
            var lista = filas.Select(m => new Publicado(m.Id, m.Clave, m.Nombre, m.Descripcion, m.Icono, m.Grupo, m.Ambito, m.Version, m.Orden,
                DefinicionModulo.Leer(m.DefinicionJson))).ToList();
            _todos = lista;
            CatalogoModulos.FijarCreados(lista.Select(m => new CatalogoModulos.Def(m.ClavePermiso, m.Nombre, m.Grupo, m.Ambito,
                EnEmpresa: m.Ambito != AmbitoModulo.Proyecto, EnProyecto: m.Ambito != AmbitoModulo.Empresa,
                Configuracion: m.Grupo == "Configuración")));
            scope.ServiceProvider.GetService<ServicioPermisos>()?.Invalidar();
        }
        catch (Exception ex)
        {
            _log.LogWarning(ex, "Creador de módulos: no se pudieron leer los módulos creados (¿falta la migración de la base central?).");
        }
        Cambio?.Invoke();
    }
}
