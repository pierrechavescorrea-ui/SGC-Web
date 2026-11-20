using Microsoft.EntityFrameworkCore;
using SigWeb.Models;

namespace SigWeb.Data.Demo;

public sealed class DemoIndicadores : IModuloDemo
{
    public string Clave => "indicadores";
    public int Orden => 12;

    public async Task SembrarAsync(ContextoDemo c, CancellationToken ct)
    {
        var db = c.Db;
        if (await db.Indicadores.IgnoreQueryFilters().AnyAsync(ct)) return;

        await using var tx = await db.Database.BeginTransactionAsync(ct);
        var actividad = c.Indice == 1 ? "montaje" : "cimentación";
        var calidad = new Indicador
        {
            Nombre = $"Aceptación de inspecciones de {actividad} al primer intento",
            Formula = "Inspecciones aceptadas al primer intento / inspecciones realizadas × 100",
            Objetivo = "≥ 90%", Meta = 90, Responsable = "Inspector de calidad",
            FuenteInformacion = "Registros de inspección de obra", FechaInicio = new DateTime(2026, 5, 1),
        };
        var sst = new Indicador
        {
            Nombre = $"Cumplimiento de inspecciones SST en frentes de {actividad}",
            Formula = "Inspecciones SST ejecutadas / inspecciones SST programadas × 100",
            Objetivo = "≥ 95%", Meta = 95, Responsable = "Coordinador SST",
            FuenteInformacion = "Programa mensual y listas de inspección SST", FechaInicio = calidad.FechaInicio, Orden = 1,
        };
        db.Indicadores.AddRange(calidad, sst);
        await db.SaveChangesAsync(ct);

        var soporte = Medicion(calidad.Id, 5, c.Indice == 1 ? 19 : 18, 20);
        db.MedicionesIndicador.AddRange(soporte, Medicion(calidad.Id, 6, 24, 25),
            Medicion(sst.Id, 5, c.Indice == 1 ? 10 : 9, 10), Medicion(sst.Id, 6, 12, 12));
        await db.SaveChangesAsync(ct);

        var (ruta, _) = c.CopiarArchivo(ContextoDemo.PdfEnsayo, "indicadores");
        db.ArchivosMedicionIndicador.Add(new ArchivoMedicionIndicador
        {
            MedicionIndicadorId = soporte.Id, NombreOriginal = "Informe de ensayo (soporte demo).pdf", RutaArchivo = ruta,
        });
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
    }

    private static MedicionIndicador Medicion(int indicadorId, int mes, int numerador, int denominador) => new()
    {
        IndicadorId = indicadorId, Anio = 2026, Mes = mes,
        Numerador = numerador, Denominador = denominador, Valor = 100m * numerador / denominador,
        Congelado = true, FechaCongelado = new DateTime(2026, mes + 1, 1),
    };
}
