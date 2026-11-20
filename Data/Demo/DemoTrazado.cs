using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using SigWeb.Data.Modelos3D;
using SigWeb.Models;

namespace SigWeb.Data.Demo;

// Trazado: 10 torres BP6 por proyecto sobre un trazado de ejemplo (recursos/torres-norte.json y torres-sur.json,
// coordenadas ficticias), tiros, capa, datos de la tabla de torres, hoja de vida + patas A–D,
// PDF en la carpeta «Adjuntos» de la hoja de vida y en Archivos de la hoja de vida y de la pata A.
public sealed class DemoTrazado : IModuloDemo
{
    public string Clave => "trazado";
    public int Orden => 0;

    private sealed record TorreJson(string codigo, double lat, double lon, string? tiro, Dictionary<string, string>? campos);

    public async Task SembrarAsync(ContextoDemo c, CancellationToken ct)
    {
        var db = c.Db;
        if (await db.ElementosTrazado.IgnoreQueryFilters().AnyAsync(ct)) return;

        var archivo = Path.Combine(ContextoDemo.CarpetaRecursos, c.Indice == 1 ? "torres-sur.json" : "torres-norte.json");
        var torres = JsonSerializer.Deserialize<List<TorreJson>>(await File.ReadAllTextAsync(archivo, ct)) ?? new();

        // Tiros en el orden en que aparecen.
        var tiros = new Dictionary<string, Tiro>();
        foreach (var nombre in torres.Select(t => t.tiro ?? "Tiro 1").Distinct())
        {
            var tiro = new Tiro { Nombre = nombre, Orden = tiros.Count + 1 };
            db.Tiros.Add(tiro); tiros[nombre] = tiro;
        }
        var capa = new CapaTrazado { Nombre = "Demostración — torres BP6", Origen = "Excel", Version = "DEMO-1", FechaCarga = DateTime.Now };
        db.Capas.Add(capa);
        await db.SaveChangesAsync(ct);

        var elementos = new List<ElementoTrazado>();
        foreach (var t in torres)
        {
            var e = new ElementoTrazado
            {
                Codigo = t.codigo, Nombre = "Torre " + t.codigo, Latitud = t.lat, Longitud = t.lon,
                CapaTrazadoId = capa.Id, TiroId = tiros[t.tiro ?? "Tiro 1"].Id,
            };
            db.ElementosTrazado.Add(e); elementos.Add(e);
        }
        await db.SaveChangesAsync(ct);

        const string origen = "Demostración (DEMO-1)";
        for (var i = 0; i < torres.Count; i++)
        {
            var id = elementos[i].Id;
            var campos = new Dictionary<string, string>(torres[i].campos ?? new(), StringComparer.OrdinalIgnoreCase)
            {
                ["Tipo de Estructura"] = "BP H45",
                ["Cuerpo"] = "BP6",
                ["Cantidad de Cuerpos"] = "6",
                ["Tipo — Cuerpo"] = "BP6",
                ["Cimentación — Tipo"] = i % 3 == 0 ? "Pila" : "Zapata",
                ["Altura Total Torre (m)"] = "45",
            };
            foreach (var (k, v) in campos)
                db.CamposExtraTorre.Add(new CampoExtraTorre { ElementoTrazadoId = id, Clave = k, Valor = v, Origen = origen });

            db.Componentes.Add(new Componente { ElementoTrazadoId = id, Nombre = "Hoja de vida", EsPredeterminado = true, EsHojaDeVida = true, Orden = -1 });
            string[] letras = { "A", "B", "C", "D" };
            for (var j = 0; j < letras.Length; j++)
                db.Componentes.Add(new Componente { ElementoTrazadoId = id, Nombre = $"Pata {letras[j]}", EsPredeterminado = true, Orden = j });
        }
        await db.SaveChangesAsync(ct);

        SigWeb.Data.Trazado.SincronizadorCatalogoLocalizacion.SincronizarTodas(db);
        db.ChangeTracker.Clear();

        // Carpeta «Adjuntos» de la hoja de vida (definición global) con un PDF por torre.
        var carpeta = await db.CarpetasHojaVida.FirstOrDefaultAsync(x => x.ClaveElemento == null && x.NombreNormalizado == "ADJUNTOS", ct);
        if (carpeta is null)
        {
            carpeta = new CarpetaHojaVida { Nombre = "Adjuntos", NombreNormalizado = "ADJUNTOS", Orden = 0 };
            db.CarpetasHojaVida.Add(carpeta);
            await db.SaveChangesAsync(ct);
        }

        var componentes = await db.Componentes.Where(x => !x.Eliminado && (x.EsHojaDeVida || x.Nombre == "Pata A"))
            .Select(x => new { x.Id, x.ElementoTrazadoId, x.EsHojaDeVida }).ToListAsync(ct);
        foreach (var e in elementos)
        {
            var (ruta, tamano) = c.CopiarArchivo(ContextoDemo.PdfEnsayo, $"hojavida/{e.Id}", ".bin");
            db.ArchivosCarpetaHojaVida.Add(new ArchivoCarpetaHojaVida
            {
                CarpetaHojaVidaId = carpeta.Id, ElementoTrazadoId = e.Id, NombreOriginal = $"Informe de ensayo torre {e.Codigo}.pdf",
                Ruta = ruta, Tamano = tamano, ContentType = "application/pdf", Descripcion = "Ejemplo de demostración",
            });
            foreach (var comp in componentes.Where(x => x.ElementoTrazadoId == e.Id))
            {
                var (r2, _) = c.CopiarArchivo(comp.EsHojaDeVida ? ContextoDemo.PdfCertificado : ContextoDemo.PdfPolitica, "trazado");
                db.ArchivosComponente.Add(new ArchivoComponente
                {
                    ComponenteId = comp.Id, RutaArchivo = r2,
                    NombreOriginal = comp.EsHojaDeVida ? "Certificado de acero.pdf" : "Plano de cimentación pata A.pdf",
                });
            }
        }
        await db.SaveChangesAsync(ct);
        c.Log.LogInformation("Demo: {Codigo}: {N} torres BP6 con hoja de vida y adjuntos.", c.Proyecto.Codigo, elementos.Count);
    }
}

