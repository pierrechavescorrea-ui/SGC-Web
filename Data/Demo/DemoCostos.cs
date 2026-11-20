using Microsoft.EntityFrameworkCore;
using SigWeb.Models;

namespace SigWeb.Data.Demo;

public sealed class DemoCostos : IModuloDemo
{
    public string Clave => "costos";
    public int Orden => 22;

    public async Task SembrarAsync(ContextoDemo c, CancellationToken ct)
    {
        var db = c.Db;
        if (await db.ItemsPago.IgnoreQueryFilters().AnyAsync(ct)) return;

        var torres = c.Torres().Take(2).ToList();
        var fecha = c.Hoy.AddDays(-2);
        var frente = c.Indice == 1 ? "SUR" : "NORTE";
        var cuadrilla = $"CIMENTACIÓN {frente}";
        var actividades = await db.ActividadesObra.AsNoTracking().Where(x => x.Activo).OrderBy(x => x.Orden).ToListAsync(ct);

        await using var transaccion = await db.Database.BeginTransactionAsync(ct);
        var configuracion = await db.ConfiguracionCostos.OrderBy(x => x.Id).FirstOrDefaultAsync(ct);
        if (configuracion is null)
        {
            configuracion = new ConfiguracionCostos();
            db.ConfiguracionCostos.Add(configuracion);
        }
        // El arranque puede haber creado la fila para configurar las longitudes de los tiros.
        configuracion.Cliente ??= (c.Indice == 1 ? SembradorDemo.Segundo : SembradorDemo.Primero).Cliente;
        configuracion.Contrato ??= $"DEMO-{frente}-{fecha.Year}";
        configuracion.Interventor ??= "Control de Redes Demo S.A.S.";
        configuracion.Objeto ??= $"Construcción de {c.NombreProyecto}";
        configuracion.Inicio ??= fecha.AddDays(-20);
        configuracion.Fin ??= fecha.AddMonths(6);

        var excavacion = Item("2.1.1", "Excavación para cimentaciones", "m³", 320, 85000, 65000, EfectoAvance3D.Excavacion, cuadrilla, 1);
        var concreto = Item("2.1.2", "Suministro y colocación de concreto estructural", "m³", 180, 760000, 620000, EfectoAvance3D.Concreto, cuadrilla, 2);
        var montaje = Item("3.1.1", "Montaje de estructura metálica BP6", "t", 100, 2100000, 1700000, EfectoAvance3D.Montaje, $"MONTAJE {frente}", 3);
        db.ItemsPago.AddRange(excavacion, concreto, montaje);

        var presupuesto = new CodigoPresupuesto
        {
            Codigo = $"CIV-{frente}", Nombre = "Recursos directos de cimentación",
            Grupo = GrupoPresupuesto.General, PresupuestoInicial = 65000000, Orden = 1,
        };
        db.CodigosPresupuesto.Add(presupuesto);
        await db.SaveChangesAsync(ct);

        foreach (var torre in torres)
        {
            db.CantidadesTorre.AddRange(
                new CantidadTorre { ElementoTrazadoId = torre, ItemPagoId = excavacion.Id, CantidadPrevista = 32, TipoTorre = "BP6" },
                new CantidadTorre { ElementoTrazadoId = torre, ItemPagoId = concreto.Id, CantidadPrevista = 18, TipoTorre = "BP6" },
                new CantidadTorre { ElementoTrazadoId = torre, ItemPagoId = montaje.Id, CantidadPrevista = 10, TipoTorre = "BP6" });
        }

        db.RegistrosCantidad.AddRange(
            new RegistroCantidad { Fecha = fecha, ElementoTrazadoId = torres[0], ItemPagoId = excavacion.Id, Cantidad = 12.4m, Cuadrilla = cuadrilla, Descripcion = "Excavación de pata A", Origen = "Demostración" },
            new RegistroCantidad { Fecha = fecha, ElementoTrazadoId = torres[1], ItemPagoId = concreto.Id, Cantidad = 7.8m, Cuadrilla = cuadrilla, Descripcion = "Vaciado de pata A", Origen = "Demostración" });
        db.ValoresPresupuesto.Add(new ValorPresupuesto
        {
            CodigoPresupuestoId = presupuesto.Id, Fecha = fecha, Valor = 68000000, Motivo = "Actualización de transporte a los sitios de torre.",
        });
        db.CostosOrden.Add(new CostoOrden
        {
            NumeroOrden = $"OC-DEMO-{frente}-01", CodigoPresupuestoId = presupuesto.Id, ItemPagoId = concreto.Id,
            Proveedor = "Concretos del Cañón Demo S.A.S.", Concepto = "Suministro de concreto para cimentación",
            Valor = 3900000, Desde = fecha, Hasta = fecha, Cuadrilla = cuadrilla, Pf1 = true,
        });
        db.Personas.Add(new Persona
        {
            Nombre = $"Operario de cimentación Demo {frente}", Cargo = "Oficial de obra civil", CodigoPresupuestoId = presupuesto.Id,
            Ingreso = fecha.AddDays(-1), Salario = 2650000, Auxilio = 250000, Cuadrilla = cuadrilla,
        });
        await db.SaveChangesAsync(ct);
        await transaccion.CommitAsync(ct);

        ItemPago Item(string codigo, string descripcion, string unidad, decimal contratada, decimal precio, decimal costo,
            EfectoAvance3D efecto, string equipo, int orden) => new()
        {
            Codigo = codigo, Descripcion = descripcion, Unidad = unidad, CantidadContratada = contratada,
            PrecioBase = precio, CostoUnitarioPresupuestado = costo, AnioBase = fecha.Year, AiuPct = 0.25m,
            Ambito = AmbitoItemPago.Torre, ActividadObraId = actividades.FirstOrDefault(a => a.Efecto == efecto)?.Id,
            CuadrillaPorDefecto = equipo, Orden = orden,
        };
    }
}
