using Microsoft.EntityFrameworkCore;
using SigWeb.Data.Tablas;
using SigWeb.Models;

namespace SigWeb.Data.Dossier.Fuentes;

// Tablas propias del Dossier para vistas que no usan MotorTabla en pantalla.
internal static class TablaDetalleObra
{
    public static async Task<FuenteCargada> CargarAsync<T>(AppDbContext db, IFuenteDossier fuente, IEnumerable<T> filas,
        Func<T, int> id, Func<T, string> nombre, string columnaNombre, string nombreGrupo, Func<T, string?> grupo,
        (string Clave, string Titulo, TipoCampoTabla Tipo, Func<T, object?> Valor)[] columnas,
        Action<T, ElementoDossier>? adjuntar = null, string? clase = null)
    {
        var porId = filas.ToDictionary(id);
        var nativas = columnas.Select(c => new ColumnaNativa(c.Clave, c.Titulo, c.Tipo, i => c.Valor(porId[i]))).ToArray();
        var motor = await MotorTabla.CargarLecturaAsync(db, $"dossier:{fuente.Clave}", nativas, porId.Select(f => (f.Key, nombre(f.Value))));
        var elementos = porId.Select(f =>
        {
            var elemento = new ElementoDossier { Id = f.Key, Grupo = grupo(f.Value) };
            adjuntar?.Invoke(f.Value, elemento);
            return elemento;
        }).ToList();
        return new FuenteCargada
        {
            Fuente = fuente, Motor = motor, Elementos = elementos, ColumnaNombre = columnaNombre,
            NombreGrupo = nombreGrupo, Clases = clase is null ? [] : [clase], TieneVersiones = clase is not null
        };
    }
}

public sealed class FuenteActividadesObra : IFuenteDossier
{
    public string Clave => "actividades-obra";
    public string Nombre => "Actividades de obra";
    public string Icono => "actividades";

    public async Task<FuenteCargada> CargarAsync(AppDbContext db, CancellationToken ct)
    {
        var filas = await db.ActividadesObra.AsNoTracking().Where(a => !a.Eliminado).OrderBy(a => a.Orden).ThenBy(a => a.Id)
            .Select(a => new { a.Id, a.Nombre, a.Grupo, a.Ambito, a.Peso, a.Unidad, a.ItemContrato, a.Orden, a.Activo }).ToListAsync(ct);
        return await TablaDetalleObra.CargarAsync(db, this, filas, a => a.Id, a => a.Nombre, "Nombre", "Grupo", a => a.Grupo,
        [
            ("Nombre", "Actividad", TipoCampoTabla.Texto, a => a.Nombre),
            ("Grupo", "Grupo", TipoCampoTabla.Texto, a => a.Grupo),
            ("Ambito", "Ámbito", TipoCampoTabla.Texto, a => a.Ambito.ToString()),
            ("Peso", "Peso", TipoCampoTabla.Numero, a => a.Peso),
            ("Unidad", "Unidad", TipoCampoTabla.Texto, a => a.Unidad),
            ("ItemContrato", "Ítem de contrato", TipoCampoTabla.Texto, a => a.ItemContrato),
            ("Orden", "Orden", TipoCampoTabla.Numero, a => a.Orden),
            ("Activo", "Activa", TipoCampoTabla.Texto, a => a.Activo ? "Sí" : "No")
        ]);
    }
}

public sealed class FuenteRegistrosAvance : IFuenteDossier
{
    public string Clave => "registros-avance";
    public string Nombre => "Registros de avance de torres";
    public string Icono => "avance-obra";

