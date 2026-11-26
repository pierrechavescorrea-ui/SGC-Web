namespace SigWeb.Data.Seguridad;

// Límite de trabajos pesados simultáneos en el servidor (auditoría 2026-11-22, S20): abrir libros Excel, leer planos
// DWG/DXF, leer IFC y generar plantillas consumen mucha memoria; varios a la vez pueden tumbar la instancia.
// Seguridad:TrabajosPesadosSimultaneos (2 por defecto) a la vez en toda la instancia; el siguiente espera hasta
// Seguridad:SegundosEsperaTrabajoPesado (60) y, si no hay turno, recibe un mensaje claro en vez de agotar la memoria.
public static class TrabajoPesado
{
    private static SemaphoreSlim _cupo = new(2, 2);
    private static TimeSpan _espera = TimeSpan.FromSeconds(60);

    public static void Configurar(IConfiguration cfg)
    {
        var n = Math.Clamp(cfg.GetValue("Seguridad:TrabajosPesadosSimultaneos", 2), 1, 64);
        _cupo = new SemaphoreSlim(n, n);
        _espera = TimeSpan.FromSeconds(Math.Clamp(cfg.GetValue("Seguridad:SegundosEsperaTrabajoPesado", 60), 1, 600));
    }

    public sealed class OcupadoException : InvalidOperationException
    {
        public OcupadoException() : base("El servidor está procesando otros archivos grandes. Intente de nuevo en un momento.") { }
    }

    private sealed class Turno : IDisposable
    {
        private SemaphoreSlim? _s;
        public Turno(SemaphoreSlim s) { _s = s; }
        public void Dispose() { Interlocked.Exchange(ref _s, null)?.Release(); }
    }

    public static IDisposable Entrar()
    {
        var s = _cupo;
        if (!s.Wait(_espera)) throw new OcupadoException();
        return new Turno(s);
    }

    public static async Task<IDisposable> EntrarAsync(CancellationToken ct = default)
    {
        var s = _cupo;
        if (!await s.WaitAsync(_espera, ct)) throw new OcupadoException();
        return new Turno(s);
    }

    public static T Ejecutar<T>(Func<T> trabajo) { using (Entrar()) return trabajo(); }
}
