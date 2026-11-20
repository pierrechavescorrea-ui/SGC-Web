using Microsoft.EntityFrameworkCore;
using SigWeb.Models;

namespace SigWeb.Data.AvanceObra;

// Actividades de obra de fábrica. Estructura y pesos: hoja "Avance acumulado" del formato de informe diario
// ENL-DIP-PRO-021-F-054 (informe del 28/10/2026), fila 1 "PESOS". Ítems contractuales: hoja
// "Reporte Diario", capítulo 2.1 ZAPATA (las parrillas usan 2.2/2.3: se ajusta en Ajustes).
// Tendido (0,28) y preliminares (0,07) no se siembran: van por tiro / fuera del alcance de la Fase 1.
// Solo corre si la tabla está vacía (incluida la Papelera); después todo es editable.
public static class SembradorActividadesObra
{
    public static void SembrarSiVacio(AppDbContext db)
    {
        if (db.ActividadesObra.IgnoreQueryFilters().Any()) return;
        int o = 0;
        ActividadObra A(string nombre, string grupo, AmbitoAvance amb, decimal peso, string? unidad, string? item, EfectoAvance3D ef) =>
            new() { Nombre = nombre, Grupo = grupo, Ambito = amb, Peso = peso, Unidad = unidad, ItemContrato = item, Efecto = ef, Orden = ++o };
        db.ActividadesObra.AddRange(
            A("Excavación", "Obra civil", AmbitoAvance.Pata, 0.059m, "m³", "2.1.1", EfectoAvance3D.Excavacion),
            A("Solado", "Obra civil", AmbitoAvance.Pata, 0.007m, "m³", "2.1.5", EfectoAvance3D.Solado),
            A("Nivelación de cimentación", "Obra civil", AmbitoAvance.Pata, 0.043m, null, null, EfectoAvance3D.Nivelacion),
            A("Acero de refuerzo", "Obra civil", AmbitoAvance.Pata, 0.033m, "kg", "2.1.4", EfectoAvance3D.Acero),
            A("Concreto", "Obra civil", AmbitoAvance.Pata, 0.081m, "m³", "2.1.2", EfectoAvance3D.Concreto),
            A("Relleno y compactación", "Obra civil", AmbitoAvance.Pata, 0.037m, "m³", "2.1.3", EfectoAvance3D.Relleno),
            A("Sistema de puesta a tierra", "SPT", AmbitoAvance.Torre, 0.06m, "Sitio de torre", null, EfectoAvance3D.Ninguno),
            A("Prearmado", "Montaje", AmbitoAvance.Torre, 0m, "t", null, EfectoAvance3D.Prearmado),
            A("Montaje", "Montaje", AmbitoAvance.Torre, 0.13m, "t", null, EfectoAvance3D.Montaje),
            A("Verticalidad, torsión y torque", "Montaje", AmbitoAvance.Torre, 0.06m, null, null, EfectoAvance3D.Verticalidad),
            A("Revisión y ajuste de estructura", "Montaje", AmbitoAvance.Torre, 0m, null, null, EfectoAvance3D.Ninguno),
            A("Señalización", "Montaje", AmbitoAvance.Torre, 0m, null, null, EfectoAvance3D.Ninguno),
            A("Medición de resistencia de puesta a tierra", "Montaje", AmbitoAvance.Torre, 0m, null, null, EfectoAvance3D.Ninguno),
            A("Vestida", "Montaje", AmbitoAvance.Torre, 0.11m, "t", null, EfectoAvance3D.Vestida));
        db.SaveChanges();
    }

