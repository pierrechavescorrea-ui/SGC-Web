using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using SigWeb.Models;
using SigWeb.Data.Tablas;

namespace SigWeb.Data.Formularios;

// Valores de los campos nuevos (CampoTabla de la tabla del formulario) de cada formulario abierto en el circuito.
// Scoped = uno por pestaña. No hace falta tocar el Guardar de cada página:
//   · registro existente: los valores se escriben cuando la página guarda (SavedChanges del DbContext del circuito);
//   · registro nuevo: al guardar, el registro de la entidad del formulario que se agregó da el Id y se escriben con él.
//   · Cancelar = el formulario se cierra sin guardar nada: los valores se descartan.
// Lecturas y escrituras van en un scope propio (el DbContext del circuito puede estar ocupado por la página).
public class ValoresFormulario : IDisposable
{
    public sealed class Sesion
    {
        public required RegistroFormularios.Info Info { get; init; }
        public int FilaId { get; set; }
        public List<CampoTabla> Campos { get; set; } = new();
        public Dictionary<string, List<string>> Opciones { get; set; } = new();
        public Dictionary<int, string?> Valores { get; set; } = new();
        public bool Sucio { get; set; }
        public HashSet<int> Obligatorios { get; set; } = new();
        internal List<object> Agregados { get; } = new();
        // Campos "Archivos adjuntos": los guardados del registro, los recién subidos (aún sin registro) y los quitados.
        public Dictionary<int, List<ArchivoCampoFormulario>> Archivos { get; set; } = new();
        public List<(int Campo, string Nombre, string Ruta)> ArchivosNuevos { get; } = new();
        public HashSet<int> ArchivosQuitados { get; } = new();
        // Pasos marcados "N/A" (no aplica) en este registro (clave del paso).
        public HashSet<string> NoAplica { get; set; } = new(StringComparer.OrdinalIgnoreCase);
        internal bool NoAplicaCambio { get; set; }
        // Se dispara al cambiar un valor o un archivo: el formulario se repinta (campos que dependen de una opción).
        public event Action? Cambio;
        internal void Avisar() => Cambio?.Invoke();

        // Un campo ligado a una opción solo cuenta si su campo padre tiene elegida esa opción.
        public bool EsVisible(CampoTabla c) =>
            c.CampoPadreId is not int padre || string.IsNullOrWhiteSpace(c.OpcionPadre)
            || (CamposPorOpcion.EsPlantilla(c)
                ? MotorTabla.Partes(Valores.GetValueOrDefault(padre)).Count > 0     // plantilla: con alguna opción elegida
                : MotorTabla.Partes(Valores.GetValueOrDefault(padre)).Contains(c.OpcionPadre!.Trim(), StringComparer.OrdinalIgnoreCase));

        // Plantilla "todas las opciones": el campo de cada opción elegida en la lista padre.
        public List<(string Opcion, CampoTabla Campo)> PorOpcion(CampoTabla plantilla)
        {
            if (plantilla.CampoPadreId is not int padre) return new();
            var hijos = CamposPorOpcion.HijosDe(plantilla, Campos);
            return MotorTabla.Partes(Valores.GetValueOrDefault(padre))
                .Select(op => (op, hijos.FirstOrDefault(h => string.Equals(h.OpcionPadre?.Trim(), op, StringComparison.OrdinalIgnoreCase))))
                .Where(x => x.Item2 is not null).Select(x => (x.op, x.Item2!)).ToList();
        }

        public int CuantosArchivos(int campo) =>
            (Archivos.TryGetValue(campo, out var l) ? l.Count(a => !ArchivosQuitados.Contains(a.Id)) : 0) + ArchivosNuevos.Count(n => n.Campo == campo);
        public CampoTabla? Campo(int id) => Campos.FirstOrDefault(c => c.Id == id);
        public List<string> OpcionesDe(CampoTabla c)
        {
            if (c.Tipo != TipoCampoTabla.Lista) return new();
            if (!string.IsNullOrWhiteSpace(c.CatalogoTipo)) return Opciones.TryGetValue(c.CatalogoTipo, out var l) ? l : new();
            return (c.Opciones ?? "").Split('\n').Select(x => x.Trim()).Where(x => x.Length > 0).ToList();
        }
    }

