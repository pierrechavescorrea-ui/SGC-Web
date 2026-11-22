// Asistente de ayuda interno (Components/Layout/AsistenteAyudaPanel.razor): baja al último mensaje y deja el foco en la caja.
window.sigAsistente = {
    alFinal(caja, entrada) {
        if (caja) {
            // Muestra el comienzo de la última respuesta (no su final), para leerla desde arriba.
            const ultimo = caja.lastElementChild;
            if (ultimo && ultimo.offsetHeight > caja.clientHeight * 0.8) caja.scrollTop = ultimo.offsetTop - 8;
            else caja.scrollTop = caja.scrollHeight;
        }
        const panel = entrada?.closest('.asis-panel');
        if (panel) window.sigPir?.iniciar(panel);
        if (!panel || panel.contains(document.activeElement) && document.activeElement !== entrada) return;
        const foco = window.matchMedia('(min-width: 601px)').matches ? entrada : panel;
        foco.focus({ preventScroll: true });
    }
};
function pirClonar(original) {
    const clon = original.cloneNode(true);
    clon.classList.remove('parpadea', 'pir-reaparece');
    clon.setAttribute('aria-hidden', 'true');
    clon.removeAttribute('role');
    clon.removeAttribute('tabindex');
    clon.removeAttribute('aria-label');
    clon.removeAttribute('data-gesto');
    return clon;
}

function pirVolar(figura, x, y, duracion) {
    return figura.animate([
        { transform: getComputedStyle(figura).transform },
        { transform: `translate(${x}px, ${y}px)` }
    ], { duration: duracion, easing: 'cubic-bezier(.2,.75,.25,1)', fill: 'forwards' });
}

