namespace SigWeb.Components.Layout;

// Datos de <VistaResultados>: cada página (Partes interesadas, Riesgos…) arma sus objetos con los
// resultados de los seguimientos ya calculados; el componente solo dibuja (compilado + vista rápida).
// Clases de resultado: res-ok (bien), res-medio (atención), res-mal (mal), res-gris (sin medir),
// ind-no (mes sin seguimiento ya pasado), "" (mes futuro).
public static class ClasesRes
{
    public const string Ok = "res-ok", Medio = "res-medio", Mal = "res-mal", Gris = "res-gris", Falta = "ind-no";

    // La peor del mes manda en el compilado.
    public static int Gravedad(string? c) => c switch { Mal => 4, Medio => 3, Gris => 2, Ok => 1, _ => 0 };
}

public sealed record ChipRes(string Texto, string? Clase = null, string? Color = null, string? Titulo = null);
public sealed record KpiRes(string Titulo, string Valor, string? Sub = null, string? Clase = null, bool Grande = false);
public sealed record MesRes(string Clase, string Texto, string Titulo);
public sealed record FilaRes(DateTime Fecha, string Resultado, string Clase, string? Responsable, string? Detalle, string? Observacion);

public sealed class ObjetoRes
{
    public string Clave { get; init; } = "";
    public string? Codigo { get; init; }
    public string Nombre { get; init; } = "";
    public bool Apagado { get; init; }                       // inactiva / cerrada: se ve atenuada
    public ChipRes? Ultimo { get; init; }                    // resultado más reciente (rail)
    public List<ChipRes> Chips { get; init; } = new();
    public List<KpiRes> Kpis { get; init; } = new();
    public MesRes[] Meses { get; init; } = new MesRes[12];   // ene..dic del año mostrado
    public List<(string Titulo, string? Texto)> Ficha { get; init; } = new();
    public List<FilaRes> Filas { get; init; } = new();       // todos los seguimientos, el más reciente primero
}
