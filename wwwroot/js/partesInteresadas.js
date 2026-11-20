// Partes interesadas (Components/Pages/PartesInteresadas.razor): arrastrar los puntos del mapa
// influencia × impacto cuando la versión es un borrador, e imprimir la vista actual (PDF).
// El SVG trae su escala en data-*: x0, x1 (influencia 0 → 10) e y0, y1 (impacto 0 → 10).
window.sigPartes = (function () {
    function escala(svg) {
        const d = svg.dataset;
        return { x0: +d.x0, x1: +d.x1, y0: +d.y0, y1: +d.y1, max: +(d.max || 10) };
    }

    function aSvg(svg, ev) {
        const p = svg.createSVGPoint();
        p.x = ev.clientX; p.y = ev.clientY;
        const m = svg.getScreenCTM();
        return m ? p.matrixTransform(m.inverse()) : { x: 0, y: 0 };
    }

    function redondear(v, max) { return Math.min(max, Math.max(0, Math.round(v * 2) / 2)); }

    // Se llama tras cada render del mapa: engancha una sola vez (delegación en el SVG).
    function preparar(svg, net, editable) {
        if (!svg) return;
        svg._sigNet = net;
        svg._sigEditable = !!editable;
        svg.classList.toggle('pi-editable', !!editable);
        if (svg._sigListo) return;
        svg._sigListo = true;

        let arr = null;
        svg.addEventListener('pointerdown', ev => {
            if (!svg._sigEditable || ev.button !== 0) return;
            const g = ev.target.closest('g.pi-punto[data-id]');
            if (!g) return;
            const e = escala(svg), p = aSvg(svg, ev);
            arr = { g, id: +g.dataset.id, x: p.x, y: p.y, cx: +g.dataset.cx, cy: +g.dataset.cy, movio: false, e };
            g.setPointerCapture(ev.pointerId);
        });
        svg.addEventListener('pointermove', ev => {
            if (!arr) return;
            const p = aSvg(svg, ev);
            const dx = p.x - arr.x, dy = p.y - arr.y;
            if (!arr.movio && Math.hypot(dx, dy) < 4) return;
            arr.movio = true;
            const e = arr.e;
            const nx = Math.min(e.x1, Math.max(e.x0, arr.cx + dx));
            const ny = Math.min(e.y0, Math.max(e.y1, arr.cy + dy));
            const inf = redondear((nx - e.x0) / (e.x1 - e.x0) * e.max, e.max);
            const imp = redondear((e.y0 - ny) / (e.y0 - e.y1) * e.max, e.max);
            arr.inf = inf; arr.imp = imp;
            arr.g.setAttribute('transform', 'translate(' + (nx - arr.cx) + ' ' + (ny - arr.cy) + ')');
            arr.g.classList.add('pi-arrastrando');
            const t = svg.querySelector('.pi-guia');
            if (t) { t.textContent = 'INF ' + inf.toLocaleString('es-CO') + ' · IMP ' + imp.toLocaleString('es-CO'); t.setAttribute('x', nx + 14); t.setAttribute('y', ny - 12); t.style.display = ''; }
        });
        const soltar = ev => {
            if (!arr) return;
            const a = arr; arr = null;
            const t = svg.querySelector('.pi-guia');
            if (t) t.style.display = 'none';
            if (!a.movio) return;
            a.g.classList.remove('pi-arrastrando');
            svg._sigSuprimirClic = true;
            setTimeout(() => { svg._sigSuprimirClic = false; }, 0);
            // Blazor no conoce el transform: se quita al volver (el punto ya se dibuja en su nueva posición).
            const limpiar = () => a.g.removeAttribute('transform');
            if (svg._sigNet) svg._sigNet.invokeMethodAsync('Mover', a.id, a.imp, a.inf).then(limpiar, limpiar); else limpiar();
        };
        svg.addEventListener('pointerup', soltar);
        svg.addEventListener('pointercancel', soltar);
        // Un arrastre no es un clic (Blazor escucha en el documento: se corta aquí).
        svg.addEventListener('click', ev => { if (svg._sigSuprimirClic) { ev.stopPropagation(); ev.preventDefault(); } }, true);
    }

    function imprimir(selector, titulo) {
        const el = document.querySelector(selector);
        if (!el) return;
        const estilos = Array.from(document.querySelectorAll('link[rel="stylesheet"]')).map(x => '<link rel="stylesheet" href="' + x.href + '">').join('');
        const w = window.open('', '_blank');
        if (!w) return;
        const copia = el.cloneNode(true);
        copia.querySelectorAll('.pi-no-imprimir').forEach(x => x.remove());
        w.document.write('<!doctype html><html lang="es-CO"><head><meta charset="utf-8"><title></title>' + estilos +
            '<style>@page{size:A4 landscape;margin:10mm}html,body{background:#fff!important}body{margin:0;padding:8px}.pi-cuerpo{height:auto!important}.tabla-scroll{max-height:none!important;overflow:visible!important}</style></head><body class="pi-impresion"><h5></h5>' +
            copia.outerHTML + '</body></html>');
        w.document.close();
        w.document.title = titulo || 'Partes interesadas';
        w.document.querySelector('h5').textContent = titulo || '';
        const listo = () => setTimeout(() => { w.focus(); w.print(); }, 300);
        if (w.document.readyState === 'complete') setTimeout(listo, 300); else w.addEventListener('load', listo);
    }

    return { preparar, imprimir };
})();
