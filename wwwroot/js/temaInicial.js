// Se carga en <head> (síncrono, antes de pintar): aplica el tema guardado y el menú contraído para evitar el parpadeo.
// Antes eran scripts en línea en App.razor; como archivo propio la política de seguridad (CSP) no necesita 'unsafe-inline'.
// El cascarón de cuenta (ingresar, invitación…) lo carga con data-cuenta: allí solo hay claro u oscuro.
(function () {
    var h = document.documentElement;
    var cuenta = document.currentScript && document.currentScript.hasAttribute('data-cuenta');
    try {
        var t = localStorage.getItem('sigweb.tema');
        if (cuenta) {
            if (t === 'oscuro' || t === 'cristal') h.setAttribute('data-tema', 'oscuro');
        } else if (t && t !== 'institucional') {
            h.setAttribute('data-tema', t);
            if (t === 'oscuro' || t === 'cristal') h.setAttribute('data-bs-theme', 'dark');
        }
    } catch (e) { }
    if (cuenta) return;
    var c = true;
    try { var v = localStorage.getItem('sigweb.menu.contraido'); if (v !== null) c = JSON.parse(v); } catch (e) { }
    if (c) h.setAttribute('data-menu', 'contraido');
})();
