using ClosedXML.Excel;
using Microsoft.EntityFrameworkCore;

namespace SigWeb.Data.Seguridad;

// Filtro común de la auditoría (pantalla y exportación a Excel). Solo LINQ: el texto del usuario nunca va a SQL crudo.
public sealed record FiltroAuditoria(DateTime? Desde, DateTime? Hasta, string? Texto, string? Resultado, string? Evento, int? ProyectoId)
{
    public IQueryable<RegistroAuditoria> Aplicar(IQueryable<RegistroAuditoria> q)
    {
        if (Desde is { } d) { var u = DateTime.SpecifyKind(d.Date, DateTimeKind.Local).ToUniversalTime(); q = q.Where(a => a.Fecha >= u); }
        if (Hasta is { } h) { var u = DateTime.SpecifyKind(h.Date.AddDays(1), DateTimeKind.Local).ToUniversalTime(); q = q.Where(a => a.Fecha < u); }
        if (Resultado == "fallo") q = q.Where(a => !a.Exito);
        else if (Resultado == "exito") q = q.Where(a => a.Exito);
        if (!string.IsNullOrWhiteSpace(Evento)) q = q.Where(a => a.Evento == Evento);
        if (ProyectoId is int p) q = q.Where(a => a.ProyectoId == p);
        if (!string.IsNullOrWhiteSpace(Texto))
        {
            var t = "%" + Texto.Trim().Replace("\\", "\\\\").Replace("%", "\\%").Replace("_", "\\_") + "%";
            q = q.Where(a => EF.Functions.ILike(a.Email ?? "", t) || EF.Functions.ILike(a.Evento, t) ||
                             EF.Functions.ILike(a.Detalle ?? "", t) || EF.Functions.ILike(a.Ip ?? "", t));
        }
        return q;
    }

    public static void MapExportar(WebApplication app) =>
        app.MapGet("/api/admin/auditoria.xlsx", async (DateTime? desde, DateTime? hasta, string? texto, string? resultado, string? evento, int? proyecto,
            SeguridadDbContext db, ServicioProyectos proyectos, AuditoriaAccesos aud, HttpContext ctx) =>
        {
            var filas = await new FiltroAuditoria(desde, hasta, texto, resultado, evento, proyecto)
                .Aplicar(db.Auditoria.AsNoTracking()).OrderByDescending(a => a.Id).Take(100_000).ToListAsync();
            using var wb = new XLWorkbook();
            var ws = wb.AddWorksheet("Auditoría");
            string[] cab = { "Fecha", "Evento", "Resultado", "Usuario", "Proyecto", "Detalle", "IP", "Navegador" };
            for (var i = 0; i < cab.Length; i++) ws.Cell(1, i + 1).Value = cab[i];
            var f = 2;
            foreach (var r in filas)
            {
                ws.Cell(f, 1).Value = r.Fecha.ToLocalTime();
                ws.Cell(f, 1).Style.DateFormat.Format = "dd/mm/yyyy hh:mm:ss";
                ws.Cell(f, 2).Value = Seguro(r.Evento);
                ws.Cell(f, 3).Value = r.Exito ? "Correcto" : "Fallido";
                ws.Cell(f, 4).Value = Seguro(r.Email);
                ws.Cell(f, 5).Value = r.ProyectoId is int p ? proyectos.Proyecto(p)?.Codigo ?? p.ToString() : "";
                ws.Cell(f, 6).Value = Seguro(r.Detalle);
                ws.Cell(f, 7).Value = Seguro(r.Ip);
                ws.Cell(f, 8).Value = Seguro(r.Navegador);
                f++;
            }
            ws.Row(1).Style.Font.Bold = true;
            ws.SheetView.FreezeRows(1);
            ws.Range(1, 1, Math.Max(1, f - 1), cab.Length).SetAutoFilter();
            ws.Columns().AdjustToContents(1, Math.Min(f, 300), 8, 70);
            var ms = new MemoryStream();
            wb.SaveAs(ms);
            ms.Position = 0;
            aud.Registrar(ctx, "Auditoría exportada", true, $"{filas.Count} registros", null, ctx.User.Identity?.Name);
            return Results.File(ms, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", $"auditoria-sigweb-{DateTime.Now:yyyyMMdd-HHmm}.xlsx");
        }).RequireAuthorization(ArranqueSeguridad.PoliticaAuditoria);

    // ClosedXML guarda un string como texto (nunca como fórmula): "=HYPERLINK(...)" escrito por un atacante no se ejecuta.
    private static string Seguro(string? s) => s ?? "";
}
