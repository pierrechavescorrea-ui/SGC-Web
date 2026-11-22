using System.Globalization;
using System.Text.Json;
using ClosedXML.Excel;
using Microsoft.EntityFrameworkCore;
using SigWeb.Models;
using SigWeb.Data.Seguridad;

namespace SigWeb.Data.Plantillas;

// Orquestador de las plantillas de proyecto (ver Contrato.cs). Scoped: usa el AppDbContext del circuito.
public class ServicioPlantillas
{
    private readonly AppDbContext _db;
    private readonly IReadOnlyList<IPlantillaModulo> _modulos;

    public ServicioPlantillas(AppDbContext db, IEnumerable<IPlantillaModulo> modulos)
    {
        _db = db;
        _modulos = modulos.OrderBy(m => m.Orden).ToList();
        var repetidas = _modulos.SelectMany(m => m.Hojas.Select(h => h.Nombre)).GroupBy(n => n, StringComparer.OrdinalIgnoreCase).Where(g => g.Count() > 1).Select(g => g.Key).ToList();
        if (repetidas.Count > 0) throw new InvalidOperationException("Hojas de plantilla repetidas: " + string.Join(", ", repetidas));
    }

    public IReadOnlyList<IPlantillaModulo> Modulos => _modulos;

    public bool PuedeUsar(string clave, Accion accion)
    {
        var modulo = clave.ToLowerInvariant() switch
        {
            "tiros" or "torres" => "trazado",
            "catalogos" => "catalogos",
            _ when PlantillaCostos.EsDeCostos(clave) => "costos",
            _ => null,
        };
        return modulo is not null && AmbienteSig.Actual is { } sesion &&
            (sesion.Sistema || sesion.Permisos.Puede(modulo, Accion.Ver | accion));
    }

    private const string HojaInstrucciones = "Instrucciones";
    private const string HojaListas = "_Listas";
    private const string HojaEsquema = "_Esquema";
    private const int FilasConValidacion = 2000;