    // Efecto 3D deducido del nombre de la actividad (importador genérico y reparación al arrancar).
    // Ninguno = no se reconoce. Las de tendido (ámbito Tiro) no se deducen: las siembra SembrarTendidoSiFalta.
    public static EfectoAvance3D DeducirEfecto(string? nombre, AmbitoAvance ambito)
    {
        if (ambito == AmbitoAvance.Tiro) return EfectoAvance3D.Ninguno;
        var n = ImportadorF054.Norm(nombre);
        if (n.Length == 0) return EfectoAvance3D.Ninguno;
        bool T(params string[] p) => p.Any(n.Contains);
        if (T("revision", "senaliz", "medicion", "puesta a tierra", "spt", "contrapeso")) return EfectoAvance3D.Ninguno;
        if (T("prearmado", "pre-armado", "pre armado", "preensamble")) return EfectoAvance3D.Prearmado;
        if (T("verticalidad", "torque", "torsion", "apriete")) return EfectoAvance3D.Verticalidad;
        if (T("vestida", "vestido", "aislador", "cadena")) return EfectoAvance3D.Vestida;
        if (T("montaje", "izado", "ereccion", "armado de estructura", "armado de torre")) return EfectoAvance3D.Montaje;
        if (T("excavac")) return EfectoAvance3D.Excavacion;
        if (T("solado")) return EfectoAvance3D.Solado;
        if (T("nivelac", "stub", "parrilla")) return EfectoAvance3D.Nivelacion;
        if (T("concreto", "vaciado", "fundida")) return EfectoAvance3D.Concreto;
        if (T("acero de refuerzo", "refuerzo", "armadura", "hierro")) return EfectoAvance3D.Acero;
        if (T("relleno", "compactac")) return EfectoAvance3D.Relleno;
        return EfectoAvance3D.Ninguno;
    }

    // Da el efecto 3D a las actividades sin efecto cuyo nombre lo indica, solo para los efectos que ninguna
    // actividad activa tiene todavía (no duplica ni pisa lo elegido en Ajustes). Si varias candidatas
    // compiten por un efecto, gana la de mayor peso y luego la primera en el orden. Devuelve cuántas cambió.
    public static int AsignarEfectosFaltantes(AppDbContext db)
    {
        var acts = db.ActividadesObra.Where(a => a.Activo && a.Ambito != AmbitoAvance.Tiro).ToList();
        var usados = acts.Where(a => a.Efecto != EfectoAvance3D.Ninguno).Select(a => a.Efecto).ToHashSet();
        int n = 0;
        foreach (var g in acts.Where(a => a.Efecto == EfectoAvance3D.Ninguno)
                     .Select(a => (a, ef: DeducirEfecto(a.Nombre, a.Ambito)))
                     .Where(x => x.ef != EfectoAvance3D.Ninguno && !usados.Contains(x.ef))
                     .GroupBy(x => x.ef))
        {
            var elegida = g.OrderByDescending(x => x.a.Peso).ThenBy(x => x.a.Orden).First().a;
            elegida.Efecto = g.Key; usados.Add(g.Key); n++;
        }
        if (n > 0) db.SaveChanges();
        return n;
    }

    // Tendido por tiro (vista Tendido). Pesos en 0: el F-054 da 0,28 para todo el tendido pero no su reparto
    // por actividad (se ajustan en Ajustes). Solo corre si no hay ninguna actividad de ámbito Tiro (ni en la Papelera).
    public static void SembrarTendidoSiFalta(AppDbContext db)
    {
        if (!db.ActividadesObra.IgnoreQueryFilters().Any() || db.ActividadesObra.IgnoreQueryFilters().Any(a => a.Ambito == AmbitoAvance.Tiro)) return;
        int o = db.ActividadesObra.IgnoreQueryFilters().Max(a => a.Orden);
        ActividadObra A(string nombre, EfectoAvance3D ef) =>
            new() { Nombre = nombre, Grupo = "Tendido", Ambito = AmbitoAvance.Tiro, Peso = 0m, Efecto = ef, Orden = ++o };
        db.ActividadesObra.AddRange(
            A("Izado de poleas", EfectoAvance3D.Poleas),
            A("Tendido de cable guía", EfectoAvance3D.CableGuia),
            A("Tendido de cable de guarda", EfectoAvance3D.Guarda),
            A("Tendido de OPGW", EfectoAvance3D.Opgw),
            A("Tendido de conductor", EfectoAvance3D.Conductor),
            A("Flechado y regulación", EfectoAvance3D.Flechado),
            A("Engrapado y amarre", EfectoAvance3D.Engrapado),
            A("Separadores y amortiguadores", EfectoAvance3D.Separadores));
        db.SaveChanges();
    }
}