    private readonly AppDbContext _db;
    private readonly IServiceScopeFactory _scopes;
    private readonly List<Sesion> _abiertas = new();

    public ValoresFormulario(AppDbContext db, IServiceScopeFactory scopes)
    {
        _db = db; _scopes = scopes;
        _db.SavingChanges += AlGuardando;
        _db.SavedChanges += AlGuardado;
    }

    public async Task<Sesion> AbrirAsync(RegistroFormularios.Info info, int filaId)
    {
        var s = new Sesion { Info = info, FilaId = filaId };
        await CargarAsync(s);
        lock (_abiertas) _abiertas.Add(s);
        return s;
    }

    public async Task CambiarFilaAsync(Sesion s, int filaId)
    {
        if (s.FilaId == filaId) return;
        s.FilaId = filaId; s.Sucio = false; s.Agregados.Clear();
        await CargarAsync(s);
    }

    public void Cerrar(Sesion s) { lock (_abiertas) _abiertas.Remove(s); }

    // Campos nuevos de la tabla (los creados a mano o desde Excel; los nativos ya los pinta el formulario).
    public async Task CargarAsync(Sesion s)
    {
        try
        {
            using var scope = _scopes.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            // Campos asociados por opción: nombres "Base - Opción" y un campo por cada opción nueva de las plantillas.
            try { await CamposPorOpcion.SincronizarAsync(db, s.Info.Tabla); } catch (Exception ex) { Console.Error.WriteLine("Campos por opción: " + ex.Message); }
            s.Campos = await db.CamposTabla.AsNoTracking()
                .Where(c => c.Tabla == s.Info.Tabla && !c.Eliminado && c.Origen != OrigenCampoTabla.Nativo)
                .OrderBy(c => c.Orden).ToListAsync();
            var tipos = s.Campos.Where(c => c.Tipo == TipoCampoTabla.Lista && c.CatalogoTipo != null).Select(c => c.CatalogoTipo!).Distinct().ToList();
            s.Opciones = (await db.Catalogos.AsNoTracking().Where(i => tipos.Contains(i.Tipo) && i.Activo).ToListAsync())
                .GroupBy(i => i.Tipo).ToDictionary(g => g.Key, g => g.OrderBy(i => i.Nombre).Select(i => i.Nombre).ToList());
            var ids = s.Campos.Select(c => c.Id).ToList();
            s.Valores = s.FilaId <= 0 ? new()
                : await db.ValoresCampoTabla.AsNoTracking().Where(v => ids.Contains(v.CampoTablaId) && v.FilaId == s.FilaId)
                    .ToDictionaryAsync(v => v.CampoTablaId, v => v.Valor);
            var conArchivos = s.Campos.Where(c => c.Tipo == TipoCampoTabla.Archivos).Select(c => c.Id).ToList();
            s.Archivos = s.FilaId <= 0 || conArchivos.Count == 0 ? new()
                : (await db.ArchivosCampoFormulario.AsNoTracking().Where(a => conArchivos.Contains(a.CampoTablaId) && a.FilaId == s.FilaId)
                    .OrderBy(a => a.FechaCarga).ToListAsync()).GroupBy(a => a.CampoTablaId).ToDictionary(g => g.Key, g => g.ToList());
            s.ArchivosNuevos.Clear(); s.ArchivosQuitados.Clear();
            s.NoAplicaCambio = false;
            try
            {
                s.NoAplica = s.FilaId <= 0 ? new(StringComparer.OrdinalIgnoreCase)
                    : (await db.PasosNoAplica.AsNoTracking().Where(p => p.Formulario == s.Info.Clave && p.FilaId == s.FilaId).Select(p => p.Paso).ToListAsync())
                        .ToHashSet(StringComparer.OrdinalIgnoreCase);
            }
            catch { s.NoAplica = new(StringComparer.OrdinalIgnoreCase); }   // sin la migración aplicada
        }
        catch (Exception ex) { Console.Error.WriteLine("Campos del formulario: " + ex.Message); }
    }