    // ------------------------------------------------------------------ Generar
    public async Task<byte[]> GenerarAsync(IReadOnlyCollection<string> claves, bool conDatos, CancellationToken ct = default)
    {
        var mods = _modulos.Where(m => claves.Count == 0 || claves.Contains(m.Clave, StringComparer.OrdinalIgnoreCase)).ToList();
        using var wb = new XLWorkbook();
        var ins = wb.Worksheets.Add(HojaInstrucciones);
        var listas = wb.Worksheets.Add(HojaListas);
        var esquema = wb.Worksheets.Add(HojaEsquema);

        // Listas desplegables: una columna por catálogo o lista fija, con nombre definido.
        var catalogos = await _db.Catalogos.AsNoTracking().Where(c => c.Activo).Select(c => new { c.Tipo, c.Nombre }).ToListAsync(ct);
        var entidades = mods.SelectMany(m => m.Hojas).SelectMany(h => h.Columnas).Any(c => c.ListaEntidad is not null)
            ? await ReferenciasCostos.ListasAsync(_db, ct) : null;
        var nombresLista = new Dictionary<string, string>();
        string RangoLista(ColumnaPlantilla c)
        {
            var clave = c.Tipo == TipoColumna.SiNo ? "__sino" : c.ListaFija is not null ? "__fija:" + string.Join("|", c.ListaFija)
                : c.ListaEntidad is not null ? "ent:" + c.ListaEntidad : "cat:" + c.Lista;
            if (nombresLista.TryGetValue(clave, out var n)) return n;
            var valores = c.Tipo == TipoColumna.SiNo ? new List<string> { "Sí", "No" }
                : c.ListaFija is not null ? c.ListaFija.ToList()
                : c.ListaEntidad is string entidad ? entidades![entidad].ToList()
                : catalogos.Where(x => string.Equals(x.Tipo, c.Lista, StringComparison.OrdinalIgnoreCase)).Select(x => x.Nombre).Distinct().OrderBy(x => x).ToList();
            var col = nombresLista.Count + 1;
            listas.Cell(1, col).Value = c.Tipo == TipoColumna.SiNo ? "Sí/No" : c.Lista ?? c.Titulo;
            for (var i = 0; i < valores.Count; i++) listas.Cell(i + 2, col).Value = valores[i];
            n = "L_" + col;
            // Lista vacía (día 0): el rango apunta a una celda vacía y Excel acepta lo que se escriba (aviso, no bloqueo).
            var hasta = Math.Max(2, valores.Count + 1);
            wb.DefinedNames.Add(n, listas.Range(2, col, hasta, col));
            nombresLista[clave] = n;
            return n;
        }

        ins.Cell(1, 1).Value = "Plantilla de proyecto SGC Web";
        ins.Cell(1, 1).Style.Font.SetBold().Font.SetFontSize(14);
        var reglas = new[]
        {
            "1. Llene cada hoja desde la fila 2. No cambie el nombre de las hojas; los encabezados se reconocen por su texto.",
            "2. Columnas con * son obligatorias. La primera columna obligatoria es el código que identifica la fila.",
            "3. Si el código ya existe, la fila ACTUALIZA ese registro; las celdas vacías no borran lo que ya hay.",
            "4. Relaciones entre hojas por código o nombre (p. ej. la torre lleva el nombre de su tiro).",
            "5. Fechas en formato fecha de Excel (dd/mm/aaaa). Números sin separador de miles.",
            "6. Las listas desplegables salen de catálogos, entidades del proyecto o listas fijas. El sistema valida las referencias al cargar.",
            "7. Al cargar, SGC Web muestra una vista previa con los errores por celda; nada se guarda hasta confirmar.",
            "8. Cada carga se puede deshacer completa; lo creado va a la Papelera o se elimina si la entidad no tiene Papelera.",
        };
        for (var i = 0; i < reglas.Length; i++) ins.Cell(3 + i, 1).Value = reglas[i];
        var fi = 4 + reglas.Length;
        ins.Cell(fi, 1).Value = "Hoja"; ins.Cell(fi, 2).Value = "Módulo"; ins.Cell(fi, 3).Value = "Contenido"; ins.Cell(fi, 4).Value = "Obligatorias";
        ins.Range(fi, 1, fi, 4).Style.Font.SetBold().Fill.SetBackgroundColor(XLColor.FromHtml("#e9edf3"));

        esquema.Cell(1, 1).Value = "Hoja"; esquema.Cell(1, 2).Value = "Modulo"; esquema.Cell(1, 3).Value = "Clave"; esquema.Cell(1, 4).Value = "Titulo";
        var fe = 2;
        foreach (var m in mods)
            foreach (var h in m.Hojas)
            {
                fi++;
                ins.Cell(fi, 1).Value = h.Nombre; ins.Cell(fi, 2).Value = m.Nombre; ins.Cell(fi, 3).Value = h.Descripcion;
                ins.Cell(fi, 4).Value = string.Join(", ", h.Columnas.Where(c => c.Obligatoria).Select(c => c.Titulo));
                ins.Cell(fi, 1).SetHyperlink(new XLHyperlink($"'{h.Nombre}'!A1"));

                var ws = wb.Worksheets.Add(h.Nombre);
                for (var c = 0; c < h.Columnas.Count; c++)
                {
                    var col = h.Columnas[c];
                    var celda = ws.Cell(1, c + 1);
                    celda.Value = col.Titulo + (col.Obligatoria ? " *" : "");
                    if (!string.IsNullOrWhiteSpace(col.Ayuda)) celda.CreateComment().AddText(col.Ayuda);
                    var rango = ws.Range(2, c + 1, FilasConValidacion, c + 1);
                    switch (col.Tipo)
                    {
                        case TipoColumna.Texto: rango.Style.NumberFormat.Format = "@"; break;
                        case TipoColumna.Entero: rango.Style.NumberFormat.Format = "0"; break;
                        case TipoColumna.Fecha: rango.Style.DateFormat.Format = "dd/mm/yyyy"; break;
                        case TipoColumna.Lista:
                            rango.Style.NumberFormat.Format = "@";
                            goto case TipoColumna.SiNo;
                        case TipoColumna.SiNo:
                            var dv = rango.CreateDataValidation();
                            dv.List("=" + RangoLista(col), true);
                            dv.ErrorStyle = col.Tipo == TipoColumna.SiNo || col.ListaFija is not null ? XLErrorStyle.Stop : XLErrorStyle.Warning;
                            break;
                    }
                    esquema.Cell(fe, 1).Value = h.Nombre; esquema.Cell(fe, 2).Value = m.Clave;
                    esquema.Cell(fe, 3).Value = col.Clave; esquema.Cell(fe, 4).Value = col.Titulo; fe++;
                }
                var cab = ws.Range(1, 1, 1, h.Columnas.Count);
                cab.Style.Font.SetBold().Fill.SetBackgroundColor(XLColor.FromHtml("#1f2a3a")).Font.SetFontColor(XLColor.White);
                foreach (var (col, k) in h.Columnas.Select((c, k) => (c, k)).Where(x => x.c.Obligatoria))
                    ws.Cell(1, k + 1).Style.Fill.SetBackgroundColor(XLColor.FromHtml("#8b1a1f"));
                ws.SheetView.FreezeRows(1);

                if (conDatos)
                {
                    var filas = await m.FilasActualesAsync(h.Nombre, _db, ct);
                    for (var r = 0; r < filas.Count; r++)
                        for (var c = 0; c < h.Columnas.Count; c++)
                            if (filas[r].TryGetValue(h.Columnas[c].Clave, out var v) && v is not null)
                                ws.Cell(r + 2, c + 1).Value = v switch
                                {
                                    DateTime d => d,
                                    int n => n,
                                    double d => d,
                                    decimal d => d,
                                    bool b => b ? "Sí" : "No",
                                    _ => v.ToString()
                                };
                }
                ws.Range(1, 1, Math.Max(2, ws.LastRowUsed()?.RowNumber() ?? 2), h.Columnas.Count).SetAutoFilter();
                ws.Columns(1, h.Columnas.Count).AdjustToContents(1, 1, 12, 60);
            }
        ins.Columns(1, 4).AdjustToContents(4, fi, 10, 90);
        listas.Hide();
        esquema.Hide();
        using var ms = new MemoryStream();
        wb.SaveAs(ms);
        return ms.ToArray();
    }

