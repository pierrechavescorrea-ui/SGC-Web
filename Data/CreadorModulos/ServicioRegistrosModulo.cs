using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.EntityFrameworkCore;
using SigWeb.Data.Tablas;
using SigWeb.Models;

namespace SigWeb.Data.CreadorModulos;

// Registros de un módulo creado en la base del proyecto. Cada operación usa su propio scope (no comparte el DbContext del
// circuito). Valores = CampoTabla/ValorCampoTabla de la tabla "mod:{clave}"; vínculos y adjuntos en sus tablas.
public sealed class ServicioRegistrosModulo
{
    public const long MaxArchivo = 50L * 1024 * 1024;
    public static readonly string[] Extensiones =
    {
        ".pdf", ".jpg", ".jpeg", ".png", ".webp", ".gif", ".heic", ".bmp", ".tif", ".tiff",
        ".doc", ".docx", ".xls", ".xlsx", ".xlsm", ".csv", ".ppt", ".pptx", ".txt", ".zip", ".rar", ".7z",
        ".dwg", ".dxf", ".kmz", ".kml", ".ifc", ".mp4", ".mov",
    };

    private readonly IServiceScopeFactory _scopes;
    private readonly IWebHostEnvironment _env;

    public ServicioRegistrosModulo(IServiceScopeFactory scopes, IWebHostEnvironment env) { _scopes = scopes; _env = env; }

    private async Task<T> ConDb<T>(Func<AppDbContext, Task<T>> f)
    {
        using var scope = _scopes.CreateScope();
        return await f(scope.ServiceProvider.GetRequiredService<AppDbContext>());
    }

    // Escrituras estructurales que la app hace sola (registrar columnas): no son decisiones del usuario, no pasan por el
    // control de escritura por módulo (como EstructuraFormulario). Los registros y valores SÍ se guardan con la sesión del usuario.
    private static IDisposable? ComoSistema()
    {
        var s = Seguridad.AmbienteSig.Actual;
        return s is null ? null : Seguridad.AmbienteSig.Usar(new Seguridad.SesionSig
        { Proyecto = s.Proyecto, UsuarioId = s.UsuarioId, Email = s.Email, Permisos = s.Permisos, Modulo = s.Modulo, Sistema = true, Ip = s.Ip });
    }

    // ---------- Campos de la tabla del módulo ----------

    // Deja en CamposTabla una columna por campo de la definición (Clave = clave del campo): crea las que faltan, revive las
    // eliminadas que siguen en el diseño y actualiza nombre/tipo/opciones. Lo que el usuario ajustó en el panel Campos
    // (visible, orden, formatos, fórmulas en otras columnas) no se toca. Campos quitados del diseño: se ocultan (datos intactos).
    public Task SincronizarCamposAsync(RegistroModulosCreados.Publicado m) => ConDb(async db =>
    {
        using var _ = ComoSistema();
        var tabla = m.Tabla;
        var existentes = await db.CamposTabla.IgnoreQueryFilters().Where(c => c.Tabla == tabla).ToListAsync();
        int orden = existentes.Count == 0 ? 0 : existentes.Max(c => c.Orden) + 1;
        var claves = new HashSet<string>(StringComparer.Ordinal);
        foreach (var c in m.Definicion.CamposConValor)
        {
            claves.Add(c.Clave);
            var ct = existentes.FirstOrDefault(x => x.Clave == c.Clave && x.Origen != OrigenCampoTabla.Nativo);
            if (ct is null)
            {
                ct = new CampoTabla { Tabla = tabla, Clave = c.Clave, Origen = OrigenCampoTabla.Manual, Orden = orden++, Visible = c.EnLista };
                db.CamposTabla.Add(ct);
            }
            ct.Eliminado = false; ct.FechaEliminacion = null;
            ct.Nombre = string.IsNullOrWhiteSpace(c.Etiqueta) ? c.Clave : c.Etiqueta.Trim();
            if (!ct.TipoFijadoPorUsuario) ct.Tipo = c.TipoTabla;
            ct.CatalogoTipo = c.Tipo == TipoCampoModulo.Lista && !string.IsNullOrWhiteSpace(c.Catalogo) ? c.Catalogo : null;
            ct.Opciones = c.Tipo == TipoCampoModulo.Lista && string.IsNullOrWhiteSpace(c.Catalogo)
                ? string.Join("\n", c.Opciones.Where(o => !string.IsNullOrWhiteSpace(o)).Select(o => o.Trim())) : null;
            ct.Multiple = c.Tipo is TipoCampoModulo.Lista or TipoCampoModulo.Vinculo && c.Multiple;
            ct.Decimales = c.Tipo == TipoCampoModulo.Numero ? c.Decimales : null;
            ct.Calculado = false;
        }
        foreach (var ct in existentes.Where(x => x.Origen == OrigenCampoTabla.Manual && x.Clave.StartsWith('c') && !claves.Contains(x.Clave) && x.Visible))
            if (!string.IsNullOrEmpty(ct.Clave) && ct.Clave.Length == 9) ct.Visible = false;   // campo del diseño quitado (clave "c" + 8)
        await db.SaveChangesAsync();
        return true;
    });

