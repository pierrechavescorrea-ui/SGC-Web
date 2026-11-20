using Microsoft.EntityFrameworkCore;
using SigWeb.Models;

namespace SigWeb.Data.Demo;

public sealed class DemoPapelera : IModuloDemo
{
    public string Clave => "papelera";
    public bool EnEmpresa => true;
    public int Orden => 23;

    public async Task SembrarAsync(ContextoDemo c, CancellationToken ct)
    {
        var db = c.Db;
        const string ejemplo = "Borrador de demostración retirado antes de emitir el informe.";
        if (await db.InformesDiarios.IgnoreQueryFilters().AnyAsync(x => x.Eliminado || x.Observaciones == ejemplo, ct)) return;

        var fecha = c.Hoy.AddDays(-3);
        while (await db.InformesDiarios.IgnoreQueryFilters().AnyAsync(x => x.Fecha == fecha, ct))
            fecha = fecha.AddDays(-1);

        db.InformesDiarios.Add(new InformeDiario
        {
            Fecha = fecha, ElaboradoPor = c.Indice == -1 ? "Coordinación de construcción" : $"Residente de {c.NombreProyecto}",
            ClimaManana = "Nublado", Observaciones = ejemplo, Eliminado = true, FechaEliminacion = DateTime.Now,
        });
        await db.SaveChangesAsync(ct);
    }
}