// Modelos 3D: el IFC BP6 del repositorio cargado como versión 1 (igual que Modelos 3D › Cargar), con su JSON y miniatura.
public sealed class DemoModeloIfc : IModuloDemo
{
    public string Clave => "ifc";
    public int Orden => 1;

    public async Task SembrarAsync(ContextoDemo c, CancellationToken ct)
    {
        var db = c.Db;
        if (await db.ModelosIfcCargados.IgnoreQueryFilters().AnyAsync(ct)) return;
        var ifc = Path.Combine(c.WebRoot, "modelos-ifc", "torre-bp6.ifc");
        if (!File.Exists(ifc)) { c.Log.LogWarning("Demo: falta {Ruta}; no se carga el IFC BP6.", ifc); return; }
        var json = Path.Combine(c.WebRoot, "modelos3d", "torre-bp6.json");
        var png = Path.Combine(c.WebRoot, "modelos-ifc", "torre-bp6.png");

        var version = ModelosCargados.SiguienteVersion(db, "BP6");
        var raiz = ModelosCargados.Raiz("BP6", version);
        var dirIfc = Path.Combine(c.WebRoot, "uploads", "modelos-ifc");
        var dirJson = Path.Combine(c.WebRoot, "uploads", "modelos3d");
        Directory.CreateDirectory(dirIfc); Directory.CreateDirectory(dirJson);
        File.Copy(ifc, Path.Combine(dirIfc, raiz + ".ifc"), overwrite: true);
        string? rutaJson = null, rutaPng = null;
        if (File.Exists(json)) { File.Copy(json, Path.Combine(dirJson, raiz + ".json"), true); rutaJson = $"/uploads/modelos3d/{raiz}.json"; }
        if (File.Exists(png)) { File.Copy(png, Path.Combine(dirIfc, raiz + ".png"), true); rutaPng = $"/uploads/modelos-ifc/{raiz}.png"; }
        var lectura = ModelosCargados.LeerIfc(ifc);

        db.ModelosIfcCargados.Add(new ModeloIfcCargado
        {
            Codigo = "BP6", Version = version, Nombre = "BP6", Familia = "BP",
            Descripcion = "Torre de suspensión tipo BP6 (delta) — cargada como ejemplo de demostración",
            Cambios = "Versión inicial (demostración)",
            ArchivoIfc = $"/uploads/modelos-ifc/{raiz}.ifc", NombreOriginalIfc = "torre-bp6.ifc",
            ArchivoJson = rutaJson, ArchivoMiniatura = rutaPng,
            Validaciones = lectura.Validaciones, Supuestos = lectura.Supuestos,
        });
        await db.SaveChangesAsync(ct);
    }
}
