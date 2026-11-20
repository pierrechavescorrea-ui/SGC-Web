using System.Globalization;
using Microsoft.EntityFrameworkCore;
using SigWeb.Models;

namespace SigWeb.Data.Trazado;

// Revisiones de la tabla de torres: consulta del historial y deshacer la revisión vigente (ver Models/RevisionTablaTorres).
// Deshacer devuelve cada valor anterior: reactiva el campo reemplazado (si sigue en la Papelera) o lo vuelve a crear con
// ValorAnterior (si la Papelera ya lo purgó); los datos nuevos se retiran. Si un valor se volvió a cambiar después de la
// revisión, no se toca y se informa. La revisión anterior vuelve a quedar vigente.
public static class ServicioRevisionesTabla
{
    public sealed record ItemHistorial(RevisionTablaTorres Revision, List<CambioTablaTorres> Cambios);

    public static async Task<List<RevisionTablaTorres>> RevisionesAsync(AppDbContext db) =>
        await db.RevisionesTablaTorres.AsNoTracking().OrderByDescending(r => r.FechaCarga).ToListAsync();

    public static async Task<List<CambioTablaTorres>> CambiosAsync(AppDbContext db, int revisionId) =>
        await db.CambiosTablaTorres.AsNoTracking().Where(c => c.RevisionTablaTorresId == revisionId)
            .OrderBy(c => c.CodigoTorre).ThenBy(c => c.Ambito).ThenBy(c => c.Clave).ToListAsync();

    // Historial de una torre (revisiones no deshechas, de la más reciente a la más antigua).
    public static async Task<List<ItemHistorial>> HistorialTorreAsync(AppDbContext db, int torreId)
    {
        var cambios = await db.CambiosTablaTorres.AsNoTracking().Where(c => c.ElementoTrazadoId == torreId).ToListAsync();
        if (cambios.Count == 0) return new();
        var ids = cambios.Select(c => c.RevisionTablaTorresId).Distinct().ToList();
        var revs = await db.RevisionesTablaTorres.AsNoTracking().Where(r => ids.Contains(r.Id) && r.Estado != EstadoRevisionTabla.Deshecha).ToListAsync();
        return revs.OrderByDescending(r => r.Fecha ?? r.FechaCarga).ThenByDescending(r => r.Id)
            .Select(r => new ItemHistorial(r, cambios.Where(c => c.RevisionTablaTorresId == r.Id).OrderBy(c => c.Ambito).ThenBy(c => c.Clave).ToList()))
            .ToList();
    }

    public sealed record ResultadoDeshacer(int Devueltos, int Retirados, List<string> NoTocados);