    public async Task<FuenteCargada> CargarAsync(AppDbContext db, CancellationToken ct)
    {
        var filas = await (
            from r in db.RegistrosAvance.AsNoTracking()
            join i in db.InformesDiarios on r.InformeDiarioId equals i.Id
            join torre in db.ElementosTrazado on r.ElementoTrazadoId equals torre.Id
            join actividad in db.ActividadesObra on r.ActividadObraId equals actividad.Id
            join componente in db.Componentes on r.ComponenteId equals (int?)componente.Id into componentes
            from pata in componentes.DefaultIfEmpty()
            where !r.Eliminado && !i.Eliminado && !torre.Eliminado && !actividad.Eliminado
                && (r.ComponenteId == null || (pata != null && !pata.Eliminado))
            orderby r.Fecha descending, torre.Codigo, actividad.Orden, r.Id
            select new { r.Id, r.Fecha, Torre = torre.Codigo, Pata = pata == null ? null : pata.Nombre,
                Actividad = actividad.Nombre, actividad.Unidad, r.Cantidad, r.AvanceAcumulado, r.NoAplica, r.Programado, r.Observaciones }
        ).ToListAsync(ct);
        string NombreFila(DateTime fecha, string torre, string? pata, string actividad) =>
            string.Join(" · ", new[] { fecha.ToString("yyyy-MM-dd"), torre, pata, actividad }.Where(s => !string.IsNullOrEmpty(s)));
        return await TablaDetalleObra.CargarAsync(db, this, filas, r => r.Id, r => NombreFila(r.Fecha, r.Torre, r.Pata, r.Actividad),
            "Registro", "Mes", r => r.Fecha.ToString("yyyy-MM"),
        [
            ("Registro", "Registro", TipoCampoTabla.Texto, r => NombreFila(r.Fecha, r.Torre, r.Pata, r.Actividad)),
            ("Fecha", "Fecha", TipoCampoTabla.Fecha, r => r.Fecha),
            ("Torre", "Torre", TipoCampoTabla.Texto, r => r.Torre),
            ("Pata", "Pata", TipoCampoTabla.Texto, r => r.Pata),
            ("Actividad", "Actividad", TipoCampoTabla.Texto, r => r.Actividad),
            ("Unidad", "Unidad", TipoCampoTabla.Texto, r => r.Unidad),
            ("Cantidad", "Cantidad del día", TipoCampoTabla.Numero, r => r.Cantidad),
            ("Avance", "Avance acumulado %", TipoCampoTabla.Numero, r => r.AvanceAcumulado),
            ("NoAplica", "No aplica", TipoCampoTabla.Texto, r => r.NoAplica ? "Sí" : "No"),
            ("Programado", "Programado", TipoCampoTabla.Texto, r => r.Programado ? "Sí" : "No"),
            ("Observaciones", "Observaciones", TipoCampoTabla.Texto, r => r.Observaciones)
        ]);
    }
}

public sealed class FuenteRegistrosTendido : IFuenteDossier
{
    public string Clave => "registros-tendido";
    public string Nombre => "Registros de tendido";
    public string Icono => "linea3d";

    public async Task<FuenteCargada> CargarAsync(AppDbContext db, CancellationToken ct)
    {
        var filas = await (
            from r in db.RegistrosAvanceTiro.AsNoTracking()
            join i in db.InformesDiarios on r.InformeDiarioId equals i.Id
            join tiro in db.Tiros on r.TiroId equals tiro.Id
            join actividad in db.ActividadesObra on r.ActividadObraId equals actividad.Id
            where !r.Eliminado && !i.Eliminado && !tiro.Eliminado && !actividad.Eliminado
            orderby r.Fecha descending, tiro.Orden, actividad.Orden, r.Id
            select new { r.Id, r.Fecha, Tiro = tiro.Nombre, Actividad = actividad.Nombre, actividad.Unidad,
                r.Cantidad, r.AvanceAcumulado, r.NoAplica, r.Observaciones }
        ).ToListAsync(ct);
        static string NombreFila(DateTime fecha, string tiro, string actividad) => $"{fecha:yyyy-MM-dd} · {tiro} · {actividad}";
        return await TablaDetalleObra.CargarAsync(db, this, filas, r => r.Id, r => NombreFila(r.Fecha, r.Tiro, r.Actividad),
            "Registro", "Mes", r => r.Fecha.ToString("yyyy-MM"),
        [
            ("Registro", "Registro", TipoCampoTabla.Texto, r => NombreFila(r.Fecha, r.Tiro, r.Actividad)),
            ("Fecha", "Fecha", TipoCampoTabla.Fecha, r => r.Fecha),
            ("Tiro", "Tiro", TipoCampoTabla.Texto, r => r.Tiro),
            ("Actividad", "Actividad", TipoCampoTabla.Texto, r => r.Actividad),
            ("Unidad", "Unidad", TipoCampoTabla.Texto, r => r.Unidad),
            ("Cantidad", "Cantidad del día", TipoCampoTabla.Numero, r => r.Cantidad),
            ("Avance", "Avance acumulado %", TipoCampoTabla.Numero, r => r.AvanceAcumulado),
            ("NoAplica", "No aplica", TipoCampoTabla.Texto, r => r.NoAplica ? "Sí" : "No"),
            ("Observaciones", "Observaciones", TipoCampoTabla.Texto, r => r.Observaciones)
        ]);
    }
}