    // Columnas propias de la lista (además de los campos): código, estado, título y fechas.
    public static List<ColumnaNativa> ColumnasNativas(IReadOnlyDictionary<int, RegistroModulo> filas, DefinicionModulo def)
    {
        var l = new List<ColumnaNativa>
        {
            new("Codigo", "Código", TipoCampoTabla.Texto, id => filas.GetValueOrDefault(id)?.Codigo),
        };
        if (def.Estados.Count > 0) l.Add(new("Estado", "Estado", TipoCampoTabla.Texto, id => filas.GetValueOrDefault(id)?.Estado));
        l.Add(new("CreadoEn", "Creado", TipoCampoTabla.Fecha, id => filas.GetValueOrDefault(id)?.CreadoEn.ToLocalTime().Date, visiblePorDefecto: false));
        l.Add(new("CreadoPor", "Creado por", TipoCampoTabla.Texto, id => filas.GetValueOrDefault(id)?.CreadoPor, visiblePorDefecto: false));
        l.Add(new("ModificadoEn", "Modificado", TipoCampoTabla.Fecha, id => filas.GetValueOrDefault(id)?.ModificadoEn.ToLocalTime().Date, visiblePorDefecto: false));
        return l;
    }

    // ---------- Lectura ----------

    // Deja lista la tabla del módulo en este proyecto: columnas de los campos y columnas propias registradas (como sistema).
    // Después la página carga su MotorTabla con su propio DbContext sin escribir nada (las columnas ya existen).
    // Orden en una tabla nueva: primero las propias (Código, Estado…), luego los campos del diseño.
    public async Task PrepararTablaAsync(RegistroModulosCreados.Publicado m)
    {
        await ConDb(async db =>
        {
            using var _ = ComoSistema();
            await MotorTabla.CargarAsync(db, m.Tabla, ColumnasNativas(new Dictionary<int, RegistroModulo>(), m.Definicion), Array.Empty<(int, string)>());
            return true;
        });
        await SincronizarCamposAsync(m);
    }

    public sealed class Edicion
    {
        public int Id { get; set; }
        public string Codigo { get; set; } = "";
        public string? Estado { get; set; }
        public Dictionary<string, string?> Valores { get; } = new(StringComparer.Ordinal);
        public Dictionary<string, List<VinculoElegido>> Vinculos { get; } = new(StringComparer.Ordinal);
        public Dictionary<string, List<ArchivoRegistroModulo>> Archivos { get; } = new(StringComparer.Ordinal);
        public HashSet<string> PasosNoAplica { get; } = new(StringComparer.Ordinal);
        // Adjuntos quitados en pantalla: van a la Papelera al guardar.
        public List<int> ArchivosQuitados { get; } = new();
        public string? CreadoPor { get; set; }
        public DateTime? CreadoEn { get; set; }
        public string? ModificadoPor { get; set; }
        public DateTime? ModificadoEn { get; set; }

        public string? Valor(string campo) => Valores.GetValueOrDefault(campo);
        public List<VinculoElegido> VinculosDe(string campo) => Vinculos.TryGetValue(campo, out var l) ? l : Vinculos[campo] = new();
        public List<ArchivoRegistroModulo> ArchivosDe(string campo) => Archivos.TryGetValue(campo, out var l) ? l : Archivos[campo] = new();
    }

