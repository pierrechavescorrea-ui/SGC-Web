/* Contexto de formularios internos. No navega ni modifica pendientes: volver.js sigue
   siendo el dueño del historial y AvisoCambios de la confirmación al salir. */
(() => {
    'use strict';
    const selector = '.panel-formulario, .modal-box-custom, .modal-content, .frm-linea, .ind-panel, [data-sig-pendientes], [data-formulario-contexto]';
    const abiertos = new Map();
    let anterior = null;
    let restauracion = 0;
    const ruta = () => location.pathname;
    function capturar() {
        const raiz = document.querySelector('article.content') || document.body;
        const elementos = [...raiz.querySelectorAll('*')].filter(e => e.scrollTop || e.scrollLeft).map(e => {
            const s = e.id ? '#' + CSS.escape(e.id) : e.tagName.toLowerCase() + [...e.classList].map(c => '.' + CSS.escape(c)).join('');
            return { e, s, i: [...raiz.querySelectorAll(s)].indexOf(e), x: e.scrollLeft, y: e.scrollTop };
        });
        return { ruta: ruta(), x: scrollX, y: scrollY, elementos };
    }
    function restaurar(m) {
        if (!m || m.ruta !== ruta()) return;
        const turno = ++restauracion;
        let intentos = 0;
        const aplicar = () => {
            if (turno !== restauracion || m.ruta !== ruta()) return;
            const raiz = document.querySelector('article.content') || document.body;
            window.scrollTo({ left: m.x, top: m.y, behavior: 'instant' });
            for (const p of m.elementos) {
                const e = p.e.isConnected ? p.e : raiz.querySelectorAll(p.s)[p.i];
                if (e) { e.scrollTop = p.y; e.scrollLeft = p.x; }
            }
            if (++intentos < 8) setTimeout(aplicar, 100);
        };
        aplicar();
    }
    function antes(e) {
        ++restauracion;
        // Una acción dentro de un editor puede abrir un formulario anidado.
        anterior = capturar();
    }
    document.addEventListener('click', antes, true);
    document.addEventListener('keydown', antes, true);
    for (const evento of ['wheel', 'touchstart']) document.addEventListener(evento, () => ++restauracion, { passive: true, capture: true });
    function revisar() {
        const actuales = new Set(document.querySelectorAll(selector));
        const quitados = [...abiertos].filter(([e]) => !actuales.has(e));
        const cerrado = quitados.find(([e]) => !quitados.some(([padre]) => padre !== e && padre.contains(e)))?.[1];
        for (const [e] of quitados) abiertos.delete(e);
        for (const e of actuales) if (!abiertos.has(e)) abiertos.set(e, anterior || capturar());
        // Cerrar al cancelar o al guardar restaura el origen; un fallo deja el nodo abierto.
        if (cerrado) restaurar(cerrado);
        for (const s of document.querySelectorAll('select')) {
            const titulo = [...s.selectedOptions].map(o => o.textContent.trim()).join(', ');
            if (s.title !== titulo) s.title = titulo;
            for (const o of s.options) if (o.title !== o.textContent.trim()) o.title = o.textContent.trim();
        }
    }
    let pendiente = false;
    const observar = new MutationObserver(() => {
        if (pendiente) return;
        pendiente = true;
        queueMicrotask(() => { pendiente = false; revisar(); });
    });
    observar.observe(document.body, { childList: true, subtree: true, characterData: true });
    document.addEventListener('change', revisar, true);
    window.addEventListener('popstate', () => { abiertos.clear(); anterior = null; ++restauracion; });
    revisar();
    // Para un editor sin clase estándar: añadir data-formulario-contexto a su raíz.
    window.sigFormulario = { capturar, restaurar };
})();
