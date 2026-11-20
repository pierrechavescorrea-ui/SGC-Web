// Organigrama (Components/Pages/Organigrama.razor): zoom (botones y Ctrl + rueda), arrastrar el fondo
// para desplazarse, ir a un cargo, pantalla completa e impresión / PDF del diagrama.
// El zoom va en style.zoom de .org-lienzo (Blazor no pinta ese atributo) y se guarda en el navegador.
window.sigOrganigrama = (function () {
    const CLAVE = 'sigweb.organigrama.zoom';
    const MIN = 0.25, MAX = 2;

    function marcoDe(x) { return x && x.closest ? x.closest('.org-marco') : x; }
    function lienzo(m) { return m ? m.querySelector('.org-lienzo') : null; }
    function leer() { try { const v = parseFloat(localStorage.getItem(CLAVE)); return isFinite(v) ? v : null; } catch (e) { return null; } }
    function guardar(z) { try { localStorage.setItem(CLAVE, String(z)); } catch (e) { } }

    function aplicar(m, z, cx, cy) {
        const l = lienzo(m);
        if (!l) return;
        const antes = parseFloat(l.style.zoom || '1') || 1;
        z = Math.min(MAX, Math.max(MIN, z));
        // Conserva el punto bajo el cursor (o el centro) al cambiar el zoom.
        const r = m.getBoundingClientRect();
        const px = (cx ?? r.left + m.clientWidth / 2) - r.left, py = (cy ?? r.top + m.clientHeight / 2) - r.top;
        const ux = (m.scrollLeft + px) / antes, uy = (m.scrollTop + py) / antes;
        l.style.zoom = String(z);
        m.scrollLeft = ux * z - px;
        m.scrollTop = uy * z - py;
        guardar(z);
    }

    function ajustar(x) {
        const m = marcoDe(x), l = lienzo(m);
        if (!m || !l) return;
        l.style.zoom = '1';
        const z = Math.min(1, (m.clientWidth - 24) / Math.max(1, l.scrollWidth), (m.clientHeight - 24) / Math.max(1, l.scrollHeight));
        l.style.zoom = String(Math.max(MIN, z));
        m.scrollLeft = (m.scrollWidth - m.clientWidth) / 2;
        m.scrollTop = 0;
        guardar(Math.max(MIN, z));
    }

    // Primera vez: todo el organigrama si se lee (zoom ≥ 0,55); si no, 0,55 centrado en la cabeza.
    function inicial(m) {
        ajustar(m);
        const l = lienzo(m);
        if ((parseFloat(l.style.zoom) || 1) < 0.55) { l.style.zoom = '0.55'; guardar(0.55); centrarRaiz(m); }
    }

    function centrarRaiz(m) {
        const c = m.querySelector('.org-arbol > li > .org-caja > .org-card');
        if (!c) { m.scrollLeft = (m.scrollWidth - m.clientWidth) / 2; return; }
        const r = m.getBoundingClientRect(), rc = c.getBoundingClientRect();
        m.scrollLeft += (rc.left - r.left) - (m.clientWidth - rc.width) / 2;
        m.scrollTop = 0;
    }

    function preparar(m, net, primera) {
        if (!m) return;
        m._sigNet = net;
        const l = lienzo(m);
        if (primera && l && !m._sigZoomListo) {
            m._sigZoomListo = true;
            const z = leer();
            if (z) { l.style.zoom = String(z); centrarRaiz(m); }
            else inicial(m);
        }
        if (m._sigListo) return;
        m._sigListo = true;

        // Arrastrar tarjetas o piezas de la paleta: Firefox solo arrastra si hay datos en dataTransfer.
        m.addEventListener('dragstart', e => {
            if (!e.target.closest || !e.target.closest('.org-card, .org-pieza')) return;
            try { e.dataTransfer.setData('text/plain', 'organigrama'); e.dataTransfer.effectAllowed = 'move'; } catch (x) { }
        });
        m.addEventListener('dragover', e => { if (m.classList.contains('org-editando')) e.dataTransfer.dropEffect = 'move'; });

        m.addEventListener('wheel', e => {
            if (!e.ctrlKey && !e.metaKey) return;
            e.preventDefault();
            const z = parseFloat((lienzo(m) || {}).style?.zoom || '1') || 1;
            aplicar(m, z * (e.deltaY < 0 ? 1.1 : 1 / 1.1), e.clientX, e.clientY);
        }, { passive: false });

        // Desplazar arrastrando el fondo (no sobre tarjetas en edición ni sobre controles).
        let arr = null;
        m.addEventListener('pointerdown', e => {
            if (e.button !== 0) return;
            if (e.target.closest('button, input, select, textarea, a, .org-herr')) return;
            if (m.classList.contains('org-editando') && e.target.closest('.org-card')) return;
            arr = { x: e.clientX, y: e.clientY, l: m.scrollLeft, t: m.scrollTop, movio: false };
        });
        window.addEventListener('pointermove', e => {
            if (!arr) return;
            const dx = e.clientX - arr.x, dy = e.clientY - arr.y;
            if (!arr.movio && Math.abs(dx) + Math.abs(dy) < 4) return;
            arr.movio = true;
            m.classList.add('org-moviendo');
            m.scrollLeft = arr.l - dx;
            m.scrollTop = arr.t - dy;
        });
        window.addEventListener('pointerup', () => {
            if (arr && arr.movio) {
                // Evita que el clic al soltar seleccione una tarjeta.
                const cancelar = ev => { ev.stopPropagation(); ev.preventDefault(); };
                m.addEventListener('click', cancelar, { capture: true, once: true });
                setTimeout(() => m.removeEventListener('click', cancelar, { capture: true }), 0);
            }
            arr = null;
            m.classList.remove('org-moviendo');
        });
    }

    function irA(m, clave) {
        if (!m) return;
        const c = m.querySelector('.org-card[data-clave="' + clave + '"]');
        if (!c) return;
        const r = m.getBoundingClientRect(), rc = c.getBoundingClientRect();
        m.scrollTo({
            left: m.scrollLeft + (rc.left - r.left) - (m.clientWidth - rc.width) / 2,
            top: m.scrollTop + (rc.top - r.top) - (m.clientHeight - rc.height) / 3,
            behavior: 'smooth'
        });
    }

    function enfocar(sel) {
        const el = document.querySelector(sel);
        if (el) { el.focus(); if (el.select) el.select(); }
    }

    let netPc = null;
    document.addEventListener('fullscreenchange', () => {
        if (!document.fullscreenElement && netPc) { const n = netPc; netPc = null; n.invokeMethodAsync('SalioPantallaCompleta').catch(() => { }); }
    });

    async function pantallaCompleta(el) {
        if (document.fullscreenElement) { await document.exitFullscreen(); return false; }
        if (!el || !el.requestFullscreen) return false;
        await el.requestFullscreen();
        const m = el.querySelector('.org-marco');
        netPc = m ? m._sigNet : null;
        setTimeout(() => m && ajustar(m), 150);
        return true;
    }

    function salirPantallaCompleta() { netPc = null; if (document.fullscreenElement) document.exitFullscreen().catch(() => { }); }

    // Imprime / guarda en PDF solo el diagrama (hoja A3 horizontal, ajustado al ancho).
    function imprimir(m, titulo) {
        const l = lienzo(m);
        if (!l) return;
        const estilos = Array.from(document.querySelectorAll('link[rel="stylesheet"]')).map(x => '<link rel="stylesheet" href="' + x.href + '">').join('');
        const copia = l.cloneNode(true);
        copia.style.zoom = '1';
        copia.querySelectorAll('.org-plegar').forEach(b => b.remove());
        copia.querySelectorAll('.sel, .org-tenue, .org-coincide').forEach(c => c.classList.remove('sel', 'org-tenue', 'org-coincide'));
        const w = window.open('', '_blank');
        if (!w) return;
        const tema = document.documentElement.getAttribute('data-tema');
        w.document.write('<!doctype html><html lang="es-CO"><head><meta charset="utf-8"><title></title>' + estilos +
            '<style>@page{size:A3 landscape;margin:10mm}html,body{background:#fff!important}body{margin:0}</style></head><body class="org-impresion">' +
            copia.outerHTML + '</body></html>');
        w.document.close();
        w.document.title = titulo || 'Organigrama';
        if (tema === 'oscuro') { /* la impresión siempre en claro */ }
        const listo = () => {
            const c = w.document.querySelector('.org-lienzo');
            if (c) {
                const ancho = w.document.documentElement.clientWidth || 1500;
                const z = Math.min(1, (ancho - 10) / Math.max(1, c.scrollWidth));
                c.style.zoom = String(z);
            }
            setTimeout(() => { w.focus(); w.print(); }, 250);
        };
        if (w.document.readyState === 'complete') setTimeout(listo, 400); else w.addEventListener('load', listo);
    }

    return {
        preparar, ajustar, irA, enfocar, pantallaCompleta, salirPantallaCompleta, imprimir,
        zoom: (btn, f) => { const m = marcoDe(btn), l = lienzo(m); if (l) aplicar(m, (parseFloat(l.style.zoom || '1') || 1) * f); }
    };
})();
