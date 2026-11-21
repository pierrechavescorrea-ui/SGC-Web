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
// Escape cierra el panel (pulsa su botón Cerrar).
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

// ---------- πr vivo ----------
// Mira el cursor del texto mientras el usuario escribe; si no, el puntero del mouse. De vez en cuando mira al frente
// (al usuario) y parpadea; balanceo leve (CSS). Al enviar asiente y «piensa»: la respuesta nueva se retiene un momento
// (data-pir-espera) y luego la mira. prefers-reduced-motion = quieto, sin retener respuestas.
window.sigPir = (() => {
    const quieto = () => matchMedia('(prefers-reduced-motion: reduce)').matches;
    const ahora = () => performance.now();
    const azar = (a, b) => a + Math.random() * (b - a);
    const lim = v => Math.max(-1, Math.min(1, v));
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
            lx: 0, ly: 0, rot: 0, escrito: 0, alUsuario: ahora() + 1800, mirarA: null, mirarHasta: 0,
            pensando: false, pensandoDesde: 0, timers: [],
        };
        const entrada = panel.querySelector('.asis-pie input');
        s.alEscribir = () => { s.escrito = ahora(); };
        entrada?.addEventListener('input', s.alEscribir);
        entrada?.addEventListener('keyup', s.alEscribir);
        entrada?.addEventListener('click', s.alEscribir);
        s.entrada = entrada;
        // Enter / Preguntar: asiente y piensa.
        s.alEnviar = () => { if (s.entrada?.value.trim()) pensar(); };
        panel.querySelector('.asis-pie')?.addEventListener('submit', s.alEnviar, true);
        panel.addEventListener('click', s.alClic = e => { if (e.target.closest('button.asis-sug')) pensar(); }, true);
        // Respuesta nueva (después de una pregunta): se retiene mientras «piensa».
        s.obs = new MutationObserver(ms => {
            for (const m of ms) for (const n of m.addedNodes) {
                if (n.nodeType !== 1 || !n.classList.contains('asis-el') || !n.previousElementSibling?.classList.contains('asis-yo')) continue;
                responder(n);
            }
        });
        if (caja) s.obs.observe(caja, { childList: true });
        if (quieto()) return;
        asentir();                                   // saludo al aparecer
        programarParpadeo();
        programarMirada();
        s.raf = requestAnimationFrame(cuadro);
    }

    function detener() {
        if (!st) return;
        cancelAnimationFrame(st.raf);
        st.timers.forEach(clearTimeout);
        st.obs?.disconnect();
        st = null;
    }

    const tarde = (fn, ms) => { const s = st; const t = setTimeout(() => { if (st === s) fn(); }, ms); s.timers.push(t); };

    function asentir() {
        if (!st || quieto()) return;
        st.asiente.classList.remove('si'); void st.asiente.getBoundingClientRect(); st.asiente.classList.add('si');
        tarde(() => st.asiente.classList.remove('si'), 600);
    }
    function parpadear() {
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
        if (!s) return;
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
