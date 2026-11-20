using System.Globalization;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace SigWeb.Data.Modelos3D;

public record ResultadoIfcJson(string Json, int Miembros, int MiembrosSupuestos, double PesoKg, List<string> Avisos);

public static class LectorIfcTorre
{
    private static readonly string[] Paleta = ["#d9822b", "#3b82c4", "#8e5bd0", "#d04b5b", "#279a79", "#6a7483"];
    private static readonly StringComparer OrdenNatural = StringComparer.Create(CultureInfo.InvariantCulture, CompareOptions.NumericOrdering);

    public static ResultadoIfcJson Convertir(string rutaIfc) => new Modelo(LeerStep(File.ReadAllText(rutaIfc))).Convertir(rutaIfc);

    private sealed record Referencia(int Id);
    private sealed record Entidad(int Id, string Tipo, object?[] Args)
    {
        public object? this[int i] => i < Args.Length ? Args[i] : null;
    }

    // Las comillas y los comentarios se consumen como tokens completos para que
    // sus paréntesis, comas y puntos y coma no interfieran con la estructura STEP.
    private static readonly Regex Tokens = new(
        """/\*[\s\S]*?\*/|'(?:''|[^'])*'|"[^"]*"|#[0-9]+|\.[A-Z][A-Z0-9_]*\.|[+-]?(?:\d+(?:\.\d*)?|\.\d+)(?:E[+-]?\d+)?|[A-Z_][A-Z_0-9]*|[^\s]""",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
    private static readonly Regex UnicodeStep = new(
        """\\X2\\([0-9A-F]+)\\X0\\|\\X4\\([0-9A-F]+)\\X0\\|\\X\\([0-9A-F]{2})|\\\\""",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    private static string TextoStep(string s) => UnicodeStep.Replace(s[1..^1].Replace("''", "'"), m =>
    {
        if (m.Groups[3].Success) return ((char)int.Parse(m.Groups[3].Value, NumberStyles.HexNumber)).ToString();
        if (m.Length == 2) return "\\";
        var hex = m.Groups[1].Success ? m.Groups[1].Value : m.Groups[2].Value;
        var ancho = m.Groups[1].Success ? 4 : 8;
        if (hex.Length % ancho != 0) return m.Value;
        var texto = new StringBuilder();
        for (var i = 0; i < hex.Length; i += ancho)
        {
            var valor = int.Parse(hex.AsSpan(i, ancho), NumberStyles.HexNumber);
            if (ancho == 4) texto.Append((char)valor);
            else texto.Append(Rune.IsValid(valor) ? char.ConvertFromUtf32(valor) : "�");
        }
        return texto.ToString();
    });

    private static Dictionary<int, Entidad> LeerStep(string archivo)
    {
        Match? tokenActual = null;
        string? actual = null;
        void Siguiente()
        {
            do
            {
                tokenActual = tokenActual == null ? Tokens.Match(archivo) : tokenActual.NextMatch();
            } while (tokenActual.Success && tokenActual.Value.StartsWith("/*", StringComparison.Ordinal));
            actual = tokenActual.Success ? tokenActual.Value : null;
        }
        void Consumir(string esperado)
        {
            if (actual != esperado) throw new FormatException($"IFC STEP: se esperaba {esperado} y se encontró {actual ?? "fin del archivo"}.");
            Siguiente();
        }
        object?[] Lista(int profundidad)
        {
            if (profundidad > 128) throw new FormatException("IFC STEP: demasiados niveles de listas anidadas.");
            Consumir("(");
            var valores = new List<object?>();
            while (actual != ")")
            {
                var token = actual ?? throw new FormatException("IFC STEP: lista incompleta.");
                if (token == "(") valores.Add(Lista(profundidad + 1));
                else
                {
                    Siguiente();
                    if (char.IsLetter(token[0]) && actual == "(")
                    {
                        var tipado = Lista(profundidad + 1);
                        valores.Add(tipado.Length == 1 ? tipado[0] : tipado);
                    }
                    else valores.Add(token[0] switch
                    {
                        '$' or '*' => null,
                        '#' => new Referencia(int.Parse(token.AsSpan(1), CultureInfo.InvariantCulture)),
                        '\'' => TextoStep(token),
                        _ => Numero(token) is double n ? n : token.ToUpperInvariant()
                    });
                }
                if (actual != ",") break;
                Siguiente();
            }
            Consumir(")");
            return valores.ToArray();
        }

        var entidades = new Dictionary<int, Entidad>();
        Siguiente();
        while (actual != null)
        {
            if (!actual.StartsWith('#'))
            {
                Siguiente();
                continue;
            }
            var id = int.Parse(actual.AsSpan(1), CultureInfo.InvariantCulture);
            Siguiente();
            if (actual != "=") continue;
            Siguiente();
            var tipo = actual ?? throw new FormatException($"IFC STEP: falta el tipo de la entidad #{id}.");
            Siguiente();
            entidades.Add(id, new Entidad(id, tipo.ToUpperInvariant(), Lista(0)));
            Consumir(";");
        }
        if (entidades.Count == 0) throw new FormatException("El archivo IFC no contiene entidades STEP.");
        return entidades;
    }

    private static string? Texto(object? valor) => valor switch
    {
        string s when !string.IsNullOrWhiteSpace(s) => s.Trim(),
        double n => n.ToString("G", CultureInfo.InvariantCulture),
        _ => null
    };

    private static double? Numero(object? valor)
    {
        if (valor is double n) return n;
        return valor is string s && double.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out var v)
            && double.IsFinite(v) ? v : null;
    }

    private static IEnumerable<Referencia> Referencias(object? valor) => valor switch
    {
        Referencia r => [r],
        object?[] lista => lista.SelectMany(Referencias),
        _ => []
    };

    private readonly record struct Vector(double X, double Y, double Z)
    {
        public static Vector operator +(Vector a, Vector b) => new(a.X + b.X, a.Y + b.Y, a.Z + b.Z);
        public static Vector operator -(Vector a, Vector b) => a + b * -1;
        public static Vector operator *(Vector a, double k) => new(a.X * k, a.Y * k, a.Z * k);
        public double Norma => Math.Sqrt(X * X + Y * Y + Z * Z);
        public Vector? Unitario() => Norma is > 0 and < double.PositiveInfinity ? this * (1 / Norma) : null;
        public Vector Cruz(Vector b) => new(Y * b.Z - Z * b.Y, Z * b.X - X * b.Z, X * b.Y - Y * b.X);
        // Inversa de ifcgen: (x, y, z) del modelo → (x, -z, y) del IFC.
        public double[] Json(double mm) => [X * mm, Z * mm, -Y * mm];
    }

    private readonly record struct Marco(Vector O, Vector X, Vector Y, Vector Z)
    {
        public static readonly Marco Identidad = new(default, new(1, 0, 0), new(0, 1, 0), new(0, 0, 1));
        private Vector Direccion(Vector v) => X * v.X + Y * v.Y + Z * v.Z;
        public Vector Punto(Vector v) => O + Direccion(v);
        public Marco Componer(Marco local) => new(Punto(local.O), Direccion(local.X), Direccion(local.Y), Direccion(local.Z));
    }

    private sealed record Pieza(string Clase, string Marca, string? Perfil, double? Largo, string? Acero,
        string? Conjunto, double? Peso, string? Hoja, bool MarcaSupuesta, bool PerfilSupuesto,
        Dictionary<string, object?>? Miembro);

    private sealed class Modelo
    {
        private readonly Dictionary<int, Entidad> entidades;
        private readonly Dictionary<int, List<Entidad>> definiciones = [];
        private readonly Dictionary<int, int> contenedores = [];
        private readonly Dictionary<int, int> agregados = [];
        private readonly Dictionary<int, string> materiales = [];
        private readonly Dictionary<int, Marco?> marcos = [];
        private readonly Dictionary<string, int> deducciones = [];
        private readonly double mm;
        private readonly Entidad? unidadMasa;

        public Modelo(Dictionary<int, Entidad> entidades)
        {
            this.entidades = entidades;
            var proyecto = entidades.Values.FirstOrDefault(e => e.Tipo == "IFCPROJECT");
            var unidades = Resolver(proyecto?[8]) is { Tipo: "IFCUNITASSIGNMENT" } asignacion
                ? ResolverLista(asignacion[0]) : entidades.Values.Where(e => e.Tipo == "IFCSIUNIT");
            var unidadLongitud = unidades.FirstOrDefault(e => e.Tipo == "IFCSIUNIT" && Texto(e[1]) == ".LENGTHUNIT." && Texto(e[3]) == ".METRE.");
            mm = FactorSi(unidadLongitud) is double factor ? factor * 1000 : 1;
            if (FactorSi(unidadLongitud) == null) Avisar("Unidad de longitud desconocida; se asumieron milímetros.");
            unidadMasa = unidades.FirstOrDefault(e => e.Tipo == "IFCSIUNIT" && Texto(e[1]) == ".MASSUNIT." && Texto(e[3]) == ".GRAM.");

            foreach (var e in entidades.Values)
            {
                switch (e.Tipo)
                {
                    case "IFCRELDEFINESBYPROPERTIES":
                        var conjuntos = ResolverLista(e[5]).ToArray();
                        foreach (var r in Referencias(e[4]))
                        {
                            if (!definiciones.TryGetValue(r.Id, out var lista)) definiciones[r.Id] = lista = [];
                            lista.AddRange(conjuntos);
                        }
                        break;
                    case "IFCRELAGGREGATES" when e[4] is Referencia padre:
                        foreach (var r in Referencias(e[5])) agregados.TryAdd(r.Id, padre.Id);
                        break;
                    case "IFCRELCONTAINEDINSPATIALSTRUCTURE" when e[5] is Referencia edificio:
                        foreach (var r in Referencias(e[4])) contenedores.TryAdd(r.Id, edificio.Id);
                        break;
                    case "IFCRELASSOCIATESMATERIAL" when Resolver(e[5]) is { Tipo: "IFCMATERIAL" } material
                        && Texto(material[0]) is string nombre:
                        foreach (var r in Referencias(e[4])) materiales.TryAdd(r.Id, nombre);
                        break;
                }
            }
        }

        private static double? FactorSi(Entidad? u) => u is null ? null : Texto(u[2]) switch
        {
            null => 1, ".MILLI." => 0.001, ".CENTI." => 0.01, ".DECI." => 0.1,
            ".KILO." => 1000, ".MICRO." => 0.000001, _ => null
        };

        private Entidad? Resolver(object? valor) => valor is Referencia r ? entidades.GetValueOrDefault(r.Id) : null;
        private IEnumerable<Entidad> ResolverLista(object? valor) => Referencias(valor).Select(r => entidades.GetValueOrDefault(r.Id)).OfType<Entidad>();
        private void Avisar(string texto) => deducciones[texto] = deducciones.GetValueOrDefault(texto) + 1;
        private IEnumerable<Entidad> Definiciones(int id) => definiciones.TryGetValue(id, out var lista) ? lista : [];

        private Dictionary<string, object?> Propiedades(int id, string nombre)
        {
            var props = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
            foreach (var p in Definiciones(id).Where(e => e.Tipo == "IFCPROPERTYSET" && string.Equals(Texto(e[2]), nombre, StringComparison.OrdinalIgnoreCase)))
                foreach (var v in ResolverLista(p[4]))
                    if (v.Tipo == "IFCPROPERTYSINGLEVALUE" && Texto(v[0]) is string clave) props.TryAdd(clave, v[2]);
            return props;
        }

        private double? PesoNeto(int id)
        {
            foreach (var q in Definiciones(id).Where(e => e.Tipo == "IFCELEMENTQUANTITY").SelectMany(e => ResolverLista(e[5])))
            {
                if (q.Tipo != "IFCQUANTITYWEIGHT" || !string.Equals(Texto(q[0]), "NetWeight", StringComparison.OrdinalIgnoreCase)
                    || Numero(q[3]) is not (>= 0 and var peso)) continue;
                var unidad = q[2] is null ? unidadMasa : Resolver(q[2]);
                if (unidad is { Tipo: "IFCSIUNIT" } && Texto(unidad[3]) == ".GRAM." && FactorSi(unidad) is double factor)
                    return peso * factor / 1000;
                if (q[2] is null && unidadMasa is null)
                {
                    Avisar("NetWeight sin unidad de masa; se asumieron kilogramos.");
                    return peso;
                }
                Avisar("Unidad de NetWeight no admitida; se descartó la cantidad.");
            }
            return null;
        }

        private string? NombreContenedor(int id, HashSet<int> visitados)
        {
            if (visitados.Count > 128 || !visitados.Add(id)) return null;
            foreach (var mapa in new[] { agregados, contenedores })
            {
                if (!mapa.TryGetValue(id, out var padre) || !entidades.TryGetValue(padre, out var e)) continue;
                if (e.Tipo is "IFCBUILDINGSTOREY" or "IFCELEMENTASSEMBLY" && Texto(e[2]) is string nombre) return nombre;
                if (NombreContenedor(padre, visitados) is string superior) return superior;
            }
            return null;
        }

        private Vector? Coordenadas(object? referencia, string tipo)
        {
            if (Resolver(referencia) is not { } e || e.Tipo != tipo || e[0] is not object?[] v || v.Length < 2
                || Numero(v[0]) is not double x || Numero(v[1]) is not double y) return null;
            return v.Length == 2 ? new Vector(x, y, 0) : Numero(v[2]) is double z ? new Vector(x, y, z) : null;
        }

        private Marco? Ejes(object? referencia)
        {
            if (referencia is null) return Marco.Identidad;
            if (Resolver(referencia) is not { Tipo: "IFCAXIS2PLACEMENT3D" or "IFCAXIS2PLACEMENT2D" } e
                || Coordenadas(e[0], "IFCCARTESIANPOINT") is not Vector o) return null;
            var refX = e.Tipo == "IFCAXIS2PLACEMENT3D" ? e[2] : e[1];
            var refZ = e.Tipo == "IFCAXIS2PLACEMENT3D" ? e[1] : null;
            var z = refZ is null ? Marco.Identidad.Z : Coordenadas(refZ, "IFCDIRECTION")?.Unitario();
            if (z is not Vector ejeZ) return null;
            var x = refX is null
                ? Math.Abs(ejeZ.X) > 0.999999 ? Marco.Identidad.Y : Marco.Identidad.X
                : Coordenadas(refX, "IFCDIRECTION")?.Unitario();
            if (x is not Vector ejeX || ejeZ.Cruz(ejeX).Unitario() is not Vector ejeY) return null;
            return new Marco(o, ejeY.Cruz(ejeZ), ejeY, ejeZ);
        }

        private Marco? Colocacion(object? referencia)
        {
            var cadena = new List<Entidad>();
            var visitados = new HashSet<int>();
            Marco? marco = Marco.Identidad;
            while (referencia != null)
            {
                if (Resolver(referencia) is not { Tipo: "IFCLOCALPLACEMENT" } e || !visitados.Add(e.Id)) return null;
                if (marcos.TryGetValue(e.Id, out marco)) break;
                cadena.Add(e);
                referencia = e[0];
                marco = Marco.Identidad;
            }
            for (var i = cadena.Count - 1; i >= 0; i--)
            {
                var e = cadena[i];
                marco = marco is Marco padre && Ejes(e[1]) is Marco local ? padre.Componer(local) : null;
                marcos[e.Id] = marco;
            }
            return marco;
        }

        private (string? Nombre, double? Area) Perfil(Entidad? perfil)
        {
            if (perfil == null) return (null, null);
            var nombre = Texto(perfil[1]);
            if (perfil.Tipo != "IFCLSHAPEPROFILEDEF" ||
                Numero(perfil[3]) * mm is not (> 0 and var a) ||
                (Numero(perfil[4]) * mm ?? a) is not (> 0 and var b) ||
                Numero(perfil[5]) * mm is not (> 0 and var t) || t > Math.Min(a, b))
                return (nombre, null);
            if (nombre == null)
            {
                string F(double v) => v.ToString("G12", CultureInfo.InvariantCulture);
                nombre = Math.Abs(a - b) < 1e-8 ? $"L {F(a)}X{F(t)}" : $"L {F(a)}X{F(b)}X{F(t)}";
                Avisar("Nombre del perfil L reconstruido a partir de sus dimensiones IFC.");
            }
            return (nombre, (a + b - t) * t);
        }

        private (Vector A, Vector B, double Largo)? Eje(Entidad e, Entidad? polilinea, Entidad? extrusion)
        {
            if (Colocacion(e[5]) is not Marco local) return null;
            if (e[5] == null) Avisar("Pieza sin colocación local; se usaron las coordenadas de su representación.");
            if (polilinea != null)
            {
                var puntos = Referencias(polilinea[0]).Select(p => Coordenadas(p, "IFCCARTESIANPOINT")).ToArray();
                if (puntos.Length >= 2 && puntos.All(p => p.HasValue))
                {
                    var a = puntos[0]!.Value;
                    var b = puntos[^1]!.Value;
                    var largo = 0.0;
                    for (var i = 1; i < puntos.Length; i++) largo += (puntos[i]!.Value - puntos[i - 1]!.Value).Norma;
                    if (puntos.Length > 2) Avisar("Eje con varios segmentos representado por sus extremos; longitud medida sobre la polilínea.");
                    if (largo > 0 && (b - a).Norma > 0) return (local.Punto(a), local.Punto(b), largo * mm);
                }
            }
            if (extrusion == null || Ejes(extrusion[1]) is not Marco solido ||
                Coordenadas(extrusion[2], "IFCDIRECTION")?.Unitario() is not Vector direccion ||
                Numero(extrusion[3]) is not (> 0 and var profundidad)) return null;
            var marco = local.Componer(solido);
            Avisar("Eje de la barra reconstruido a partir de la posición, dirección y profundidad de la extrusión.");
            return (marco.O, marco.Punto(direccion * profundidad), profundidad * mm);
        }

        private Pieza LeerPieza(Entidad e, HashSet<string> marcasOcupadas, ref int consecutivo)
        {
            var p = Propiedades(e.Id, "SigWeb_Pieza");
            string? Prop(string k) => Texto(p.GetValueOrDefault(k));
            if (p.Count == 0) Avisar("Pieza sin Pset SigWeb_Pieza; se usaron los atributos y relaciones disponibles del IFC.");
            var marca = Texto(e[7]) ?? Prop("Marca");
            var marcaSupuesta = marca == null;
            if (marca == null)
            {
                do marca = $"S/M-{++consecutivo}"; while (!marcasOcupadas.Add(marca));
                Avisar("Marca ausente; se asignó una marca S/M consecutiva.");
            }

            var reps = ResolverLista(Resolver(e[6])?[2]).Where(r => r.Tipo == "IFCSHAPEREPRESENTATION").ToArray();
            var extrusion = reps.SelectMany(r => ResolverLista(r[3])).FirstOrDefault(r => r.Tipo == "IFCEXTRUDEDAREASOLID");
            var linea = reps.Where(r => string.Equals(Texto(r[1]), "Axis", StringComparison.OrdinalIgnoreCase))
                .SelectMany(r => ResolverLista(r[3])).FirstOrDefault(r => r.Tipo == "IFCPOLYLINE");
            var (perfil, area) = Perfil(Resolver(extrusion?[0]));
            if (perfil == null) Avisar("Perfil ausente o sin nombre; se dejó nulo.");
            var acero = Prop("Acero") ?? materiales.GetValueOrDefault(e.Id);
            var conjunto = Prop("Conjunto");
            var hoja = Prop("Hoja");
            var plano = Prop("Plano");
            var perfilSupuesto = Prop("PerfilSupuesto")?.ToUpperInvariant() is ".T." or "SÍ" or "SI" or "TRUE" or "1";
            if (perfilSupuesto) Avisar("Perfil marcado como supuesto en SigWeb_Pieza.");
            var placa = e.Tipo == "IFCPLATE";
            var eje = placa ? null : Eje(e, linea, extrusion);
            var largo = eje?.Largo;
            var peso = PesoNeto(e.Id);
            if (peso == null && area is double a && largo is double l)
            {
                peso = a * l * 7.85e-6;
                Avisar("Peso calculado con el área del ángulo L, la longitud del eje y 7850 kg/m³.");
            }

            Dictionary<string, object?>? miembro = null;
            if (eje is { } v)
            {
                var id = Prop("Id") ?? $"IFC-{e.Id}";
                if (Prop("Id") == null) Avisar("Id de miembro derivado del número de entidad STEP.");
                var nombre = Texto(e[2]) ?? marca;
                if (Texto(e[2]) == null) Avisar("Nombre de miembro ausente; se usó la marca.");
                var grupo = Prop("Grupo");
                if (grupo == null)
                {
                    grupo = NombreContenedor(e.Id, []);
                    Avisar(grupo == null ? "Grupo ausente; se asignó Estructura." : "Grupo tomado del nombre del nivel o conjunto contenedor.");
                    grupo ??= "Estructura";
                }
                var tipo = Prop("Tipo") ?? Texto(e[4]);
                if (tipo == null)
                {
                    tipo = e.Tipo == "IFCCOLUMN" || Texto(e[8]) == ".CHORD." ? "Montante" : e.Tipo == "IFCBEAM" ? "Viga" : "Barra";
                    Avisar("Tipo de pieza deducido de la entidad IFC y su tipo predefinido.");
                }
                miembro = new()
                {
                    ["id"] = id, ["nombre"] = nombre, ["grupo"] = grupo, ["tramo"] = Prop("Tramo"),
                    ["tipo"] = tipo, ["cara"] = Prop("Cara"), ["a"] = v.A.Json(mm), ["b"] = v.B.Json(mm),
                    ["longitud_mm"] = largo, ["marca"] = marca, ["perfil"] = perfil, ["acero"] = acero,
                    ["peso_kg"] = peso, ["conjunto"] = conjunto
                };
                if (marcaSupuesta) miembro["marca_supuesta"] = true;
                if (perfilSupuesto || (area.HasValue && marcaSupuesta))
                {
                    miembro["perfil_supuesto"] = perfil is null ? true : perfil;
                    miembro["perfil_supuesto_origen"] = Prop("PerfilSupuestoOrigen");
                }
                if (plano != null || hoja != null)
                    miembro["plano"] = new { codigo = plano, hoja };
                var delta = (v.B - v.A).Json(mm);
                miembro["dx_mm"] = delta[0];
                miembro["dy_mm"] = delta[1];
                miembro["dz_mm"] = delta[2];
                if (Texto(e[3]) is string fuentePieza) miembro["fuente"] = fuentePieza;
            }
            else if (!placa) Avisar("Barra sin eje lineal resoluble; se conservó únicamente en la lista de materiales.");
            return new Pieza(placa ? "LÁMINA" : "PERFIL", marca, perfil, largo, acero, conjunto, peso, hoja,
                marcaSupuesta, perfilSupuesto, miembro);
        }

        public ResultadoIfcJson Convertir(string ruta)
        {
            var edificio = entidades.Values.FirstOrDefault(e => e.Tipo == "IFCBUILDING" &&
                Definiciones(e.Id).Any(p => Texto(p[2]) == "SigWeb_Torre"))
                ?? entidades.Values.FirstOrDefault(e => e.Tipo == "IFCBUILDING");
            var p = Propiedades(edificio?.Id ?? 0, "SigWeb_Torre");
            if (p.Count == 0) Avisar("Falta el Pset SigWeb_Torre; se reconstruyeron las dimensiones disponibles.");
            var elementos = entidades.Values.Where(e => e.Tipo is "IFCMEMBER" or "IFCBEAM" or "IFCCOLUMN" or "IFCPLATE").ToArray();
            var marcasOcupadas = elementos.Select(e => Texto(e[7]) ?? Texto(Propiedades(e.Id, "SigWeb_Pieza").GetValueOrDefault("Marca")))
                .OfType<string>().ToHashSet(StringComparer.OrdinalIgnoreCase);
            var consecutivo = 0;
            var piezas = elementos.Select(e => LeerPieza(e, marcasOcupadas, ref consecutivo)).ToArray();
            if (piezas.Length == 0) Avisar("El IFC no contiene barras ni placas reconocidas.");
            var miembros = piezas.Where(x => x.Miembro != null).Select(x => x.Miembro!).ToArray();
            var grupos = miembros.Select(m => (string)m["grupo"]!).Distinct().Select((nombre, i) =>
                new { nombre, color = Paleta[i % Paleta.Length] }).ToArray();
            var marcas = new Dictionary<string, object>();
            var filas = new List<object>();
            var clavesUsadas = new HashSet<string>();
            var hojaInterna = 0;
            foreach (var grupo in piezas.GroupBy(x => (x.Clase, x.Marca, x.Perfil, Largo: Math.Round(x.Largo ?? 0, MidpointRounding.AwayFromZero), TieneLargo: x.Largo.HasValue))
                .OrderBy(g => g.Key.Marca, OrdenNatural).ThenBy(g => g.Key.Largo).ThenBy(g => g.Key.Perfil, OrdenNatural))
            {
                var primero = grupo.First();
                var (clase, marca, perfil, longitud, tieneLargo) = grupo.Key;
                var largo = tieneLargo ? longitud : (double?)null;
                var aceros = grupo.Select(x => x.Acero).OfType<string>().Distinct().ToArray();
                var conjuntos = grupo.Select(x => x.Conjunto).OfType<string>().Distinct().ToArray();
                var acero = aceros.Length > 0 ? string.Join(" / ", aceros) : null;
                var conjunto = conjuntos.Length > 0 ? string.Join(" / ", conjuntos) : null;
                var cant = grupo.Count();
                double? peso = grupo.Any(x => x.Peso.HasValue) ? grupo.Sum(x => x.Peso ?? 0) : null;
                var obs = primero.MarcaSupuesta ? "Marca supuesta." : "";
                if (grupo.Any(x => x.PerfilSupuesto)) obs = $"{obs} Perfil supuesto.".Trim();
                if (peso.HasValue && grupo.Any(x => x.Peso == null)) obs = $"{obs} Peso parcial.".Trim();
                if (aceros.Length > 1 || conjuntos.Length > 1) Avisar("Fila consolidada con varios aceros o conjuntos; se listaron los valores del IFC.");
                // Los visores enlazan pesos con hoja|marca. Ante filas repetidas,
                // una hoja interna negativa conserva el enlace de cada longitud.
                var hoja = NumeroHoja(primero.Hoja);
                var clave = $"{hoja.ToString(CultureInfo.InvariantCulture)}|{marca}";
                while (!clavesUsadas.Add(clave))
                {
                    hoja = --hojaInterna;
                    clave = $"{hoja.ToString(CultureInfo.InvariantCulture)}|{marca}";
                }
                filas.Add(new
                {
                    clase, marca, perfil = perfil ?? "", long_mm = Texto(largo) ?? "",
                    cant = cant.ToString(CultureInfo.InvariantCulture), acero = acero ?? "", conjunto = conjunto ?? "",
                    hoja, hoja_txt = primero.Hoja ?? "—", peso_unit_kg = peso / cant, peso_kg = peso, obs
                });
                foreach (var pieza in grupo)
                    if (pieza.Miembro != null) pieza.Miembro["marcas"] = new[] { clave };
                if (hoja < 0) Avisar("Se asignó un identificador de hoja interno para enlazar una fila repetida con el visor.");
            }

            foreach (var g in piezas.GroupBy(x => x.Marca).OrderBy(g => g.Key, OrdenNatural))
            {
                var x = g.First();
                var longitudes = g.Select(v => Math.Round(v.Largo ?? 0, MidpointRounding.AwayFromZero)).Distinct().ToArray();
                var perfiles = g.Select(v => v.Perfil).Distinct().ToArray();
                marcas[g.Key] = new
                {
                    marca = g.Key, clase = x.Clase, perfil = perfiles.Length == 1 ? perfiles[0] : null,
                    long_mm = longitudes.Length == 1 && x.Largo.HasValue ? longitudes[0] : (double?)null,
                    cant = g.Count(), acero = x.Acero, conjunto = x.Conjunto, en_lista = true
                };
            }
            var puntos = miembros.SelectMany(m => new[] { (double[])m["a"]!, (double[])m["b"]! }).ToArray();
            var alto = Numero(p.GetValueOrDefault("AltoTotal_mm"));
            var ancho = Numero(p.GetValueOrDefault("AnchoBase_mm"));
            if (puntos.Length > 0 && alto == null)
            {
                alto = puntos.Max(v => v[1]) - puntos.Min(v => v[1]);
                Avisar("Altura deducida de la diferencia entre las cotas extremas de los ejes.");
            }
            if (puntos.Length > 0 && ancho == null)
            {
                var baseY = puntos.Min(v => v[1]);
                var basePuntos = puntos.Where(v => v[1] - baseY < 1).ToArray();
                ancho = basePuntos.Max(v => v[0]) - basePuntos.Min(v => v[0]);
                Avisar("Ancho de base deducido del alcance horizontal X de los extremos en la cota inferior.");
            }
            var nombreTorre = Texto(edificio?[2]);
            if (nombreTorre == null)
            {
                nombreTorre = Path.GetFileNameWithoutExtension(ruta);
                Avisar("Nombre de torre tomado del nombre del archivo.");
            }
            var pesoTotal = piezas.Sum(x => x.Peso ?? 0);
            if (piezas.Any(x => x.Peso == null)) Avisar("El peso total suma solo las piezas con peso disponible.");
            var avisos = deducciones.Select(k => k.Value == 1 ? k.Key : $"{k.Key} ({k.Value} casos)").ToList();
            var meta = new Dictionary<string, object?>
            {
                ["nombre"] = nombreTorre, ["fuente"] = $"Generado desde IFC: {Path.GetFileName(ruta)}",
                ["origen"] = "ifc", ["peso_total_kg"] = pesoTotal,
                ["marcas_vinculadas"] = piezas.Count(x => x.Miembro != null && !x.MarcaSupuesta),
                ["supuestos"] = Entradas(p, "Supuesto").Concat(avisos).ToArray()
            };
            if (alto.HasValue) meta["alto_total_mm"] = alto;
            if (ancho.HasValue) meta["ancho_base_mm"] = ancho;
            if (Texto(p.GetValueOrDefault("Fuente")) is string fuente) meta["fuente_original"] = fuente;
            var planoBase = elementos.Select(e => Texto(Propiedades(e.Id, "SigWeb_Pieza").GetValueOrDefault("Plano")))
                .OfType<string>().Distinct().Take(2).ToArray();
            if (planoBase.Length == 1) meta["plano_base"] = planoBase[0];
            var validacion = Entradas(p, "Validacion").Select(Validacion).OfType<object>().ToArray();
            if (validacion.Length > 0) meta["validacion"] = validacion;
            var json = JsonSerializer.Serialize(new { meta, grupos, miembros, marcas, lista_materiales = filas },
                new JsonSerializerOptions { WriteIndented = true, Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping });
            return new ResultadoIfcJson(json, miembros.Length,
                piezas.Count(x => x.Miembro != null && (x.PerfilSupuesto || x.MarcaSupuesta)), pesoTotal, avisos);
        }
    }

    private static int NumeroHoja(string? hoja)
    {
        var digitos = Regex.Match(hoja ?? "", @"[0-9]+").Value;
        return int.TryParse(digitos, NumberStyles.None, CultureInfo.InvariantCulture, out var n) ? n : 0;
    }

    private static IEnumerable<string> Entradas(Dictionary<string, object?> p, string prefijo) => p
        .Where(x => x.Key.StartsWith(prefijo, StringComparison.OrdinalIgnoreCase))
        .OrderBy(x => x.Key, OrdenNatural).Select(x => Texto(x.Value)).OfType<string>();

    private static object? Validacion(string texto)
    {
        var m = Regex.Match(texto, @"^(.*?) · (.*?): plano (.+?) mm / modelo (.+?) mm$");
        if (!m.Success || Numero(m.Groups[3].Value) is not double plano || Numero(m.Groups[4].Value) is not double modelo) return null;
        return new { hoja = m.Groups[1].Value, cota = m.Groups[2].Value, plano_mm = plano, modelo_mm = modelo };
    }
}
