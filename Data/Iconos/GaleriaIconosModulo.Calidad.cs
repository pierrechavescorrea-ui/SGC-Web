namespace SigWeb.Data.Iconos;

public static partial class GaleriaIconosModulo
{
    // Calidad, Seguridad y salud, Gestión, Ambiental y topografía.
    private static IEnumerable<IconoModulo> IconosCalidad() => new IconoModulo[]
    {
        new("inspeccion", "Inspección", Calidad, "<path d=\"M9 5H6.5A1.5 1.5 0 0 0 5 6.5v13A1.5 1.5 0 0 0 6.5 21h11a1.5 1.5 0 0 0 1.5-1.5v-13A1.5 1.5 0 0 0 17.5 5H15\"/><rect x=\"9\" y=\"3\" width=\"6\" height=\"4\" rx=\"1.5\"/><path d=\"m9 13 2 2 4-4\"/>", "revisión chequeo visita"),
        new("lista-chequeo", "Lista de chequeo", Calidad, "<path d=\"m3.5 6 1.5 1.5 3-3m-4.5 8 1.5 1.5 3-3m-4.5 8 1.5 1.5 3-3M12 6h8.5M12 12.5h8.5M12 19h8.5\"/>", "checklist verificación controles cotejo"),
        new("no-conformidad", "No conformidad", Calidad, "<path d=\"M6 3h8l4 4v12.5a1.5 1.5 0 0 1-1.5 1.5h-9A1.5 1.5 0 0 1 6 19.5zM14 3v4h4\"/><path d=\"m9.5 11.5 5 5m0-5-5 5\"/>", "nc defecto incumplimiento rechazo desviación"),
        new("accion-correctiva", "Acción correctiva", Calidad, "<path d=\"M3.5 7.5v5h5M3.5 12.5a8.5 8.5 0 0 1 16-4M20.5 15a8.5 8.5 0 0 1-13 4\"/><path d=\"m10 13 2 2 4-4\"/>", "corrección pf reparación causa remediación"),
        new("punto-inspeccion", "Punto de inspección", Calidad, "<path d=\"M3 12s3.5-5.5 9-5.5 9 5.5 9 5.5-3.5 5.5-9 5.5S3 12 3 12z\"/><circle cx=\"12\" cy=\"12\" r=\"2.5\"/>", "pie hito verificación testigo control"),
        new("liberacion", "Liberación", Calidad, "<path d=\"M8 14v-2l1.5-2.5v-3a2.5 2.5 0 0 1 5 0v3L16 12v2\"/><rect x=\"4\" y=\"14\" width=\"16\" height=\"4\" rx=\"1.5\"/><path d=\"M6.5 21h11\"/>", "sello aprobación autorización aceptación"),
        new("muestra-laboratorio", "Muestra de laboratorio", Calidad, "<ellipse cx=\"12\" cy=\"5.5\" rx=\"5\" ry=\"2.5\"/><path d=\"M7 5.5v13c0 1.4 2.2 2.5 5 2.5s5-1.1 5-2.5v-13M10.5 12h3M10.5 15.5h3\"/>", "probeta cilindro concreto testigo ensayo"),
        new("torque", "Torque", Calidad, "<path d=\"m6 20 7-7m-8 5 2 2\"/><circle cx=\"15.5\" cy=\"10.5\" r=\"3.5\"/><path d=\"M10 6.5a7 7 0 0 1 10-1.5M20 2.5v3.5h-3.5\"/>", "apriete par torsión llave dinamométrica"),
        new("soldadura", "Soldadura", Calidad, "<path d=\"M3.5 18.5h17M6 5l2-2 7 7-2 2zM14 11l2 2M16 15.5v.5M18.5 13l2-.5M19 15.5l1.5 1\"/>", "cordón unión electrodo chispa soldador"),
        new("galvanizado-espesor", "Espesor de galvanizado", Calidad, "<rect x=\"6.5\" y=\"3\" width=\"11\" height=\"11\" rx=\"2\"/><path d=\"M10 7h4M10 11h2M10 14v3h4v-3M3 18h18M3 21h18\"/>", "medidor recubrimiento zinc micras película"),
        new("metrologia", "Metrología", Calidad, "<path d=\"M5 3v13l4 4v-5m-4-8h14v4H5M13 11v4l4 4v-8M9 7v1.5M13 7v1.5M17 7v1.5\"/>", "calibrador pie de rey vernier medición dimensiones"),
        new("registro-fotografico", "Registro fotográfico", Calidad, "<path d=\"M5 7.5h3l1.5-2.5h5L16 7.5h3a2 2 0 0 1 2 2V17a2 2 0 0 1-2 2H5a2 2 0 0 1-2-2V9.5a2 2 0 0 1 2-2z\"/><circle cx=\"12\" cy=\"13\" r=\"3\"/>", "foto cámara imagen evidencia"),
        new("hallazgo", "Hallazgo", Calidad, "<circle cx=\"10.5\" cy=\"10.5\" r=\"6.5\"/><path d=\"m15.5 15.5 5 5M10.5 7.5V11M10.5 14v.1\"/>", "observación evidencia auditoría detección"),
        new("mejora-continua", "Mejora continua", Calidad, "<path d=\"M4 10a8 8 0 0 1 14-3l1.5 2M19.5 4.5V9H15M20 14a8 8 0 0 1-14 3l-1.5-2M4.5 19.5V15H9\"/>", "phva pdca ciclo optimización proceso"),

        new("casco", "Casco", Seguridad, "<path d=\"M4.5 15v-2a7.5 7.5 0 0 1 15 0v2M12 5.5V11\"/><rect x=\"3\" y=\"15\" width=\"18\" height=\"4\" rx=\"1.5\"/>", "protección cabeza epp seguridad"),
        new("permiso-trabajo", "Permiso de trabajo", Seguridad, "<rect x=\"4\" y=\"5\" width=\"16\" height=\"14\" rx=\"2\"/><circle cx=\"8.5\" cy=\"10\" r=\"1.5\"/><path d=\"M6.5 15h4m3-3 2 2 2.5-2.5\"/>", "autorización ats apt tarea segura"),
        new("extintor", "Extintor", Seguridad, "<rect x=\"8\" y=\"8.5\" width=\"8\" height=\"12.5\" rx=\"2\"/><path d=\"M12 8.5V4h5M12 5H9a4 4 0 0 0-4 4v2M8 14h8\"/>", "incendio fuego emergencia recarga"),
        new("primeros-auxilios", "Primeros auxilios", Seguridad, "<rect x=\"3\" y=\"7\" width=\"18\" height=\"13\" rx=\"2\"/><path d=\"M9 7V4h6v3M12 10.5v6M9 13.5h6\"/>", "botiquín atención emergencia cruz curación"),
        new("senalizacion", "Señalización", Seguridad, "<path d=\"M5.5 17 10.5 4h3l5 13M8 11h8\"/><rect x=\"3\" y=\"17\" width=\"18\" height=\"3.5\" rx=\"1.5\"/>", "cono tránsito demarcación delimitación prevención"),
        new("riesgo-electrico", "Riesgo eléctrico", Seguridad, "<path d=\"M10.7 4.8a1.5 1.5 0 0 1 2.6 0l7.5 13a1.5 1.5 0 0 1-1.3 2.2h-15a1.5 1.5 0 0 1-1.3-2.2z\"/><path d=\"m12.5 9-2 4h3l-2 4\"/>", "rayo tensión choque electrocución peligro"),

        new("acta-reunion", "Acta de reunión", Gestion, "<path d=\"M7 16l-3 4v-4a1.5 1.5 0 0 1-1.5-1.5v-8A1.5 1.5 0 0 1 4 5h16a1.5 1.5 0 0 1 1.5 1.5v8A1.5 1.5 0 0 1 20 16z\"/><path d=\"M7 9h10M7 12.5h6\"/>", "minuta comité acuerdos compromisos reunión"),
        new("cronograma", "Cronograma", Gestion, "<path d=\"M3.5 4v16h17\"/><rect x=\"7\" y=\"5\" width=\"7\" height=\"3.5\" rx=\"1.5\"/><rect x=\"12\" y=\"12\" width=\"8.5\" height=\"3.5\" rx=\"1.5\"/>", "gantt programación plazos planificación fechas"),
        new("capacitacion", "Capacitación", Gestion, "<path d=\"M3 9l9-4 9 4-9 4zM6.5 10.6v5c3.7 2.5 7.3 2.5 11 0v-5M21 9v7\"/>", "formación inducción entrenamiento curso"),
        new("proveedor", "Proveedor", Gestion, "<path d=\"M4.5 17H3V6h11v11h-5M14 9h3.5l3.5 4v4h-1.5M14 17h1\"/><circle cx=\"6.75\" cy=\"17.5\" r=\"2\"/><circle cx=\"17.25\" cy=\"17.5\" r=\"2\"/>", "camión entrega suministro despacho logística"),

        new("ambiental", "Ambiental", Ambiental, "<path d=\"M6 17.5C2 10.5 8.5 5 20.5 4c-1 11.5-6 17.5-14.5 13.5zM3.5 20.5 14 10\"/>", "hoja vegetación ecología sostenibilidad gestión ambiental"),
        new("arbol-servidumbre", "Árbol en servidumbre", Ambiental, "<path d=\"M8 17a4 4 0 0 1-2-7.5 6 6 0 0 1 12 0 4 4 0 0 1-2 7.5zM12 21v-7l2.5-2.5\"/>", "tala poda forestal vegetación inventario"),
        new("topografia", "Topografía", Ambiental, "<rect x=\"7\" y=\"4\" width=\"10\" height=\"7\" rx=\"1.5\"/><path d=\"M17 6h3v3h-3M12 11v3M9 14h6M10 14l-5 7m9-7 5 7M12 14v7\"/>", "estación total levantamiento replanteo trípode"),
        new("predio", "Predio", Ambiental, "<path d=\"m4 7 12-3 4 11-8 5-7-3zM5 17 12 9l8 6\"/>", "lote lindero parcela catastro terreno"),
    };
}
