using System.Globalization;
using SigWeb.Models;

namespace SigWeb.Data.Plantillas;

// =====================================================================================================
// Plantillas de proyecto (Configuración › Catálogos): un libro Excel con una hoja por conjunto de datos,
// que el usuario descarga, llena y carga. Cada módulo es un IPlantillaModulo registrado en DI; el
// orquestador (ServicioPlantillas) genera el libro, lo lee, pide a cada módulo validar (sin escribir) y,
// si el usuario confirma, aplica todo en UNA transacción registrando cada cambio para poder deshacerlo.
//
// Reglas para quien implemente un módulo nuevo:
//  · Las filas se identifican por un código de negocio (columna Obligatoria, normalmente la primera),
//    nunca por Id. Relaciones entre hojas/módulos = por código (ej. la torre lleva el NOMBRE del tiro).
//  · ValidarAsync NO escribe en la base: solo lee y arma el plan (altas, actualizaciones, errores por celda).
//    Puede ver lo que otros módulos del mismo libro van a crear con ctx.SeCrea/ctx.Crear (orden = Orden).
//  · AplicarAsync escribe con ctx.Db (no abre otro contexto ni hace commit) y registra CADA alta con
//    ctx.Alta(entidad) y CADA actualización con ctx.AntesDeActualizar(entidad) ANTES de cambiarla.
//    El orquestador guarda (SaveChanges) después de cada módulo; si algo falla se revierte todo.
//  · Celda vacía en una actualización = no cambia ese campo (no borra datos existentes).
//  · Nada de datos de ejemplo: la plantilla de un proyecto vacío sale solo con encabezados.
// =====================================================================================================

public enum TipoColumna { Texto, Entero, Decimal, Fecha, SiNo, Lista }

// Lista = valores de un catálogo (ItemCatalogo.Tipo = Lista) o de una lista fija (ListaFija).
public sealed record ColumnaPlantilla(
    string Clave, string Titulo, TipoColumna Tipo = TipoColumna.Texto, bool Obligatoria = false,
    string? Lista = null, IReadOnlyList<string>? ListaFija = null, string? Ayuda = null, string? ListaEntidad = null);

// Nombre: máximo 31 caracteres, sin : \ / ? * [ ]. Es también el nombre de la hoja del libro.
public sealed record HojaPlantilla(string Nombre, string Descripcion, IReadOnlyList<ColumnaPlantilla> Columnas);

// Una fila leída del libro: valores por Clave de columna, ya normalizados a texto
// (fechas yyyy-MM-dd, números con punto decimal, Sí/No como "Sí"/"No"). Vacío = null.
public sealed class FilaLeida
{
    public int NumeroFila { get; init; }                       // fila del Excel (para mensajes)
    public Dictionary<string, string?> Valores { get; } = new(StringComparer.OrdinalIgnoreCase);
    public string? Texto(string clave) => Valores.TryGetValue(clave, out var v) && !string.IsNullOrWhiteSpace(v) ? v.Trim() : null;
    public bool Tiene(string clave) => Texto(clave) is not null;
    public int? Entero(string clave) => int.TryParse(Texto(clave), NumberStyles.Integer, CultureInfo.InvariantCulture, out var n) ? n : null;
    public double? Decimal(string clave) => double.TryParse(Texto(clave), NumberStyles.Float, CultureInfo.InvariantCulture, out var n) ? n : null;
    public DateTime? Fecha(string clave) => DateTime.TryParseExact(Texto(clave), "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var d) ? d : null;
    public bool? SiNo(string clave) => Texto(clave) switch { "Sí" => true, "No" => false, _ => null };
}

public enum OperacionFila { Alta, Actualizacion, SinCambios }

// Error de validación con su ubicación en el libro. Columna = Titulo de la columna (o vacío si es de la fila).
public sealed record ErrorCelda(string Hoja, int Fila, string Columna, string Mensaje);

// Lo que se hará con una fila. Datos = lo que el módulo necesite para aplicarla (DTO propio).
public sealed class PlanFila
{
    public string Hoja { get; init; } = "";
    public int NumeroFila { get; init; }
    public string Codigo { get; init; } = "";
    public OperacionFila Operacion { get; init; }
    public List<string> Cambios { get; } = new();              // "Campo: antes → después" (actualizaciones)
    public object? Datos { get; init; }
}

public sealed class PlanModulo
{
    public string Clave { get; init; } = "";
    public string Nombre { get; init; } = "";
    public List<PlanFila> Filas { get; } = new();
    public List<ErrorCelda> Errores { get; } = new();
    public int Altas => Filas.Count(f => f.Operacion == OperacionFila.Alta);
    public int Actualizaciones => Filas.Count(f => f.Operacion == OperacionFila.Actualizacion);
}

public interface IPlantillaModulo
{
    string Clave { get; }                                      // estable, minúsculas (ej. "tiros")
    string Nombre { get; }                                     // visible (ej. "Tiros")
    int Orden { get; }                                         // orden de validación/aplicación (dependencias)
    IReadOnlyList<HojaPlantilla> Hojas { get; }

