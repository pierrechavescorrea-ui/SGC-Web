using System.Runtime.CompilerServices;
using Microsoft.Extensions.Configuration;
using SigWeb.Data.Papelera;
using SigWeb.Data.Seguridad;

namespace SigWeb.Pruebas.Seguridad;

// Pruebas de las reglas de seguridad que no necesitan base de datos ni navegador (se corren en el CI).
public class PruebasSeguridad
{
    private static IEnumerable<IProveedorPapelera> Proveedores() =>
        typeof(IProveedorPapelera).Assembly.GetTypes()
            .Where(t => t is { IsClass: true, IsAbstract: false, ContainsGenericParameters: false } && typeof(IProveedorPapelera).IsAssignableFrom(t))
            .Select(t => (IProveedorPapelera)RuntimeHelpers.GetUninitializedObject(t));

    private static string? ModuloSeguro(IProveedorPapelera p)
    {
        try { return p.Modulo; } catch { return null; }   // proveedores genéricos o que leen campos: se omiten
    }

    [Fact]
    public void TodoProveedorDePapeleraTieneModuloDeOrigenDecidido()
    {
        Assert.True(Proveedores().Count(p => ModuloSeguro(p) is not null) > 50, "No se encontraron los proveedores de Papelera.");
        var sinDecidir = Proveedores().Select(ModuloSeguro).Where(m => m is not null && !ModuloOrigenPapelera.Conocido(m!)).Distinct().ToList();
        Assert.True(sinDecidir.Count == 0, "Agregar a ModuloOrigenPapelera: " + string.Join(", ", sinDecidir));
    }

    [Fact]
    public void LosModulosDeOrigenExistenEnElCatalogo()
    {
        var claves = CatalogoModulos.Lista.Select(m => m.Clave).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var faltan = Proveedores().Select(ModuloSeguro).Where(m => m is not null)
            .Select(m => ModuloOrigenPapelera.ClaveDe(m!)).Where(c => c is not null && !claves.Contains(c!)).Distinct().ToList();
        Assert.True(faltan.Count == 0, "Claves inexistentes: " + string.Join(", ", faltan));
    }

    [Theory]
    [InlineData("/uploads/documentos/a.pdf", true)]
    [InlineData("/uploads/../appsettings.json", false)]
    [InlineData("/uploads/documentos/../../Program.cs", false)]
    [InlineData("https://otro.sitio/uploads/x.pdf", false)]
    [InlineData("/css/app.css", false)]
    public void RutaFisicaQuedaDentroDeUploads(string url, bool permitida)
    {
        var raiz = Path.Combine(Path.GetTempPath(), "sigweb-pruebas", "wwwroot");
        Assert.Equal(permitida, ArchivosSeguros.RutaFisica(raiz, url) is not null);
    }

    [Theory]
    [InlineData("uploads/documentos/a%20b.pdf?v=2", "/uploads/documentos/a b.pdf")]
    [InlineData("/uploads/planos/x.pdf#page=2", "/uploads/planos/x.pdf")]
    [InlineData(@"\uploads\equipos\f.png", "/uploads/equipos/f.png")]
    public void NormalizaLasRutasDeAdjuntos(string entrada, string esperada) =>
        Assert.Equal(esperada, IndiceArchivosProyecto.Normalizar(entrada));

    [Fact]
    public void ConsultaSinDescargaNoDescargaNiEdita()
    {
        var m = CatalogoModulos.Lista.First(x => x.Clave == "documentos");
        var a = RolesFabrica.Permiso("consulta-restringida", m);
        Assert.True((a & Accion.Ver) != 0);
        Assert.Equal(Accion.Ninguna, a & (Accion.Descargar | Accion.Editar | Accion.Eliminar | Accion.Crear));
    }

    [Fact]
    public void TrabajoPesadoRespetaElCupo()
    {
        var cfg = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Seguridad:TrabajosPesadosSimultaneos"] = "1",
            ["Seguridad:SegundosEsperaTrabajoPesado"] = "1",
        }).Build();
        TrabajoPesado.Configurar(cfg);
        using (TrabajoPesado.Entrar())
            Assert.Throws<TrabajoPesado.OcupadoException>(() => TrabajoPesado.Entrar());
        using (TrabajoPesado.Entrar()) { }   // se liberó
        TrabajoPesado.Configurar(new ConfigurationBuilder().Build());
    }

    [Fact]
    public void ElCertificadoDeLlavesEsOpcional()
    {
        var cfg = new ConfigurationBuilder().Build();
        Assert.Null(ArranqueSeguridad.CertificadoLlaves(cfg, "Seguridad:LlavesCertificado"));
    }
}