window.sigGuia = (() => {
    const clave = 'sigweb.pir.guia';
    let sesion;
    const reducido = () => matchMedia('(prefers-reduced-motion: reduce)').matches;
    const normal = s => (s || '').normalize('NFD').replace(/[\u0300-\u036f]/g, '').replace(/\s+/g, ' ').trim().toLowerCase();
    const rutaLocal = ruta => new URL(ruta.replace(/^\//, ''), document.baseURI);

    function coincidePlantilla(plantilla, destino) {
        if (!plantilla) return false;
        const base = new URL(document.baseURI).pathname.replace(/\/$/, '');
        const segmentos = plantilla.split('/');
        const actuales = destino.pathname.slice(base.length).split('/');
        return destino.pathname.startsWith(base + '/') && segmentos.length === actuales.length &&
            segmentos.every((segmento, i) => segmento.startsWith('{') ? /^[\w-]+$/.test(actuales[i]) : segmento === actuales[i]);
    }

    function recuperar() {
        try {
            const dato = sessionStorage.getItem(clave);
            sessionStorage.removeItem(clave);
            if (!dato) return null;
            const r = JSON.parse(dato);
            const destino = new URL(r.destino, location.origin);
            const actual = new URL(location.href);
            return destino.origin === actual.origin && destino.pathname === actual.pathname &&
                Array.from(destino.searchParams).every(([k, v]) => actual.searchParams.get(k) === v) &&
                Number.isInteger(r.paso) && Date.now() - r.hora < 60000
                ? { id: r.id, paso: r.paso } : null;
        } catch { return null; }
    }

    function navegar(s, i, destino) {
        if (destino.origin !== location.origin) return false;
        try {
            sessionStorage.setItem(clave, JSON.stringify({
                id: s.guia.seccionId, paso: i, destino: destino.pathname + destino.search, hora: Date.now()
            }));
        } catch { return false; }
        finalizar(false);
        location.assign(destino.href);
        return true;
    }

    function finalizar(regresar = true) {
        const s = sesion;
        if (!s) return;
        sesion = null;
        s.eventos.abort();
        s.observador.disconnect();
        cancelAnimationFrame(s.frame);
        s.objetivo?.classList.remove('asis-guia-objetivo');
        s.globo.remove();
        s.panel.classList.remove('asis-guiando');
        const limpiar = () => { s.figura.remove(); s.panel.classList.remove('asis-pir-ausente'); };
        const posicion = getComputedStyle(s.figura).transform;
        s.vuelo?.cancel();
        if (!regresar || reducido() || !s.panel.isConnected || s.figura.hidden) { limpiar(); return; }
        s.figura.style.transform = posicion;
        const casa = s.panel.querySelector('.pir-cab').getBoundingClientRect();
        pirVolar(s.figura, casa.left, casa.top, 380).finished.then(limpiar, limpiar);
    }

    function localizar(nombre) {
        if (!nombre) return null;
        const q = normal(nombre);
        const campos = 'input:not([type="hidden"]),textarea,select';
        const controles = document.querySelectorAll('button,a,input,textarea,select,summary,[role="tab"],[title],[aria-label],label');
        const visibles = [];
        for (const el of controles) {
            if (el.closest('.asis-panel,.asis-guia-globo,[inert],[aria-hidden="true"]') ||
                !el.checkVisibility({ visibilityProperty: true, opacityProperty: true })) continue;
            if (![el.title, el.getAttribute('aria-label'), el.placeholder,
                el.matches('a,button,summary,label,[role="tab"]') ? el.textContent : ''].some(v => normal(v) === q)) continue;
            const hermano = el.nextElementSibling;
            visibles.push(el.control || (el.matches('label') && hermano?.matches(campos) ? hermano : el));
        }
        return visibles.find(el => el.closest('[role="dialog"],.modal-box-custom,.panel-formulario')) || visibles[0] || null;
    }

    function colocar(s) {
        if (sesion !== s) return;
        if (!s.panel.isConnected || location.pathname !== s.pagina) { finalizar(false); return; }
        const paso = s.guia.pasos[s.i];
        const el = localizar(paso.control);
        const nuevo = s.objetivo !== el;
        if (nuevo) {
            s.objetivo?.classList.remove('asis-guia-objetivo');
            s.objetivo = el;
            el?.classList.add('asis-guia-objetivo');
            if (el) {
                el.scrollIntoView({ block: 'center', inline: 'nearest', behavior: 'instant' });
                s.figura.classList.remove('asis-guia-presenta');
            }
        }
        const nota = !paso.bloqueado && !el ? (paso.control ? 'Abra la vista o el menú que contiene «' + paso.control + '».' : 'Este paso no tiene un control identificado.') : '';
        if (s.nota.textContent !== nota) s.nota.textContent = nota;
        const r = el?.getBoundingClientRect();
        const ancho = s.globo.offsetWidth, alto = s.globo.offsetHeight;
        const limitar = (x,max) => Math.max(6,Math.min(x,max - 6));
        function junto(w, h, preferirAbajo, disponible = () => true) {
            if (!r) return [limitar((innerWidth - w) / 2, innerWidth - w), limitar(innerHeight - h - 64, innerHeight - h)];
            const x = limitar(r.left, innerWidth - w), y = limitar(r.top, innerHeight - h);
            const abajo = [x, r.bottom + 8], arriba = [x, r.top - h - 8];
            const lados = [[r.right + 8, y], [r.left - w - 8, y]];
            return (preferirAbajo ? [abajo, arriba, ...lados] : [...lados, arriba, abajo])
                .find(([a, b]) => a >= 6 && b >= 6 && a + w <= innerWidth - 6 && b + h <= innerHeight - 6 && disponible(a, b));
        }
        const globo = junto(ancho, alto, true) || [6, limitar(innerHeight - alto - 64, innerHeight - alto)];
        s.globo.style.transform = `translate(${globo[0]}px,${globo[1]}px)`;
        if (r) {
            const libre = (x, y) => x >= 6 && y >= 6 && x + 70 <= innerWidth - 6 && y + 52 <= innerHeight - 6;
            // Sin sitio junto al control (p. ej. control pegado al borde y el globo ocupando el único hueco): π se posa
            // pegado al globo, del lado más cercano al control, en vez de ocultarse.
            const lugar = junto(70, 52, false, (x, y) =>
                x + 70 < globo[0] || x > globo[0] + ancho || y + 52 < globo[1] || y > globo[1] + alto)
                || [[globo[0] - 76, globo[1]], [globo[0] + ancho + 6, globo[1]], [globo[0], globo[1] + alto + 6], [globo[0], globo[1] - 58]]
                    .filter(([x, y]) => libre(x, y))
                    .sort((a, b) => Math.hypot(a[0] + 35 - (r.left + r.width / 2), a[1] + 26 - (r.top + r.height / 2))
                        - Math.hypot(b[0] + 35 - (r.left + r.width / 2), b[1] + 26 - (r.top + r.height / 2)))[0];
            if (lugar) {
                const [x, y] = lugar;
                s.figura.hidden = false;
                s.figura.dataset.lado = x >= r.right ? 'derecha' : x + 70 <= r.left ? 'izquierda' : 'vertical';
                const destino = `translate(${x}px,${y}px)`;
                if (!nuevo && s.destinoFigura === destino) return;
                s.destinoFigura = destino;
                const posicion = getComputedStyle(s.figura).transform;
                s.vuelo?.cancel();
                s.figura.style.transform = posicion;
                if (nuevo && !reducido()) {
                    const vuelo = s.vuelo = pirVolar(s.figura, x, y, 420);
                    vuelo.finished.then(() => {
                        if (sesion === s) s.figura.classList.add('asis-guia-presenta');
                        vuelo.cancel();
                    }, () => {});
                } else {
                    s.figura.classList.add('asis-guia-presenta');
                }
                s.figura.style.transform = destino;
                return;
            }
        }
        s.destinoFigura = null;
        s.figura.hidden = true;
    }

    function mostrar(s, i) {
        if (i >= s.guia.pasos.length) { finalizar(); return; }
        s.i = Math.max(0,i);
        const paso = s.guia.pasos[s.i];
        const destino = paso.ruta && rutaLocal(paso.ruta);
        const actual = new URL(location.href);
        if (destino && !coincidePlantilla(paso.rutaPlantilla, actual) &&
            (destino.pathname !== actual.pathname || (!localizar(paso.control) &&
                Array.from(destino.searchParams).some(([k, v]) => actual.searchParams.get(k) !== v))) &&
            navegar(s, s.i, destino)) return;
        s.contador.textContent = 'Paso ' + (s.i+1) + ' de ' + s.guia.pasos.length;
        s.texto.textContent = paso.texto;
        s.atras.disabled = s.i === 0;
        s.siguiente.hidden = s.i === s.guia.pasos.length-1;
        colocar(s);
    }

    function iniciar(guia, i = 0) {
        finalizar(false);
        const panel = document.querySelector('.asis-panel');
        if (!panel || !guia.pasos.length) return;
        const globo = document.createElement('aside'), figura = document.createElement('div');
        globo.className = 'asis-guia-globo';
        globo.setAttribute('aria-label','Guía paso a paso');
        figura.className = 'asis-guia-figura';
        figura.setAttribute('aria-hidden','true');
        for (const original of panel.querySelectorAll('svg.pir')) figura.append(pirClonar(original));
        const origen = panel.querySelector('.pir-cab').getBoundingClientRect();
        figura.style.transform = 'translate(' + origen.left + 'px,' + origen.top + 'px)';
        const eventos = new AbortController();
        const s = sesion = {panel,guia,globo,figura,eventos,pagina:location.pathname,i:0};
        s.contador = document.createElement('strong'); s.texto = document.createElement('p'); s.nota = document.createElement('small');
        s.contador.setAttribute('aria-live','polite'); s.texto.setAttribute('aria-live','polite');
        const nav = document.createElement('div');
        function boton(titulo,icono,accion) {
            const b = document.createElement('button');
            b.type='button'; b.title=titulo; b.setAttribute('aria-label',titulo); b.textContent=icono;
            b.addEventListener('click',accion,{signal:eventos.signal}); nav.append(b); return b;
        }
        s.atras = boton('Anterior','←',() => mostrar(s,s.i-1));
        s.siguiente = boton('Siguiente','→',() => mostrar(s,s.i+1));
        boton('Terminar','×',() => finalizar());
        globo.append(s.contador,s.texto,s.nota,nav); document.body.append(figura,globo);
        panel.classList.add('asis-guiando', 'asis-pir-ausente');
        figura.getBoundingClientRect();
        const actualizar = () => { cancelAnimationFrame(s.frame); s.frame=requestAnimationFrame(() => colocar(s)); };
        s.observador = new MutationObserver(cambios => {
            if (cambios.some(m => !globo.contains(m.target) && !figura.contains(m.target))) actualizar();
        });
        s.observador.observe(document.body,{childList:true,subtree:true,attributes:true,attributeFilter:['class','hidden','open','aria-expanded']});
        window.addEventListener('resize',actualizar,{signal:eventos.signal});
        window.addEventListener('scroll',actualizar,{capture:true,signal:eventos.signal});
        window.addEventListener('pagehide',() => finalizar(false),{signal:eventos.signal});
        window.addEventListener('popstate',() => finalizar(false),{signal:eventos.signal});
        document.addEventListener('keydown',e => {
            if(e.key==='Escape') {e.preventDefault();e.stopImmediatePropagation();finalizar();}
        },{capture:true,signal:eventos.signal});
        const avanzarDesdeControl = e => {
            if (s.objetivo?.contains(e.target) && !s.objetivo.matches(':disabled')) {
                const campo = s.objetivo.matches('input:not([type="button"]):not([type="submit"]),textarea,select');
                if (campo !== (e.type === 'change')) return;
                const pasoActual = s.i;
                const siguiente = s.guia.pasos[pasoActual + 1];
                const enlace = s.objetivo.closest('a[href]:not([download])');
                if (enlace && siguiente && e.type === 'click' && e.button === 0 && !e.ctrlKey && !e.metaKey && !e.shiftKey && !e.altKey &&
                    (!enlace.target || enlace.target === '_self')) {
                    const destino = new URL(enlace.href);
                    const ruta = siguiente.ruta && rutaLocal(siguiente.ruta);
                    if ((destino.pathname === ruta?.pathname || coincidePlantilla(siguiente.rutaPlantilla, destino)) &&
                        destino.pathname !== location.pathname && navegar(s, pasoActual + 1, destino)) {
                        e.preventDefault();
                        e.stopPropagation();
                        return;
                    }
                }
                setTimeout(() => { if(sesion === s && s.i === pasoActual) mostrar(s,s.i+1); },100);
            }
        };
        document.addEventListener('click',avanzarDesdeControl,{capture:true,signal:eventos.signal});
        document.addEventListener('change',avanzarDesdeControl,{capture:true,signal:eventos.signal});
        panel.querySelector('[title="Nueva conversación"]')?.addEventListener('click', () => finalizar(false), {signal:eventos.signal});
        mostrar(s,Math.min(Math.max(0,i),guia.pasos.length-1));
        if (sesion === s) globo.querySelector('button:not(:disabled):not([hidden])')?.focus({preventScroll:true});
    }
    return { iniciar, recuperar };
})();
// Escape cierra el panel cuando no hay una guía activa.
document.addEventListener('keydown', e => {
    if (e.defaultPrevented || e.isComposing || e.key !== 'Escape') return;
    const panel = document.querySelector('.asis-panel');
    if (panel?.contains(document.activeElement)) {
        e.preventDefault();
        panel.querySelector('.asis-cab-btn[title="Cerrar"]')?.click();
    }
});

document.addEventListener('click', e => {
    if (e.target.closest?.('.asis-panel .asis-cab-btn[title="Cerrar"]') &&
        document.querySelector('.asis-panel')?.contains(document.activeElement)) {
        document.getElementById('asis-abrir')?.focus({ preventScroll: true });
    }
});

// ---------- Enlaces directos a una opción ----------
// Todo <a href> del panel lleva a la pantalla exacta (la navegación la hace Blazor). Si trae data-resaltar, al llegar
// se busca el control con ese title/aria-label/texto, se centra y se ilumina unos segundos. El pedido se guarda en
// sessionStorage por si la navegación recarga la página (rutas no interactivas).
(() => {
    const CLAVE = 'sigAyudaResaltar';
    const norm = s => (s || '').normalize('NFD').replace(/[̀-ͯ]/g, '').replace(/\s+/g, ' ').trim().toLowerCase();
    const visible = el => {
        if (el.closest('.asis-panel')) return false;
        const r = el.getBoundingClientRect();
        return r.width > 0 && r.height > 0 && getComputedStyle(el).visibility !== 'hidden';
    };

    function buscar(texto) {
        const q = norm(texto);
        if (!q) return null;
        let exacto = null, porTexto = null, empieza = null;
        for (const el of document.querySelectorAll('[title],[aria-label],button,a,[role="tab"],summary,label,th,.nav-link,option')) {
            if (!visible(el)) continue;
            const t = norm(el.getAttribute('title')), a = norm(el.getAttribute('aria-label'));
            if (t === q || a === q) { exacto = el; break; }
            if (!porTexto && el.children.length < 4 && norm(el.textContent) === q) porTexto = el;
            if (!empieza && ((t && t.startsWith(q)) || (a && a.startsWith(q)))) empieza = el;
        }
        return exacto || porTexto || empieza;
    }

    // «!Texto» en el manual = además de señalarlo, abrirlo (vistas y paneles: Ajustes, Revisiones…).
    function iluminar(el, abrir) {
        el.scrollIntoView({ block: 'center', inline: 'nearest', behavior: matchMedia('(prefers-reduced-motion: reduce)').matches ? 'instant' : 'smooth' });
        el.classList.remove('asis-resalte');
        void el.offsetWidth;                         // reinicia la animación si ya estaba
        el.classList.add('asis-resalte');
        setTimeout(() => el.classList.remove('asis-resalte'), 4200);
        if (abrir && !el.disabled && !el.classList.contains('activo')) setTimeout(() => el.click(), 700);
    }

    const rutaDe = r => (r || '').split(/[?#]/)[0].replace(/\/+$/, '').toLowerCase();
    const enRuta = ruta => !ruta || location.pathname.replace(/\/+$/, '').toLowerCase().endsWith(rutaDe(ruta));
    const cerrarPanel = () => setTimeout(() => document.querySelector('.asis-panel .asis-cab-btn[title="Cerrar"]')?.click(), 0);

    // Reintenta mientras la pantalla carga (datos, pestañas, Blazor interactivo): hasta ~8 s.
    function resaltar(pedido, ruta) {
        const abrir = pedido.startsWith('!'), texto = abrir ? pedido.slice(1) : pedido;
        const inicio = Date.now();
        const intentar = () => {
            const el = enRuta(ruta) ? buscar(texto) : null;
            if (el) { iluminar(el, abrir); try { sessionStorage.removeItem(CLAVE); } catch { } return; }
            if (Date.now() - inicio < 8000) setTimeout(intentar, 300);
            else { try { sessionStorage.removeItem(CLAVE); } catch { } }
        };
        setTimeout(intentar, 250);
    }

    // Fase de captura: corre antes que la navegación de Blazor, para poder quedarse en la pantalla actual.
    document.addEventListener('click', e => {
        const a = e.target.closest?.('.asis-panel a[href]');
        if (!a || e.button !== 0 || e.ctrlKey || e.metaKey || e.shiftKey) return;
        const pedido = a.dataset.resaltar;
        const ruta = '/' + (a.getAttribute('href') || '').replace(/^\/+/, '');
        // Ya está en esa pantalla y el control se ve (p. ej. con OTRO modelo o torre abiertos): no navega, solo lo señala.
        if (pedido && enRuta(ruta) && buscar(pedido.replace(/^!/, ''))) {
            e.preventDefault(); e.stopPropagation();
            cerrarPanel();
            resaltar(pedido, ruta);
            return;
        }
        if (pedido) {
            try { sessionStorage.setItem(CLAVE, JSON.stringify({ texto: pedido, ruta, t: Date.now() })); } catch { }
            resaltar(pedido, ruta);
        }
        // Misma pantalla con otra consulta (?vista=tendido): muchas páginas leen la consulta solo al abrir, así que se
        // carga de nuevo para que la vista pedida se aplique de verdad.
        const destino = new URL(a.href, location.href);
        if (destino.pathname === location.pathname && destino.search !== location.search) {
            e.preventDefault(); e.stopPropagation();
            location.assign(destino.href);
            return;
        }
        cerrarPanel();                               // que no tape la opción (después de que Blazor tome la navegación)
    }, true);

    // Llegada con recarga completa: retoma el pedido reciente.
    function retomar() {
        try {
            const p = JSON.parse(sessionStorage.getItem(CLAVE) || 'null');
            if (p && Date.now() - p.t < 15000) resaltar(p.texto, p.ruta);
        } catch { }
    }
    if (document.readyState === 'loading') document.addEventListener('DOMContentLoaded', retomar); else retomar();
})();

// ---------- π vivo ----------
// Mira el cursor del texto mientras el usuario escribe; si no, el puntero del mouse. De vez en cuando mira al frente
// (al usuario) y parpadea; balanceo leve (CSS). Al enviar asiente y «piensa»: la respuesta nueva se retiene un momento
// (data-pir-espera) y luego la mira. Con movimiento reducido se usan gestos estáticos y la respuesta aparece de inmediato.
window.sigPir = (() => {
    const movimientoReducido = matchMedia('(prefers-reduced-motion: reduce)');
    const quieto = () => movimientoReducido.matches;
    const ahora = () => performance.now();
    const azar = (a, b) => a + Math.random() * (b - a);
    const lim = v => Math.max(-1, Math.min(1, v));
    // Cada fila corresponde a un tramo de toques. Se excluye el gesto anterior al sortear.
    const niveles = [
        [
            ['doble', '¿Sí?', 560],
            ['salto', 'Aquí sigo.', 620],
            ['inclina', 'Dígame.', 720],
            ['guiño', 'Le escucho.', 650],
        ],
        [
            ['giro', 'Vaya.', 620],
            ['ojos', 'Eso hace cosquillas.', 650],
            ['asoma', 'Por aquí.', 800],
            ['duda', '¿Algo más?', 900],
        ],
        [
            ['ceño', 'Ya van {n}.', 900],
            ['sacude', 'Con calma.', 660],
            ['aire', 'Un momento.', 850],
            ['cruzadas', 'Sigo contando.', 900],
        ],
        [
            ['espirales', 'Me mareo…', 1100],
            ['tambalea', 'Necesito un segundo.', 1000],
            ['esconde', 'Vuelvo enseguida.', 1400],
            ['serio', 'Confirmado. Han sido {n}.', 1000],
        ],
    ];
    let st = null;
    let raton = null, ratonT = 0;
    document.addEventListener('mousemove', e => { raton = { x: e.clientX, y: e.clientY }; ratonT = ahora(); }, { passive: true });

    let lienzo;
    function caret(inp) {
        const r = inp.getBoundingClientRect(), cs = getComputedStyle(inp);
        lienzo ??= document.createElement('canvas').getContext('2d');
        lienzo.font = cs.font;
        const pos = inp.selectionStart ?? inp.value.length;
        const w = lienzo.measureText(inp.value.slice(0, pos)).width;
        const x = Math.min(r.right - 4, r.left + parseFloat(cs.paddingLeft) + w - inp.scrollLeft);
        return { x, y: r.top + r.height / 2 };
    }

    function iniciar(panel) {
        if (st?.panel === panel) return;
        detener();
        const svg = panel.querySelector('.pir-cab');
        if (!svg) return;
        const caja = panel.querySelector('.asis-mensajes');
        const s = st = {
            panel, svg, caja,
            ojos: svg.querySelector('.pir-ojos'), cabeza: svg.querySelector('.pir-cabeza'), boca: svg.querySelector('.pir-boca'),
            asiente: svg.querySelector('.pir-asiente'),
            lx: 0, ly: 0, escrito: 0, alUsuario: ahora() + 1800, mirarA: null, mirarHasta: 0,
            pensando: false, pensandoDesde: 0, timers: new Set(), eventos: new AbortController(),
            manos: panel.querySelector('.pir-manos'), globo: panel.querySelector('.pir-globo'),
            toques: 0, ultimoToque: -Infinity, ultimoGesto: null,
        };
        const opciones = { signal: s.eventos.signal };
        panel.addEventListener('pointerdown', e => agarrar(s, e), opciones);
        panel.addEventListener('pointermove', e => arrastrar(s, e), opciones);
        panel.addEventListener('pointerup', e => soltar(s, e), opciones);
        for (const tipo of ['pointercancel', 'lostpointercapture']) {
            panel.addEventListener(tipo, e => {
                if (s.puntero?.id !== e.pointerId) return;
                s.puntero = null;
                volver(s);
            }, opciones);
        }
        svg.addEventListener('click', e => { if (e.detail === 0 && !s.vuelo) tocar(s); }, opciones);
        svg.addEventListener('keydown', e => {
            if (e.key !== 'Enter' && e.key !== ' ') return;
            e.preventDefault();
            if (!e.repeat && !s.vuelo) tocar(s);
        }, opciones);
        s.cierre = new MutationObserver(() => { if (!panel.isConnected) detener(); });
        s.cierre.observe(document.body, { childList: true, subtree: true });
        const entrada = panel.querySelector('.asis-pie input');
        const alEscribir = () => { s.escrito = ahora(); };
        for (const tipo of ['input', 'keyup', 'click']) entrada?.addEventListener(tipo, alEscribir, opciones);
        s.entrada = entrada;
        // Enter / Preguntar: asiente y piensa.
        panel.querySelector('.asis-pie')?.addEventListener('submit', () => {
            if (entrada?.value.trim()) pensar();
        }, opciones);
        panel.addEventListener('click', e => {
            if (e.target.closest?.('button.asis-sug')) pensar();
        }, { ...opciones, capture: true });
        // Respuesta nueva (después de una pregunta): se retiene mientras «piensa».
        s.obs = new MutationObserver(ms => {
            for (const m of ms) for (const n of m.addedNodes) {
                if (n.nodeType !== 1 || !n.classList.contains('asis-el') || !n.previousElementSibling?.classList.contains('asis-yo')) continue;
                responder(n);
            }
        });
        if (caja) s.obs.observe(caja, { childList: true });
        movimientoReducido.addEventListener('change', () => {
            if (!quieto()) {
                s.raf = requestAnimationFrame(cuadro);
                return;
            }
            cancelAnimationFrame(s.raf);
            s.lx = s.ly = 0;
            for (const parte of [s.ojos, s.boca, s.cabeza]) parte.style.removeProperty('transform');
            s.svg.classList.remove('parpadea');
            terminar(null);
            if (s.vuelo && !s.puntero) volver(s);
        }, opciones);
        asentir();                                   // saludo al aparecer
        programarParpadeo();
        programarMirada();
        if (!quieto()) s.raf = requestAnimationFrame(cuadro);
    }

    function detener() {
        if (!st) return;
        cancelAnimationFrame(st.raf);
        st.timers.forEach(clearTimeout);
        st.obs.disconnect();
        st.cierre.disconnect();
        st.eventos.abort();
        limpiarGesto(st);
        retirarVuelo(st);
        terminar(null);
        st = null;
    }

    function tarde(fn, ms) {
        const s = st;
        const id = setTimeout(() => {
            s.timers.delete(id);
            if (st === s) fn();
        }, ms);
        s.timers.add(id);
    }

    function limpiarGesto(s) {
        clearTimeout(s.finGesto);
        delete s.svg.dataset.gesto;
        delete s.manos.dataset.gesto;
        s.globo.textContent = '';
    }

    function gesto(s, nombre, frase, ms) {
        limpiarGesto(s);
        s.svg.dataset.gesto = nombre;
        s.manos.dataset.gesto = nombre;
        s.globo.textContent = frase;
        s.alUsuario = ahora() + ms;
        s.finGesto = setTimeout(() => limpiarGesto(s), ms);
    }

    function tocar(s) {
        const t = ahora();
        s.toques = t - s.ultimoToque >= 4000 ? 1 : s.toques + 1;
        s.ultimoToque = t;
        const nivel = s.toques <= 2 ? 0 : s.toques <= 5 ? 1 : s.toques <= 9 ? 2 : 3;
        const posibles = niveles[nivel].filter(([nombre]) => nombre !== s.ultimoGesto);
        const [nombre, frase, ms] = posibles[Math.floor(Math.random() * posibles.length)];
        s.ultimoGesto = nombre;
        gesto(s, nombre, frase.replace('{n}', s.toques), ms);
    }

    function agarrar(s, e) {
        if (!e.isPrimary || e.button !== 0 || s.puntero || s.vuelo ||
            !e.target.closest?.('.pir-cara, .pir-manos path')) return;
        e.preventDefault();
        s.panel.setPointerCapture(e.pointerId);
        const { clientX: x, clientY: y } = e;
        s.puntero = { id: e.pointerId, x, y, muestras: [{ x, y, t: ahora() }] };
    }

    function posicionar(v) {
        v.svg.style.transform = `translate(${v.x}px, ${v.y}px) rotate(${v.angulo}deg)`;
    }

    function arrastrar(s, e) {
        const p = s.puntero;
        if (p?.id !== e.pointerId) return;
        const dx = e.clientX - p.x, dy = e.clientY - p.y;
        if (!s.vuelo) {
            if (Math.hypot(dx, dy) < 5) return;
            limpiarGesto(s);
            const r = s.svg.getBoundingClientRect();
            const svg = pirClonar(s.svg);
            svg.classList.add('pir-vuelo');
            svg.dataset.gesto = 'sorpresa';
            svg.style.left = '0px';
            svg.style.top = '0px';
            s.vuelo = { svg, origenX: r.left, origenY: r.top, ancho: r.width, alto: r.height, angulo: 0 };
            s.panel.classList.add('pir-panel-arrastre');
            document.body.append(svg);
        }
        const v = s.vuelo;
        v.x = v.origenX + dx;
        v.y = v.origenY + dy;
        posicionar(v);
        const t = ahora();
        p.muestras = p.muestras.filter(m => t - m.t <= 120);
        p.muestras.push({ x: e.clientX, y: e.clientY, t });
    }

    function soltar(s, e) {
        const p = s.puntero;
        if (p?.id !== e.pointerId) return;
        const reciente = p.muestras.find(m => ahora() - m.t <= 120);
        arrastrar(s, e);
        s.puntero = null;
        if (!s.vuelo) { tocar(s); return; }
        if (quieto() || !reciente) { volver(s); return; }
        const dt = Math.max(16, ahora() - reciente.t);
        const vx = (e.clientX - reciente.x) / dt, vy = (e.clientY - reciente.y) / dt;
        if (Math.hypot(vx, vy) < .22) { volver(s); return; }

        const v = s.vuelo;
        v.vx = lim(vx / 1.8) * 1.8;
        v.vy = lim(vy / 1.8) * 1.8;
        const inicio = ahora();
        let anterior = inicio;
        function volar(t) {
            if (!s.panel.isConnected) { detener(); return; }
            if (quieto() || t - inicio > 2500) { volver(s); return; }
            const paso = Math.min(t - anterior, 32);
            anterior = t;
            v.vy += .0025 * paso;
            v.x += v.vx * paso;
            v.y += v.vy * paso;
            const maxX = Math.max(0, innerWidth - v.ancho);
            const maxY = Math.max(0, innerHeight - v.alto);
            if (v.x < 0 || v.x > maxX) v.vx *= -.65;
            if (v.y < 0 || v.y > maxY) {
                v.vy *= -.56;
                v.vx *= .82;
            }
            v.x = Math.max(0, Math.min(v.x, maxX));
            v.y = Math.max(0, Math.min(v.y, maxY));
            v.vx *= Math.pow(.998, paso);
            v.angulo += v.vx * paso * .19;
            posicionar(v);
            if (v.y === maxY && Math.hypot(v.vx, v.vy) < .18) { volver(s); return; }
            v.raf = requestAnimationFrame(volar);
        }
        v.raf = requestAnimationFrame(volar);
    }

    function retirarVuelo(s) {
        const v = s.vuelo;
        if (!v) return;
        cancelAnimationFrame(v.raf);
        v.regreso?.cancel();
        v.svg.remove();
        s.panel.classList.remove('pir-panel-arrastre');
        s.vuelo = null;
    }

    function volver(s) {
        const v = s.vuelo;
        if (!v) return;
        cancelAnimationFrame(v.raf);
        const terminarRegreso = () => {
            retirarVuelo(s);
            s.svg.classList.remove('pir-reaparece');
            void s.svg.getBoundingClientRect();
            s.svg.classList.add('pir-reaparece');
            gesto(s, 'alivio', 'De vuelta.', 850);
        };
        if (quieto()) { terminarRegreso(); return; }
        const destino = s.svg.getBoundingClientRect();
        v.regreso = pirVolar(v.svg, destino.left, destino.top, 460);
        v.regreso.onfinish = terminarRegreso;
    }

    function asentir() {
        if (!st || quieto() || st.vuelo || st.svg.dataset.gesto) return;
        st.asiente.classList.remove('si'); void st.asiente.getBoundingClientRect(); st.asiente.classList.add('si');
        tarde(() => st.asiente.classList.remove('si'), 600);
    }
    function parpadear() {
        if (quieto() || st.vuelo || st.svg.dataset.gesto) return;
        st.svg.classList.add('parpadea');
        tarde(() => st.svg.classList.remove('parpadea'), 130);
    }
    function programarParpadeo() {
        tarde(() => { parpadear(); if (Math.random() < .18) tarde(parpadear, 260); programarParpadeo(); }, azar(2600, 6500));
    }
    function programarMirada() {
        tarde(() => {
            if (ahora() - st.escrito > 900 && !st.pensando) st.alUsuario = ahora() + azar(1100, 1900);
            programarMirada();
        }, azar(7000, 14000));
    }

    function pensar() {
        if (!st) return;
        asentir();
        if (quieto()) return;
        st.pensando = true; st.pensandoDesde = ahora();
        st.panel.classList.add('pir-pensando');
        tarde(() => terminar(null), 4000);           // por si la respuesta no llega
    }
    function responder(el) {
        if (!st) return;
        if (!st.pensando && !quieto()) pensar();
        if (quieto()) return;
        el.setAttribute('data-pir-espera', '');
        const espera = Math.max(0, azar(650, 950) - (ahora() - st.pensandoDesde));
        tarde(() => terminar(el), espera);
    }
    function terminar(el) {
        if (!st) return;
        st.pensando = false;
        st.panel.classList.remove('pir-pensando');
        st.panel.querySelectorAll('[data-pir-espera]').forEach(n => n.removeAttribute('data-pir-espera'));
        if (el) { st.mirarA = el; st.mirarHasta = ahora() + 1700; }
    }

    // Punto al que mira (coordenadas de pantalla) o null = al frente.
    function objetivo(s, t) {
        if (s.svg.dataset.gesto || s.vuelo) return null;
        if (s.pensando) return { dx: 0.6, dy: -0.8, fijo: true };            // arriba, a un lado
        if (s.mirarA && t < s.mirarHasta && s.mirarA.isConnected) {
            const r = s.mirarA.getBoundingClientRect();
            return { x: r.left + Math.min(r.width, 160) / 2, y: Math.max(r.top + 12, Math.min(r.bottom, innerHeight) - 30) };
        }
        if (t < s.alUsuario) return null;
        if (s.entrada && document.activeElement === s.entrada && t - s.escrito < 3500) return caret(s.entrada);
        if (raton && t - ratonT < 9000) return raton;
        if (s.entrada && document.activeElement === s.entrada) return caret(s.entrada);
        return null;
    }

    function cuadro() {
        const s = st;
        if (!s || quieto()) return;
        if (!s.panel.isConnected) { detener(); return; }
        const t = ahora();
        const o = objetivo(s, t);
        let tx = 0, ty = 0;
        if (o?.fijo) { tx = o.dx; ty = o.dy; }
        else if (o) {
            const r = s.svg.getBoundingClientRect();
            const cx = r.left + r.width * 60 / 120, cy = r.top + r.height * 37 / 86;
            const dx = o.x - cx, dy = o.y - cy, d = Math.hypot(dx, dy) || 1;
            const k = Math.min(1, d / 220);                                     // cerca = mira poco
            tx = lim(dx / d * k); ty = lim(dy / d * k);
        }
        s.lx += (tx - s.lx) * .16; s.ly += (ty - s.ly) * .16;
        s.ojos.style.transform = `translate(${(s.lx * 6).toFixed(2)}px, ${(s.ly * 4).toFixed(2)}px)`;
        s.boca.style.transform = `translate(${(s.lx * 3).toFixed(2)}px, ${(s.ly * 1.5).toFixed(2)}px)`;
        s.cabeza.style.transform = `rotate(${(s.lx * 5).toFixed(2)}deg) translate(${(s.lx * 1.5).toFixed(2)}px, 0)`;
        s.raf = requestAnimationFrame(cuadro);
    }

    return { iniciar, pensar, detener };
})();
