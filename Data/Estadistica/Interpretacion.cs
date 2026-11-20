#nullable enable
using System.Globalization;

namespace SigWeb.Data.Estadistica;

// Lectura automática de un ResultadoEstadistico en español. Los umbrales son criterios de uso común y se nombran en el texto
// (Bulmer 1979 para la asimetría, ±2 errores estándar para significancia, α = 0,05 en normalidad, 1,33 en Cpk).
public static class Interpretacion
{
    public sealed record Parrafo(string Tema, string Texto, string Nivel = "info"); // Nivel: info | ok | alerta

    public static List<Parrafo> Leer(ResultadoEstadistico r, string campo, string? unidad = null)
    {
        var p = new List<Parrafo>();
        if (r.N == 0) { p.Add(new("Datos", $"No hay valores numéricos de «{campo}» con los filtros elegidos.", "alerta")); return p; }
        string F(double v) => Num(v, r);
        var u = string.IsNullOrWhiteSpace(unidad) ? "" : " " + unidad;

        if (r.N < 8)
            p.Add(new("Datos", $"Solo hay {r.N} valores: las medidas de forma, la normalidad y los intervalos son poco confiables con tan pocos datos.", "alerta"));

        // Centro.
        var sesgoCentro = r.Desviacion > 0 ? (r.Media - r.Mediana) / r.Desviacion : 0;
        var modaTxt = r.Modas.Count == 0 ? $"ningún valor se repite (moda de la clase modal ≈ {F(r.ModaAgrupada ?? r.Mediana)}{u})"
            : r.Modas.Count == 1 ? $"la moda es {F(r.Modas[0])}{u} ({r.FrecuenciaModa} veces)"
            : $"hay {r.Modas.Count} modas ({string.Join("; ", r.Modas.Take(5).Select(F))}{(r.Modas.Count > 5 ? "…" : "")}), con {r.FrecuenciaModa} repeticiones cada una";
        var orden = Math.Abs(sesgoCentro) < 0.05 ? "media y mediana prácticamente coinciden, señal de una distribución centrada"
            : sesgoCentro > 0 ? "la media está a la derecha de la mediana: los valores altos la arrastran (cola hacia la derecha)"
            : "la media está a la izquierda de la mediana: los valores bajos la arrastran (cola hacia la izquierda)";
        p.Add(new("Ubicación", $"La media es {F(r.Media)}{u} y la mediana {F(r.Mediana)}{u}; {modaTxt}. {Mayus(orden)}. " +
            $"El 50 % central de los resultados está entre {F(r.Q1)} y {F(r.Q3)}{u} (Q1–Q3)."));

        // Dispersión.
        if (r.N > 1)
        {
            var cv = r.CoefVariacion;
            var nivelCv = cv is null ? "" : cv < 15 ? " — variabilidad baja" : cv < 30 ? " — variabilidad moderada" : " — variabilidad alta";
            p.Add(new("Dispersión", $"Desviación estándar s = {F(r.Desviacion)}{u} (varianza {F(r.Varianza)}). " +
                (cv is double c ? $"Coeficiente de variación {c.ToString("0.0", CultureInfo.CurrentCulture)} %{nivelCv} (criterio orientativo: < 15 % baja, 15–30 % moderada, > 30 % alta). " : "") +
                $"Rango {F(r.Rango)}{u}, de {F(r.Min)} a {F(r.Max)}."));
        }

        // Forma.
        if (r.Asimetria is double g1)
        {
            var mag = Math.Abs(g1) < 0.5 ? "aproximadamente simétrica" : Math.Abs(g1) < 1 ? "moderadamente asimétrica" : "muy asimétrica";
            var lado = Math.Abs(g1) < 0.5 ? "" : g1 > 0 ? " a la derecha (positiva: pocos valores altos alejados)" : " a la izquierda (negativa: pocos valores bajos alejados)";
            var sig = r.ErrorAsimetria is double ses && ses > 0 ? (Math.Abs(g1 / ses) > 2
                ? $" La asimetría es estadísticamente significativa (|G1/SES| = {Math.Abs(g1 / ses):0.0} > 2)."
                : $" No es significativa (|G1/SES| = {Math.Abs(g1 / ses):0.0} ≤ 2): puede deberse al azar de la muestra.") : "";
            p.Add(new("Asimetría", $"Asimetría G1 = {g1:0.000}: distribución {mag}{lado} (Bulmer: |G1| < 0,5 simétrica, 0,5–1 moderada, > 1 alta).{sig}",
                Math.Abs(g1) >= 1 ? "alerta" : "info"));
        }
        if (r.Curtosis is double g2)
        {
            var tipo = Math.Abs(g2) < 0.5 ? "mesocúrtica (apuntamiento parecido al de la normal)"
                : g2 > 0 ? "leptocúrtica (más apuntada que la normal y colas más pesadas: más valores extremos de lo esperado)"
                : "platicúrtica (más aplanada que la normal: valores repartidos sin un pico marcado)";
            var sig = r.ErrorCurtosis is double sek && sek > 0 ? (Math.Abs(g2 / sek) > 2 ? " Diferencia significativa frente a la normal (|G2/SEK| > 2)." : " La diferencia con la normal no es significativa (|G2/SEK| ≤ 2).") : "";
            p.Add(new("Curtosis", $"Curtosis (exceso) G2 = {g2:0.000}: {tipo}.{sig}"));
        }

        // Normalidad.
        if (r.PAndersonDarling is double pad)
        {
            var normal = pad >= 0.05;
            p.Add(new("Distribución", $"Prueba de Anderson-Darling: A² = {r.AndersonDarling:0.000}, valor p = {Pv(pad)}. " +
                (normal ? "Con α = 0,05 no se rechaza la normalidad: la campana de Gauss describe bien los datos y las probabilidades normales son utilizables."
                        : "Con α = 0,05 se rechaza la normalidad: las probabilidades de la campana son solo aproximadas; prefiera las probabilidades observadas.") +
                (r.PJarqueBera is double pj ? $" Jarque-Bera: JB = {r.JarqueBera:0.00}, p = {Pv(pj)}." : ""), normal ? "ok" : "alerta"));
        }
        else if (r.PJarqueBera is double pj)
            p.Add(new("Distribución", $"Jarque-Bera: JB = {r.JarqueBera:0.00}, p = {Pv(pj)} (Anderson-Darling requiere al menos 8 datos)."));

        if (r.N >= 8 && r.Desviacion > 0)
        {
            var d = r.DentroSigma;
            p.Add(new("Regla empírica", $"Dentro de μ ± 1σ está el {d[0]:0.0} % de los datos (normal: 68,3 %), en ± 2σ el {d[1]:0.0} % (95,4 %) y en ± 3σ el {d[2]:0.0} % (99,7 %)."));
        }

        // Atípicos.
        var at = r.AtipicosLeves.Count + r.AtipicosExtremos.Count;
        p.Add(at == 0
            ? new("Atípicos", $"Sin valores atípicos según Tukey (fuera de {F(r.LimiteInfTukey)} – {F(r.LimiteSupTukey)}).", "ok")
            : new("Atípicos", $"{at} valor(es) atípico(s) según Tukey (fuera de Q1 − 1,5·RIC = {F(r.LimiteInfTukey)} y Q3 + 1,5·RIC = {F(r.LimiteSupTukey)})" +
                (r.AtipicosExtremos.Count > 0 ? $", {r.AtipicosExtremos.Count} extremo(s) (más de 3·RIC)" : "") +
                $": {string.Join("; ", r.AtipicosLeves.Concat(r.AtipicosExtremos).OrderBy(v => v).Take(8).Select(F))}. Revise si son errores de digitación o resultados reales.", "alerta"));

        // Intervalos.
        if (r.IcMediaInf is double ia && r.IcMediaSup is double ib)
            p.Add(new("Confianza", $"Con {r.Confianza * 100:0.#} % de confianza la media real del proceso está entre {F(ia)} y {F(ib)}{u} " +
                $"(± {F(r.MargenError ?? 0)}, t = {r.TCritico:0.000}, gl = {r.N - 1}) y la desviación real entre {F(r.IcDesvInf ?? 0)} y {F(r.IcDesvSup ?? 0)}{u}."));

        // Especificación.
        if (r.Cpk is double cpk)
        {
            var nivel = cpk >= 1.33 ? "ok" : cpk >= 1 ? "info" : "alerta";
            var txt = cpk >= 1.33 ? "proceso capaz" : cpk >= 1 ? "capacidad justa (entre 1 y 1,33)" : "proceso no capaz (Cpk < 1)";
            p.Add(new("Especificación", (r.Cp is double cp ? $"Cp = {cp:0.00}, " : "") + $"Cpk = {cpk:0.00}: {txt}. " +
                $"Fuera de especificación: esperado {r.FueraEspecNormal:0.00} % según la normal, observado {r.FueraEspecReal:0.00} %." +
                (r.EsNormal ? "" : " Ojo: los datos no son normales, Cp/Cpk son indicativos."), nivel));
        }
        return p;
    }

    private static string Mayus(string s) => s.Length == 0 ? s : char.ToUpper(s[0]) + s[1..];

    public static string Pv(double p) => p < 0.001 ? "< 0,001" : p.ToString("0.000", CultureInfo.CurrentCulture);

    // Decimales según la escala de los datos (hasta 4).
    public static string Num(double v, ResultadoEstadistico r)
    {
        var escala = Math.Max(Math.Abs(r.Max), Math.Abs(r.Min));
        var dec = escala >= 1000 ? 1 : escala >= 100 ? 2 : escala >= 1 ? 3 : 4;
        return v.ToString("N" + dec, CultureInfo.CurrentCulture);
    }
}
