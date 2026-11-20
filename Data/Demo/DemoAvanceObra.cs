using Microsoft.EntityFrameworkCore;
using SigWeb.Data.AvanceObra;
using SigWeb.Models;

namespace SigWeb.Data.Demo;

public sealed class DemoAvanceObra : IModuloDemo
{
    public string Clave => "avance-obra";
    public int Orden => 21;

    public async Task SembrarAsync(ContextoDemo c, CancellationToken ct)
    {
        var db = c.Db;
        if (await db.InformesDiarios.IgnoreQueryFilters().AnyAsync(ct)) return;

        await using var transaccion = await db.Database.BeginTransactionAsync(ct);
        SembradorActividadesObra.SembrarSiVacio(db);
        SembradorActividadesObra.SembrarTendidoSiFalta(db);
        var actividades = await db.ActividadesObra.Where(x => x.Activo).OrderBy(x => x.Orden).ToListAsync(ct);
        int Actividad(EfectoAvance3D efecto) => actividades.First(x => x.Efecto == efecto).Id;

        var torres = c.Torres().Take(5).ToList();
        var patas = await db.Componentes.Where(x => torres.Contains(x.ElementoTrazadoId) && x.Nombre == "Pata A")
            .ToDictionaryAsync(x => x.ElementoTrazadoId, x => x.Id, ct);
        var tiroId = await db.Tiros.OrderBy(x => x.Orden).ThenBy(x => x.Id).Select(x => x.Id).FirstAsync(ct);
        var frente = c.Indice == 1 ? "sur" : "norte";
        var civil = new InformeDiario
        {
            Fecha = c.Hoy.AddDays(-2), ClimaManana = "Nublado", ClimaTarde = "Lluvia ligera", HorasLluvia = 1,
            ElaboradoPor = $"Residente de obra civil · frente {frente}", RevisadoPor = "Coordinación de construcción",
            Observaciones = "Excavación manual y vaciado de concretos según liberación de interventoría.",
        };
        var montaje = new InformeDiario
        {
            Fecha = c.Hoy.AddDays(-1), ClimaManana = "Despejado", ClimaTarde = "Nublado", HorasLluvia = 0,
            ElaboradoPor = $"Residente electromecánico · frente {frente}", RevisadoPor = "Coordinación de construcción",
            Observaciones = "Izado de cuerpos de torre y alistamiento de poleas en el tramo liberado.",
        };
        db.InformesDiarios.AddRange(civil, montaje);
        await db.SaveChangesAsync(ct);

        db.RegistrosAvance.AddRange(
            Registro(civil, torres[0], patas[torres[0]], EfectoAvance3D.Excavacion, 12.4m, 100, "Excavación recibida a cota de diseño."),
            Registro(civil, torres[1], patas[torres[1]], EfectoAvance3D.Concreto, 7.8m, 100, "Vaciado terminado. Se tomaron cilindros para ensayo."),
            Registro(montaje, torres[2], null, EfectoAvance3D.Montaje, 4.5m, 45, "Primeros cuerpos izados. Pendiente control de torque."),
            Registro(montaje, torres[3], null, EfectoAvance3D.Montaje, 6m, 60, "Montaje parcial del cuerpo y extensiones."));
        db.RegistrosAvanceTiro.Add(new RegistroAvanceTiro
        {
            InformeDiarioId = montaje.Id, Fecha = montaje.Fecha, TiroId = tiroId,
            ActividadObraId = Actividad(EfectoAvance3D.Poleas), Cantidad = 3, AvanceAcumulado = 50,
            Observaciones = "Tres poleas izadas de las seis previstas para el tiro.",
        });
        if (!await db.EstadosTorreManual.IgnoreQueryFilters().AnyAsync(ct))
        {
            db.EstadosTorreManual.Add(new EstadoTorreManual
            {
                ElementoTrazadoId = torres[4], Estado = "RESTRICCIÓN", Desde = montaje.Fecha, Alerta = true,
                Nota = c.Indice == 1 ? "Ingreso pendiente por mantenimiento de vía veredal." : "Pendiente autorización de ingreso al predio.",
            });
        }

        await db.SaveChangesAsync(ct);
        await transaccion.CommitAsync(ct);

        RegistroAvance Registro(InformeDiario informe, int torre, int? pata, EfectoAvance3D efecto, decimal cantidad, decimal avance, string nota)
            => new()
            {
                InformeDiarioId = informe.Id, Fecha = informe.Fecha, ElementoTrazadoId = torre, ComponenteId = pata,
                ActividadObraId = Actividad(efecto), Cantidad = cantidad, AvanceAcumulado = avance, Observaciones = nota,
            };
    }
}
