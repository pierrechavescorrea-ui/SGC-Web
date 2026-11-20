using System.Text;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using SigWeb.Data.Seguridad;

namespace SigWeb.Data.CreadorModulos;

// Operaciones del administrador sobre las definiciones (base central). La pantalla (Administración › Creador de módulos)
// abre un scope por operación y llama aquí; tras publicar/archivar llama a RegistroModulosCreados.RecargarAsync.
public static class AdminModulosCreados
{
    public static Task<List<ModuloCreado>> ListarAsync(SeguridadDbContext db) =>
        db.ModulosCreados.AsNoTracking().OrderBy(m => m.Estado == EstadoModuloCreado.Eliminado).ThenBy(m => m.Estado == EstadoModuloCreado.Archivado).ThenBy(m => m.Grupo).ThenBy(m => m.Orden).ThenBy(m => m.Nombre).ToListAsync();

    public static async Task<ModuloCreado> CrearAsync(SeguridadDbContext db, string nombre, string? usuario, ModuloCreado? copiaDe = null)
    {
        nombre = (nombre ?? "").Trim();
        if (nombre.Length < 3) throw new InvalidOperationException("Escriba un nombre de al menos 3 letras.");
        if (await db.ModulosCreados.AnyAsync(m => m.Estado != EstadoModuloCreado.Eliminado && m.Nombre.ToLower() == nombre.ToLower()))
            throw new InvalidOperationException($"Ya existe un módulo llamado «{nombre}».");
        var clave = await ClaveLibreAsync(db, nombre);
        var def = copiaDe is null ? Inicial() : DefinicionModulo.Leer(copiaDe.BorradorJson ?? copiaDe.DefinicionJson);
        var m = new ModuloCreado
        {
            Clave = clave, Nombre = nombre, Icono = copiaDe?.Icono ?? "inspeccion", Grupo = copiaDe?.Grupo ?? "Operación",
            Ambito = copiaDe?.Ambito ?? AmbitoModulo.Proyecto, Descripcion = copiaDe?.Descripcion,
            BorradorJson = def.Json(), CreadoPor = usuario, CreadoEn = DateTime.UtcNow, ModificadoEn = DateTime.UtcNow,
            Orden = (await db.ModulosCreados.MaxAsync(x => (int?)x.Orden) ?? 0) + 1,
        };
        db.ModulosCreados.Add(m);
        await db.SaveChangesAsync();
        return m;
    }

    // Definición de partida: un paso con un campo de texto, para que el administrador vea de inmediato cómo se arma.
    public static DefinicionModulo Inicial() => new()
    {
        Pasos = { new PasoModulo { Titulo = "Datos generales", Campos = { new CampoModulo { Etiqueta = "Descripción", Tipo = TipoCampoModulo.Texto, Ancho = 12, Obligatorio = true } } } },
    };

    // Rutas del primer tramo que ya usa la app (un módulo creado vive en /mod/{clave}, pero la clave también nombra carpetas y tablas).
    private static readonly HashSet<string> Reservadas = new(StringComparer.OrdinalIgnoreCase) { "admin", "api", "cuenta", "nuevo", "mod" };

    public static string Slug(string nombre)
    {
        var s = SigWeb.Data.Asistente.TextoAyuda.SinTildes(nombre.ToLowerInvariant());
        s = Regex.Replace(s, "[^a-z0-9]+", "-").Trim('-');
        if (s.Length > 30) s = s[..30].Trim('-');
        return s.Length == 0 ? "modulo" : s;
    }

    private static async Task<string> ClaveLibreAsync(SeguridadDbContext db, string nombre)
    {
        var baseClave = Slug(nombre);
        var usadas = (await db.ModulosCreados.Select(m => m.Clave).ToListAsync()).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var clave = baseClave;
        for (int i = 2; usadas.Contains(clave) || Reservadas.Contains(clave) || CatalogoModulos.De(clave) is not null; i++) clave = $"{baseClave}-{i}";
        return clave;
    }

    // Guarda lo diseñado como borrador (no afecta a nadie hasta Publicar). Los datos generales (nombre, ícono, grupo…) sí
    // aplican al guardar, igual que en el menú, porque no cambian la forma de los registros.
    public static async Task GuardarBorradorAsync(SeguridadDbContext db, int id, string nombre, string? descripcion, string icono, string grupo,
        AmbitoModulo ambito, DefinicionModulo def)
    {
        var m = await db.ModulosCreados.FirstOrDefaultAsync(x => x.Id == id) ?? throw new InvalidOperationException("El módulo ya no existe.");
        nombre = (nombre ?? "").Trim();
        if (nombre.Length < 3) throw new InvalidOperationException("El nombre debe tener al menos 3 letras.");
        if (await db.ModulosCreados.AnyAsync(x => x.Id != id && x.Estado != EstadoModuloCreado.Eliminado && x.Nombre.ToLower() == nombre.ToLower()))
            throw new InvalidOperationException($"Ya existe otro módulo llamado «{nombre}».");
        if (!CatalogoModulos.GruposMenu.Contains(grupo)) grupo = "Operación";
        m.Nombre = nombre; m.Descripcion = string.IsNullOrWhiteSpace(descripcion) ? null : descripcion.Trim();
        m.Icono = string.IsNullOrWhiteSpace(icono) ? "inspeccion" : icono; m.Grupo = grupo; m.Ambito = ambito;
        var json = def.Json();
        m.BorradorJson = m.DefinicionJson == json ? null : json;
        m.ModificadoEn = DateTime.UtcNow;
        await db.SaveChangesAsync();
    }

