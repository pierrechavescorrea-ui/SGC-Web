using SigWeb.Models;

namespace SigWeb.Data.Formularios;

// Lo que un <DisenoFormulario> comparte (CascadingValue) con sus SeccionFormulario, BloqueFormulario y CampoFormulario:
//   · recoge lo que el código declara (pasos y campos, en orden) → Estructura que ve el diseñador;
//   · responde dónde va cada campo, cómo se llama cada paso y qué campos nuevos lleva cada paso.
// Sin diseño guardado (Diseno null) no cambia nada: los formularios se pintan exactamente como siempre.
public class ContextoFormulario
{
    public required RegistroFormularios.Info Info { get; init; }
    public Diseno? Diseno { get; private set; }
    public ValoresFormulario.Sesion? Sesion { get; set; }
    public bool ConDiseno => Diseno is not null;

    // Recolección de este render (el código manda el orden).
    private readonly List<SeccionE> _secciones = new();
    private readonly List<CampoE> _campos = new();
    private Estructura? _conocida;                         // última estructura guardada (campos condicionales que hoy no se pintan)
    private Dictionary<string, (string Seccion, Ubicacion U)> _ubicaciones = new();
    private List<(string Clave, string Titulo, bool Bloque, bool Nueva)> _pasos = new();

    public void Preparar(Diseno? diseno, Estructura? conocida)
    {
        Diseno = diseno;
        _conocida = conocida;
        Recalcular();
    }

    public void RegistrarSeccion(string clave, string titulo, bool bloque, bool libre = false)
    {
        if (_secciones.Any(s => s.Clave == clave)) return;
        _secciones.Add(new SeccionE { Clave = clave, Titulo = titulo, Bloque = bloque, Libre = libre });
    }

    public void RegistrarCampo(CampoE c)
    {
        var i = _campos.FindIndex(x => x.Clave == c.Clave);
        if (i >= 0) _campos[i] = c; else _campos.Add(c);
    }

    // Estructura completa: lo pintado ahora + lo conocido que hoy no se pintó (campos que dependen de una condición).
    public Estructura Estructura()
    {
        var e = new Estructura { Secciones = _secciones.ToList(), Campos = _campos.ToList() };
        if (_conocida is not null)
        {
            foreach (var s in _conocida.Secciones.Where(s => !e.Secciones.Any(x => x.Clave == s.Clave)))
            {
                // Se intercala detrás del paso que la precedía la última vez.
                var antes = _conocida.Secciones.TakeWhile(x => x.Clave != s.Clave).LastOrDefault(x => e.Secciones.Any(y => y.Clave == x.Clave));
                var pos = antes is null ? 0 : e.Secciones.FindIndex(x => x.Clave == antes.Clave) + 1;
                e.Secciones.Insert(pos, s);
            }
            foreach (var c in _conocida.Campos.Where(c => !e.Campos.Any(x => x.Clave == c.Clave) && e.Secciones.Any(s => s.Clave == c.Seccion)))
            {
                var antes = _conocida.Campos.TakeWhile(x => x.Clave != c.Clave).LastOrDefault(x => e.Campos.Any(y => y.Clave == x.Clave));
                var pos = antes is null ? e.Campos.Count : e.Campos.FindIndex(x => x.Clave == antes.Clave) + 1;
                e.Campos.Insert(pos, c);
            }
        }
        return e;
    }

    // Tras registrar (primer render) se recalcula la disposición con todo lo declarado.
    // Filas globales: el formulario diseñado es una sola rejilla; cada paso ocupa una fila de título y luego tantas filas
    // como necesiten sus campos (los bloques y pasos libres, una fila de alto automático).
    private readonly Dictionary<string, int> _filaPaso = new();       // fila (1..) del título del paso
    private readonly Dictionary<string, int> _filaCampos = new();     // fila (1..) donde empiezan los campos del paso
    private string _plantillaFilas = "";

    public void Recalcular()
    {
        var e = Estructura();
        _ubicaciones = Diseno is null ? new() : DisenoModelo.Disponer(e, Diseno);
        _pasos = DisenoModelo.Pasos(e, Diseno);
        _filaPaso.Clear(); _filaCampos.Clear();
        var filas = new List<string>();
        foreach (var p in _pasos)
        {
            _filaPaso[p.Clave] = filas.Count + 1;
            filas.Add("auto");
            if (!DisenoModelo.EsRejilla(e, Diseno, p.Clave)) continue;
            var alto = _ubicaciones.Values.Where(u => u.Seccion == p.Clave && !u.U.Oculto)
                .Select(u => u.U.Y + u.U.H).DefaultIfEmpty(0).Max();
            _filaCampos[p.Clave] = filas.Count + 1;
            for (int i = 0; i < alto; i++) filas.Add("minmax(var(--flot-alto, 3.25rem), auto)");
        }
        _plantillaFilas = string.Join(" ", filas);
    }