    public static async Task<ResultadoDeshacer> DeshacerAsync(AppDbContext db, int revisionId, string motivo)
    {
        var rev = await db.RevisionesTablaTorres.FirstOrDefaultAsync(r => r.Id == revisionId)
            ?? throw new InvalidOperationException("La revisión no existe.");
        if (rev.Estado != EstadoRevisionTabla.Vigente) throw new InvalidOperationException("Solo se puede deshacer la revisión vigente (la última cargada).");
        if (string.IsNullOrWhiteSpace(motivo)) throw new InvalidOperationException("Escribe el motivo.");

        await using var tx = await db.Database.BeginTransactionAsync();
        // Orden inverso: si la revisión tocó dos veces lo mismo (filas repetidas), se deshace del último al primero.
        var cambios = await db.CambiosTablaTorres.Where(c => c.RevisionTablaTorresId == revisionId).OrderByDescending(c => c.Id).ToListAsync();
        var ahora = DateTime.Now;
        int devueltos = 0, retirados = 0;
        var noTocados = new List<string>();
        var retiradas = new List<int>();

        foreach (var c in cambios)
        {
            // Cambios de la torre misma (cargas de KMZ / coordenadas): ubicación, nombre, código o alta.
            // Sin campo ligado: así un campo personalizado que se llame "Nombre" o "Código" no se confunde con el de la torre.
            if (c.ComponenteId is null && c.Ambito == "Torre" && c.CampoNuevoId is null && c.CampoAnteriorId is null
                && c.Clave is ClaveUbicacion or ClaveNombre or ClaveCodigo or ClaveAlta)
            {
                var torre = await db.ElementosTrazado.FirstOrDefaultAsync(x => x.Id == c.ElementoTrazadoId);
                if (torre is null) continue;
                switch (c.Clave)
                {
                    case ClaveUbicacion:
                        var actualT = Coordenada(torre.Latitud, torre.Longitud);
                        if (actualT != c.ValorNuevo) { noTocados.Add($"{c.CodigoTorre} · ubicación (cambió después)"); continue; }
                        // Sin valor anterior = la torre no tenía ubicación (0, 0).
                        var antT = LeerCoordenada(c.ValorAnterior) ?? (0d, 0d);
                        torre.Latitud = antT.Lat; torre.Longitud = antT.Lon; devueltos++;
                        break;
                    case ClaveNombre:
                        if ((torre.Nombre ?? "") != (c.ValorNuevo ?? "")) { noTocados.Add($"{c.CodigoTorre} · nombre (cambió después)"); continue; }
                        torre.Nombre = c.ValorAnterior; devueltos++;
                        break;
                    case ClaveCodigo:
                        if (torre.Codigo != c.ValorNuevo) { noTocados.Add($"{c.CodigoTorre} · código (cambió después)"); continue; }
                        torre.Codigo = c.ValorAnterior ?? torre.Codigo; devueltos++;
                        break;
                    case ClaveAlta:
                        // Torre creada por esta carga: a la Papelera (recuperable; sus datos se conservan).
                        if (!torre.Eliminado)
                        {
                            torre.Eliminado = true; torre.FechaEliminacion = ahora; retirados++;
                            retiradas.Add(torre.Id);   // su ítem de "Localización" se desactiva al final (MarcarInactivo limpia el contexto)
                        }
                        break;
                }
                continue;
            }

            if (c.Tipo == TipoCambioTabla.Coordenada)
            {
                var pata = c.ComponenteId is int pid ? await db.Componentes.FirstOrDefaultAsync(x => x.Id == pid) : null;
                if (pata is null) continue;
                var nuevo = LeerCoordenada(c.ValorNuevo);
                if (nuevo is not null && (pata.Latitud is null || Math.Abs(pata.Latitud.Value - nuevo.Value.Lat) > 1e-7 || Math.Abs((pata.Longitud ?? 0) - nuevo.Value.Lon) > 1e-7))
                { noTocados.Add($"{c.CodigoTorre} · {c.Ambito} · ubicación (cambió después)"); continue; }
                var ant = LeerCoordenada(c.ValorAnterior);
                pata.Latitud = ant?.Lat; pata.Longitud = ant?.Lon;
                devueltos++;
                continue;
            }

            if (c.ComponenteId is int compId)
            {
                var actual = c.CampoNuevoId is int nid ? await db.CamposExtraComponente.FirstOrDefaultAsync(x => x.Id == nid) : null;
                if (actual is null || actual.Eliminado) { noTocados.Add($"{c.CodigoTorre} · {c.Ambito} · {c.Clave} (cambió después)"); continue; }
                actual.Eliminado = true; actual.FechaEliminacion = ahora;
                if (c.ValorAnterior is null) { retirados++; continue; }
                var previo = c.CampoAnteriorId is int aid ? await db.CamposExtraComponente.IgnoreQueryFilters().FirstOrDefaultAsync(x => x.Id == aid) : null;
                if (previo is not null) { previo.Eliminado = false; previo.FechaEliminacion = null; }
                else db.CamposExtraComponente.Add(new CampoExtraComponente { ComponenteId = compId, Clave = c.Clave, Valor = c.ValorAnterior, Origen = $"Deshacer revisión {rev.Codigo}" });
                devueltos++;
            }
            else
            {
                var actual = c.CampoNuevoId is int nid ? await db.CamposExtraTorre.FirstOrDefaultAsync(x => x.Id == nid) : null;
                if (actual is null || actual.Eliminado) { noTocados.Add($"{c.CodigoTorre} · {c.Clave} (cambió después)"); continue; }
                actual.Eliminado = true; actual.FechaEliminacion = ahora;
                if (c.ValorAnterior is null) { retirados++; continue; }
                var previo = c.CampoAnteriorId is int aid ? await db.CamposExtraTorre.IgnoreQueryFilters().FirstOrDefaultAsync(x => x.Id == aid) : null;
                if (previo is not null) { previo.Eliminado = false; previo.FechaEliminacion = null; }
                else db.CamposExtraTorre.Add(new CampoExtraTorre { ElementoTrazadoId = c.ElementoTrazadoId, Clave = c.Clave, Valor = c.ValorAnterior, Origen = $"Deshacer revisión {rev.Codigo}" });
                devueltos++;
            }
        }

        rev.Estado = EstadoRevisionTabla.Deshecha; rev.FechaDeshecha = ahora; rev.MotivoDeshacer = motivo.Trim();
        var anterior = await db.RevisionesTablaTorres.Where(r => r.Id != rev.Id && r.Origen == rev.Origen && r.Estado == EstadoRevisionTabla.Anterior)
            .OrderByDescending(r => r.FechaCarga).FirstOrDefaultAsync();
        if (anterior is not null) anterior.Estado = EstadoRevisionTabla.Vigente;
        await db.SaveChangesAsync();
        foreach (var id in retiradas) SincronizadorCatalogoLocalizacion.MarcarInactivo(db, id);
        await tx.CommitAsync();
        return new ResultadoDeshacer(devueltos, retirados, noTocados);
    }