    public static async Task<int> PublicarAsync(SeguridadDbContext db, int id, string? nota, string? usuario)
    {
        var m = await db.ModulosCreados.FirstOrDefaultAsync(x => x.Id == id) ?? throw new InvalidOperationException("El módulo ya no existe.");
        var json = m.BorradorJson ?? m.DefinicionJson;
        var def = DefinicionModulo.Leer(json);
        var errores = def.Validar();
        if (m.DefinicionJson is not null) errores.AddRange(CambiosPeligrosos(DefinicionModulo.Leer(m.DefinicionJson), def));
        if (errores.Count > 0) throw new InvalidOperationException("No se puede publicar: " + string.Join(" ", errores));
        m.Version++;
        m.DefinicionJson = def.Json();
        m.BorradorJson = null;
        m.Estado = EstadoModuloCreado.Publicado;
        m.PublicadoEn = DateTime.UtcNow; m.ModificadoEn = DateTime.UtcNow;
        db.VersionesModuloCreado.Add(new VersionModuloCreado
        {
            ModuloCreadoId = m.Id, Version = m.Version, Json = m.DefinicionJson, Nombre = m.Nombre,
            Nota = string.IsNullOrWhiteSpace(nota) ? null : nota.Trim(), PublicadoPor = usuario, PublicadoEn = DateTime.UtcNow,
        });
        await db.SaveChangesAsync();
        return m.Version;
    }

    // Cambios que dañarían datos ya guardados: cambiar el tipo de un campo publicado a uno incompatible.
    // (Quitar campos o pasos sí se permite: sus valores quedan guardados y la columna se oculta.)
    public static List<string> CambiosPeligrosos(DefinicionModulo antes, DefinicionModulo despues)
    {
        var e = new List<string>();
        foreach (var c in despues.Campos)
        {
            var a = antes.Campo(c.Clave);
            if (a is null || a.Tipo == c.Tipo) continue;
            var compatible = (a.Tipo, c.Tipo) switch
            {
                (TipoCampoModulo.Texto, TipoCampoModulo.Parrafo) or (TipoCampoModulo.Parrafo, TipoCampoModulo.Texto) => true,
                (TipoCampoModulo.Lista, TipoCampoModulo.Texto) or (TipoCampoModulo.Texto, TipoCampoModulo.Lista) => true,
                (TipoCampoModulo.Titulo, _) => true,
                _ => false,
            };
            if (!compatible)
                e.Add($"«{c.Etiqueta}» era {CampoModulo.NombreTipo(a.Tipo)} y ahora es {CampoModulo.NombreTipo(c.Tipo)}: los datos guardados no se pueden convertir. Cree un campo nuevo y quite el anterior.");
        }
        return e;
    }

    public static async Task DescartarBorradorAsync(SeguridadDbContext db, int id)
    {
        var m = await db.ModulosCreados.FirstOrDefaultAsync(x => x.Id == id) ?? throw new InvalidOperationException("El módulo ya no existe.");
        if (m.DefinicionJson is null) throw new InvalidOperationException("El módulo nunca se ha publicado: no hay versión a la cual volver.");
        m.BorradorJson = null; m.ModificadoEn = DateTime.UtcNow;
        await db.SaveChangesAsync();
    }

    // Lleva al borrador una versión anterior (para revisarla y volver a publicarla).
    public static async Task TraerVersionAsync(SeguridadDbContext db, int id, int version)
    {
        var m = await db.ModulosCreados.FirstOrDefaultAsync(x => x.Id == id) ?? throw new InvalidOperationException("El módulo ya no existe.");
        var v = await db.VersionesModuloCreado.AsNoTracking().FirstOrDefaultAsync(x => x.ModuloCreadoId == id && x.Version == version)
            ?? throw new InvalidOperationException("Esa versión no existe.");
        m.BorradorJson = v.Json; m.ModificadoEn = DateTime.UtcNow;
        await db.SaveChangesAsync();
    }

    public static Task<List<VersionModuloCreado>> VersionesAsync(SeguridadDbContext db, int id) =>
        db.VersionesModuloCreado.AsNoTracking().Where(v => v.ModuloCreadoId == id).OrderByDescending(v => v.Version).ToListAsync();

    // Archivar = sale del menú y de los permisos en todos los proyectos; los registros quedan intactos en cada base.
    public static async Task ArchivarAsync(SeguridadDbContext db, int id, bool archivar)
    {
        var m = await db.ModulosCreados.FirstOrDefaultAsync(x => x.Id == id) ?? throw new InvalidOperationException("El módulo ya no existe.");
        if (archivar) m.Estado = EstadoModuloCreado.Archivado;
        else m.Estado = m.DefinicionJson is null ? EstadoModuloCreado.Borrador : EstadoModuloCreado.Publicado;
        m.ModificadoEn = DateTime.UtcNow;
        await db.SaveChangesAsync();
    }

    // Eliminar / restaurar / borrar definitivamente: EliminacionModulos.

    public static async Task OrdenarAsync(SeguridadDbContext db, IReadOnlyList<int> ids)
    {
        var mods = await db.ModulosCreados.Where(m => ids.Contains(m.Id)).ToListAsync();
        for (int i = 0; i < ids.Count; i++) if (mods.FirstOrDefault(m => m.Id == ids[i]) is { } m) m.Orden = i + 1;
        await db.SaveChangesAsync();
    }

    // Resumen legible de una definición (para comparar versiones).
    public static string Resumen(DefinicionModulo d)
    {
        var sb = new StringBuilder();
        foreach (var p in d.Pasos)
            sb.Append(p.Titulo).Append(": ").Append(string.Join(", ", p.Campos.Select(c => c.Etiqueta))).Append(" · ");
        return sb.ToString().TrimEnd(' ', '·');
    }
}