public sealed class FuenteRevisionesTrazado : IFuenteDossier
{
    public string Clave => "revisiones-trazado";
    public string Nombre => "Revisiones del trazado";
    public string Icono => "trazado";
    private const string Original = "Archivo de origen";

    public async Task<FuenteCargada> CargarAsync(AppDbContext db, CancellationToken ct)
    {
        var filas = await db.RevisionesTablaTorres.AsNoTracking().OrderByDescending(r => r.FechaCarga).ThenByDescending(r => r.Id)
            .Select(r => new { r.Id, r.Codigo, r.Origen, r.Fecha, r.FechaCarga, r.Estado, r.Descripcion, r.Torres, r.Cambios,
                r.NombreArchivo, r.RutaArchivo, r.FechaDeshecha, r.MotivoDeshacer }).ToListAsync(ct);
        return await TablaDetalleObra.CargarAsync(db, this, filas, r => r.Id, r => r.Codigo, "Codigo", "Origen", r => r.Origen,
        [
            ("Codigo", "Revisión", TipoCampoTabla.Texto, r => r.Codigo),
            ("Origen", "Origen", TipoCampoTabla.Texto, r => r.Origen),
            ("Fecha", "Fecha de revisión", TipoCampoTabla.Fecha, r => r.Fecha),
            ("FechaCarga", "Fecha de carga", TipoCampoTabla.Fecha, r => r.FechaCarga),
            ("Estado", "Estado", TipoCampoTabla.Texto, r => r.Estado.ToString()),
            ("Descripcion", "Descripción", TipoCampoTabla.Texto, r => r.Descripcion),
            ("Torres", "Torres afectadas", TipoCampoTabla.Numero, r => r.Torres),
            ("Cambios", "Cambios", TipoCampoTabla.Numero, r => r.Cambios),
            ("FechaDeshecha", "Fecha de reversión", TipoCampoTabla.Fecha, r => r.FechaDeshecha),
            ("MotivoDeshacer", "Motivo de reversión", TipoCampoTabla.Texto, r => r.MotivoDeshacer)
        ], (r, elemento) =>
        {
            if (!string.IsNullOrWhiteSpace(r.RutaArchivo))
                elemento.Archivos.Add(new ArchivoDossier(Original, r.NombreArchivo, r.RutaArchivo, Actual: r.Estado == EstadoRevisionTabla.Vigente));
        }, Original);
    }
}

public sealed class FuenteEstadoTorres : IFuenteDossier
{
    public string Clave => "estado-torres";
    public string Nombre => "Estado de las torres";
    public string Icono => "estado-torres";

    public async Task<FuenteCargada> CargarAsync(AppDbContext db, CancellationToken ct)
    {
        var finHoy = DateTime.Today.AddDays(1);
        var filas = await (
            from torre in db.ElementosTrazado.AsNoTracking()
            join tiro in db.Tiros on torre.TiroId equals (int?)tiro.Id into tiros
            from tiro in tiros.DefaultIfEmpty()
            where !torre.Eliminado
            orderby torre.Codigo, torre.Id
            select new
            {
                torre.Id, torre.Codigo, Tiro = tiro == null || tiro.Eliminado ? null : tiro.Nombre,
                Estado = db.EstadosTorreManual.Where(e => e.ElementoTrazadoId == torre.Id && e.Desde < finHoy)
                    .OrderByDescending(e => e.Desde).ThenByDescending(e => e.Id)
                    .Select(e => new { e.Estado, e.Desde, e.Alerta, e.Nota }).FirstOrDefault()
            }
        ).ToListAsync(ct);
        return await TablaDetalleObra.CargarAsync(db, this, filas, t => t.Id, t => t.Codigo, "Torre", "Tiro", t => t.Tiro,
        [
            ("Torre", "Torre", TipoCampoTabla.Texto, t => t.Codigo),
            ("Tiro", "Tiro", TipoCampoTabla.Texto, t => t.Tiro),
            ("Estado", "Estado", TipoCampoTabla.Texto, t => t.Estado?.Estado),
            ("Desde", "Desde", TipoCampoTabla.Fecha, t => t.Estado?.Desde),
            ("Alerta", "Alerta", TipoCampoTabla.Texto, t => t.Estado is { } e ? e.Alerta ? "Sí" : "No" : null),
            ("Nota", "Nota", TipoCampoTabla.Texto, t => t.Estado?.Nota)
        ]);
    }
}
