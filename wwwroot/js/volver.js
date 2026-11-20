// Botón «volver» de todas las pantallas y memoria de la vista.
//  · sigVolver(respaldo): vuelve a la pantalla anterior de la app en esta pestaña (history.back). Si no
//    hay ninguna (pestaña nueva, enlace directo), va a «respaldo» o al Inicio, sin dejar un bucle.
//    La profundidad de cada entrada del historial se guarda en history.state.sigProf.
//  · sigRecordarVista({ torre: 12, vista: null }): las páginas anotan en la URL (sin recargar ni avisar
//    a Blazor) lo que hay seleccionado; al volver con Atrás la página se abre otra vez así.
//  · Al salir de una pantalla se guarda su scroll (ventana y contenedores con scroll); al volver con
//    Atrás se restaura cuando la página termina de pintar.
//  · sigGuardarPila/sigLeerPila: vistas internas que la página ya mostró (las usa PilaVistas en C#).
//  · [data-sig-pendientes]: confirmar antes de salir con cambios sin guardar (AvisoCambios).
//  · <input data-recordar="buscar">: el valor se anota en la URL (?buscar=…) mientras se escribe.
(function () {
    const K_PROF = 'sigweb.vista.prof';
    const K_MEM = 'sigweb.vista.memoria';
    const MAX_MEM = 40;

    const sget = (k) => { try { return sessionStorage.getItem(k); } catch { return null; } };
    const sset = (k, v) => { try { sessionStorage.setItem(k, v); } catch { } };
    const urlActual = () => location.pathname + location.search;

    const pushOrig = history.pushState.bind(history);
    const replaceOrig = history.replaceState.bind(history);

    const profundidad = () => {
        const p = history.state && history.state.sigProf;
        return typeof p === 'number' ? p : null;
    };
    const conProf = (state, prof) => Object.assign({}, (state && typeof state === 'object') ? state : {}, { sigProf: prof });

    // ---------- Profundidad de la entrada actual ----------
    if (profundidad() === null) {
        // Carga completa en una entrada nueva: si se viene de otra pantalla de la app, va una más.
        let mismaApp = false;
        try { mismaApp = history.length > 1 && !!document.referrer && new URL(document.referrer).origin === location.origin; } catch { }
        const previa = parseInt(sget(K_PROF) ?? '-1', 10);
        const reemplazo = sget(K_PROF + '.reemplazo') === '1';   // llegó con location.replace: misma entrada
        sset(K_PROF + '.reemplazo', '');
        const prof = !mismaApp || previa < 0 ? 0 : reemplazo ? previa : previa + 1;
        try { replaceOrig(conProf(history.state, prof), ''); } catch { }
    }
    sset(K_PROF, String(profundidad() ?? 0));

    let ultimaUrl = urlActual();
    let volviendo = false;   // tras pedir history.back no se anota nada hasta llegar a la otra pantalla

    history.pushState = function (state, titulo, url) {
        guardarMemoria(urlActual());
        const prof = (profundidad() ?? 0) + 1;
        const r = pushOrig(conProf(state, prof), titulo, url);
        sset(K_PROF, String(prof));
        volviendo = false;
        ultimaUrl = urlActual();
        return r;
    };
    // Blazor también reemplaza la entrada (NavigateTo con replace, arranque): se conserva la profundidad.
    history.replaceState = function (state, titulo, url) {
        const prof = profundidad();
        let nuevo = state;
        if (prof !== null && !(state && typeof state === 'object' && 'sigProf' in state)) nuevo = conProf(state, prof);
        const pilas = history.state && history.state.sigPila;
        if (pilas && nuevo && typeof nuevo === 'object' && !('sigPila' in nuevo)) nuevo = Object.assign({}, nuevo, { sigPila: pilas });
        const r = replaceOrig(nuevo, titulo, url);
        ultimaUrl = urlActual();
        return r;
    };
    window.addEventListener('popstate', () => {
        guardarMemoria(ultimaUrl);
        volviendo = false;
        sset(K_PROF, String(profundidad() ?? 0));
        ultimaUrl = urlActual();
        restaurar();
    });
    window.addEventListener('pagehide', () => guardarMemoria(urlActual()));

    // ---------- Volver ----------
    function irA(url, reemplazar) {
        const destino = !url ? '/' : (url.startsWith('/') ? url : '/' + url);
        if (window.Blazor && typeof Blazor.navigateTo === 'function') Blazor.navigateTo(destino, { replaceHistoryEntry: !!reemplazar });
        else if (reemplazar) { sset(K_PROF + '.reemplazo', '1'); location.replace(destino); }
        else location.href = destino;
    }
    // ---------- Cambios sin guardar ----------
    // <AvisoCambios Pendientes="n" /> pinta [data-sig-pendientes="n"]: con n > 0 se pide confirmar antes
    // de salir de la pantalla (volver, vínculos de la app, cerrar o recargar la pestaña).
    const MSG_PENDIENTES = (n) => `Hay ${n} cambio${n === 1 ? '' : 's'} sin guardar. ¿Salir sin guardar?`;
    function pendientes() {
        let n = 0;
        document.querySelectorAll('[data-sig-pendientes]').forEach(el => { n += parseInt(el.getAttribute('data-sig-pendientes') || '0', 10) || 0; });
        return n;
    }
    function puedeSalir() { const n = pendientes(); return n <= 0 || confirm(MSG_PENDIENTES(n)); }
    window.addEventListener('beforeunload', e => { if (pendientes() > 0) { e.preventDefault(); e.returnValue = ''; } });
    document.addEventListener('click', e => {
        if (e.defaultPrevented || e.button !== 0 || e.ctrlKey || e.metaKey || e.shiftKey || e.altKey) return;
        const a = e.target.closest && e.target.closest('a[href]');
        if (!a || a.target === '_blank' || a.hasAttribute('download')) return;
        const href = a.getAttribute('href') || '';
        if (href.startsWith('#') || href.startsWith('javascript:')) return;
        let u; try { u = new URL(a.href, location.href); } catch { return; }
        if (u.origin !== location.origin || u.pathname === location.pathname) return;
        if (!puedeSalir()) { e.preventDefault(); e.stopImmediatePropagation(); }
    }, true);

    let esperaVuelta = null;
    function volver(respaldo) {
        if (!puedeSalir()) return;
        if ((profundidad() ?? 0) > 0) {
            volviendo = true;
            clearTimeout(esperaVuelta);
            esperaVuelta = setTimeout(() => { volviendo = false; }, 2000);
            history.back();
        }
        else irA(respaldo || '/', true);
    }

    // ---------- Anotar la vista en la URL ----------
    // «ruta»: la ruta de la página que anota. Si ya no es la actual (la página se está yendo y aún
    // pinta), no se toca la URL de la pantalla nueva.
    const esRuta = (ruta) => !ruta || location.pathname.toLowerCase() === ('/' + ruta.replace(/^\/+/, '')).toLowerCase();
    function recordar(valores, ruta) {
        if (!valores || volviendo || !esRuta(ruta)) return;
        const u = new URL(location.href);
        for (const [k, v] of Object.entries(valores)) {
            if (v === null || v === undefined || v === '' || v === false) u.searchParams.delete(k);
            else u.searchParams.set(k, String(v));
        }
        const nueva = u.pathname + u.search + u.hash;
        if (nueva === location.pathname + location.search + location.hash) return;
        history.replaceState(history.state, '', nueva);
    }
    // Cambia la ruta (p. ej. /riesgos → /riesgos/12) conservando lo demás de la entrada.
    function recordarRuta(ruta, desde) {
        if (!ruta || volviendo || !esRuta(desde)) return;
        const destino = (ruta.startsWith('/') ? ruta : '/' + ruta) + location.search;
        if (destino === urlActual()) return;
        history.replaceState(history.state, '', destino);
    }

    // Pila de vistas internas de una página (pestañas, detalle abierto desde la lista…), guardada en
    // la entrada del historial: al volver a esta pantalla con Atrás, la página sigue sabiendo qué vio antes.
    function guardarPila(clave, pila, ruta) {
        if (!clave || volviendo || !esRuta(ruta)) return;
        const st = (history.state && typeof history.state === 'object') ? history.state : {};
        const pilas = Object.assign({}, st.sigPila || {});
        if (pila && pila.length) pilas[clave] = pila; else delete pilas[clave];
        replaceOrig(Object.assign({}, st, { sigPila: pilas }), '');
    }
    function leerPila(clave) {
        const p = history.state && history.state.sigPila && history.state.sigPila[clave];
        return Array.isArray(p) ? p : [];
    }

    let tempFiltro = null;
    document.addEventListener('input', e => {
        const el = e.target.closest && e.target.closest('[data-recordar]');
        if (!el) return;
        clearTimeout(tempFiltro);
        tempFiltro = setTimeout(() => recordar({ [el.getAttribute('data-recordar')]: (el.value || '').trim() }), 350);
    }, true);
    document.addEventListener('change', e => {
        const el = e.target.closest && e.target.closest('select[data-recordar]');
        if (el) recordar({ [el.getAttribute('data-recordar')]: el.value });
    }, true);

    // ---------- Memoria de scroll ----------
    const leerMem = () => { try { return JSON.parse(sget(K_MEM) || '{}'); } catch { return {}; } };
    function firma(el) {
        let s = el.tagName.toLowerCase();
        if (el.id) return s + '#' + CSS.escape(el.id);
        for (const c of el.classList) s += '.' + CSS.escape(c);
        return s;
    }
    function guardarMemoria(url) {
        if (!url) return;
        try {
            const raiz = document.querySelector('article.content') || document.body;
            const els = [];
            raiz.querySelectorAll('*').forEach(el => {
                if (el.scrollTop > 0 || el.scrollLeft > 0) {
                    const sel = firma(el);
                    const i = [...raiz.querySelectorAll(sel)].indexOf(el);
                    if (i >= 0) els.push({ s: sel, i, t: el.scrollTop, l: el.scrollLeft });
                }
            });
            const mem = leerMem();
            delete mem[url];
            mem[url] = { y: window.scrollY, x: window.scrollX, els: els.slice(0, 20) };
            const claves = Object.keys(mem);
            for (const k of claves.slice(0, Math.max(0, claves.length - MAX_MEM))) delete mem[k];
            sset(K_MEM, JSON.stringify(mem));
        } catch { }
    }

    let restaurando = null;
    function restaurar() {
        clearInterval(restaurando);
        const m = leerMem()[urlActual()];
        if (!m) return;
        const fin = Date.now() + 6000;
        const url = urlActual();
        let tocado = false;
        const parar = () => { tocado = true; };
        ['wheel', 'touchstart', 'keydown', 'mousedown'].forEach(ev => window.addEventListener(ev, parar, { once: true, capture: true, passive: true }));
        restaurando = setInterval(() => {
            if (tocado || Date.now() > fin || urlActual() !== url) { clearInterval(restaurando); return; }
            const raiz = document.querySelector('article.content') || document.body;
            if (Math.abs(window.scrollY - m.y) > 2 && document.documentElement.scrollHeight - window.innerHeight >= m.y - 2) window.scrollTo(m.x || 0, m.y);
            for (const e of m.els) {
                const el = raiz.querySelectorAll(e.s)[e.i];
                if (!el) continue;
                if (Math.abs(el.scrollTop - e.t) > 2 && el.scrollHeight - el.clientHeight >= e.t - 2) el.scrollTop = e.t;
                if (Math.abs(el.scrollLeft - e.l) > 2 && el.scrollWidth - el.clientWidth >= e.l - 2) el.scrollLeft = e.l;
            }
        }, 150);
    }
    // Atrás que el navegador resuelve con una carga completa (sin caché): también se restaura.
    if (performance.getEntriesByType && performance.getEntriesByType('navigation')[0]?.type === 'back_forward') restaurar();

    window.sigVolver = volver;
    window.sigRecordarVista = recordar;
    window.sigRecordarRuta = recordarRuta;
    window.sigGuardarPila = guardarPila;
    window.sigLeerPila = leerPila;
})();
