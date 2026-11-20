using Microsoft.EntityFrameworkCore;
using SigWeb.Models;

namespace SigWeb.Data.Organigrama;

// Primera versión del organigrama de ejemplo (cargos de un proyecto de línea de transmisión). Solo corre si no hay ninguna versión (tampoco en la Papelera).
// El Excel no trae conectores: el jefe de cada casilla se dedujo de su fila y columna. Las
// relaciones dudosas llevan Supuesto (se ven en la tarjeta y en la lista) para revisarlas.
public static class SembradorOrganigrama
{
    private const string Dir = "Dirección", Arq = "Arqueología", Amb = "Ambiental", For = "Forestal", Con = "Construcción y montaje",
        Adm = "Administración", Soc = "Social", Sst = "Seguridad y salud en el trabajo", Cal = "Calidad";

    private const string SupFila = "Jefe deducido por la posición en el Excel (sin conectores): casilla bajo esta en la misma columna.";
    private const string SupPar = "En el Excel está debajo de un par del mismo nivel; se dejó reportando al coordinador.";

    // (clave local, cargo, ocupante, jefe, área, apoyo, supuesto)
    private static readonly (string K, string Cargo, string? Ocupante, string? Jefe, string Area, bool Staff, string? Supuesto)[] Filas =
    {
        ("ger", "Gerente de proyecto", null, null, Dir, false, null),
        ("cg", "Coordinador General", null, "ger", Dir, false, null),
        ("pla", "Planner", null, "cg", Dir, true, "Casilla desplazada a la derecha del Coordinador General: se tomó como apoyo (staff) suyo."),

        ("arq", "Coordinador Arqueólogo", null, "cg", Arq, false, null),
        ("arq1", "Auxiliar Arqueólogo", null, "arq", Arq, false, null),
        ("arq2", "Arqueólogo Auxiliar Adicional", null, "arq", Arq, false, null),

        ("amb", "Coordinador Ambiental", null, "cg", Amb, false, null),
        ("bio11", "Supervisor Biólogo 1 - 1", null, "amb", Amb, false, null),
        ("bio12", "Supervisor Biólogo 1 - 2", null, "amb", Amb, false, SupPar),
        ("bio21", "Supervisor de campo Biólogo 2 - 1", null, "amb", Amb, false, null),
        ("bio22", "Supervisor de campo Biólogo 2 - 1", null, "amb", Amb, false, "Nombre repetido en el Excel (columna N, filas 12 y 14); quizá sea «2 - 2». " + SupPar),
        ("aux11", "Auxiliar de campo Biólogo 1 - 1", null, "bio11", Amb, false, "Jefe asignado por la numeración 1 - 1."),
        ("aux12", "Auxiliar de campo Biólogo 1 - 2", null, "bio12", Amb, false, "Jefe asignado por la numeración 1 - 2."),
        ("aux21", "Auxiliar de campo Biólogo 2 - 1", null, "bio21", Amb, false, "Jefe asignado por la numeración 2 - 1."),
        ("aux22", "Auxiliar de campo Biólogo 2 - 2", null, "bio22", Amb, false, "Jefe asignado por la numeración (2 - 2 = segundo supervisor de la columna)."),
        ("rn1", "Supervisor Recursos Naturales 1", null, "amb", Amb, false, null),
        ("rn2", "Supervisor Recursos Naturales 2", null, "amb", Amb, false, SupPar),
        ("sb1", "Supervisor en Saneamiento Básico 1", null, "amb", Amb, false, null),
        ("sb2", "Supervisor en Saneamiento Básico 2", null, "amb", Amb, false, SupPar),

        ("for", "Coordinador Forestal", null, "cg", For, false, null),
        ("sf1", "Supervisor Forestal 1", null, "for", For, false, null),
        ("sf2", "Supervisor Forestal 2", null, "for", For, false, null),
        ("sf3", "Supervisor Forestal 3", null, "for", For, false, null),
        ("af1", "Auxiliar Forestal de Campo", null, "sf1", For, false, SupFila),
        ("af2", "Auxiliar Forestal de Campo", null, "sf2", For, false, SupFila),
        ("af3", "Auxiliar Forestal de Campo", null, "sf3", For, false, SupFila),

        ("con", "Coordinador de Construcción y montaje", null, "cg", Con, false, null),
        ("rc", "Residente Civil", null, "con", Con, false, null),
        ("rg", "Residente civil/geotecnista", null, "con", Con, false, null),
        ("re", "Residente Electricista", null, "con", Con, false, null),
        ("top", "Topógrafo líder", null, "con", Con, false, null),
        ("sc1", "Supervisor de construcción", null, "rc", Con, false, null),
        ("sc2", "Supervisor de construcción", null, "rg", Con, false, null),
        ("sm", "Supervisor de Montaje", null, "re", Con, false, null),
        ("topc", "Topógrafos de campo", null, "top", Con, false, null),
        ("cap1", "Capataz civil", null, "sc1", Con, false, null),
        ("cap2", "Capataz civil", null, "sc2", Con, false, null),
        ("capm", "Capataz de montaje", null, "sm", Con, false, null),
        ("of1", "Oficial Civil", null, "cap1", Con, false, "En el Excel comparte casilla con «Ayudante Civil»."),
        ("ay1", "Ayudante Civil", null, "cap1", Con, false, "En el Excel comparte casilla con «Oficial Civil»."),
        ("of2", "Oficial Civil", null, "cap2", Con, false, "En el Excel comparte casilla con «Ayudante Civil»."),
        ("ay2", "Ayudante Civil", null, "cap2", Con, false, "En el Excel comparte casilla con «Oficial Civil»."),
        ("ofm", "Oficiales I y II", null, "capm", Con, false, "En el Excel comparte casilla con «Ayudantes de montaje»."),
        ("aym", "Ayudantes de montaje", null, "capm", Con, false, "En el Excel comparte casilla con «Oficiales I y II»."),

        ("adm", "Administrador de obra", null, "cg", Adm, false, null),
        ("adp", "Administrador de personal", null, "adm", Adm, false, null),
        ("log", "Logístico", null, "adm", Adm, false, null),
        ("per", "Personal", null, "adp", Adm, false, SupFila),
        ("alm", "Almacenista", null, "log", Adm, false, SupFila),

        ("soc", "Coordinador Social", null, "cg", Soc, false, null),
        ("ges", "Gestor Social", null, "soc", Soc, false, null),

        ("sst", "Coordinador seguridad y salud en el trabajo", null, "cg", Sst, false, null),
        ("hsq", "Profesional HSQ", null, "sst", Sst, false, null),
        ("psst", "Profesional SST", null, "sst", Sst, false, null),
        ("ins", "Inspectores SST", null, "hsq", Sst, false, SupFila),
        ("enf", "Enfermero", null, "psst", Sst, false, SupFila),

        ("cal", "Líder calidad", null, "cg", Cal, false, null),
        ("lab", "Laboratorio", null, "cal", Cal, false, null),
    };