    // Filas actuales para descargar la plantilla "con los datos del proyecto" (valores por Clave de columna;
    // DateTime, números y bool se escriben con su tipo). Sin datos = lista vacía.
    Task<IReadOnlyList<Dictionary<string, object?>>> FilasActualesAsync(string hoja, AppDbContext db, CancellationToken ct);

    Task<PlanModulo> ValidarAsync(IReadOnlyDictionary<string, IReadOnlyList<FilaLeida>> hojas, ContextoValidacion ctx, CancellationToken ct);

    Task AplicarAsync(PlanModulo plan, ContextoAplicacion ctx, CancellationToken ct);

    // Después de guardar el módulo (Ids ya asignados): efectos secundarios (p. ej. hoja de vida de la torre).
    Task DespuesDeGuardarAsync(PlanModulo plan, ContextoAplicacion ctx, CancellationToken ct) => Task.CompletedTask;

    // Al deshacer una carga, antes de revertir los cambios de este módulo (p. ej. inactivar localizaciones).
    Task AlDeshacerAsync(IReadOnlyList<CambioCargaProyecto> cambios, AppDbContext db, CancellationToken ct) => Task.CompletedTask;
}

// Contexto compartido mientras se valida un libro: lectura de la base y lo que crearán los módulos anteriores.
public sealed class ContextoValidacion
{
    public required AppDbContext Db { get; init; }
    // Valores activos de cada catálogo (ItemCatalogo.Tipo → nombres), más los que el libro da de alta.
    public required Dictionary<string, HashSet<string>> Catalogos { get; init; }
    private readonly Dictionary<string, HashSet<string>> nuevos = new(StringComparer.OrdinalIgnoreCase);

    // Un módulo anuncia que creará este código (p. ej. "Tiro", "T-1") para que los siguientes lo acepten.
    public void Crear(string entidad, string codigo)
    {
        if (!nuevos.TryGetValue(entidad, out var s)) nuevos[entidad] = s = new(StringComparer.OrdinalIgnoreCase);
        s.Add(codigo.Trim());
    }
    public bool SeCrea(string entidad, string codigo) => nuevos.TryGetValue(entidad, out var s) && s.Contains(codigo.Trim());

    public bool EnCatalogo(string tipo, string valor) =>
        Catalogos.TryGetValue(tipo, out var s) && s.Contains(valor.Trim());
    public void AgregarACatalogo(string tipo, string valor)
    {
        if (!Catalogos.TryGetValue(tipo, out var s)) Catalogos[tipo] = s = new(StringComparer.OrdinalIgnoreCase);
        s.Add(valor.Trim());
    }
}

// Contexto mientras se aplica: escribe con Db y anota los cambios (el orquestador los persiste con Ids reales).
public sealed class ContextoAplicacion
{
    public required AppDbContext Db { get; init; }
    public required CargaProyecto Carga { get; init; }
    public required string ModuloActual { get; set; }
    internal List<(string Modulo, object Entidad, string Operacion, string? Antes)> Pendientes { get; } = new();

    public void Alta(object entidad) => Pendientes.Add((ModuloActual, entidad, OperacionCambioCarga.Alta, null));

    // Llamar ANTES de modificar la entidad (rastreada por Db): guarda sus valores escalares para deshacer.
    public void AntesDeActualizar(object entidad) =>
        Pendientes.Add((ModuloActual, entidad, OperacionCambioCarga.Actualizacion, ServicioPlantillas.Instantanea(Db, entidad)));
}
