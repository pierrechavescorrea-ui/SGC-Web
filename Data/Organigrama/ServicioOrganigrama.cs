using ClosedXML.Excel;
using Microsoft.EntityFrameworkCore;
using SigWeb.Models;

namespace SigWeb.Data.Organigrama;

// Cargo en memoria (pantalla de Organigrama). La identidad es la Clave (igual en todas las versiones);
// el jefe se guarda por Clave para editar, mover y comparar sin depender de los Id de la base.
public sealed class NodoOrg
{
    public int Id { get; set; }                 // 0 = nuevo (aún no guardado)
    public Guid Clave { get; set; } = Guid.NewGuid();
    public Guid? Jefe { get; set; }
    public string Nombre { get; set; } = "";
    public string? Ocupante { get; set; }
    public string? Area { get; set; }
    public int? Plazas { get; set; }
    public bool Staff { get; set; }
    public int Orden { get; set; }
    public string? Notas { get; set; }
    public string? Supuesto { get; set; }

    public NodoOrg Copia() => (NodoOrg)MemberwiseClone();
}

public enum TipoCambioOrg { Agregado, Eliminado, Renombrado, Jefe, Ocupante, Area, Plazas, Staff }

public sealed record CambioOrg(TipoCambioOrg Tipo, Guid Clave, string Cargo, string? Antes, string? Despues);

public sealed class DatosPublicacion
{
    public DateTime? VigenteDesde { get; set; } = DateTime.Today;
    public string? Motivo { get; set; }
    public string? Elaboro { get; set; }
    public string? Reviso { get; set; }
    public string? Aprobo { get; set; }
    public DateTime? FechaAprobacion { get; set; } = DateTime.Today;
}

public static class ServicioOrganigrama
{
    // ---------- Lectura ----------
    public static List<VersionOrganigrama> Versiones(AppDbContext db) =>
        db.VersionesOrganigrama.AsNoTracking().OrderByDescending(v => v.Numero).ToList();

    public static List<NodoOrg> Nodos(AppDbContext db, int versionId)
    {
        var cargos = db.CargosOrganigrama.AsNoTracking().Where(c => c.VersionOrganigramaId == versionId).ToList();
        var clavePorId = cargos.ToDictionary(c => c.Id, c => c.Clave);
        return cargos.OrderBy(c => c.Orden).ThenBy(c => c.Id).Select(c => new NodoOrg
        {
            Id = c.Id, Clave = c.Clave, Jefe = c.JefeId is int j && clavePorId.TryGetValue(j, out var k) ? k : null,
            Nombre = c.Nombre, Ocupante = c.Ocupante, Area = c.Area, Plazas = c.Plazas, Staff = c.Staff, Orden = c.Orden,
            Notas = c.Notas, Supuesto = c.Supuesto
        }).ToList();
    }

