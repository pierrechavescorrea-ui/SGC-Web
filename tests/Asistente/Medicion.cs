using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Logging.Abstractions;
using SigWeb.Data.Asistente;
using SigWeb.Data.Seguridad;

namespace Pruebas.Asistente;

public sealed class Entorno : IWebHostEnvironment
{
    public string ContentRootPath { get; set; } = Directory.GetCurrentDirectory();
    public string ApplicationName { get; set; } = "SigWeb";
    public string EnvironmentName { get; set; } = "Development";
    public string WebRootPath { get; set; } = "";
    public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    public IFileProvider WebRootFileProvider { get; set; } = new NullFileProvider();
}

public static class Medicion
{
    public record Caso(string Modulo, string Ruta, string Pregunta, string? Esperado, string? Opcion, string? Anterior, string? Contiene, string? Rol);
    public record Fila(Caso Caso, string[] Respuestas, int Puesto);
    public static List<Fila> Ejecutar(string raiz)
    {
        var entorno = new Entorno { ContentRootPath = raiz };
        var manual = new ManualAyuda(entorno, NullLogger<ManualAyuda>.Instance);
        var casos = JsonSerializer.Deserialize<Caso[]>(File.ReadAllText(Path.Combine(raiz,"tests/Asistente/preguntas.json")))!;
        var filas = new List<Fila>();
        foreach (var c in casos)
        {
            if (c.Contiene != null || c.Anterior != null)
            {
                var rol = c.Rol ?? "admin-proyecto";
                var permisos = CatalogoModulos.Fijos.ToDictionary(m => m.Clave,m => RolesFabrica.Permiso(rol,m));
                using var sesion = AmbienteSig.Usar(new SesionSig { Proyecto = new(1,"P","Proyecto de prueba","","",false,true,null),
                    UsuarioId = "prueba", Permisos = new(permisos,false,false,rol,true) });
                var motor = new AsistenteAyuda(manual,new UsuarioActual(),entorno,NullLogger<AsistenteAyuda>.Instance);
                if (c.Anterior != null) motor.Preguntar(c.Anterior,c.Ruta);
                var respuesta = motor.Preguntar(c.Pregunta,c.Ruta);
                var opcionesSesion = new[]{respuesta.Seccion?.Titulo ?? respuesta.Texto}.Concat(respuesta.Sugerencias.Select(s => s.Texto)).Take(3).ToArray();
                var pos = Array.FindIndex(opcionesSesion,t => t.Contains(c.Contiene ?? c.Esperado!,StringComparison.OrdinalIgnoreCase));
                filas.Add(new(c,opcionesSesion,pos + 1));
                continue;
            }
            string[] opciones = c.Opcion != null
                ? manual.OpcionesPara(c.Pregunta,c.Ruta).Select(o => o.Capitulo.Archivo + "|" + o.Nombre).ToArray()
                : manual.Buscar(c.Pregunta,c.Ruta,3,null,TextoAyuda.TerminosDeFrases(c.Pregunta))
                    .Select(r => r.Seccion.Capitulo.Archivo + "|" + r.Seccion.Titulo).ToArray();
            var indice = Array.IndexOf(opciones,c.Modulo + "|" + (c.Opcion ?? c.Esperado));
            filas.Add(new(c,opciones,indice + 1));
        }
        return filas;
    }
}
