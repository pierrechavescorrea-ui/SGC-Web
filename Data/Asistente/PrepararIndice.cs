namespace SigWeb.Data.Asistente;

internal sealed class PrepararIndice(ManualAyuda manual) : IHostedService
{
    public Task StartAsync(CancellationToken cancellationToken)
    {
        _ = manual.Capitulos;
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