    public Edicion Nueva(DefinicionModulo def)
    {
        var e = new Edicion { Estado = def.Estados.FirstOrDefault()?.Nombre };
        return e;
    }

    public Task<Edicion?> CargarAsync(RegistroModulosCreados.Publicado m, int id) => ConDb<Edicion?>(async db =>
    {
        var r = await db.RegistrosModulo.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id && x.Modulo == m.Clave);
        if (r is null) return null;
        var e = new Edicion
        {
            Id = r.Id, Codigo = r.Codigo, Estado = r.Estado, CreadoPor = r.CreadoPor, CreadoEn = r.CreadoEn,
            ModificadoPor = r.ModificadoPor, ModificadoEn = r.ModificadoEn,
        };
        foreach (var p in (r.PasosNoAplica ?? "").Split(';', StringSplitOptions.RemoveEmptyEntries)) e.PasosNoAplica.Add(p);
        var valores = await (from v in db.ValoresCampoTabla.AsNoTracking()
                             join c in db.CamposTabla.AsNoTracking() on v.CampoTablaId equals c.Id
                             where c.Tabla == m.Tabla && v.FilaId == id
                             select new { c.Clave, v.Valor }).ToListAsync();
        foreach (var v in valores) e.Valores[v.Clave] = v.Valor;
        var vinculos = await db.VinculosRegistroModulo.AsNoTracking().Where(v => v.RegistroModuloId == id).OrderBy(v => v.Orden).ToListAsync();
        foreach (var v in vinculos) e.VinculosDe(v.Campo).Add(new VinculoElegido(v.Fuente, v.ElementoId, v.Etiqueta));
        var archivos = await db.ArchivosRegistroModulo.AsNoTracking().Where(a => a.RegistroModuloId == id).OrderBy(a => a.SubidoEn).ToListAsync();
        foreach (var a in archivos) e.ArchivosDe(a.Campo).Add(a);
        return e;
    });

    // ---------- Validación y guardado ----------

    // «Visible si»: el control solo se ve (y solo se exige) cuando el campo indicado tiene ese valor.
    public static bool EsVisible(DefinicionModulo def, CampoModulo c, Edicion e)
    {
        if (string.IsNullOrWhiteSpace(c.VisibleSiCampo) || string.IsNullOrWhiteSpace(c.VisibleSiValor)) return true;
        var padre = def.Campo(c.VisibleSiCampo);
        if (padre is null) return true;
        var v = e.Valor(padre.Clave);
        if (padre.Tipo == TipoCampoModulo.SiNo) v = v == "Sí" ? "Sí" : "No";
        return MotorTabla.Partes(v).Any(x => string.Equals(x.Trim(), c.VisibleSiValor.Trim(), StringComparison.OrdinalIgnoreCase));
    }

    public static List<string> Faltantes(DefinicionModulo def, Edicion e)
    {
        var faltan = new List<string>();
        foreach (var p in def.Pasos)
        {
            if (p.PermiteNoAplica && e.PasosNoAplica.Contains(p.Clave)) continue;
            foreach (var c in p.Campos.Where(c => c.Obligatorio && c.GuardaValor && c.Modo == ModoCampo.Editable && EsVisible(def, c, e)))
            {
                var vacio = c.Tipo switch
                {
                    TipoCampoModulo.Vinculo => e.VinculosDe(c.Clave).Count == 0,
                    TipoCampoModulo.Archivos or TipoCampoModulo.Foto or TipoCampoModulo.Firma => e.ArchivosDe(c.Clave).Count(a => !e.ArchivosQuitados.Contains(a.Id)) == 0,
                    TipoCampoModulo.SiNo => false,
                    _ => string.IsNullOrWhiteSpace(e.Valor(c.Clave)),
                };
                if (vacio) faltan.Add(c.Etiqueta);
            }
        }
        return faltan;
    }

    public static List<string> ErroresFormato(DefinicionModulo def, Edicion e)
    {
        var err = new List<string>();
        foreach (var c in def.CamposConValor)
        {
            var v = e.Valor(c.Clave);
            if (string.IsNullOrWhiteSpace(v)) continue;
            if (c.Tipo == TipoCampoModulo.Numero && !decimal.TryParse(v.Replace(',', '.'), NumberStyles.Number, CultureInfo.InvariantCulture, out _))
                err.Add($"«{c.Etiqueta}» debe ser un número.");
            if (c.Tipo == TipoCampoModulo.Fecha && !DateTime.TryParseExact(v, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out _))
                err.Add($"«{c.Etiqueta}» no es una fecha válida.");
        }
        return err;
    }

    // Guarda todo en una transacción. Devuelve el Id del registro. Lanza InvalidOperationException con el mensaje para el usuario.
    public Task<int> GuardarAsync(RegistroModulosCreados.Publicado m, Edicion e, string usuario) => ConDb(async db =>
    {
        var def = m.Definicion;
        var faltan = Faltantes(def, e);
        if (faltan.Count > 0) throw new InvalidOperationException("Falta diligenciar: " + string.Join(", ", faltan) + ".");
        var errores = ErroresFormato(def, e);
        if (errores.Count > 0) throw new InvalidOperationException(string.Join(" ", errores));
        if (e.Estado is not null && def.Estados.Count > 0 && !def.Estados.Any(s => s.Nombre == e.Estado))
            e.Estado = def.Estados[0].Nombre;

        await using var tx = await db.Database.BeginTransactionAsync();
        RegistroModulo r;
        if (e.Id == 0)
        {
            var max = await db.RegistrosModulo.IgnoreQueryFilters().Where(x => x.Modulo == m.Clave).MaxAsync(x => (int?)x.Consecutivo) ?? 0;
            r = new RegistroModulo
            {
                Modulo = m.Clave, Consecutivo = max + 1, Codigo = def.Codigo(max + 1), CreadoPor = usuario, CreadoEn = DateTime.UtcNow,
                Proyecto = Seguridad.AmbienteSig.Actual?.Proyecto?.Codigo,
            };
            db.RegistrosModulo.Add(r);
        }
        else
        {
            r = await db.RegistrosModulo.FirstOrDefaultAsync(x => x.Id == e.Id && x.Modulo == m.Clave)
                ?? throw new InvalidOperationException("El registro ya no existe (¿lo enviaron a la Papelera?).");
        }
        r.Estado = def.Estados.Count > 0 ? e.Estado : null;
        r.PasosNoAplica = e.PasosNoAplica.Count == 0 ? null : string.Join(';', e.PasosNoAplica.Where(p => def.Pasos.Any(x => x.Clave == p && x.PermiteNoAplica)));
        r.VersionDefinicion = m.Version;
        r.ModificadoPor = usuario; r.ModificadoEn = DateTime.UtcNow;
        await db.SaveChangesAsync();

        // Valores (una fila por campo con valor; vacío = se borra la fila).
        var campos = await db.CamposTabla.Where(c => c.Tabla == m.Tabla && c.Origen != OrigenCampoTabla.Nativo).ToDictionaryAsync(c => c.Clave, c => c.Id);
        var actuales = await db.ValoresCampoTabla.Where(v => v.FilaId == r.Id && campos.Values.Contains(v.CampoTablaId)).ToListAsync();

        // Vínculos.
        var vincPrevios = await db.VinculosRegistroModulo.Where(v => v.RegistroModuloId == r.Id).ToListAsync();
        db.VinculosRegistroModulo.RemoveRange(vincPrevios);
        foreach (var c in def.Campos.Where(c => c.Tipo == TipoCampoModulo.Vinculo))
        {
            var lista = e.VinculosDe(c.Clave);
            if (!c.Multiple && lista.Count > 1) lista = lista.Take(1).ToList();
            int i = 0;
            foreach (var v in lista)
                db.VinculosRegistroModulo.Add(new VinculoRegistroModulo
                { RegistroModuloId = r.Id, Campo = c.Clave, Fuente = v.Fuente, ElementoId = v.Id, Etiqueta = Recortar(v.Etiqueta, 300), Orden = i++ });
            e.Valores[c.Clave] = lista.Count == 0 ? null : MotorTabla.UnirVarios(lista.Select(v => v.Etiqueta));
        }

        // Adjuntos: los nuevos (Id 0) se ligan; los quitados van a la Papelera.
        foreach (var (campo, archivos) in e.Archivos)
            foreach (var a in archivos.Where(a => a.Id == 0 && !e.ArchivosQuitados.Contains(a.Id)))
            {
                a.RegistroModuloId = r.Id; a.Campo = campo;
                db.ArchivosRegistroModulo.Add(a);
            }
        if (e.ArchivosQuitados.Count > 0)
        {
            var quitar = await db.ArchivosRegistroModulo.Where(a => a.RegistroModuloId == r.Id && e.ArchivosQuitados.Contains(a.Id)).ToListAsync();
            foreach (var a in quitar) { a.Eliminado = true; a.FechaEliminacion = DateTime.UtcNow; }
        }
        foreach (var c in def.Campos.Where(c => c.EsAdjunto))
        {
            var n = e.ArchivosDe(c.Clave).Count(a => a.Id == 0 || !e.ArchivosQuitados.Contains(a.Id));
            e.Valores[c.Clave] = n == 0 ? null : n.ToString(CultureInfo.InvariantCulture);
        }

        foreach (var c in def.CamposConValor)
        {
            if (!campos.TryGetValue(c.Clave, out var campoId)) continue;
            var texto = Normalizar(c, e.Valor(c.Clave));
            var fila = actuales.FirstOrDefault(v => v.CampoTablaId == campoId);
            if (string.IsNullOrEmpty(texto)) { if (fila is not null) db.ValoresCampoTabla.Remove(fila); continue; }
            if (fila is null) db.ValoresCampoTabla.Add(new ValorCampoTabla { CampoTablaId = campoId, FilaId = r.Id, Valor = texto });
            else { fila.Valor = texto; fila.Formula = null; }
        }

        var titula = def.CampoQueTitula;
        r.Titulo = titula is null ? null : Recortar(e.Valor(titula.Clave), 300);
        await db.SaveChangesAsync();
        await tx.CommitAsync();
        return r.Id;
    });

    // Texto que se guarda en ValorCampoTabla (mismos formatos que el resto de la app: número invariante, fecha yyyy-MM-dd, Sí/No).
    private static string? Normalizar(CampoModulo c, string? v)
    {
        if (string.IsNullOrWhiteSpace(v)) return null;
        v = v.Trim();
        return c.Tipo switch
        {
            TipoCampoModulo.Numero => decimal.TryParse(v.Replace(',', '.'), NumberStyles.Number, CultureInfo.InvariantCulture, out var d)
                ? d.ToString(CultureInfo.InvariantCulture) : v,
            TipoCampoModulo.SiNo => v is "true" or "Sí" or "Si" or "1" ? "Sí" : "No",
            _ => v,
        };
    }

    [return: NotNullIfNotNull(nameof(s))]
    private static string? Recortar(string? s, int max) => s is null ? null : s.Length <= max ? s : s[..max];

    public Task CambiarEstadoAsync(RegistroModulosCreados.Publicado m, int id, string estado, string usuario) => ConDb(async db =>
    {
        if (!m.Definicion.Estados.Any(s => s.Nombre == estado)) throw new InvalidOperationException("Ese estado no existe en el módulo.");
        var r = await db.RegistrosModulo.FirstOrDefaultAsync(x => x.Id == id && x.Modulo == m.Clave)
            ?? throw new InvalidOperationException("El registro ya no existe.");
        r.Estado = estado; r.ModificadoPor = usuario; r.ModificadoEn = DateTime.UtcNow;
        await db.SaveChangesAsync();
        return true;
    });

    // Papelera (con sus adjuntos y la misma marca de tiempo).
    public Task EliminarAsync(RegistroModulosCreados.Publicado m, int id) => ConDb(async db =>
    {
        var r = await db.RegistrosModulo.FirstOrDefaultAsync(x => x.Id == id && x.Modulo == m.Clave);
        if (r is null) return false;
        var ahora = DateTime.UtcNow;
        r.Eliminado = true; r.FechaEliminacion = ahora;
        await db.SaveChangesAsync();
        return true;
    });

    // ---------- Archivos ----------

    // Sube un archivo y devuelve el adjunto SIN guardarlo en la base (se liga al guardar el registro; Cancelar lo descarta).
    public async Task<ArchivoRegistroModulo> SubirAsync(RegistroModulosCreados.Publicado m, IBrowserFile archivo, string usuario)
    {
        var ext = Path.GetExtension(archivo.Name).ToLowerInvariant();
        if (!Extensiones.Contains(ext)) throw new InvalidOperationException($"«{archivo.Name}»: tipo de archivo no permitido.");
        if (archivo.Size > MaxArchivo) throw new InvalidOperationException($"«{archivo.Name}» supera 50 MB.");
        var carpeta = Path.Combine(_env.WebRootPath, "uploads", "modulos", m.Clave);
        Directory.CreateDirectory(carpeta);
        var nombre = $"{Guid.NewGuid():N}{ext}";
        var destino = Path.GetFullPath(Path.Combine(carpeta, nombre));
        if (!destino.StartsWith(Path.GetFullPath(carpeta), StringComparison.OrdinalIgnoreCase)) throw new InvalidOperationException("Ruta no válida.");
        await using (var s = archivo.OpenReadStream(MaxArchivo))
        await using (var f = File.Create(destino))
            await s.CopyToAsync(f);
        return new ArchivoRegistroModulo
        {
            Nombre = Path.GetFileName(archivo.Name), Ruta = $"uploads/modulos/{m.Clave}/{nombre}", Tamano = archivo.Size,
            SubidoPor = usuario, SubidoEn = DateTime.UtcNow,
        };
    }

    // Firma dibujada en pantalla (PNG en base64): se guarda como un adjunto más del campo (se liga al guardar el registro).
    public async Task<ArchivoRegistroModulo> GuardarFirmaAsync(RegistroModulosCreados.Publicado m, string base64, string usuario)
    {
        var bytes = Convert.FromBase64String(base64);
        if (bytes.Length == 0 || bytes.Length > 2 * 1024 * 1024) throw new InvalidOperationException("La firma no es válida.");
        if (bytes.Length < 8 || bytes[0] != 0x89 || bytes[1] != 0x50 || bytes[2] != 0x4E || bytes[3] != 0x47) throw new InvalidOperationException("La firma no es una imagen PNG.");
        var carpeta = Path.Combine(_env.WebRootPath, "uploads", "modulos", m.Clave);
        Directory.CreateDirectory(carpeta);
        var nombre = $"{Guid.NewGuid():N}.png";
        await File.WriteAllBytesAsync(Path.Combine(carpeta, nombre), bytes);
        return new ArchivoRegistroModulo
        {
            Nombre = $"Firma {usuario} {DateTime.Now:dd-MM-yyyy HH-mm}.png", Ruta = $"uploads/modulos/{m.Clave}/{nombre}", Tamano = bytes.Length,
            SubidoPor = usuario, SubidoEn = DateTime.UtcNow,
        };
    }

    // Archivos subidos que no llegaron a guardarse (Cancelar): se borran del disco.
    public void DescartarSubidos(IEnumerable<ArchivoRegistroModulo> nuevos)
    {
        foreach (var a in nuevos.Where(a => a.Id == 0))
        {
            var ruta = Seguridad.ArchivosSeguros.RutaFisica(_env.WebRootPath, a.Ruta);
            try { if (ruta is not null && File.Exists(ruta)) File.Delete(ruta); } catch { /* queda huérfano, sin consecuencias */ }
        }
    }

    // ---------- Vínculos inversos ----------

    // Registros de módulos creados que enlazan un elemento (p. ej. inspecciones de una torre): para hojas de vida y fichas.
    public Task<List<(string Modulo, int Id, string Codigo, string? Titulo, string? Estado)>> QueEnlazanAsync(string fuente, int elementoId) => ConDb(async db =>
    {
        var l = await (from v in db.VinculosRegistroModulo.AsNoTracking()
                       join r in db.RegistrosModulo.AsNoTracking() on v.RegistroModuloId equals r.Id
                       where v.Fuente == fuente && v.ElementoId == elementoId
                       select new { r.Modulo, r.Id, r.Codigo, r.Titulo, r.Estado }).Distinct().ToListAsync();
        return l.Select(x => (x.Modulo, x.Id, x.Codigo, (string?)x.Titulo, (string?)x.Estado)).ToList();
    });
}