    // Estilo del contenedor: las filas de la rejilla única.
    public string? EstiloRejilla() => ConDiseno && _plantillaFilas.Length > 0 ? $"grid-template-rows:{_plantillaFilas}" : null;

    // Fila del título de un paso (o del bloque / paso libre entero).
    public int? FilaPaso(string clave) => ConDiseno && _filaPaso.TryGetValue(clave, out var f) ? f : null;

    // Estilo de un campo en la rejilla única (variables de css/formularios.css): columna, fila absoluta, tamaño y orden en
    // teléfono. Null = sin diseño o paso desconocido (el campo se pinta como siempre).
    public string? EstiloCampo(string clave)
    {
        if (!ConDiseno || !_ubicaciones.TryGetValue(clave, out var u) || !_filaCampos.TryGetValue(u.Seccion, out var base0)) return null;
        var fila = base0 + u.U.Y;
        return $"--fx:{u.U.X};--fy:{fila};--fw:{u.U.W};--fh:{u.U.H};--fo:{fila * DisenoModelo.Columnas + u.U.X}";
    }

    public bool Recolectado => _secciones.Count > 0 || _campos.Count > 0;

    // ---------- Respuestas para pintar ----------

    public Ubicacion? Ubicacion(string clave) => ConDiseno && _ubicaciones.TryGetValue(clave, out var u) ? u.U : null;

    public string Titulo(string clave, string original)
    {
        var d = Diseno?.Secciones.FirstOrDefault(s => s.Clave == clave);
        return string.IsNullOrWhiteSpace(d?.Titulo) ? original : d!.Titulo!;
    }

    // Número visible del paso (los bloques no llevan número) y su orden (CSS order) cuando el diseño mueve pasos.
    public int? Numero(string clave)
    {
        if (!ConDiseno) return null;
        int n = 0;
        foreach (var p in _pasos)
        {
            if (!p.Bloque) n++;
            if (p.Clave == clave) return p.Bloque ? null : n;
        }
        return null;
    }

    // Primer paso visible (sin línea divisoria arriba aunque en el código no sea el primero).
    public bool Primero(string clave) => ConDiseno && _pasos.Count > 0 && _pasos[0].Clave == clave;

    public int? Orden(string clave)
    {
        if (!ConDiseno) return null;
        var i = _pasos.FindIndex(p => p.Clave == clave);
        return i < 0 ? null : (i + 1) * 10;
    }

    // Etiqueta efectiva: la renombrada en el diseño o la del código.
    public string Etiqueta(string clave, string original)
    {
        var d = Diseno?.Campos.FirstOrDefault(c => c.Clave == clave);
        return string.IsNullOrWhiteSpace(d?.Etiqueta) ? original : d!.Etiqueta!;
    }

    // Campos nuevos (CampoTabla) que van en un paso.
    public IEnumerable<(CampoD D, CampoTabla Campo, Ubicacion U)> NuevosDe(string seccion)
    {
        if (Diseno is null || Sesion is null || Info.PintaCamposTabla) yield break;
        foreach (var d in Diseno.Campos.Where(c => c.CampoTablaId is int && c.Seccion == seccion && !c.Oculto))
        {
            var campo = Sesion.Campo(d.CampoTablaId!.Value);
            if (campo is null) continue;                                   // borrado (Papelera): no se pinta
            if (!Sesion.EsVisible(campo)) continue;                        // ligado a una opción que no está elegida
            if (CamposPorOpcion.PlantillaDe(campo, Sesion.Campos) is { } pl
                && Diseno.Campos.Any(x => x.CampoTablaId == pl.Id && !x.Oculto)) continue;   // lo pinta el bloque de su plantilla
            var u = _ubicaciones.TryGetValue(d.Clave, out var x) ? x.U : new Ubicacion(d.X, d.Y, d.W, d.H, d.Etiqueta, false);
            yield return (d, campo, u);
        }
    }

    public IEnumerable<SeccionD> PasosNuevos() =>
        Diseno?.Secciones.Where(s => s.Nueva) ?? Enumerable.Empty<SeccionD>();

    public IEnumerable<int> Obligatorios() =>
        Diseno?.Campos.Where(c => c.CampoTablaId is int && c.Obligatorio && !c.Oculto && Sesion?.NoAplica.Contains(c.Seccion ?? "") != true)
            .Select(c => c.CampoTablaId!.Value) ?? Enumerable.Empty<int>();

    // Paso marcado "N/A" en este registro.
    public bool NoAplica(string paso) => Sesion?.NoAplica.Contains(paso) == true;

}
