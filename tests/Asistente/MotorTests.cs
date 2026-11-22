using Microsoft.Extensions.Logging.Abstractions;
using SigWeb.Data.Asistente;
using SigWeb.Data.Seguridad;

namespace Pruebas.Asistente;

public class MotorTests
{
    private static readonly string Raiz = EncontrarRaiz();
    private static readonly Entorno Entorno = new() { ContentRootPath = Raiz };
    private static readonly ManualAyuda Manual = new(Entorno,NullLogger<ManualAyuda>.Instance);

    private static string EncontrarRaiz()
    {
        for (var p = new DirectoryInfo(AppContext.BaseDirectory); p != null; p = p.Parent)
            if (File.Exists(Path.Combine(p.FullName,"SigWeb.csproj"))) return p.FullName;
        throw new DirectoryNotFoundException("No se encontró el proyecto SigWeb.");
    }

    [Fact]
    public void MantieneLosAciertosDeReferencia()
    {
        var filas = Medicion.Ejecutar(Raiz);
        Assert.True(filas.Count >= 250);
        Assert.True(filas.Count(f => f.Puesto == 1) >= filas.Count * .90);
        Assert.True(filas.Count(f => f.Puesto > 0) >= filas.Count * .98);
    }

    [Fact]
    public void LeeElIndicePublicadoSinNecesitarComponentesRazor()
    {
        var publicado = new Entorno { ContentRootPath = AppContext.BaseDirectory, EnvironmentName = "Production" };
        var manual = new ManualAyuda(publicado,NullLogger<ManualAyuda>.Instance);
        Assert.Contains(manual.OpcionesPara("dónde está el botón Ronda de seguimiento","/riesgos"),
            o => o.Ruta == "/riesgos" && o.Resaltar?.TrimStart('!') == "Ronda de seguimiento");
    }

    [Theory]
    [InlineData("Enséñame paso a paso cómo registrar un mantenimiento", true)]
    [InlineData("¿Puede mostrarme cómo abrir el listado?", true)]
    [InlineData("Lléveme al formulario de la matriz", true)]
    [InlineData("¿Cómo registro un mantenimiento?", false)]
    public void DistingueLasPeticionesDeGuia(string pregunta, bool esperada)
    {
        Assert.Equal(esperada, GuiaAyuda.Tarea(pregunta) != null);
    }

    [Theory]
    [InlineData("Guíame para importar revisiones de una matriz","admin-proyecto",false)]
    [InlineData("Muéstreme cómo importar revisiones de una matriz","consulta-restringida",true)]
    public void GuiaImportacionRespetaElPermiso(string pregunta,string rol,bool bloqueado)
    {
        var matriz = CatalogoModulos.Fijos.ToDictionary(m => m.Clave,m => RolesFabrica.Permiso(rol,m));
        using var sesion = AmbienteSig.Usar(new SesionSig { Proyecto = new(5,"P","Proyecto de prueba","","",false,true,null),
            UsuarioId = "prueba", Permisos = new(matriz,false,false,rol,true) });
        var motor = new AsistenteAyuda(Manual,new UsuarioActual(),Entorno,NullLogger<AsistenteAyuda>.Instance);
        var guia = Assert.IsType<GuiaAyuda>(motor.Preguntar(pregunta,"/riesgos").Guia);
        var paso = guia.Pasos.First();
        Assert.Equal(bloqueado,paso.Bloqueado);
        if (bloqueado) { Assert.Null(paso.Control); Assert.Null(paso.Ruta); Assert.Contains("administrador",paso.Texto); }
        else
        {
            Assert.Equal("Importar el libro Excel de la matriz", paso.Control);
            Assert.Equal("/riesgos", paso.Ruta);
            Assert.Equal(6, guia.Pasos.Count);
            Assert.Equal("Libro Excel de la matriz de riesgos", guia.Pasos[1].Control);
            Assert.Null(guia.Pasos[3].Control);
            Assert.Equal(guia.Pasos, motor.RetomarGuia(guia.SeccionId)?.Pasos);
        }
    }

