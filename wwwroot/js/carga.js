// Indicadores de presentación: no intercepta consultas, fetch ni el historial de vistas.
(() => {
    if (window.sigCarga) return;
    const reducido = matchMedia('(prefers-reduced-motion: reduce)');
    const selector = [
        'article.content > *', 'tbody > tr', '.card', '.ini-panel', '.ens-cuadro',
        '.cm-tarjeta', '.m3d-card', '.tema-tarjeta', '.pa-tarjeta', '.ind-tarjeta',
        '.pv-tarjeta', '.rm-tarjeta', '.gs-card', '.vr-tarjeta', '.dos-panel',
        '.panel-formulario', '[data-sig-entrada]'
    ].join(',');
    const excluir = 'script, style, link, [hidden], .sig-espera, .sig-espera *, .pg-cab, [data-sin-entrada], [data-sin-entrada] *';
    let inicial = true, navegando = false, conectado = false, activo = false;
    let avance = .08, pulso, limite, ocultar, cuadro;
    let vistos = new WeakSet();
    const animaciones = new Set();
    const barra = () => document.getElementById('sig-progreso');

    function mostrar() {
        if (activo) return;
        activo = true;
        clearTimeout(ocultar);
        avance = .08;
        const el = barra();
        if (el) {
            el.dataset.activo = '';
            el.firstElementChild.style.transform = `scaleX(${avance})`;
        }
        // Indeterminado: nunca declara 100 % hasta recibir la señal de fin.
        pulso = setInterval(() => {
            avance += (.9 - avance) * .12;
            if (barra()) barra().firstElementChild.style.transform = `scaleX(${avance})`;
        }, 320);
        // Navegación cancelada, descarga o circuito fallido: no dejar una barra eterna.
        limite = setTimeout(() => { inicial = navegando = false; terminar(); }, 30000);
    }

    function terminar() {
        if (!activo) return;
        activo = false;
        clearInterval(pulso);
        clearTimeout(limite);
        const el = barra();
        if (!el) return;
        el.firstElementChild.style.transform = 'scaleX(1)';
        ocultar = setTimeout(() => { el.removeAttribute('data-activo'); }, reducido.matches ? 0 : 180);
    }

    function aparecer() {
        const raiz = document.querySelector('article.content');
        if (!raiz) return;
        let orden = 0;
        for (const el of raiz.querySelectorAll(selector)) {
            if (vistos.has(el) || el.matches(excluir) || !el.getClientRects().length) continue;
            vistos.add(el);
            if (reducido.matches || !el.animate) continue;
            // No anidar entradas de panel + filas ni animar motores de mapas/visores.
            if (el.querySelector('tbody, canvas, .leaflet-container') ||
                (el.matches('article.content > *') && el.querySelector(selector))) continue;
            const rect = el.getBoundingClientRect();
            if (rect.bottom < 0 || rect.top > innerHeight || orden >= 80) continue;
            const estilo = getComputedStyle(el);
            if (estilo.position === 'fixed' || estilo.position === 'sticky') continue;
            const frames = [{ opacity: .35 }, { opacity: 1 }];
            // top relativo no crea el containing block de position:fixed que crea transform.
            // Solo mover filas y elementos ya relativos: no alterar anclajes de hijos absolutos.
            const fila = el.tagName === 'TR' && estilo.position === 'static';
            if (fila || (estilo.position === 'relative' && (estilo.top === 'auto' || estilo.top === '0px'))) {
                if (fila) frames.forEach(f => { f.position = 'relative'; });
                frames[0].top = '4px';
                frames[1].top = '0px';
            }
            const animacion = el.animate(frames, {
                duration: 220, delay: Math.min(orden++ * 14, 140), easing: 'ease-out', fill: 'backwards'
            });
            animaciones.add(animacion);
            const limpiar = () => { animaciones.delete(animacion); };
            animacion.onfinish = limpiar;
            animacion.oncancel = limpiar;
        }
    }

    function actualizar() {
        cuadro = null;
        const esperando = [...document.querySelectorAll('main [data-sig-esqueleto]')]
            .some(el => el.getClientRects().length);
        if (inicial || navegando || esperando) mostrar();
        else terminar();
        if (!inicial && !navegando) aparecer();
    }
    function programar() { if (!cuadro) cuadro = requestAnimationFrame(actualizar); }
    function iniciarNavegacion() { navegando = true; mostrar(); }

    window.sigCarga = {
        interactiva() { inicial = false; programar(); },
        conectarBlazor() {
            if (conectado || !window.Blazor?.addEventListener) return;
            conectado = true;
            Blazor.addEventListener('enhancednavigationstart', iniciarNavegacion);
            Blazor.addEventListener('enhancednavigationend', () => {
                navegando = false;
                vistos = new WeakSet();
                programar();
            });
            // También se dispara con actualizaciones de streaming SSR.
            Blazor.addEventListener('enhancedload', programar);
        }
    };

    // Se registra antes que Blazor, después de la protección de cambios sin guardar.
    document.addEventListener('click', e => {
        if (e.defaultPrevented || e.button !== 0 || e.ctrlKey || e.metaKey || e.shiftKey || e.altKey) return;
        const a = e.target.closest?.('a[href]');
        if (!a || a.hasAttribute('download') || (a.target && a.target !== '_self')) return;
        const u = new URL(a.href, document.baseURI);
        if (u.origin !== location.origin || !/^https?:$/.test(u.protocol) ||
            (u.pathname === location.pathname && u.search === location.search) ||
            /\.[a-z0-9]{2,5}$/i.test(u.pathname) || /\/(api|uploads)\//i.test(u.pathname)) return;
        // Para los enlaces no mejorados, solo iniciar si ningún manejador canceló el clic.
        // Los mejorados ya notifican enhancednavigationstart aunque hagan preventDefault.
        queueMicrotask(() => { if (!e.defaultPrevented) iniciarNavegacion(); });
    });
    window.addEventListener('pageshow', e => {
        if (e.persisted) { inicial = navegando = false; terminar(); programar(); }
    });
    reducido.addEventListener('change', () => {
        if (reducido.matches) for (const animacion of animaciones) animacion.cancel();
    });
    function observar() {
        new MutationObserver(registros => {
            if (registros.some(r => r.target.closest?.('main') ||
                [...r.addedNodes].some(n => n.nodeType === 1 && (n.matches('main, .page') || n.querySelector('main'))))) programar();
        }).observe(document.body, { childList: true, subtree: true });
        programar();
    }
    mostrar();
    if (document.readyState === 'loading') document.addEventListener('DOMContentLoaded', observar, { once: true });
    else observar();
})();
