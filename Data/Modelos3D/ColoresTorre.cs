namespace SigWeb.Data.Modelos3D;

// Color de cada familia de torre en los visores 3D (Línea 3D y Avance de obra › Tendido, modo "por tipo").
public static class ColoresTorre
{
    private static readonly Dictionary<string, string> PorFamilia = new()
    {
        ["A"] = "#4e79a7", ["AA"] = "#59a14f", ["AAE"] = "#e15759", ["AP"] = "#76b7b2",
        ["BP"] = "#edc948", ["BE"] = "#b07aa1", ["C"] = "#f28e2b", ["D"] = "#9c755f"
    };

    public static string De(string? familia) => familia is not null && PorFamilia.TryGetValue(familia, out var c) ? c : "#9aa4b2";
}
