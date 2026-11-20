namespace SigWeb.Data.Navegacion;

// Menú del ícono del encabezado (EncabezadoPagina): al hacer clic en el ícono que acompaña al título se despliega una lista
// de opciones que la página registra aquí — las VISTAS del módulo (VistaWidgets, Planes de acción) o la lista de PROYECTOS
// (tablero del administrador). Uno por circuito (scoped): la página lo registra al cargar y lo quita al salir (Dispose).
public sealed class MenuIcono
{
    public sealed record Opcion(string Clave, string Nombre, string? Detalle = null, bool Destacada = false, string? Color = null, string? Marca = null, bool EsEmpresa = false);

    public sealed class Menu
    {
        public string Titulo { get; init; } = "";                 // "Vista" / "Proyecto"
        public IReadOnlyList<Opcion> Opciones { get; init; } = Array.Empty<Opcion>();
        public string? Actual { get; init; }                      // Clave de la opción elegida
        public Func<string, Task> Elegir { get; init; } = _ => Task.CompletedTask;
        public string? EnlaceGestion { get; init; }               // p. ej. editor?pestana=vistas
        public string? TituloGestion { get; init; }
        public object Dueno { get; init; } = new();               // quien lo registró (solo él lo quita)
    }

    public Menu? Actual { get; private set; }
    public event Action? Cambio;

    public void Registrar(Menu m) { Actual = m; Cambio?.Invoke(); }

    public void Quitar(object dueno)
    {
        if (Actual is null || !ReferenceEquals(Actual.Dueno, dueno)) return;
        Actual = null;
        Cambio?.Invoke();
    }
}