    // ------------------------------------------------------------------ Leer y validar
    public sealed class ResultadoValidacion
    {
        public string NombreArchivo { get; init; } = "";
        public List<PlanModulo> Planes { get; } = new();
        public List<ErrorCelda> Errores { get; } = new();             // generales (archivo, hojas, tipos de dato)
        public List<string> HojasIgnoradas { get; } = new();
        public IEnumerable<ErrorCelda> TodosLosErrores => Errores.Concat(Planes.SelectMany(p => p.Errores));
        public bool Valido => !TodosLosErrores.Any();
        public int Altas => Planes.Sum(p => p.Altas);
        public int Actualizaciones => Planes.Sum(p => p.Actualizaciones);
    }

    public async Task<ResultadoValidacion> ValidarAsync(Stream archivo, string nombreArchivo, CancellationToken ct = default, string? modulo = null)
    {
        var res = new ResultadoValidacion { NombreArchivo = nombreArchivo };
        XLWorkbook wb;
        try { wb = SigWeb.Data.Excel.LibroExcel.Abrir(archivo); }
        catch (Exception ex) { res.Errores.Add(new("", 0, "", "No se pudo leer el libro: " + ex.Message)); return res; }
        using (wb)
        {
            var mods = _modulos.Where(m => modulo is null ? !PlantillaCostos.EsDeCostos(m.Clave) : m.Clave == modulo).ToList();
            var conocidas = mods.SelectMany(m => m.Hojas).Select(h => h.Nombre).ToHashSet(StringComparer.OrdinalIgnoreCase);
            foreach (var ws in wb.Worksheets)
                if (!conocidas.Contains(ws.Name) && ws.Name is not (HojaInstrucciones or HojaListas or HojaEsquema)) res.HojasIgnoradas.Add(ws.Name);

            var catalogos = (await _db.Catalogos.AsNoTracking().Where(c => c.Activo).Select(c => new { c.Tipo, c.Nombre }).ToListAsync(ct))
                .GroupBy(c => c.Tipo, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(g => g.Key, g => g.Select(x => x.Nombre.Trim()).ToHashSet(StringComparer.OrdinalIgnoreCase), StringComparer.OrdinalIgnoreCase);
            var ctx = new ContextoValidacion { Db = _db, Catalogos = catalogos };

            foreach (var m in mods)
            {
                var hojas = new Dictionary<string, IReadOnlyList<FilaLeida>>(StringComparer.OrdinalIgnoreCase);
                foreach (var h in m.Hojas)
                    if (wb.Worksheets.TryGetWorksheet(h.Nombre, out var ws)) hojas[h.Nombre] = LeerHoja(ws, h, res.Errores);
                if (hojas.Values.All(f => f.Count == 0)) continue;
                if (!PuedeUsar(m.Clave, Accion.Importar))
                {
                    res.Errores.Add(new("", 0, "", $"No tiene permiso para importar {m.Nombre}."));
                    continue;
                }
                // Listas: después de los módulos anteriores (el de catálogos agrega sus altas al contexto).
                var limpias = new Dictionary<string, IReadOnlyList<FilaLeida>>(StringComparer.OrdinalIgnoreCase);
                foreach (var (nombre, filas) in hojas)
                {
                    var h = m.Hojas.First(x => string.Equals(x.Nombre, nombre, StringComparison.OrdinalIgnoreCase));
                    limpias[nombre] = filas.Where(f => ValidarListas(f, h, ctx, res.Errores)).ToList();
                }
                var plan = await m.ValidarAsync(limpias, ctx, ct);
                res.Planes.Add(plan);
            }
        }
        return res;
    }

    private static List<FilaLeida> LeerHoja(IXLWorksheet ws, HojaPlantilla h, List<ErrorCelda> errores)
    {
        var filas = new List<FilaLeida>();
        var ultimaCol = ws.LastColumnUsed()?.ColumnNumber() ?? 0;
        var mapa = new Dictionary<int, ColumnaPlantilla>();
        for (var c = 1; c <= ultimaCol; c++)
        {
            var t = ws.Cell(1, c).GetString().Replace("*", "").Trim();
            var col = h.Columnas.FirstOrDefault(x => string.Equals(x.Titulo, t, StringComparison.OrdinalIgnoreCase)
                                                  || string.Equals(x.Clave, t, StringComparison.OrdinalIgnoreCase));
            if (col is not null && !mapa.ContainsValue(col)) mapa[c] = col;
        }
        foreach (var falta in h.Columnas.Where(c => c.Obligatoria && !mapa.ContainsValue(c)))
            errores.Add(new(h.Nombre, 1, falta.Titulo, "Falta la columna obligatoria."));
        if (h.Columnas.Any(c => c.Obligatoria && !mapa.ContainsValue(c))) return filas;

        var ultima = ws.LastRowUsed()?.RowNumber() ?? 1;
        for (var r = 2; r <= ultima; r++)
        {
            var fila = new FilaLeida { NumeroFila = r };
            var vacia = true;
            var ok = true;
            foreach (var (c, col) in mapa)
            {
                var celda = ws.Cell(r, c);
                if (celda.IsEmpty()) continue;
                var (valor, error) = Normalizar(celda, col);
                if (valor is null && error is null) continue;
                vacia = false;
                if (error is not null) { errores.Add(new(h.Nombre, r, col.Titulo, error)); ok = false; continue; }
                fila.Valores[col.Clave] = valor;
            }
            if (vacia) continue;
            foreach (var col in h.Columnas.Where(c => c.Obligatoria && !fila.Tiene(c.Clave) && mapa.ContainsValue(c)))
            { errores.Add(new(h.Nombre, r, col.Titulo, "Obligatorio.")); ok = false; }
            if (ok) filas.Add(fila);
        }
        return filas;
    }

    private static (string? Valor, string? Error) Normalizar(IXLCell celda, ColumnaPlantilla col)
    {
        var v = celda.Value;
        string Texto() => (v.IsText ? v.GetText() : celda.GetFormattedString()).Trim();
        switch (col.Tipo)
        {
            case TipoColumna.Entero:
                if (v.IsNumber)
                {
                    var numero = v.GetNumber();
                    return numero is >= int.MinValue and <= int.MaxValue && numero % 1 == 0
                        ? (((int)numero).ToString(CultureInfo.InvariantCulture), null)
                        : (null, "Debe ser un número entero.");
                }
                return int.TryParse(Texto(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var n) ? (n.ToString(CultureInfo.InvariantCulture), null)
                    : Texto().Length == 0 ? (null, null) : (null, "Debe ser un número entero.");
            case TipoColumna.Decimal:
                if (v.IsNumber) return (v.GetNumber().ToString("R", CultureInfo.InvariantCulture), null);
                var t = Texto().Replace(" ", "");
                if (t.Length == 0) return (null, null);
                // Sin separador de miles: la coma o el punto es el decimal.
                if (t.Count(ch => ch is ',' or '.') <= 1 && double.TryParse(t.Replace(',', '.'), NumberStyles.Float, CultureInfo.InvariantCulture, out var d) && double.IsFinite(d))
                    return (d.ToString("R", CultureInfo.InvariantCulture), null);
                return (null, "Debe ser un número (sin separador de miles).");
            case TipoColumna.Fecha:
                if (v.IsDateTime) return (v.GetDateTime().ToString("yyyy-MM-dd", CultureInfo.InvariantCulture), null);
                if (v.IsNumber)
                {
                    try { return (DateTime.FromOADate(v.GetNumber()).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture), null); }
                    catch (ArgumentException) { return (null, "Fecha fuera del intervalo permitido."); }
                }
                var tf = Texto();
                if (tf.Length == 0) return (null, null);
                return DateTime.TryParseExact(tf, new[] { "dd/MM/yyyy", "d/M/yyyy", "yyyy-MM-dd" }, CultureInfo.InvariantCulture, DateTimeStyles.None, out var f)
                    ? (f.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture), null) : (null, "Fecha no válida (use dd/mm/aaaa).");
            case TipoColumna.SiNo:
                if (v.IsBoolean) return (v.GetBoolean() ? "Sí" : "No", null);
                var ts = Texto().ToLowerInvariant();
                if (ts.Length == 0) return (null, null);
                return ts is "sí" or "si" or "s" or "x" or "1" or "true" ? ("Sí", null) : ts is "no" or "n" or "0" or "false" ? ("No", null) : (null, "Use Sí o No.");
            default:
                var tx = Texto();
                return tx.Length == 0 ? (null, null) : (tx, null);
        }
    }

