using Microsoft.EntityFrameworkCore;
using SigWeb.Data.Organigrama;
using SigWeb.Models;

namespace SigWeb.Data.Demo;

public sealed class DemoOrganigrama : IModuloDemo
{
    public string Clave => "organigrama";
    public int Orden => 12;

    public async Task SembrarAsync(ContextoDemo c, CancellationToken ct)
    {
        var db = c.Db;
        if (await db.VersionesOrganigrama.IgnoreQueryFilters().AnyAsync(ct)) return;

        var version = new VersionOrganigrama
        {
            Numero = 1, Titulo = $"Organigrama de {c.NombreProyecto}",
            Alcance = c.Indice == 1 ? "Montaje y tendido de la línea de 500 kV" : "Cimentación y montaje de la línea de 230 kV",
            Origen = "Demostración",
        };
        db.VersionesOrganigrama.Add(version);
        await db.SaveChangesAsync(ct);

        var director = new NodoOrg { Nombre = "Director de proyecto", Area = "Dirección", Plazas = 1 };
        var residente = new NodoOrg { Nombre = "Residente de obra", Area = "Construcción", Jefe = director.Clave, Orden = 1, Plazas = 1 };
        await ServicioOrganigrama.GuardarAsync(db, version.Id, new NodoOrg[]
        {
            director, residente,
            new() { Nombre = "Coordinador SST", Area = "SST", Jefe = director.Clave, Staff = true, Orden = 2, Plazas = 1 },
            new() { Nombre = "Inspector de calidad", Area = "Calidad", Jefe = residente.Clave, Orden = 3, Plazas = 1 },
            new() { Nombre = "Topógrafo", Area = "Topografía", Jefe = residente.Clave, Orden = 4, Plazas = c.Indice == 1 ? 2 : 1 },
        });
        var inicio = new DateTime(2026, c.Indice == 1 ? 4 : 2, 1);
        await ServicioOrganigrama.PublicarAsync(db, version.Id, new DatosPublicacion
        {
            VigenteDesde = inicio, FechaAprobacion = inicio, Motivo = "Estructura inicial del equipo de obra",
            Elaboro = "Planeación de obra", Reviso = "Coordinación SGI", Aprobo = "Gerencia de operaciones",
        });
    }
}
