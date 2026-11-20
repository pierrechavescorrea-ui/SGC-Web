using Microsoft.EntityFrameworkCore;
using SigWeb.Data.CreadorModulos;
using SigWeb.Data.Tablas;

namespace SigWeb.Data.Dossier.Fuentes;

// La definición publicada identifica la fuente; la base (proyecto o Empresa) la elige quien carga el Dossier.
public sealed class FuenteModuloCreado(RegistroModulosCreados.Publicado modulo) : IFuenteDossier
{
    public string Clave => modulo.Ruta;
    public string Nombre => modulo.Nombre;
    public string Icono => modulo.Icono;

    public async Task<FuenteCargada> CargarAsync(AppDbContext db, CancellationToken ct)
    {
        var filas = await db.RegistrosModulo.AsNoTracking().Where(r => r.Modulo == modulo.Clave && !r.Eliminado)
            .OrderByDescending(r => r.Consecutivo).ToListAsync(ct);
        var porId = filas.ToDictionary(r => r.Id);
        var motor = await MotorTabla.CargarLecturaAsync(db, modulo.Tabla,
            ServicioRegistrosModulo.ColumnasNativas(porId, modulo.Definicion), filas.Select(r => (r.Id, r.Codigo)));
        ct.ThrowIfCancellationRequested();
        var elementos = filas.ToDictionary(r => r.Id, r => new ElementoDossier { Id = r.Id, Grupo = r.Estado });
        var ids = porId.Keys.ToList();
        var archivos = await db.ArchivosRegistroModulo.AsNoTracking()
            .Where(a => ids.Contains(a.RegistroModuloId) && !a.Eliminado).OrderBy(a => a.Id)
            .Select(a => new { a.RegistroModuloId, a.Campo, a.Nombre, a.Ruta }).ToListAsync(ct);
        var campos = modulo.Definicion.Campos.Where(c => c.Tipo == TipoCampoModulo.Archivos)
            .ToDictionary(c => c.Clave, c => c.Etiqueta);
        foreach (var a in archivos)
            elementos[a.RegistroModuloId].Archivos.Add(new ArchivoDossier("Adjuntos", a.Nombre, a.Ruta,
                Carpeta: campos.GetValueOrDefault(a.Campo, a.Campo)));
        return new FuenteCargada
        {
            Fuente = this, Motor = motor, Elementos = filas.Select(r => elementos[r.Id]).ToList(),
            ColumnaNombre = "Codigo", NombreGrupo = modulo.Definicion.Estados.Count > 0 ? "Estado" : null,
            Clases = ["Adjuntos"]
        };
    }
}
