// Ventanas modales accesibles (.modal-backdrop-custom > .modal-box-custom, ~40 en la app) sin reescribir cada una:
//  · role="dialog", aria-modal y nombre (aria-labelledby al título: h1–h6, .frm-cab-tit) si no lo traen;
//  · al abrir, el foco entra a la ventana (o al campo con autofocus) y al cerrar vuelve a donde estaba;
//  · Tab / Mayús+Tab dan la vuelta dentro de la ventana de arriba (no sale a la página de atrás);
//  · Escape pulsa el botón de cancelar/cerrar de la ventana de arriba (data-modal-cancelar, la X, «Cancelar»…), solo si
//    no se ha escrito nada en ella: con cambios hay que cerrar con el botón (no se pierde un formulario por una tecla).
// Se aplica sola a lo que Blazor pinte (MutationObserver). Las ventanas propias de los scripts (análisis de causa,
// avisos de impacto) tienen su propio manejo y no se tocan.
(function () {
    if (window.sigModales) return;
    const ENFOCABLES = 'a[href], button:not([disabled]), input:not([disabled]):not([type="hidden"]), select:not([disabled]), textarea:not([disabled]), [tabindex]:not([tabindex="-1"]), [contenteditable="true"]';
    const abiertas = new Map();   // backdrop → { caja, previo }
    let n = 0;

    function visible(el) { return !!(el.offsetWidth || el.offsetHeight || el.getClientRects().length); }
    function arriba() {
        let ultima = null;
        abiertas.forEach((_, b) => { if (b.isConnected && visible(b)) ultima = b; });
        // Orden del documento: la que va después queda encima (mismo z-index).
        if (abiertas.size > 1) {
            const lista = [...abiertas.keys()].filter(b => b.isConnected && visible(b));
            lista.sort((a, b) => a.compareDocumentPosition(b) & Node.DOCUMENT_POSITION_FOLLOWING ? -1 : 1);
            ultima = lista[lista.length - 1] || null;
        }
        return ultima;
    }
    function enfocables(caja) { return [...caja.querySelectorAll(ENFOCABLES)].filter(visible); }

    function preparar(b) {
        const caja = b.querySelector(':scope > .modal-box-custom') || b.querySelector('.modal-box-custom') || b;
        if (!caja.hasAttribute('role')) caja.setAttribute('role', 'dialog');
        if (!caja.hasAttribute('aria-modal')) caja.setAttribute('aria-modal', 'true');
        if (!caja.hasAttribute('aria-label') && !caja.hasAttribute('aria-labelledby')) {
            const t = caja.querySelector('h1, h2, h3, h4, h5, h6, .frm-cab-tit');
            if (t) { if (!t.id) t.id = 'sig-modal-tit-' + (++n); caja.setAttribute('aria-labelledby', t.id); }
        }
        if (!caja.hasAttribute('tabindex')) caja.setAttribute('tabindex', '-1');
        const previo = document.activeElement && document.activeElement !== document.body ? document.activeElement : null;
        abiertas.set(b, { caja, previo, sucia: false });
        // El foco entra solo si no está ya adentro (p. ej. Blazor enfocó un campo).
        if (!caja.contains(document.activeElement)) {
            const auto = caja.querySelector('[autofocus]');
            try { (auto && visible(auto) ? auto : caja).focus({ preventScroll: true }); } catch { }
        }
    }
    function cerrar(b) {
        const d = abiertas.get(b);
        abiertas.delete(b);
        if (!d) return;
        // Devuelve el foco a quien abrió la ventana, si sigue en la página y el foco quedó perdido.
        const a = document.activeElement;
        if ((!a || a === document.body || !a.isConnected) && d.previo && d.previo.isConnected) {
            try { d.previo.focus({ preventScroll: true }); } catch { }
        }
    }

    let pendiente = false;
    function revisar() {
        pendiente = false;
        abiertas.forEach((_, b) => { if (!b.isConnected) cerrar(b); });
        document.querySelectorAll('.modal-backdrop-custom').forEach(b => { if (!abiertas.has(b)) preparar(b); });
    }
    function programar() { if (!pendiente) { pendiente = true; requestAnimationFrame(revisar); } }

    function botonCancelar(caja) {
        const directo = caja.querySelector('[data-modal-cancelar]');
        if (directo) return directo;
        const botones = [...caja.querySelectorAll('button:not([disabled])')].filter(visible);
        const txt = x => ((x.getAttribute('aria-label') || x.getAttribute('title') || x.textContent || '') + '').trim();
        return botones.find(x => x.classList.contains('btn-close') || x.classList.contains('frm-cerrar'))
            || botones.find(x => /^(cancelar|cerrar|no,|volver)/i.test(txt(x)));
    }

    document.addEventListener('keydown', e => {
        if (e.defaultPrevented || e.isComposing) return;
        if (e.key !== 'Tab' && e.key !== 'Escape') return;
        const b = arriba();
        if (!b) return;
        const d = abiertas.get(b);
        const caja = d.caja;
        const a = document.activeElement;
        // Algo encima de la ventana con su propio foco (editor de análisis de causa, menús, listas desplegables): no se toca.
        if (a && a !== document.body && !caja.contains(a)) return;
        if (e.key === 'Escape') {
            if (d.sucia || caja.querySelector('[data-imp-emergente], .overlay-procesando')) return;   // con cambios, aviso de impacto (js/avisos.js) o guardando
            const c = botonCancelar(caja);
            if (c) { e.preventDefault(); c.click(); }
            return;
        }
        const lista = enfocables(caja);
        if (!lista.length) { e.preventDefault(); caja.focus({ preventScroll: true }); return; }
        const primero = lista[0], ultimo = lista[lista.length - 1];
        if (e.shiftKey && (a === primero || a === caja || a === document.body)) { e.preventDefault(); ultimo.focus(); }
        else if (!e.shiftKey && (a === ultimo || a === document.body)) { e.preventDefault(); primero.focus(); }
    });

    // Cambios dentro de una ventana: Escape deja de cerrarla.
    function marcar(e) { abiertas.forEach((d, b) => { if (b.contains(e.target)) d.sucia = true; }); }
    document.addEventListener('input', marcar, true);
    document.addEventListener('change', marcar, true);

    function iniciar() {
        new MutationObserver(programar).observe(document.documentElement, { childList: true, subtree: true });
        revisar();
    }
    if (document.body) iniciar(); else document.addEventListener('DOMContentLoaded', iniciar);
    window.sigModales = { revisar };
})();