    [Fact]
    public void GuiaDeMantenimientoIdentificaLaRutaDeLaHojaDeVida()
    {
        var matriz = CatalogoModulos.Fijos.ToDictionary(m => m.Clave, _ => Accion.Todas);
        using var sesion = AmbienteSig.Usar(new SesionSig { Proyecto = new(5,"P","Proyecto de prueba","","",false,true,null),
            UsuarioId = "prueba", Permisos = new(matriz,false,false,"admin-proyecto",true) });
        var motor = new AsistenteAyuda(Manual, new UsuarioActual(), Entorno, NullLogger<AsistenteAyuda>.Instance);
        var guia = Assert.IsType<GuiaAyuda>(motor.Preguntar("Enséñeme cómo registrar un mantenimiento", "/equipos").Guia);
        Assert.Equal("Abrir hoja de vida", guia.Pasos[0].Control);
        Assert.Equal("Registrar mantenimiento", guia.Pasos[1].Control);
        Assert.Equal("/equipos/{EquipoId:int}/hoja-de-vida", guia.Pasos[1].RutaPlantilla);
        Assert.Equal("Fecha", guia.Pasos[2].Control);
        Assert.Equal(guia.Pasos[1].RutaPlantilla, guia.Pasos[2].RutaPlantilla);
    }

    [Theory]
    [InlineData("consulta-restringida",false,false,false)]
    [InlineData("colaborador",false,true,false)]
    [InlineData("admin-proyecto",true,true,false)]
    [InlineData("AdministradorSGC",true,true,true)]
    public void RespondeConLaSesionVigente(string rol,bool elimina,bool descarga,bool global)
    {
        var matriz = CatalogoModulos.Fijos.ToDictionary(m => m.Clave,m => global ? Accion.Todas : RolesFabrica.Permiso(rol,m));
        using var sesion = AmbienteSig.Usar(new SesionSig { Proyecto = new(5,"P","Proyecto de prueba","","",false,true,null),
            UsuarioId = "prueba", Email = "Cuenta de prueba", Permisos = new(matriz,global,false,rol,true) });
        var motor = new AsistenteAyuda(Manual,new UsuarioActual(),Entorno,NullLogger<AsistenteAyuda>.Instance);
        Assert.Equal(elimina,motor.Enlace("Eliminar actividad",null,Manual.CapituloDeRuta("/actividades")) != null);
        Assert.Equal(global,motor.Enlace("Módulo nuevo",null,Manual.CapituloDeRuta("/admin/modulos")) != null);
        var m = motor.Preguntar("¿Dónde está el botón Exportar en Riesgos?","/riesgos");
        Assert.Equal(descarga,m.Atajos.Any(a => a.Resaltar == "Exportar" && a.Ruta == "/riesgos?ver=lista"));
        if (!descarga) Assert.DoesNotContain(m.Sugerencias,s => s.Resaltar == "Exportar");
        var seguimiento = motor.Preguntar("¿y cómo lo elimino?", "/riesgos");
        Assert.Empty(seguimiento.Sugerencias);
        if (elimina)
        {
            Assert.Equal("Eliminar riesgo (va a la Papelera)", seguimiento.Atajos.First().Resaltar);
            Assert.All(seguimiento.Atajos, atajo => Assert.Equal("/riesgos", atajo.Ruta));
        }
        else
        {
            Assert.Empty(seguimiento.Atajos);
            Assert.Contains("no permite", seguimiento.Texto);
        }
        Assert.Contains(rol,motor.Preguntar("¿Qué rol tengo?","/riesgos").Texto);
        Assert.Contains("Proyecto de prueba",motor.Preguntar("¿En qué proyecto estoy?","/riesgos").Texto);
        var alcance = motor.Preguntar("¿Por qué no puedo eliminar?","/riesgos").Texto;
        Assert.Equal(!elimina,alcance.Contains("solicite acceso"));

        foreach (var pregunta in new[] { "¿Qué puedo hacer aquí?", "¿Dónde está el botón eliminar actividad?",
            "¿Cómo invito usuarios?", "¿Cómo agrego un riesgo?", "¿Dónde está el botón Exportar?" })
        {
            var respuesta = motor.Preguntar(pregunta,"/riesgos");
            var enlaces = respuesta.Atajos.Concat(respuesta.Sugerencias)
                .Select(s => (s.Ruta, s.Resaltar)).Append((respuesta.RutaIr,respuesta.ResaltarIr));
            foreach (var (ruta,control) in enlaces)
            {
                if (ruta == null) continue;
                if (ruta.StartsWith("/admin")) Assert.True(global);
                else Assert.True(matriz[CatalogoModulos.ClaveDeRuta(ruta)].HasFlag(Accion.Ver));
                if (control?.Contains("Eliminar",StringComparison.OrdinalIgnoreCase) == true) Assert.True(elimina);
                if (control?.Contains("Exportar",StringComparison.OrdinalIgnoreCase) == true) Assert.True(descarga);
            }
        }
    }
}