    private static bool ValidarListas(FilaLeida f, HojaPlantilla h, ContextoValidacion ctx, List<ErrorCelda> errores)
    {
        var ok = true;
        foreach (var col in h.Columnas.Where(c => c.Tipo == TipoColumna.Lista && f.Tiene(c.Clave)))
        {
            var v = f.Texto(col.Clave)!;
            var vale = col.ListaFija is not null ? col.ListaFija.Contains(v, StringComparer.OrdinalIgnoreCase)
                : col.Lista is null || ctx.EnCatalogo(col.Lista, v);
            if (!vale)
            {
                errores.Add(new(h.Nombre, f.NumeroFila, col.Titulo,
                    col.ListaFija is not null ? $"«{v}» no es una opción válida." : $"«{v}» no está en el catálogo «{col.Lista}» (agréguelo en la hoja de ítems de catálogo)."));
                ok = false;
            }
        }
        return ok;
    }

    // ------------------------------------------------------------------ Aplicar
    public async Task<CargaProyecto> AplicarAsync(ResultadoValidacion v, string responsable, string motivo, CancellationToken ct = default)
    {
        if (!v.Valido) throw new InvalidOperationException("La plantilla tiene errores: corríjalos y vuelva a cargarla.");
        if (v.Altas + v.Actualizaciones == 0) throw new InvalidOperationException("La plantilla no trae cambios.");
        if (string.IsNullOrWhiteSpace(motivo)) throw new InvalidOperationException("Indique el motivo de la carga.");
        if (v.Planes.Any(p => p.Altas + p.Actualizaciones > 0 && !PuedeUsar(p.Clave, Accion.Importar)))
            throw new PermisoDenegadoException("No tiene permiso para importar todos los módulos de la plantilla.");
        _db.ChangeTracker.Clear();
        await using var tx = v.Planes.Any(p => PlantillaCostos.EsDeCostos(p.Clave))
            ? await _db.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable, ct)
            : await _db.Database.BeginTransactionAsync(ct);
        try
        {
            var carga = new CargaProyecto
            {
                Fecha = DateTime.Now, NombreArchivo = v.NombreArchivo, Responsable = (responsable ?? "").Trim(), Motivo = motivo.Trim(),
                Modulos = string.Join(",", v.Planes.Where(p => p.Altas + p.Actualizaciones > 0).Select(p => p.Clave)),
                Altas = v.Altas, Actualizaciones = v.Actualizaciones
            };
            _db.CargasProyecto.Add(carga);
            await _db.SaveChangesAsync(ct);
            var ctx = new ContextoAplicacion { Db = _db, Carga = carga, ModuloActual = "" };
            var orden = 0;
            foreach (var plan in v.Planes.Where(p => p.Altas + p.Actualizaciones > 0))
            {
                var m = _modulos.First(x => x.Clave == plan.Clave);
                ctx.ModuloActual = m.Clave;
                ctx.Pendientes.Clear();
                await m.AplicarAsync(plan, ctx, ct);
                await _db.SaveChangesAsync(ct);
                await PersistirPendientes();
                // Lo que el módulo cree en sus efectos secundarios (p. ej. hoja de vida de la torre) también se registra.
                await m.DespuesDeGuardarAsync(plan, ctx, ct);
                await _db.SaveChangesAsync(ct);
                await PersistirPendientes();

                async Task PersistirPendientes()
                {
                    foreach (var (mod, ent, op, antes) in ctx.Pendientes)
                        _db.CambiosCargaProyecto.Add(new CambioCargaProyecto
                        {
                            CargaProyectoId = carga.Id, Orden = ++orden, Modulo = mod, Entidad = ent.GetType().Name,
                            EntidadId = Convert.ToInt32(_db.Entry(ent).Property("Id").CurrentValue), Operacion = op, AntesJson = antes
                        });
                    ctx.Pendientes.Clear();
                    await _db.SaveChangesAsync(ct);
                }
            }
            await tx.CommitAsync(ct);
            _db.ChangeTracker.Clear();
            return carga;
        }
        catch
        {
            await tx.RollbackAsync(CancellationToken.None);
            _db.ChangeTracker.Clear();
            throw;
        }
    }

    // ------------------------------------------------------------------ Historial y deshacer
    public Task<List<CargaProyecto>> CargasAsync(CancellationToken ct = default) =>
        _db.CargasProyecto.AsNoTracking().OrderByDescending(c => c.Fecha).ThenByDescending(c => c.Id).ToListAsync(ct);

    // Deshace una carga completa: las altas van a la Papelera (o se borran si la entidad no tiene Papelera) con una
    // misma marca de tiempo, y las actualizaciones recuperan sus valores anteriores. En orden inverso.
    public async Task DeshacerAsync(int cargaId, CancellationToken ct = default)
    {
        _db.ChangeTracker.Clear();
        await using var tx = await _db.Database.BeginTransactionAsync(ct);
        try
        {
            var carga = await _db.CargasProyecto.FirstAsync(c => c.Id == cargaId, ct);
            if (carga.Estado != EstadoCargaProyecto.Aplicada) throw new InvalidOperationException("Esta carga ya se deshizo.");
            var cambios = await _db.CambiosCargaProyecto.AsNoTracking().Where(c => c.CargaProyectoId == cargaId).OrderByDescending(c => c.Orden).ToListAsync(ct);
            if (cambios.Any(c => !PuedeUsar(c.Modulo, Accion.Editar | Accion.Eliminar) && !PuedeUsar(c.Modulo, Accion.Administrar)))
                throw new PermisoDenegadoException("No tiene permiso para deshacer todos los cambios de esta carga.");
            foreach (var grupo in cambios.GroupBy(c => c.Modulo))
                if (_modulos.FirstOrDefault(m => m.Clave == grupo.Key) is { } m) await m.AlDeshacerAsync(grupo.ToList(), _db, ct);
            var ahora = DateTime.Now;
            var tipos = _db.Model.GetEntityTypes().GroupBy(t => t.ClrType.Name).ToDictionary(g => g.Key, g => g.First());
            foreach (var c in cambios)
            {
                if (!tipos.TryGetValue(c.Entidad, out var tipo)) continue;
                var ent = await BuscarAsync(tipo.ClrType, c.EntidadId, ct);
                if (ent is null) continue;
                var entrada = _db.Entry(ent);
                if (c.Operacion == OperacionCambioCarga.Alta)
                {
                    if (tipo.FindProperty("Eliminado") is not null)
                    {
                        entrada.Property("Eliminado").CurrentValue = true;
                        if (tipo.FindProperty("FechaEliminacion") is not null) entrada.Property("FechaEliminacion").CurrentValue = ahora;
                    }
                    // Sin Papelera: se borra solo si nada lo referencia (ni siquiera algo que esté en la Papelera, que al
                    // restaurarse lo necesitaría; p. ej. la capa de las torres deshechas).
                    else if (!await TieneDependientesAsync(tipo, c.EntidadId, ct)) _db.Remove(ent);
                }
                else if (c.AntesJson is not null)
                {
                    var antes = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(c.AntesJson) ?? new();
                    foreach (var p in tipo.GetProperties().Where(p => !p.IsPrimaryKey() && antes.ContainsKey(p.Name)))
                        entrada.Property(p.Name).CurrentValue = antes[p.Name].Deserialize(p.ClrType);
                }
                await _db.SaveChangesAsync(ct);
            }
            // Los efectos secundarios (p. ej. el sincronizador de Localización) pueden limpiar el ChangeTracker:
            // se vuelve a leer la carga para marcarla, si no el cambio de estado se perdería y se podría deshacer dos veces.
            carga = await _db.CargasProyecto.FirstAsync(c => c.Id == cargaId, ct);
            carga.Estado = EstadoCargaProyecto.Deshecha;
            carga.FechaDeshecha = ahora;
            await _db.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);
            _db.ChangeTracker.Clear();
        }
        catch
        {
            await tx.RollbackAsync(CancellationToken.None);
            _db.ChangeTracker.Clear();
            throw;
        }
    }

    private async Task<bool> TieneDependientesAsync(Microsoft.EntityFrameworkCore.Metadata.IEntityType tipo, int id, CancellationToken ct)
    {
        foreach (var fk in tipo.GetReferencingForeignKeys().Where(f => f.Properties.Count == 1))
        {
            var metodo = typeof(ServicioPlantillas).GetMethod(nameof(ContarDependientes), System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!
                .MakeGenericMethod(fk.DeclaringEntityType.ClrType);
            if (await (Task<bool>)metodo.Invoke(this, new object[] { fk.Properties[0].Name, id, ct })!) return true;
        }
        return false;
    }

    private Task<bool> ContarDependientes<T>(string propiedad, int id, CancellationToken ct) where T : class =>
        _db.Set<T>().IgnoreQueryFilters().AnyAsync(x => EF.Property<int?>(x, propiedad) == id, ct);

    private Task<object?> BuscarAsync(Type tipo, int id, CancellationToken ct) =>
        (Task<object?>)typeof(ServicioPlantillas).GetMethod(nameof(Buscar), System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!
            .MakeGenericMethod(tipo).Invoke(this, new object[] { id, ct })!;

    private async Task<object?> Buscar<T>(int id, CancellationToken ct) where T : class =>
        await _db.Set<T>().IgnoreQueryFilters().FirstOrDefaultAsync(x => EF.Property<int>(x, "Id") == id, ct);

    // Valores escalares actuales de una entidad rastreada (para deshacer una actualización).
    public static string Instantanea(AppDbContext db, object entidad)
    {
        var e = db.Entry(entidad);
        var d = e.Metadata.GetProperties().Where(p => !p.IsPrimaryKey()).ToDictionary(p => p.Name, p => e.Property(p.Name).CurrentValue);
        return JsonSerializer.Serialize(d);
    }
}