    public void Poner(Sesion s, int campoId, string? valor)
    {
        var antes = s.Valores.GetValueOrDefault(campoId);
        s.Valores[campoId] = string.IsNullOrWhiteSpace(valor) ? null : valor;
        s.Sucio = true;
        // Si algún campo depende de este (también una plantilla por opción), el formulario se repinta para mostrarlo u ocultarlo.
        if (antes != s.Valores[campoId] && s.Campos.Any(c => c.CampoPadreId == campoId)) s.Avisar();
    }

    // Marca o quita "N/A" en un paso; se guarda cuando la página guarda el registro (como los valores).
    public void AlternarNoAplica(Sesion s, string paso)
    {
        if (!s.NoAplica.Remove(paso)) s.NoAplica.Add(paso);
        s.NoAplicaCambio = true; s.Sucio = true; s.Avisar();
    }

    // Archivos: el archivo ya está en disco; el registro (ArchivoCampoFormulario) se crea cuando la página guarda.
    public void AgregarArchivo(Sesion s, int campo, string nombre, string ruta) { s.ArchivosNuevos.Add((campo, nombre, ruta)); s.Sucio = true; s.Avisar(); }
    public void QuitarArchivo(Sesion s, int archivoId) { s.ArchivosQuitados.Add(archivoId); s.Sucio = true; s.Avisar(); }
    public void QuitarNuevo(Sesion s, int indice) { if (indice >= 0 && indice < s.ArchivosNuevos.Count) { s.ArchivosNuevos.RemoveAt(indice); s.Sucio = true; s.Avisar(); } }

    // Faltantes obligatorios (campos nuevos marcados Obligatorio en el diseño). Adjuntos: al menos un archivo.
    public static List<string> Faltantes(Sesion s, IEnumerable<int> obligatorios) =>
        obligatorios.Select(s.Campo).Where(c => c is not null && s.EsVisible(c) && (CamposPorOpcion.EsPlantilla(c)
                ? s.PorOpcion(c).Any(x => string.IsNullOrWhiteSpace(s.Valores.GetValueOrDefault(x.Campo.Id)))   // cada opción elegida con su valor
                : c.Tipo == TipoCampoTabla.Archivos
                    ? s.CuantosArchivos(c.Id) == 0
                    : string.IsNullOrWhiteSpace(s.Valores.GetValueOrDefault(c.Id))))
            .Select(c => c!.Nombre).ToList();

    // ---------- Escucha de los guardados de la página ----------

    private void AlGuardando(object? sender, SavingChangesEventArgs e)
    {
        // Obligatorios: si se está guardando el registro del formulario (nuevo o modificado) y falta un campo nuevo
        // obligatorio, se detiene el guardado; la página muestra el mensaje como cualquier otro error al guardar.
        List<Sesion> conObligatorios; lock (_abiertas) conObligatorios = _abiertas.Where(x => x.Obligatorios.Count > 0).ToList();
        foreach (var s in conObligatorios)
        {
            var esDelFormulario = _db.ChangeTracker.Entries().Any(x => s.Info.Entidad.IsInstanceOfType(x.Entity)
                && (x.State == EntityState.Added || (x.State == EntityState.Modified && x.Property("Id").CurrentValue is int id && id == s.FilaId)));
            if (!esDelFormulario) continue;
            var faltan = Faltantes(s, s.Obligatorios);
            if (faltan.Count > 0) throw new InvalidOperationException("Falta diligenciar: " + string.Join(", ", faltan) + ".");
        }

        List<Sesion> abiertas; lock (_abiertas) abiertas = _abiertas.Where(x => x.Sucio).ToList();
        if (abiertas.Count == 0) return;
        foreach (var s in abiertas.Where(x => x.FilaId <= 0))
        {
            s.Agregados.Clear();
            foreach (var en in _db.ChangeTracker.Entries().Where(x => x.State == EntityState.Added && s.Info.Entidad.IsInstanceOfType(x.Entity)))
                s.Agregados.Add(en.Entity);
        }
    }