    public static void SembrarSiVacio(AppDbContext db)
    {
        if (db.VersionesOrganigrama.IgnoreQueryFilters().Any()) return;
        var v = new VersionOrganigrama
        {
            Numero = 1,
            Titulo = "Organigrama: construcción, montaje, tendido, pruebas, puesta en operación, pruebas de operación comercial",
            Alcance = "Proyecto de línea de transmisión (ejemplo)",
            Estado = EstadoOrganigrama.Vigente,
            MotivoCambio = "Versión inicial cargada del organigrama actual (Excel).",
            Origen = "Ejemplo",
            Notas = "Jefes deducidos de la posición de las casillas (el Excel no trae conectores). Fecha de vigencia, elaboró, revisó y aprobó: no vienen en el Excel."
        };
        db.VersionesOrganigrama.Add(v);
        db.SaveChanges();

        var cargos = new Dictionary<string, CargoOrganigrama>();
        var orden = 0;
        foreach (var f in Filas)
        {
            var c = new CargoOrganigrama
            {
                VersionOrganigramaId = v.Id, Nombre = f.Cargo, Ocupante = f.Ocupante, Area = f.Area, Staff = f.Staff,
                Supuesto = f.Supuesto, Orden = orden++
            };
            cargos[f.K] = c;
            db.CargosOrganigrama.Add(c);
        }
        db.SaveChanges();
        foreach (var f in Filas.Where(f => f.Jefe is not null)) cargos[f.K].JefeId = cargos[f.Jefe!].Id;
        db.SaveChanges();
    }
}
