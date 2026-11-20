// Cambiar de proyecto desde Blazor (menú del ícono del encabezado): llena y envía el formulario oculto de la página
// (#sig-cambio-proyecto, con su token antifalsificación) → POST /cuenta/proyecto → redirige a «volver» (solo rutas locales).
window.sigCambiarProyecto = function (id, volver) {
    const f = document.getElementById('sig-cambio-proyecto');
    if (!f) { location.href = '/proyectos'; return; }
    f.querySelector('input[name="id"]').value = String(id);
    const v = f.querySelector('input[name="volver"]');
    if (v) v.value = volver || '/';
    f.submit();
};