    private void AlGuardado(object? sender, SavedChangesEventArgs e)
    {
        List<Sesion> abiertas; lock (_abiertas) abiertas = _abiertas.Where(x => x.Sucio).ToList();
        foreach (var s in abiertas)
        {
            int fila = s.FilaId;
            if (fila <= 0)
            {
                // Solo cuando se agregó exactamente un registro del tipo del formulario (si no, no se adivina).
                if (s.Agregados.Count != 1) continue;
                fila = _db.Entry(s.Agregados[0]).Property("Id").CurrentValue is int id ? id : 0;
                s.Agregados.Clear();
                if (fila <= 0) continue;
                s.FilaId = fila;
            }
            var archivos = s.Campos.Where(c => c.Tipo == TipoCampoTabla.Archivos).Select(c => c.Id).ToHashSet();
            var valores = s.Valores.Where(k => !archivos.Contains(k.Key)).ToDictionary(k => k.Key, k => k.Value);
            var nuevos = s.ArchivosNuevos.ToList();
            var quitados = s.ArchivosQuitados.ToList();
            var noAplica = s.NoAplicaCambio ? s.NoAplica.ToList() : null;
            s.ArchivosNuevos.Clear(); s.ArchivosQuitados.Clear();
            s.Sucio = false; s.NoAplicaCambio = false;
            _ = EscribirAsync(s.Info.Tabla, fila, valores, nuevos, quitados, archivos, s.Info.Clave, noAplica);
        }
    }

    private async Task EscribirAsync(string tabla, int fila, Dictionary<int, string?> valores,
        List<(int Campo, string Nombre, string Ruta)> nuevos, List<int> quitados, HashSet<int> camposArchivos,
        string formulario, List<string>? noAplica)
    {
        try
        {
            using var scope = _scopes.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var ids = valores.Keys.ToList();
            var existentes = await db.ValoresCampoTabla.Where(v => ids.Contains(v.CampoTablaId) && v.FilaId == fila).ToListAsync();
            foreach (var (campo, valor) in valores)
            {
                var r = existentes.FirstOrDefault(v => v.CampoTablaId == campo);
                if (r is null) { if (valor is null) continue; db.ValoresCampoTabla.Add(new ValorCampoTabla { CampoTablaId = campo, FilaId = fila, Valor = valor }); }
                else r.Valor = valor;
            }
            foreach (var n in nuevos)
                db.ArchivosCampoFormulario.Add(new ArchivoCampoFormulario { CampoTablaId = n.Campo, FilaId = fila, NombreOriginal = n.Nombre, RutaArchivo = n.Ruta });
            if (quitados.Count > 0)
            {
                var ahora = DateTime.Now;
                foreach (var a in await db.ArchivosCampoFormulario.Where(a => quitados.Contains(a.Id)).ToListAsync()) { a.Eliminado = true; a.FechaEliminacion = ahora; }
            }
            if (noAplica is not null)
            {
                var ya = await db.PasosNoAplica.Where(p => p.Formulario == formulario && p.FilaId == fila).ToListAsync();
                db.PasosNoAplica.RemoveRange(ya.Where(p => !noAplica.Contains(p.Paso, StringComparer.OrdinalIgnoreCase)));
                foreach (var paso in noAplica.Where(n => !ya.Any(p => string.Equals(p.Paso, n, StringComparison.OrdinalIgnoreCase))))
                    db.PasosNoAplica.Add(new PasoNoAplica { Formulario = formulario, FilaId = fila, Paso = paso });
            }
            await db.SaveChangesAsync();
            // El valor de celda de cada campo de adjuntos = número de archivos.
            foreach (var campo in camposArchivos) await ArchivosCampo.RecontarAsync(db, campo, fila);
        }
        catch (Exception ex) { Console.Error.WriteLine($"Campos del formulario ({tabla} #{fila}): {ex.Message}"); }
    }

    public void Dispose()
    {
        _db.SavingChanges -= AlGuardando;
        _db.SavedChanges -= AlGuardado;
    }
}
