using Microsoft.EntityFrameworkCore;
using SigWeb.Models;

namespace SigWeb.Data.Costos;

// Configuración EF del módulo Costos y presupuestos (la llama AppDbContext.OnModelCreating).
public static class ModeloCostos
{
    public static void Configurar(ModelBuilder mb)
    {
        mb.Entity<ItemPago>().HasQueryFilter(e => !e.Eliminado);
        mb.Entity<ItemPago>().HasIndex(e => e.Codigo);
        mb.Entity<ItemPago>().HasOne<ItemPago>().WithMany().HasForeignKey(e => e.CapituloId).OnDelete(DeleteBehavior.Restrict);
        mb.Entity<ItemPago>().HasOne<ActividadObra>().WithMany().HasForeignKey(e => e.ActividadObraId).OnDelete(DeleteBehavior.SetNull);
        mb.Entity<ItemPago>().Property(e => e.CantidadContratada).HasPrecision(18, 4);
        mb.Entity<ItemPago>().Property(e => e.PrecioBase).HasPrecision(18, 4);
        mb.Entity<ItemPago>().Property(e => e.AiuPct).HasPrecision(9, 6);
        mb.Entity<ItemPago>().Property(e => e.CostoUnitarioPresupuestado).HasPrecision(18, 4);

        mb.Entity<AjustePrecio>().HasQueryFilter(e => !e.Eliminado);
        mb.Entity<ComponenteAjustePrecio>().HasOne<AjustePrecio>().WithMany().HasForeignKey(e => e.AjustePrecioId).OnDelete(DeleteBehavior.Cascade);
        mb.Entity<ComponenteAjustePrecio>().Property(e => e.Peso).HasPrecision(9, 6);
        mb.Entity<ComponenteAjustePrecio>().Property(e => e.Indice).HasPrecision(9, 6);

        mb.Entity<PrecioManual>().HasOne<ItemPago>().WithMany().HasForeignKey(e => e.ItemPagoId).OnDelete(DeleteBehavior.Cascade);
        mb.Entity<PrecioManual>().HasIndex(e => new { e.ItemPagoId, e.Anio }).IsUnique();
        mb.Entity<PrecioManual>().Property(e => e.Precio).HasPrecision(18, 4);

        mb.Entity<ModificacionContrato>().HasQueryFilter(e => !e.Eliminado);
        mb.Entity<LineaModificacionContrato>().HasOne<ModificacionContrato>().WithMany().HasForeignKey(e => e.ModificacionContratoId).OnDelete(DeleteBehavior.Cascade);
        mb.Entity<LineaModificacionContrato>().HasOne<ItemPago>().WithMany().HasForeignKey(e => e.ItemPagoId).OnDelete(DeleteBehavior.Cascade);
        foreach (var p in new[] { "CantidadAntes", "CantidadDespues", "PrecioAntes", "PrecioDespues" })
            mb.Entity<LineaModificacionContrato>().Property(p).HasPrecision(18, 4);

        mb.Entity<CantidadTorre>().HasOne<ElementoTrazado>().WithMany().HasForeignKey(e => e.ElementoTrazadoId).OnDelete(DeleteBehavior.Cascade);
        mb.Entity<CantidadTorre>().HasOne<Componente>().WithMany().HasForeignKey(e => e.ComponenteId).OnDelete(DeleteBehavior.Cascade);
        mb.Entity<CantidadTorre>().HasOne<ItemPago>().WithMany().HasForeignKey(e => e.ItemPagoId).OnDelete(DeleteBehavior.Cascade);
        mb.Entity<CantidadTorre>().HasIndex(e => new { e.ElementoTrazadoId, e.ItemPagoId });
        mb.Entity<CantidadTorre>().Property(e => e.CantidadPrevista).HasPrecision(18, 4);

        mb.Entity<RegistroCantidad>().HasQueryFilter(e => !e.Eliminado);
        mb.Entity<RegistroCantidad>().HasOne<ItemPago>().WithMany().HasForeignKey(e => e.ItemPagoId).OnDelete(DeleteBehavior.Restrict);
        mb.Entity<RegistroCantidad>().HasOne<ElementoTrazado>().WithMany().HasForeignKey(e => e.ElementoTrazadoId).OnDelete(DeleteBehavior.Cascade);
        mb.Entity<RegistroCantidad>().HasOne<Componente>().WithMany().HasForeignKey(e => e.ComponenteId).OnDelete(DeleteBehavior.Cascade);
        mb.Entity<RegistroCantidad>().HasOne<Tiro>().WithMany().HasForeignKey(e => e.TiroId).OnDelete(DeleteBehavior.Cascade);
        mb.Entity<RegistroCantidad>().HasOne<ActividadPrograma>().WithMany().HasForeignKey(e => e.ActividadProgramaId).OnDelete(DeleteBehavior.SetNull);
        mb.Entity<RegistroCantidad>().HasIndex(e => e.Fecha);
        mb.Entity<RegistroCantidad>().HasIndex(e => new { e.ElementoTrazadoId, e.ItemPagoId });
        mb.Entity<RegistroCantidad>().Property(e => e.Cantidad).HasPrecision(18, 4);

        mb.Entity<ActaFacturacion>().HasQueryFilter(e => !e.Eliminado);
        mb.Entity<LineaActaFacturacion>().HasOne<ActaFacturacion>().WithMany().HasForeignKey(e => e.ActaFacturacionId).OnDelete(DeleteBehavior.Cascade);
        mb.Entity<LineaActaFacturacion>().HasOne<ItemPago>().WithMany().HasForeignKey(e => e.ItemPagoId).OnDelete(DeleteBehavior.Restrict);
        mb.Entity<LineaActaFacturacion>().Property(e => e.Cantidad).HasPrecision(18, 4);
        mb.Entity<LineaActaFacturacion>().Property(e => e.Valor).HasPrecision(18, 2);

        mb.Entity<CodigoPresupuesto>().HasQueryFilter(e => !e.Eliminado);
        mb.Entity<CodigoPresupuesto>().Property(e => e.PresupuestoInicial).HasPrecision(18, 2);
        mb.Entity<ValorPresupuesto>().HasOne<CodigoPresupuesto>().WithMany().HasForeignKey(e => e.CodigoPresupuestoId).OnDelete(DeleteBehavior.Cascade);
        mb.Entity<ValorPresupuesto>().Property(e => e.Valor).HasPrecision(18, 2);
        mb.Entity<PorcentajeIndirecto>().Property(e => e.Porcentaje).HasPrecision(9, 6);

        mb.Entity<CostoOrden>().HasQueryFilter(e => !e.Eliminado);
        mb.Entity<CostoOrden>().HasOne<CodigoPresupuesto>().WithMany().HasForeignKey(e => e.CodigoPresupuestoId).OnDelete(DeleteBehavior.SetNull);
        mb.Entity<CostoOrden>().HasOne<ItemPago>().WithMany().HasForeignKey(e => e.ItemPagoId).OnDelete(DeleteBehavior.SetNull);
        mb.Entity<CostoOrden>().Property(e => e.Valor).HasPrecision(18, 2);

        mb.Entity<Persona>().HasQueryFilter(e => !e.Eliminado);
        mb.Entity<Persona>().HasOne<CodigoPresupuesto>().WithMany().HasForeignKey(e => e.CodigoPresupuestoId).OnDelete(DeleteBehavior.SetNull);
        mb.Entity<Persona>().Property(e => e.Salario).HasPrecision(18, 2);
        mb.Entity<Persona>().Property(e => e.Auxilio).HasPrecision(18, 2);

        mb.Entity<MetaPeriodo>().HasQueryFilter(e => !e.Eliminado);
        mb.Entity<MetaPeriodo>().Property(e => e.MetaFacturacion).HasPrecision(18, 2);
        mb.Entity<MetaPeriodo>().Property(e => e.MetaProduccionTotal).HasPrecision(18, 2);
        mb.Entity<MetaProduccionDia>().HasOne<MetaPeriodo>().WithMany().HasForeignKey(e => e.MetaPeriodoId).OnDelete(DeleteBehavior.Cascade);
        mb.Entity<MetaProduccionDia>().HasOne<ItemPago>().WithMany().HasForeignKey(e => e.ItemPagoId).OnDelete(DeleteBehavior.Cascade);
        mb.Entity<MetaProduccionDia>().Property(e => e.Cantidad).HasPrecision(18, 4);

        mb.Entity<AsignacionCostoActividad>().HasOne<ItemPago>().WithMany().HasForeignKey(e => e.ItemPagoId).OnDelete(DeleteBehavior.Cascade);
        mb.Entity<AsignacionCostoActividad>().Property(e => e.Valor).HasPrecision(18, 2);

        mb.Entity<ProgramaTrabajo>().HasQueryFilter(e => !e.Eliminado);
        mb.Entity<ActividadPrograma>().HasOne<ProgramaTrabajo>().WithMany().HasForeignKey(e => e.ProgramaTrabajoId).OnDelete(DeleteBehavior.Cascade);
        mb.Entity<ActividadPrograma>().HasOne<ItemPago>().WithMany().HasForeignKey(e => e.ItemPagoId).OnDelete(DeleteBehavior.Cascade);
        mb.Entity<ActividadPrograma>().HasOne<ElementoTrazado>().WithMany().HasForeignKey(e => e.ElementoTrazadoId).OnDelete(DeleteBehavior.Cascade);
        mb.Entity<ActividadPrograma>().HasOne<Tiro>().WithMany().HasForeignKey(e => e.TiroId).OnDelete(DeleteBehavior.Cascade);
        mb.Entity<ActividadPrograma>().Property(e => e.Cantidad).HasPrecision(18, 4);
        mb.Entity<ActividadPrograma>().Property(e => e.ValorPlaneado).HasPrecision(18, 2);
        mb.Entity<ActividadPrograma>().Property(e => e.AvanceProject).HasPrecision(9, 4);
        mb.Entity<ActividadPrograma>().HasOne<ActividadObra>().WithMany().HasForeignKey(e => e.ActividadObraId).OnDelete(DeleteBehavior.SetNull);
        mb.Entity<ActividadPrograma>().HasIndex(e => new { e.ProgramaTrabajoId, e.Uid });
        mb.Entity<ProgramaTrabajo>().Property(e => e.DiasLaborables).HasMaxLength(7).HasDefaultValue("1111111");
        mb.Entity<CambioPrograma>().HasOne<ProgramaTrabajo>().WithMany().HasForeignKey(e => e.ProgramaTrabajoId).OnDelete(DeleteBehavior.Cascade);

        mb.Entity<EstadoTorreManual>().HasOne<ElementoTrazado>().WithMany().HasForeignKey(e => e.ElementoTrazadoId).OnDelete(DeleteBehavior.Cascade);

        mb.Entity<ConfiguracionCostos>().Property(e => e.ValorContrato).HasPrecision(18, 2);
        mb.Entity<ConfiguracionCostos>().Property(e => e.FactorPrestacional).HasPrecision(9, 6);
        mb.Entity<ConfiguracionCostos>().Property(e => e.RecargoA).HasPrecision(9, 6);
        mb.Entity<ConfiguracionCostos>().Property(e => e.RecargoB).HasPrecision(9, 6);
        mb.Entity<ConfiguracionCostos>().Property(e => e.BaseAdicional).HasPrecision(18, 2);
        mb.Entity<ConfiguracionCostos>().Property(e => e.DivisorAdicional).HasPrecision(9, 4);
    }
}