    // Claves de los cambios de la torre misma (cargas de KMZ / coordenadas).
    public const string ClaveUbicacion = "Ubicación", ClaveNombre = "Nombre", ClaveCodigo = "Código", ClaveAlta = "Torre nueva";

    // Diferencias entre dos revisiones (como "Comparar A → B" de Riesgos): los cambios de las revisiones posteriores a A
    // hasta B inclusive (sin las deshechas), uno por torre + dónde + campo, con el primer valor anterior y el último nuevo.
    public sealed record Diferencia(string Torre, int TorreId, string Ambito, string Clave, string? Antes, string? Ahora, TipoCambioTabla Tipo,
        string Revisiones, string? Observacion);

    public static async Task<List<Diferencia>> CompararAsync(AppDbContext db, int? desdeId, int hastaId)
    {
        var revs = await db.RevisionesTablaTorres.AsNoTracking().Where(r => r.Estado != EstadoRevisionTabla.Deshecha).OrderBy(r => r.FechaCarga).ThenBy(r => r.Id).ToListAsync();
        var hasta = revs.FirstOrDefault(r => r.Id == hastaId);
        if (hasta is null) return new();
        var desde = desdeId is int d ? revs.FirstOrDefault(r => r.Id == d) : null;
        var rango = revs.Where(r => (desde is null || r.FechaCarga > desde.FechaCarga || (r.FechaCarga == desde.FechaCarga && r.Id > desde.Id))
                                    && (r.FechaCarga < hasta.FechaCarga || (r.FechaCarga == hasta.FechaCarga && r.Id <= hasta.Id))).ToList();
        var ids = rango.Select(r => r.Id).ToList();
        var orden = rango.Select((r, i) => (r.Id, i)).ToDictionary(x => x.Id, x => x.i);
        var codigo = rango.ToDictionary(r => r.Id, r => r.Codigo);
        var cambios = await db.CambiosTablaTorres.AsNoTracking().Where(c => ids.Contains(c.RevisionTablaTorresId)).ToListAsync();
        return cambios.GroupBy(c => (c.ElementoTrazadoId, c.Ambito, c.Clave))
            .Select(g =>
            {
                var l = g.OrderBy(c => orden[c.RevisionTablaTorresId]).ThenBy(c => c.Id).ToList();
                var tipo = l.Any(c => c.Tipo == TipoCambioTabla.Coordenada) ? TipoCambioTabla.Coordenada
                         : l[0].ValorAnterior is null ? TipoCambioTabla.Nuevo : TipoCambioTabla.Cambiado;
                return new Diferencia(l[^1].CodigoTorre, g.Key.ElementoTrazadoId, g.Key.Ambito, g.Key.Clave, l[0].ValorAnterior, l[^1].ValorNuevo, tipo,
                    string.Join(", ", l.Select(c => codigo[c.RevisionTablaTorresId]).Distinct()),
                    l.Select(c => c.Observacion).LastOrDefault(o => !string.IsNullOrWhiteSpace(o)));
            })
            .Where(x => x.Antes != x.Ahora)
            .OrderBy(x => x.Torre).ThenBy(x => x.Ambito).ThenBy(x => x.Clave).ToList();
    }

    public static string NombreTipo(TipoCambioTabla t) => t switch
    {
        TipoCambioTabla.Nuevo => "Nuevos", TipoCambioTabla.Cambiado => "Modificados", _ => "Ubicación"
    };

    public static string Coordenada(double? lat, double? lon) =>
        lat is null || lon is null ? "" : string.Create(CultureInfo.InvariantCulture, $"{lat:0.0000000};{lon:0.0000000}");

    public static (double Lat, double Lon)? LeerCoordenada(string? s)
    {
        if (string.IsNullOrWhiteSpace(s)) return null;
        var p = s.Split(';');
        return p.Length == 2 && double.TryParse(p[0], NumberStyles.Any, CultureInfo.InvariantCulture, out var la)
               && double.TryParse(p[1], NumberStyles.Any, CultureInfo.InvariantCulture, out var lo) ? (la, lo) : null;
    }

    // "…SN A29-10.xlsx" → "A29-10"; "Rev 5" → "Rev 5". Null si no se reconoce.
    public static string? RevisionDesdeNombre(string? nombre)
    {
        var n = Path.GetFileNameWithoutExtension(nombre ?? "");
        var m = System.Text.RegularExpressions.Regex.Match(n, @"\b([A-Z]\d{1,3}-\d{1,3})\b(?!.*\b[A-Z]\d{1,3}-\d{1,3}\b)");
        if (m.Success) return m.Groups[1].Value;
        m = System.Text.RegularExpressions.Regex.Match(n, @"\b(Rev(?:isi[oó]n)?\.?\s*\w+)\b", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
        return m.Success ? m.Groups[1].Value : null;
    }
}
