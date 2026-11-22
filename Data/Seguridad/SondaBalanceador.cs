using System.Net;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;

namespace SigWeb.Data.Seguridad;

internal sealed class SondaBalanceador : IStartupFilter
{
    public Action<IApplicationBuilder> Configure(Action<IApplicationBuilder> next) => app =>
    {
        // El filtro de hosts sigue aplicando a toda ruta distinta de la sonda.
        app.UseWhen(ctx => HttpMethods.IsGet(ctx.Request.Method)
            && ctx.Request.Path == "/salud"
            && IPAddress.TryParse(ctx.Request.Host.Host.Trim('[', ']'), out var destino)
            && destino.MapToIPv6().Equals(ctx.Connection.LocalIpAddress?.MapToIPv6()),
            rama => rama.UseHealthChecks("/salud", new HealthCheckOptions { Predicate = _ => false }));
        next(app);
    };
}
