using Microsoft.EntityFrameworkCore;
using SigWeb.Data.Costos;
using SigWeb.Models;

namespace SigWeb.Data.Papelera;

// Papelera de Costos y presupuestos: una base genérica (todas las entidades tienen Id, Eliminado y
// FechaEliminacion con filtro de consulta) y un proveedor por entidad. El borrado definitivo puede
// rechazarlo la base si otro registro aún depende (p. ej. un ítem con registros de avance): la página
// Papelera lo reintenta en otra pasada o muestra el motivo.
public abstract class ProveedorPapeleraCostos<T> : IProveedorPapelera where T : class
{
    protected readonly AppDbContext Db;
    private const int DiasRetencion = 30;

    protected ProveedorPapeleraCostos(AppDbContext db) { Db = db; }

    public abstract string Modulo { get; }
    protected abstract (string Titulo, string Subtitulo) Describir(T x);

    private IQueryable<T> Borrados => Db.Set<T>().IgnoreQueryFilters().Where(x => EF.Property<bool>(x, "Eliminado"));
    private T? Buscar(int id) => Db.Set<T>().IgnoreQueryFilters().FirstOrDefault(x => EF.Property<int>(x, "Id") == id);

    public List<RegistroPapelera> Listar() =>
        Borrados.AsNoTracking().OrderByDescending(x => EF.Property<DateTime?>(x, "FechaEliminacion")).ToList()
            .Select(x =>
            {
                var e = Db.Entry(x);
                var (t, s) = Describir(x);
                return new RegistroPapelera
                {
                    Modulo = Modulo, Id = (int)e.Property("Id").CurrentValue!, Titulo = t, Subtitulo = s,
                    FechaEliminacion = (DateTime?)e.Property("FechaEliminacion").CurrentValue
                };
            }).ToList();

    public void Restaurar(int id)
    {
        if (Buscar(id) is T x)
        {
            Db.Entry(x).Property("Eliminado").CurrentValue = false;
            Db.Entry(x).Property("FechaEliminacion").CurrentValue = null;
            Db.SaveChanges();
        }
        Db.ChangeTracker.Clear();
    }

    public virtual void EliminarDefinitivo(int id)
    {
        try
        {
            if (Buscar(id) is T x) { Db.Set<T>().Remove(x); Db.SaveChanges(); }
        }
        finally { Db.ChangeTracker.Clear(); }
    }

    public void PurgarVencidos()
    {
        var limite = DateTime.Now.AddDays(-DiasRetencion);
        var ids = Borrados.Where(x => EF.Property<DateTime?>(x, "FechaEliminacion") != null && EF.Property<DateTime?>(x, "FechaEliminacion") < limite)
            .Select(x => EF.Property<int>(x, "Id")).ToList();
        foreach (var id in ids)
        {
            try { EliminarDefinitivo(id); } catch { /* depende de otro registro: queda para la próxima */ }
        }
    }
}

public class ProveedorPapeleraItemsPago(AppDbContext db) : ProveedorPapeleraCostos<ItemPago>(db)
{
    public override string Modulo => "Costos: ítems de pago";
    protected override (string, string) Describir(ItemPago x) => ($"{x.Codigo} · {x.Descripcion}", "Formulario de precios");
}

public class ProveedorPapeleraRegistrosCantidad(AppDbContext db) : ProveedorPapeleraCostos<RegistroCantidad>(db)
{
    public override string Modulo => "Costos: avance diario";
    protected override (string, string) Describir(RegistroCantidad x)
    {
        var it = Db.ItemsPago.IgnoreQueryFilters().AsNoTracking().Where(i => i.Id == x.ItemPagoId).Select(i => i.Codigo).FirstOrDefault();
        var torre = x.ElementoTrazadoId is int t ? Db.ElementosTrazado.IgnoreQueryFilters().AsNoTracking().Where(e => e.Id == t).Select(e => e.Nombre ?? e.Codigo).FirstOrDefault() : null;
        return ($"{FormatoCostos.Fecha(x.Fecha)} · {it} · {FormatoCostos.Num(x.Cantidad, 3)}", $"{torre} {x.Cuadrilla} · {x.Origen}".Trim());
    }
}

public class ProveedorPapeleraCostosOrden(AppDbContext db) : ProveedorPapeleraCostos<CostoOrden>(db)
{
    public override string Modulo => "Costos: órdenes y gastos";
    protected override (string, string) Describir(CostoOrden x) => ($"{x.NumeroOrden} {x.Concepto}".Trim(), $"{x.Proveedor} · {FormatoCostos.Dinero(x.Valor)}");
}

public class ProveedorPapeleraPersonas(AppDbContext db) : ProveedorPapeleraCostos<Persona>(db)
{
    public override string Modulo => "Costos: personal";
    protected override (string, string) Describir(Persona x) => (x.Nombre, $"{x.Cargo} · {x.Cuadrilla}");
}

public class ProveedorPapeleraCodigosPresupuesto(AppDbContext db) : ProveedorPapeleraCostos<CodigoPresupuesto>(db)
{
    public override string Modulo => "Costos: códigos de presupuesto";
    protected override (string, string) Describir(CodigoPresupuesto x) => ($"{x.Codigo} · {x.Nombre}", InstantaneaCostos.NombreGrupo(x.Grupo));
}

public class ProveedorPapeleraModificacionesContrato(AppDbContext db) : ProveedorPapeleraCostos<ModificacionContrato>(db)
{
    public override string Modulo => "Costos: modificaciones del contrato";
    protected override (string, string) Describir(ModificacionContrato x) => ($"{x.Numero} {x.Descripcion}".Trim(), FormatoCostos.Fecha(x.Fecha));
}

public class ProveedorPapeleraAjustesPrecio(AppDbContext db) : ProveedorPapeleraCostos<AjustePrecio>(db)
{
    public override string Modulo => "Costos: ajustes de precio";
    protected override (string, string) Describir(AjustePrecio x) => (x.Nombre, x.Anio.ToString());
}

public class ProveedorPapeleraActasFacturacion(AppDbContext db) : ProveedorPapeleraCostos<ActaFacturacion>(db)
{
    public override string Modulo => "Costos: actas de facturación";
    protected override (string, string) Describir(ActaFacturacion x) => ($"Acta {x.Numero}", FormatoCostos.Fecha(x.Fecha));
}

public class ProveedorPapeleraMetasPeriodo(AppDbContext db) : ProveedorPapeleraCostos<MetaPeriodo>(db)
{
    public override string Modulo => "Costos: metas";
    protected override (string, string) Describir(MetaPeriodo x) => ($"Meta {FormatoCostos.Fecha(x.Desde)} – {FormatoCostos.Fecha(x.Hasta)} v{x.Version}", x.Motivo ?? "");
}

public class ProveedorPapeleraProgramasTrabajo(AppDbContext db) : ProveedorPapeleraCostos<ProgramaTrabajo>(db)
{
    public override string Modulo => "Costos: programas de trabajo (PDT)";
    protected override (string, string) Describir(ProgramaTrabajo x) => ($"{x.Nombre} v{x.Version}", FormatoCostos.Fecha(x.Fecha));
}