    // Personas de un cargo (coma, punto y coma o salto de línea).
    public static List<string> Personas(string? ocupante) =>
        string.IsNullOrWhiteSpace(ocupante) ? new() :
        ocupante.Split(new[] { '\n', ';', ',' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList();

    // ---------- Validación ----------
    public static List<string> Validar(IReadOnlyList<NodoOrg> nodos)
    {
        var errores = new List<string>();
        var porClave = nodos.ToDictionary(n => n.Clave);
        if (nodos.Count == 0) errores.Add("El organigrama no tiene cargos.");
        foreach (var n in nodos.Where(n => string.IsNullOrWhiteSpace(n.Nombre))) errores.Add("Hay un cargo sin nombre.");
        foreach (var n in nodos.Where(n => n.Jefe is Guid j && !porClave.ContainsKey(j))) errores.Add($"«{n.Nombre}» tiene un jefe que ya no existe.");
        foreach (var n in nodos)
        {
            var vistos = new HashSet<Guid> { n.Clave };
            var j = n.Jefe;
            while (j is Guid g && porClave.TryGetValue(g, out var p))
            {
                if (!vistos.Add(g)) { errores.Add($"«{n.Nombre}» queda en un ciclo de jefes."); break; }
                j = p.Jefe;
            }
        }
        foreach (var n in nodos.Where(n => n.Staff && n.Jefe is null)) errores.Add($"«{n.Nombre}» es de apoyo y no tiene jefe.");
        return errores.Distinct().ToList();
    }

    // ---------- Guardar el borrador ----------
    // Escribe los nodos del borrador: nuevos, cambiados y quitados (a la Papelera con la misma marca de tiempo).
    public static async Task GuardarAsync(AppDbContext db, int versionId, IReadOnlyList<NodoOrg> nodos)
    {
        var version = await db.VersionesOrganigrama.FirstAsync(v => v.Id == versionId);
        if (version.Estado != EstadoOrganigrama.Borrador) throw new InvalidOperationException("Solo se edita una versión en borrador.");
        var errores = Validar(nodos);
        if (errores.Count > 0) throw new InvalidOperationException(string.Join(" ", errores));

        await using var tx = await db.Database.BeginTransactionAsync();
        var existentes = await db.CargosOrganigrama.Where(c => c.VersionOrganigramaId == versionId).ToListAsync();
        var porClave = existentes.ToDictionary(c => c.Clave);
        var claves = nodos.Select(n => n.Clave).ToHashSet();
        var ahora = DateTime.Now;

        // Quitados: a la Papelera juntos (misma marca de tiempo); conservan su jefe para poder restaurarlos.
        foreach (var c in existentes.Where(c => !claves.Contains(c.Clave)))
        {
            c.Eliminado = true; c.FechaEliminacion = ahora;
        }
        foreach (var n in nodos)
        {
            if (!porClave.TryGetValue(n.Clave, out var c))
            {
                c = new CargoOrganigrama { VersionOrganigramaId = versionId, Clave = n.Clave };
                db.CargosOrganigrama.Add(c);
                porClave[n.Clave] = c;
            }
            c.Nombre = n.Nombre.Trim(); c.Ocupante = Limpio(n.Ocupante); c.Area = Limpio(n.Area); c.Plazas = n.Plazas is > 0 ? n.Plazas : null;
            c.Staff = n.Staff; c.Orden = n.Orden; c.Notas = Limpio(n.Notas); c.Supuesto = Limpio(n.Supuesto);
        }
        await db.SaveChangesAsync();
        foreach (var n in nodos)
            porClave[n.Clave].JefeId = n.Jefe is Guid j ? porClave[j].Id : null;
        await db.SaveChangesAsync();
        await tx.CommitAsync();
    }

    // ---------- Versiones ----------
    // Borrador nuevo copiado de otra versión (mismas claves → trazabilidad).
    public static async Task<VersionOrganigrama> CrearBorradorAsync(AppDbContext db, int baseId)
    {
        var b = await db.VersionesOrganigrama.AsNoTracking().FirstAsync(v => v.Id == baseId);
        var numero = (await db.VersionesOrganigrama.IgnoreQueryFilters().MaxAsync(v => (int?)v.Numero) ?? 0) + 1;
        var nueva = new VersionOrganigrama
        {
            Numero = numero, Titulo = b.Titulo, Alcance = b.Alcance, Estado = EstadoOrganigrama.Borrador, VersionBaseId = b.Id,
            Origen = $"Copia de la v{b.Numero}"
        };
        db.VersionesOrganigrama.Add(nueva);
        await db.SaveChangesAsync();
        var nodos = Nodos(db, baseId).Select(n => { var c = n.Copia(); c.Id = 0; return c; }).ToList();
        await GuardarAsync(db, nueva.Id, nodos);
        return nueva;
    }

    public static async Task PublicarAsync(AppDbContext db, int versionId, DatosPublicacion d)
    {
        var v = await db.VersionesOrganigrama.FirstAsync(x => x.Id == versionId);
        if (v.Estado != EstadoOrganigrama.Borrador) throw new InvalidOperationException("Solo se publica un borrador.");
        if (string.IsNullOrWhiteSpace(d.Motivo)) throw new InvalidOperationException("Falta el motivo del cambio.");
        var errores = Validar(Nodos(db, versionId));
        if (errores.Count > 0) throw new InvalidOperationException(string.Join(" ", errores));
        var desde = (d.VigenteDesde ?? DateTime.Today).Date;
        foreach (var anterior in await db.VersionesOrganigrama.Where(x => x.Estado == EstadoOrganigrama.Vigente && x.Id != versionId).ToListAsync())
        {
            if (anterior.VigenteDesde is DateTime ad && ad.Date > desde)
                throw new InvalidOperationException($"La v{anterior.Numero} rige desde {ad:dd/MM/yyyy}: la nueva no puede empezar antes.");
            anterior.Estado = EstadoOrganigrama.Obsoleta;
            anterior.VigenteHasta = desde.AddDays(-1) < (anterior.VigenteDesde ?? DateTime.MinValue) ? desde : desde.AddDays(-1);
        }
        v.Estado = EstadoOrganigrama.Vigente;
        v.VigenteDesde = desde; v.VigenteHasta = null;
        v.MotivoCambio = d.Motivo.Trim(); v.Elaboro = Limpio(d.Elaboro); v.Reviso = Limpio(d.Reviso); v.Aprobo = Limpio(d.Aprobo);
        v.FechaAprobacion = d.FechaAprobacion;
        await db.SaveChangesAsync();
    }

    // ---------- Comparación (por Clave) ----------
    public static List<CambioOrg> Comparar(IReadOnlyList<NodoOrg> antes, IReadOnlyList<NodoOrg> despues)
    {
        var r = new List<CambioOrg>();
        var a = antes.ToDictionary(n => n.Clave);
        var b = despues.ToDictionary(n => n.Clave);
        string? NombreJefe(Dictionary<Guid, NodoOrg> m, Guid? j) => j is Guid g && m.TryGetValue(g, out var p) ? p.Nombre : null;

        foreach (var n in despues.Where(n => !a.ContainsKey(n.Clave)))
            r.Add(new(TipoCambioOrg.Agregado, n.Clave, n.Nombre, null, NombreJefe(b, n.Jefe)));
        foreach (var n in antes.Where(n => !b.ContainsKey(n.Clave)))
            r.Add(new(TipoCambioOrg.Eliminado, n.Clave, n.Nombre, NombreJefe(a, n.Jefe), null));
        foreach (var n in despues.Where(n => a.ContainsKey(n.Clave)))
        {
            var o = a[n.Clave];
            if (!Igual(o.Nombre, n.Nombre)) r.Add(new(TipoCambioOrg.Renombrado, n.Clave, n.Nombre, o.Nombre, n.Nombre));
            if (o.Jefe != n.Jefe) r.Add(new(TipoCambioOrg.Jefe, n.Clave, n.Nombre, NombreJefe(a, o.Jefe), NombreJefe(b, n.Jefe)));
            if (!Igual(string.Join(", ", Personas(o.Ocupante)), string.Join(", ", Personas(n.Ocupante))))
                r.Add(new(TipoCambioOrg.Ocupante, n.Clave, n.Nombre, Limpio(o.Ocupante), Limpio(n.Ocupante)));
            if (!Igual(o.Area, n.Area)) r.Add(new(TipoCambioOrg.Area, n.Clave, n.Nombre, o.Area, n.Area));
            if (o.Plazas != n.Plazas) r.Add(new(TipoCambioOrg.Plazas, n.Clave, n.Nombre, o.Plazas?.ToString(), n.Plazas?.ToString()));
            if (o.Staff != n.Staff) r.Add(new(TipoCambioOrg.Staff, n.Clave, n.Nombre, o.Staff ? "Apoyo" : "Línea", n.Staff ? "Apoyo" : "Línea"));
        }
        return r.OrderBy(c => c.Tipo).ThenBy(c => c.Cargo).ToList();
    }

    public static string NombreTipo(TipoCambioOrg t) => t switch
    {
        TipoCambioOrg.Agregado => "Cargo nuevo",
        TipoCambioOrg.Eliminado => "Cargo suprimido",
        TipoCambioOrg.Renombrado => "Cambio de nombre",
        TipoCambioOrg.Jefe => "Cambio de jefe inmediato",
        TipoCambioOrg.Ocupante => "Cambio de ocupante",
        TipoCambioOrg.Area => "Cambio de área",
        TipoCambioOrg.Plazas => "Cambio de plazas",
        _ => "Línea / apoyo"
    };

    // ---------- Orden de lectura (jerárquico) y numeración 1, 1.1, 1.1.2… ----------
    public static List<(NodoOrg Nodo, string Numero, int Nivel)> EnOrden(IReadOnlyList<NodoOrg> nodos)
    {
        var hijos = nodos.Where(n => n.Jefe is not null).GroupBy(n => n.Jefe!.Value)
            .ToDictionary(g => g.Key, g => g.OrderBy(n => n.Staff ? 0 : 1).ThenBy(n => n.Orden).ToList());
        var r = new List<(NodoOrg, string, int)>();
        void Recorrer(NodoOrg n, string num, int nivel)
        {
            r.Add((n, num, nivel));
            if (!hijos.TryGetValue(n.Clave, out var hs)) return;
            for (var i = 0; i < hs.Count; i++) Recorrer(hs[i], $"{num}.{i + 1}", nivel + 1);
        }
        var raices = nodos.Where(n => n.Jefe is null).OrderBy(n => n.Orden).ToList();
        for (var i = 0; i < raices.Count; i++) Recorrer(raices[i], (i + 1).ToString(), 1);
        return r;
    }

    // ---------- Excel ----------
    public static readonly string[] Columnas = { "N.º", "Cargo", "Ocupante", "Área", "Jefe inmediato (N.º)", "Jefe inmediato", "Tipo", "Plazas", "Notas", "Supuesto", "Clave" };

    public static byte[] Exportar(VersionOrganigrama v, IReadOnlyList<NodoOrg> nodos, IReadOnlyList<VersionOrganigrama> versiones)
    {
        using var wb = new XLWorkbook();
        var ws = wb.Worksheets.Add("Organigrama");
        ws.Cell(1, 1).Value = v.Titulo;
        ws.Cell(1, 1).Style.Font.Bold = true;
        ws.Cell(2, 1).Value = $"{v.Alcance} · v{v.Numero} · {v.Estado}" + (v.VigenteDesde is DateTime d ? $" desde {d:dd/MM/yyyy}" : "");
        for (var c = 0; c < Columnas.Length; c++) ws.Cell(4, c + 1).Value = Columnas[c];
        var cab = ws.Range(4, 1, 4, Columnas.Length);
        cab.Style.Font.Bold = true; cab.Style.Fill.BackgroundColor = XLColor.FromHtml("#1F2A3A"); cab.Style.Font.FontColor = XLColor.White;
        var orden = EnOrden(nodos);
        var numPorClave = orden.ToDictionary(x => x.Nodo.Clave, x => x.Numero);
        var porClave = nodos.ToDictionary(n => n.Clave);
        var fila = 5;
        foreach (var (n, num, nivel) in orden)
        {
            ws.Cell(fila, 1).Value = num;
            ws.Cell(fila, 2).Value = n.Nombre;
            ws.Cell(fila, 2).Style.Alignment.Indent = Math.Min(nivel - 1, 15);
            ws.Cell(fila, 3).Value = n.Ocupante ?? "";
            ws.Cell(fila, 4).Value = n.Area ?? "";
            ws.Cell(fila, 5).Value = n.Jefe is Guid j && numPorClave.TryGetValue(j, out var nj) ? nj : "";
            ws.Cell(fila, 6).Value = n.Jefe is Guid j2 && porClave.TryGetValue(j2, out var pj) ? pj.Nombre : "";
            ws.Cell(fila, 7).Value = n.Staff ? "Apoyo" : "Línea";
            if (n.Plazas is int p) ws.Cell(fila, 8).Value = p;
            ws.Cell(fila, 9).Value = n.Notas ?? "";
            ws.Cell(fila, 10).Value = n.Supuesto ?? "";
            ws.Cell(fila, 11).Value = n.Clave.ToString();
            fila++;
        }
        ws.Range(4, 1, Math.Max(4, fila - 1), Columnas.Length).SetAutoFilter();
        ws.SheetView.FreezeRows(4);
        ws.Columns().AdjustToContents(4, Math.Min(fila, 400));
        foreach (var col in ws.ColumnsUsed()) if (col.Width > 50) col.Width = 50;
        ws.Column(11).Hide();   // identidad del cargo entre versiones (para reimportar sin perder la trazabilidad)

        var wv = wb.Worksheets.Add("Control de cambios");
        string[] cv = { "Versión", "Estado", "Vigente desde", "Vigente hasta", "Motivo del cambio", "Elaboró", "Revisó", "Aprobó", "Fecha de aprobación", "Origen" };
        for (var c = 0; c < cv.Length; c++) wv.Cell(1, c + 1).Value = cv[c];
        var cabv = wv.Range(1, 1, 1, cv.Length);
        cabv.Style.Font.Bold = true; cabv.Style.Fill.BackgroundColor = XLColor.FromHtml("#1F2A3A"); cabv.Style.Font.FontColor = XLColor.White;
        var r = 2;
        foreach (var x in versiones.OrderBy(x => x.Numero))
        {
            wv.Cell(r, 1).Value = $"v{x.Numero}";
            wv.Cell(r, 2).Value = x.Estado;
            if (x.VigenteDesde is DateTime a) { wv.Cell(r, 3).Value = a; wv.Cell(r, 3).Style.DateFormat.Format = "dd/mm/yyyy"; }
            if (x.VigenteHasta is DateTime h) { wv.Cell(r, 4).Value = h; wv.Cell(r, 4).Style.DateFormat.Format = "dd/mm/yyyy"; }
            wv.Cell(r, 5).Value = x.MotivoCambio ?? "";
            wv.Cell(r, 6).Value = x.Elaboro ?? "";
            wv.Cell(r, 7).Value = x.Reviso ?? "";
            wv.Cell(r, 8).Value = x.Aprobo ?? "";
            if (x.FechaAprobacion is DateTime fa) { wv.Cell(r, 9).Value = fa; wv.Cell(r, 9).Style.DateFormat.Format = "dd/mm/yyyy"; }
            wv.Cell(r, 10).Value = x.Origen ?? "";
            r++;
        }
        wv.Columns().AdjustToContents();
        foreach (var col in wv.ColumnsUsed()) if (col.Width > 60) col.Width = 60;

        using var ms = new MemoryStream();
        wb.SaveAs(ms);
        return ms.ToArray();
    }

    // Lee la hoja "Organigrama" (o la primera) con las columnas del exportado. El jefe se busca por N.º;
    // si no hay N.º, por nombre del jefe cuando es único. Devuelve los nodos y los avisos.
    public static (List<NodoOrg> Nodos, List<string> Avisos) Importar(Stream excel)
    {
        var avisos = new List<string>();
        using var wb = SigWeb.Data.Excel.LibroExcel.Abrir(excel);
        var ws = wb.Worksheets.FirstOrDefault(w => w.Name.Equals("Organigrama", StringComparison.OrdinalIgnoreCase)) ?? wb.Worksheet(1);
        // Fila de encabezados = la primera que tenga "Cargo".
        var cabecera = ws.RowsUsed().FirstOrDefault(rw => rw.CellsUsed().Any(c => Norm(c.GetString()) == "cargo"));
        if (cabecera is null) { avisos.Add("No se encontró la columna «Cargo»."); return (new(), avisos); }
        var col = new Dictionary<string, int>();
        foreach (var c in cabecera.CellsUsed())
        {
            var t = Norm(c.GetString());
            string? k = t switch
            {
                "n.o" or "no" or "n°" or "n.º" or "numero" or "item" => "num",
                "cargo" => "cargo",
                "ocupante" or "nombre" or "persona" or "responsable" => "ocupante",
                "area" or "dependencia" or "proceso" => "area",
                "jefe inmediato (n.o)" or "jefe inmediato (n.º)" or "jefe (n.o)" or "jefe (n.º)" => "jefenum",
                "jefe inmediato" or "jefe" or "reporta a" => "jefe",
                "tipo" => "tipo",
                "plazas" or "cantidad" => "plazas",
                "notas" or "observaciones" => "notas",
                "supuesto" => "supuesto",
                "clave" => "clave",
                _ => null
            };
            if (k is not null && !col.ContainsKey(k)) col[k] = c.Address.ColumnNumber;
        }
        string Celda(IXLRow rw, string k) => col.TryGetValue(k, out var n) ? rw.Cell(n).GetString().Trim() : "";

        var filas = new List<(NodoOrg Nodo, string Num, string JefeNum, string JefeNombre)>();
        var orden = 0;
        foreach (var rw in ws.RowsUsed().Where(x => x.RowNumber() > cabecera.RowNumber()))
        {
            var nombre = Celda(rw, "cargo");
            if (nombre.Length == 0) continue;
            int? plazas = int.TryParse(Celda(rw, "plazas"), out var p) && p > 0 ? p : null;
            var n = new NodoOrg
            {
                Nombre = nombre, Ocupante = Limpio(Celda(rw, "ocupante")), Area = Limpio(Celda(rw, "area")), Plazas = plazas,
                Staff = Norm(Celda(rw, "tipo")) is "apoyo" or "staff" or "asesor", Notas = Limpio(Celda(rw, "notas")),
                Supuesto = Limpio(Celda(rw, "supuesto")), Orden = orden++
            };
            if (Guid.TryParse(Celda(rw, "clave"), out var clave) && filas.All(f => f.Nodo.Clave != clave)) n.Clave = clave;
            filas.Add((n, Celda(rw, "num"), Celda(rw, "jefenum"), Celda(rw, "jefe")));
        }
        var porNum = filas.Where(f => f.Num.Length > 0).GroupBy(f => f.Num).ToDictionary(g => g.Key, g => g.First().Nodo);
        var porNombre = filas.GroupBy(f => Norm(f.Nodo.Nombre)).ToDictionary(g => g.Key, g => g.Select(x => x.Nodo).ToList());
        foreach (var f in filas)
        {
            if (f.JefeNum.Length > 0)
            {
                if (porNum.TryGetValue(f.JefeNum, out var j)) f.Nodo.Jefe = j.Clave;
                else avisos.Add($"«{f.Nodo.Nombre}»: no existe el N.º de jefe {f.JefeNum}.");
            }
            else if (f.JefeNombre.Length > 0)
            {
                if (porNombre.TryGetValue(Norm(f.JefeNombre), out var js) && js.Count == 1) f.Nodo.Jefe = js[0].Clave;
                else avisos.Add($"«{f.Nodo.Nombre}»: jefe «{f.JefeNombre}» {(js is null ? "no encontrado" : "repetido; use la columna N.º")}.");
            }
        }
        var nodos = filas.Select(f => f.Nodo).ToList();
        // Ciclos: se rompen dejando el cargo como raíz.
        foreach (var e in Validar(nodos).Where(e => e.Contains("ciclo")))
            avisos.Add(e);
        if (avisos.Any(a => a.Contains("ciclo")))
            foreach (var n in nodos) if (EnCiclo(nodos, n)) n.Jefe = null;
        foreach (var n in nodos.Where(n => n.Staff && n.Jefe is null)) n.Staff = false;
        return (nodos, avisos);
    }

    private static bool EnCiclo(IReadOnlyList<NodoOrg> nodos, NodoOrg n)
    {
        var porClave = nodos.ToDictionary(x => x.Clave);
        var vistos = new HashSet<Guid> { n.Clave };
        var j = n.Jefe;
        while (j is Guid g && porClave.TryGetValue(g, out var p)) { if (!vistos.Add(g)) return true; j = p.Jefe; }
        return false;
    }

    // ---------- Utilidades ----------
    public static string? Limpio(string? s) => string.IsNullOrWhiteSpace(s) ? null : s.Trim();

    private static bool Igual(string? a, string? b) =>
        string.Equals((a ?? "").Trim(), (b ?? "").Trim(), StringComparison.Ordinal);

    private static string Norm(string s)
    {
        var t = s.Trim().ToLowerInvariant().Normalize(System.Text.NormalizationForm.FormD);
        return new string(t.Where(ch => System.Globalization.CharUnicodeInfo.GetUnicodeCategory(ch) != System.Globalization.UnicodeCategory.NonSpacingMark).ToArray())
            .Normalize(System.Text.NormalizationForm.FormC);
    }
}
