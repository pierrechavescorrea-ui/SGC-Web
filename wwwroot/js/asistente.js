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
