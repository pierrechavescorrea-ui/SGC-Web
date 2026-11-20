// Diseñador de módulos (Administración › Creador de módulos): lienzo tipo Windows Forms sobre una rejilla de 12 columnas
// × filas de 40 px. Blazor decide QUÉ hay (controles, paneles fijos o sueltos); este archivo hace lo que necesita el mouse
// sin ir y volver al servidor en cada movimiento:
//   · arrastrar una herramienta (HTML5) al lienzo → .NET Soltar(tipo, x, y)
//   · mover / cambiar tamaño de un control (vista previa local) → .NET Mover(clave, x, y, ancho, alto) al soltar
//   · clic sin mover → .NET Seleccionar(clave); clic en el fondo → Seleccionar(null)
//   · paneles sueltos: arrastrar por el título; posición en localStorage (por navegador)
//   · tooltips de [data-tip] ("Título|detalle") y atajos: Supr, Ctrl+Z, Ctrl+Y, Ctrl+S, Esc
// También: sigFirma (firma a mano en un <canvas>) para el formulario de los módulos creados.
(function () {
    'use strict';
    const FILA = 40, CLAVE_PANELES = 'sigweb.disenadorModulos.paneles';

    function leer() { try { return JSON.parse(localStorage.getItem(CLAVE_PANELES) || '{}'); } catch { return {}; } }
    function escribir(v) { try { localStorage.setItem(CLAVE_PANELES, JSON.stringify(v)); } catch { /* sin almacenamiento */ } }

    const estado = { ref: null, raiz: null, tip: null };

    function celda(rej, ev) {
        const r = rej.getBoundingClientRect();
        const ancho = r.width / 12;
        return {
            x: Math.max(0, Math.min(11, Math.floor((ev.clientX - r.left) / ancho))),
            y: Math.max(0, Math.floor((ev.clientY - r.top) / FILA)),
        };
    }

    function aplicarPos(el, x, y, w, h) {
        el.style.gridColumn = (x + 1) + ' / span ' + w;
        el.style.gridRow = (y + 1) + ' / span ' + h;
    }

    // ---------- lienzo ----------
    function alPresionar(ev) {
        const raiz = estado.raiz;
        if (!raiz || ev.button !== 0) return;
        const ctl = ev.target.closest('.dmz-ctl');
        const rej = raiz.querySelector('.dmz-rej');
        if (!ctl) {
            if (ev.target.closest('.dmz-lienzo') && !ev.target.closest('button,input,select,textarea')) estado.ref.invokeMethodAsync('Seleccionar', null);
            return;
        }
        if (ev.target.closest('button,input,select,textarea,a')) return;
        ev.preventDefault();
        const clave = ctl.dataset.clave;
        const x0 = +ctl.dataset.x, y0 = +ctl.dataset.y, w0 = +ctl.dataset.w, h0 = +ctl.dataset.h;
        const redim = !!ev.target.closest('.dmz-asa');
        const ini = celda(rej, ev);
        let x = x0, y = y0, w = w0, h = h0, movio = false;
        ctl.classList.add('moviendo');
        const mover = e => {
            const c = celda(rej, e);
            if (redim) { w = Math.max(1, Math.min(12 - x0, c.x - x0 + 1)); h = Math.max(1, c.y - y0 + 1); }
            else { x = Math.max(0, Math.min(12 - w0, x0 + c.x - ini.x)); y = Math.max(0, y0 + c.y - ini.y); }
            if (x !== x0 || y !== y0 || w !== w0 || h !== h0) movio = true;
            aplicarPos(ctl, x, y, w, h);
        };
        const soltar = () => {
            removeEventListener('pointermove', mover); removeEventListener('pointerup', soltar);
            ctl.classList.remove('moviendo');
            if (movio) estado.ref.invokeMethodAsync('Mover', clave, x, y, w, h);
            else estado.ref.invokeMethodAsync('Seleccionar', clave);
        };
        addEventListener('pointermove', mover); addEventListener('pointerup', soltar);
    }

    function alArrastrarHerramienta(ev) {
        const h = ev.target.closest('[data-herramienta]');
        if (!h) return;
        ev.dataTransfer.setData('text/plain', 'dmz:' + h.dataset.herramienta);
        ev.dataTransfer.effectAllowed = 'copy';
    }

    function alSobreLienzo(ev) {
        if (!ev.target.closest('.dmz-lienzo')) return;
        ev.preventDefault();
        ev.dataTransfer.dropEffect = 'copy';
    }

    function alSoltarEnLienzo(ev) {
        const rej = estado.raiz && estado.raiz.querySelector('.dmz-rej');
        if (!rej || !ev.target.closest('.dmz-lienzo')) return;
        const d = ev.dataTransfer.getData('text/plain') || '';
        if (!d.startsWith('dmz:')) return;
        ev.preventDefault();
        const c = celda(rej, ev);
        estado.ref.invokeMethodAsync('Soltar', d.slice(4), c.x, c.y);
    }

    // ---------- paneles sueltos ----------
    function alPresionarPanel(ev) {
        const cab = ev.target.closest('[data-panel-cab]');
        if (!cab || ev.target.closest('button')) return;
        const panel = cab.closest('[data-panel]');
        if (!panel || !panel.classList.contains('suelto')) return;
        ev.preventDefault();
        const esc = estado.raiz.querySelector('.dmz-esc').getBoundingClientRect();
        const r = panel.getBoundingClientRect();
        const dx = ev.clientX - r.left, dy = ev.clientY - r.top;
        const mover = e => {
            const x = Math.max(0, Math.min(esc.width - 60, e.clientX - esc.left - dx));
            const y = Math.max(0, Math.min(esc.height - 28, e.clientY - esc.top - dy));
            panel.style.left = x + 'px'; panel.style.top = y + 'px';
        };
        const soltar = () => {
            removeEventListener('pointermove', mover); removeEventListener('pointerup', soltar);
            const p = leer(); const id = panel.dataset.panel;
            p[id] = Object.assign(p[id] || {}, { x: parseInt(panel.style.left) || 0, y: parseInt(panel.style.top) || 0 });
            escribir(p);
        };
        addEventListener('pointermove', mover); addEventListener('pointerup', soltar);
    }

    // Pone cada panel suelto donde lo dejó el usuario (o en una posición inicial razonable).
    function aplicarPaneles() {
        const raiz = estado.raiz; if (!raiz) return;
        const p = leer();
        const esc = raiz.querySelector('.dmz-esc'); if (!esc) return;
        const ancho = esc.clientWidth;
        const inicial = { herramientas: { x: 12, y: 12 }, esquema: { x: ancho - 520, y: 12 }, propiedades: { x: ancho - 280, y: 60 } };
        raiz.querySelectorAll('[data-panel].suelto').forEach(el => {
            const id = el.dataset.panel;
            const pos = p[id] && p[id].x != null ? p[id] : (inicial[id] || { x: 40, y: 40 });
            el.style.left = Math.max(0, Math.min(ancho - 60, pos.x)) + 'px';
            el.style.top = Math.max(0, pos.y) + 'px';
        });
        raiz.querySelectorAll('[data-panel]:not(.suelto)').forEach(el => { el.style.left = ''; el.style.top = ''; });
    }

    // ---------- tooltips ----------
    function alSobre(ev) {
        const t = ev.target.closest && ev.target.closest('.dmz [data-tip]');
        const tip = estado.tip; if (!tip) return;
        if (!t) { tip.hidden = true; return; }
        const [a, b] = t.dataset.tip.split('|');
        tip.textContent = '';
        const ti = document.createElement('b'); ti.textContent = a; tip.appendChild(ti);
        if (b) { const s = document.createElement('small'); s.textContent = b; tip.appendChild(s); }
        tip.hidden = false;
        const r = t.getBoundingClientRect();
        const lado = !!t.closest('.dmz-herr');
        let x = lado ? r.right + 8 : r.left, y = lado ? r.top : r.bottom + 6;
        x = Math.min(x, innerWidth - 240); y = Math.min(y, innerHeight - 60);
        tip.style.left = x + 'px'; tip.style.top = y + 'px';
    }

    // ---------- teclado ----------
    function alTecla(ev) {
        if (!estado.raiz || !document.body.contains(estado.raiz)) return;
        const enCampo = ev.target.closest && ev.target.closest('input,textarea,select,[contenteditable]');
        const k = ev.key.toLowerCase();
        if ((ev.ctrlKey || ev.metaKey) && k === 's') { ev.preventDefault(); estado.ref.invokeMethodAsync('Tecla', 'guardar'); return; }
        if (enCampo) return;
        if ((ev.ctrlKey || ev.metaKey) && k === 'z') { ev.preventDefault(); estado.ref.invokeMethodAsync('Tecla', ev.shiftKey ? 'rehacer' : 'deshacer'); }
        else if ((ev.ctrlKey || ev.metaKey) && k === 'y') { ev.preventDefault(); estado.ref.invokeMethodAsync('Tecla', 'rehacer'); }
        else if ((ev.ctrlKey || ev.metaKey) && k === 'd') { ev.preventDefault(); estado.ref.invokeMethodAsync('Tecla', 'duplicar'); }
        else if (k === 'delete') { estado.ref.invokeMethodAsync('Tecla', 'quitar'); }
        else if (k === 'escape') { estado.ref.invokeMethodAsync('Seleccionar', null); }
        else if (k.startsWith('arrow') && estado.raiz.querySelector('.dmz-ctl.sel')) {
            ev.preventDefault();
            const d = { arrowleft: [-1, 0], arrowright: [1, 0], arrowup: [0, -1], arrowdown: [0, 1] }[k];
            estado.ref.invokeMethodAsync('Tecla', (ev.shiftKey ? 'crecer:' : 'mover:') + d[0] + ',' + d[1]);
        }
    }

    window.sigDisMod = {
        iniciar(raiz, ref) {
            estado.raiz = raiz; estado.ref = ref;
            if (!estado.tip) {
                estado.tip = document.createElement('div'); estado.tip.className = 'dmz-tip'; estado.tip.hidden = true;
                document.body.appendChild(estado.tip);
                document.addEventListener('pointerover', alSobre);
                document.addEventListener('pointerdown', () => { if (estado.tip) estado.tip.hidden = true; }, true);
                document.addEventListener('keydown', alTecla);
            }
            if (!raiz.__dmz) {
                raiz.__dmz = true;
                raiz.addEventListener('pointerdown', alPresionar);
                raiz.addEventListener('pointerdown', alPresionarPanel);
                raiz.addEventListener('dragstart', alArrastrarHerramienta);
                raiz.addEventListener('dragover', alSobreLienzo);
                raiz.addEventListener('drop', alSoltarEnLienzo);
            }
            aplicarPaneles();
        },
        aplicar() { aplicarPaneles(); },
        leerPaneles() { return JSON.stringify(leer()); },
        // Estado fijo/suelto, plegado y columnas de la barra de herramientas (la posición la guarda este archivo).
        guardarPanel(id, json) { const p = leer(); p[id] = Object.assign(p[id] || {}, JSON.parse(json)); escribir(p); },
        detener() { estado.raiz = null; estado.ref = null; },
    };

    // ---------- firma a mano ----------
    window.sigFirma = {
        iniciar(canvas) {
            if (!canvas || canvas.__firma) return;
            canvas.__firma = true;
            const ctx = canvas.getContext('2d');
            const ajustar = () => {
                const r = canvas.getBoundingClientRect(), dpr = window.devicePixelRatio || 1;
                const img = canvas.__trazos ? null : null;
                canvas.width = Math.max(1, r.width * dpr); canvas.height = Math.max(1, r.height * dpr);
                ctx.setTransform(dpr, 0, 0, dpr, 0, 0);
                ctx.lineWidth = 2; ctx.lineCap = 'round'; ctx.lineJoin = 'round';
                ctx.strokeStyle = getComputedStyle(canvas).color || '#1f2a3a';
                void img;
            };
            ajustar();
            let dibujando = false, ultimo = null;
            const punto = e => { const r = canvas.getBoundingClientRect(); return { x: e.clientX - r.left, y: e.clientY - r.top }; };
            canvas.addEventListener('pointerdown', e => { dibujando = true; ultimo = punto(e); canvas.setPointerCapture(e.pointerId); canvas.__vacio = false; });
            canvas.addEventListener('pointermove', e => {
                if (!dibujando) return;
                const p = punto(e);
                ctx.beginPath(); ctx.moveTo(ultimo.x, ultimo.y); ctx.lineTo(p.x, p.y); ctx.stroke(); ultimo = p;
            });
            const fin = () => { dibujando = false; };
            canvas.addEventListener('pointerup', fin); canvas.addEventListener('pointercancel', fin);
            canvas.__vacio = true;
        },
        limpiar(canvas) {
            if (!canvas) return;
            const ctx = canvas.getContext('2d');
            ctx.save(); ctx.setTransform(1, 0, 0, 1, 0, 0); ctx.clearRect(0, 0, canvas.width, canvas.height); ctx.restore();
            canvas.__vacio = true;
        },
        // PNG en base64 (sin el prefijo data:), o "" si no se dibujó nada.
        datos(canvas) {
            if (!canvas || canvas.__vacio) return '';
            return canvas.toDataURL('image/png').split(',')[1] || '';
        },
    };
})();
