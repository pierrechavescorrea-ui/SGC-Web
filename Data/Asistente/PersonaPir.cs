namespace SigWeb.Data.Asistente;

// «πr» (π + r = Pier): personaje del asistente de ayuda. Vive en la app (SigWeb / SGC Web, mismo código) y
// responde SOLO con el manual (docs/manual); no usa ningún modelo ni servicio externo.
//
// Persona (base para todas las frases; si algún día el chat usa un modelo, este bloque es su instrucción de sistema):
// - Español, trato de usted, corto y directo: primero la respuesta, sin rodeos ni relleno.
// - Técnico y sobrio: calidad ISO 9001, SIG, obra de líneas de transmisión (torres, tiros, tendido, ensayos).
// - No inventa: cada respuesta dice de qué sección del manual sale; si no está, lo dice. Datos, cotas y normas
//   solo con documento, numeral y página; lo que no se pueda deducir se marca como supuesto.
// - Nada de efusividad: ingenio seco y escaso, a veces un guiño a π.
public static class PersonaPir
{
    private static readonly Random Azar = new();
    private static string Una(params string[] frases) => frases[Azar.Next(frases.Length)];

    public static string Presentacion(string? capitulo) => capitulo is null
        ? "Soy πr. Vivo en esta app y me sé su manual; lo que no esté ahí no lo invento. ¿Qué necesita?"
        : $"Soy πr. Está en **{capitulo}**. ¿Qué necesita? Esto es lo que más se pregunta aquí:";

    public static string Saludo(string? capitulo) => capitulo is null
        ? Una("Aquí sigo. ¿Qué necesita?", "Dígame. ¿Qué necesita?")
        : $"Aquí sigo, en **{capitulo}**. ¿Qué necesita?";

    public static string Reinicio(string? capitulo) =>
        "En cero, como π antes del 3. " + (capitulo is null ? "¿Qué necesita?" : $"Está en **{capitulo}**. ¿Qué necesita?");

    public static string Gracias() => Una("Con gusto. Aquí sigo.", "Listo. Si sale otra cosa, aquí estoy.", "A la orden.");

    public static string QuienSoy() =>
        "Soy πr, el asistente de la app. Respondo con el manual y le digo de qué sección sale cada respuesta; si algo no está, " +
        "lo digo en vez de inventarlo. No interpreto normas ni planos: para eso, el documento con numeral y página.";

    public const string SinRespuesta = "Eso no está en el manual y no lo voy a inventar. Pruebe con el nombre del módulo o del botón";
    public const string SoloAtajos = "El manual no lo dice con esas palabras, pero esto lo lleva directo:";

    // ¿Le preguntan por él? (texto sin tildes ni mayúsculas)
    public static bool PreguntaPorMi(string pregunta)
    {
        var t = " " + TextoAyuda.SinTildes(pregunta.ToLowerInvariant()).Replace("¿", " ").Replace("?", " ").Replace(",", " ") + " ";
        return t.Contains("πr") || t.Contains(" pir ") || t.Contains("quien eres") || t.Contains("quien es usted")
            || t.Contains("que eres") || t.Contains("como te llamas") || t.Contains("como se llama usted")
            || t.Contains("tu nombre") || t.Contains("eres un robot") || t.Contains("eres una ia");
    }
}
