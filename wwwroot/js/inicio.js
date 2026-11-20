// Panel de inicio con widgets (Home.razor): rejilla de 12 columnas. Blazor pinta los widgets (.pw-w con data-w, tamaño
// por defecto data-ancho/data-alto y mínimo data-minw/data-minh); aquí se guardan posición y tamaño de cada uno
// (localStorage "sigweb.inicio.panel": [{id,x,y,w,h}]), se resuelven los choques (gravedad hacia arriba: los demás
// se apartan y vuelven a encajar) y, en modo edición, se arrastra el widget entero y se cambia el tamaño con el
// tirador de la esquina. En pantallas angostas (≤ 1000 px) los widgets van uno debajo de otro, sin edición.
(function () {
    function crear(clave, opciones) {
    var posicion = !!(opciones && opciones.posicion);
    // Opciones del diseñador de formularios: guardar(layout) en vez de localStorage, alto de fila y ancho mínimo
    // para apilar (0 = nunca apila).
    var alGuardar = opciones && typeof opciones.guardar === 'function' ? opciones.guardar : null;
    var CLAVE = clave, COLS = 12, FILA = (opciones && opciones.fila) || 46, GAP = (opciones && opciones.gap) || 10,
        ANGOSTA = opciones && opciones.angosta !== undefined ? opciones.angosta : 1000,
        EXTRA = opciones && opciones.extra !== undefined ? opciones.extra : 3;   // filas libres al final en edición
    var layout = [], panel = null, editando = false, obs = null, sombra = null, celdas = null, gesto = null;

    function guardar() {
        if (alGuardar) { alGuardar(layout.map(function (p) { return { id: p.id, x: p.x, y: p.y, w: p.w, h: p.h }; })); return; }
        try { localStorage.setItem(CLAVE, JSON.stringify(layout)); } catch (e) { }
    }
    function leer() { if (alGuardar) return null; try { return localStorage.getItem(CLAVE); } catch (e) { return null; } }
    function angosta() { return ANGOSTA > 0 && window.matchMedia('(max-width: ' + ANGOSTA + 'px)').matches; }
    function colAncho() { return (panel.clientWidth - GAP * (COLS - 1)) / COLS; }
    function px(it) {
        var c = colAncho();
        return { left: it.x * (c + GAP), top: it.y * (FILA + GAP), width: it.w * c + (it.w - 1) * GAP, height: it.h * FILA + (it.h - 1) * GAP };
    }
    function ubicar(el, left, top) {
        if (posicion) { el.style.transform = ''; el.style.left = left + 'px'; el.style.top = top + 'px'; }
        else el.style.transform = 'translate(' + left + 'px,' + top + 'px)';
    }
    function choca(a, b) { return a.x < b.x + b.w && b.x < a.x + a.w && a.y < b.y + b.h && b.y < a.y + a.h; }

    // Gravedad: en orden (y, x) cada widget sube hasta el primer hueco; el que se arrastra/estira (fijo) se ubica primero
    // en su fila, así los demás le ceden el lugar.
    function compactar(fijo) {
        var orden = layout.toSorted(function (a, b) {
            return a.y - b.y || (a === fijo ? -1 : b === fijo ? 1 : a.x - b.x);
        });
        var puestos = [];
        orden.forEach(function (it) {
            it.x = Math.max(0, Math.min(it.x, COLS - it.w));
            var y = it === fijo ? it.y : 0;
            if (it === fijo) { while (puestos.some(function (p) { return choca(p, it); })) { it.y++; } }
            else {
                it.y = y;
                while (puestos.some(function (p) { return choca(p, it); })) it.y++;
            }
            puestos.push(it);
        });
        // El fijo también sube si quedó con hueco encima y nada lo impide.
        if (fijo) {
            while (fijo.y > 0) {
                fijo.y--;
                if (layout.some(function (p) { return p !== fijo && choca(p, fijo); })) { fijo.y++; break; }
            }
        }
    }

    // Primer hueco donde cabe un widget de w × h (de arriba abajo, de izquierda a derecha).
    function hueco(w, h) {
        for (var y = 0; y < 500; y++)
            for (var x = 0; x <= COLS - w; x++) {
                var c = { x: x, y: y, w: w, h: h };
                if (!layout.some(function (p) { return choca(p, c); })) return c;
            }
        return { x: 0, y: 0, w: w, h: h };
    }

    function elementos() { return panel ? Array.prototype.slice.call(panel.querySelectorAll(':scope > .pw-w')) : []; }
    function itemDe(el) { var id = el.getAttribute('data-w'); return layout.find(function (p) { return p.id === id; }); }
    function num(el, a, d) { var v = parseInt(el.getAttribute(a), 10); return isNaN(v) ? d : v; }

    function pintar() {
        if (!panel) return;
        var estrecha = angosta();
        panel.classList.toggle('pw-apilado', estrecha);
        var alto = 0;
        elementos().forEach(function (el) {
            var it = itemDe(el);
            if (!it) return;
            if (gesto && gesto.el === el) return;
            if (estrecha) {
                el.style.transform = ''; el.style.left = ''; el.style.top = ''; el.style.width = ''; el.style.height = (it.h * FILA + (it.h - 1) * GAP) + 'px';
                el.style.order = String(it.y * COLS + it.x);
                return;
            }
            var p = px(it);
            el.style.order = '';
            el.style.width = p.width + 'px'; el.style.height = p.height + 'px';
            ubicar(el, p.left, p.top);
        });
        layout.forEach(function (it) { alto = Math.max(alto, it.y + it.h); });
        if (!estrecha) {
            var filas = alto + (editando ? EXTRA : 0);
            panel.style.height = Math.max(filas * (FILA + GAP) - GAP, editando ? (opciones && opciones.minAlto !== undefined ? opciones.minAlto : 300) : 0) + 'px';
            pintarCeldas(filas);
        } else { panel.style.height = ''; pintarCeldas(0); }
    }

    // Rejilla de casillas de fondo (solo en edición).
    function pintarCeldas(filas) {
        if (!editando || angosta()) { if (celdas) { celdas.remove(); celdas = null; } return; }
        if (!celdas) { celdas = document.createElement('div'); celdas.className = 'pw-celdas'; panel.prepend(celdas); }
        var c = colAncho(), html = '';
        for (var y = 0; y < filas; y++)
            for (var x = 0; x < COLS; x++)
                html += '<span style="transform:translate(' + (x * (c + GAP)) + 'px,' + (y * (FILA + GAP)) + 'px);width:' + c + 'px;height:' + FILA + 'px"></span>';
        celdas.innerHTML = html;
    }

    // ---------- Arrastrar y cambiar tamaño ----------
    function inicio(ev) {
        if (!editando || angosta() || ev.button !== 0) return;
        var el = ev.target.closest('.pw-w');
        if (!el || !panel.contains(el) || ev.target.closest('.pw-quitar')) return;
        var it = itemDe(el);
        if (!it) return;
        ev.preventDefault();
        var p = px(it), caja = panel.getBoundingClientRect();
        gesto = {
            el: el, it: it, tamano: !!ev.target.closest('.pw-tirador'),
            dx: ev.clientX - caja.left - p.left, dy: ev.clientY - caja.top - p.top,
            x0: ev.clientX, y0: ev.clientY, w0: it.w, h0: it.h, p0: p,
            minw: num(el, 'data-minw', 1), minh: num(el, 'data-minh', 1)
        };
        el.classList.add(gesto.tamano ? 'pw-estirando' : 'pw-moviendo');
        if (!sombra) { sombra = document.createElement('div'); sombra.className = 'pw-sombra'; }
        panel.appendChild(sombra);
        moverSombra();
        document.addEventListener('pointermove', mover);
        document.addEventListener('pointerup', fin, { once: true });
    }

    function moverSombra() {
        var p = px(gesto.it);
        sombra.style.width = p.width + 'px'; sombra.style.height = p.height + 'px';
        sombra.style.transform = 'translate(' + p.left + 'px,' + p.top + 'px)';
    }

    function mover(ev) {
        if (!gesto) return;
        var g = gesto, c = colAncho(), caja = panel.getBoundingClientRect(), cambio = false;
        if (g.tamano) {
            var ancho = Math.max(40, g.p0.width + ev.clientX - g.x0), altoPx = Math.max(40, g.p0.height + ev.clientY - g.y0);
            g.el.style.width = ancho + 'px'; g.el.style.height = altoPx + 'px';
            var w = Math.max(g.minw, Math.min(COLS - g.it.x, Math.round((ancho + GAP) / (c + GAP))));
            var h = Math.max(g.minh, Math.round((altoPx + GAP) / (FILA + GAP)));
            if (w !== g.it.w || h !== g.it.h) { g.it.w = w; g.it.h = h; cambio = true; }
        } else {
            var left = ev.clientX - caja.left - g.dx, top = ev.clientY - caja.top - g.dy;
            ubicar(g.el, left, top);
            var x = Math.max(0, Math.min(COLS - g.it.w, Math.round(left / (c + GAP))));
            var y = Math.max(0, Math.round(top / (FILA + GAP)));
            if (x !== g.it.x || y !== g.it.y) { g.it.x = x; g.it.y = y; cambio = true; }
        }
        if (cambio) { compactar(g.it); moverSombra(); pintar(); }
    }

    function fin(ev) {
        document.removeEventListener('pointermove', mover);
        if (!gesto) return;
        var el = gesto.el;
        // Diseñador de formularios: soltar el campo sobre OTRA rejilla (otro paso) = moverlo allá; aquí no cambia nada.
        if (opciones && typeof opciones.alSoltarFuera === 'function' && ev && !gesto.tamano) {
            // El campo arrastrado está bajo el puntero: se ignora para ver qué hay debajo de él.
            var pe = el.style.pointerEvents; el.style.pointerEvents = 'none';
            var bajo = document.elementFromPoint(ev.clientX, ev.clientY);
            el.style.pointerEvents = pe;
            var otro = bajo && bajo.closest ? bajo.closest('[data-rejilla-paso]') : null;
            if (otro && otro !== panel && !panel.contains(otro)) {
                var c = colAncho(), caja = otro.getBoundingClientRect();
                var x = Math.max(0, Math.min(COLS - gesto.it.w, Math.round((ev.clientX - caja.left - gesto.dx) / (c + GAP))));
                var y = Math.max(0, Math.round((ev.clientY - caja.top - gesto.dy) / (FILA + GAP)));
                var id = gesto.it.id;
                el.classList.remove('pw-moviendo', 'pw-estirando');
                gesto = null;
                if (sombra) sombra.remove();
                pintar();
                opciones.alSoltarFuera(id, otro.getAttribute('data-rejilla-paso'), x, y);
                return;
            }
        }
        el.classList.remove('pw-moviendo', 'pw-estirando');
        compactar(gesto.it);
        gesto = null;
        if (sombra) sombra.remove();
        pintar();
        guardar();
    }

    // ---------- API para Blazor ----------
    var api = {
        leer: leer,
        // Favoritos y recientes del buscador (js/navegacion.js) para el widget de accesos rápidos.
        accesos: function () {
            function l(k) { try { return JSON.parse(localStorage.getItem(k) || '[]'); } catch (e) { return []; } }
            return JSON.stringify({ fav: l('sigweb.nav.favoritos'), rec: l('sigweb.nav.recientes').filter(function (r) { return (r.url || '').split('?')[0] !== ''; }) });
        },
        // Reemplaza la disposición (restaurar o primera vez).
        cargar: function (json) {
            try { layout = JSON.parse(json) || []; } catch (e) { layout = []; }
            if (!alGuardar) guardar();
        },
        // Tras cada render: sincroniza la disposición con los widgets pintados y los ubica.
        aplicar: function (el, edicion) {
            if (!el) return;
            if (!layout.length) { try { layout = JSON.parse(leer() || '[]') || []; } catch (e) { layout = []; } }
            if (panel !== el) {
                panel = el;
                panel.addEventListener('pointerdown', inicio);
                if (obs) obs.disconnect();
                if (window.ResizeObserver) { obs = new ResizeObserver(function () { pintar(); }); obs.observe(panel); }
            }
            editando = !!edicion;
            var presentes = {};
            elementos().forEach(function (e) {
                var id = e.getAttribute('data-w');
                presentes[id] = true;
                if (!itemDe(e)) {
                    var w = Math.min(COLS, num(e, 'data-ancho', 3)), h = num(e, 'data-alto', 3);
                    var c = hueco(w, h);
                    layout.push({ id: id, x: c.x, y: c.y, w: w, h: h });
                }
            });
            layout = layout.filter(function (p) { return presentes[p.id]; });
            layout.forEach(function (p) {
                var e = panel.querySelector(':scope > .pw-w[data-w="' + CSS.escape(p.id) + '"]');
                if (e) { p.w = Math.max(num(e, 'data-minw', 1), Math.min(COLS, p.w)); p.h = Math.max(num(e, 'data-minh', 1), p.h); }
            });
            var antes = JSON.stringify(layout);
            compactar(null);
            pintar();
            if (!alGuardar || JSON.stringify(layout) !== antes) guardar();
            requestAnimationFrame(function () { panel && panel.classList.add('pw-listo'); });
        }
    };
    function redimensionar() { requestAnimationFrame(pintar); }
    window.addEventListener('resize', redimensionar);
    api.destruir = function () {
        if (obs) obs.disconnect();
        if (panel && typeof panel.removeEventListener === 'function') {
            panel.removeEventListener('pointerdown', inicio);
            elementos().forEach(function (el) { el.style.transform = ''; el.style.left = ''; el.style.top = ''; el.style.width = ''; el.style.height = ''; el.style.order = ''; });
        }
        document.removeEventListener('pointermove', mover);
        document.removeEventListener('pointerup', fin);
        window.removeEventListener('resize', redimensionar);
        panel = null;
    };
    return api;
    }
    window.sigPanel = crear('sigweb.inicio.panel');
    window.sigPanel.crear = crear;
    var hojas = new Map();
    window.sigHojaPanel = {
        aplicar: function (el, edicion, clave) {
            if (!el) return;
            clave = clave || 'sigweb.hojavida.panel';
            var entrada = hojas.get(el);
            if (entrada && entrada.clave !== clave) { entrada.api.destruir(); hojas.delete(el); entrada = null; }
            if (!entrada) { entrada = { clave: clave, api: crear(clave, { posicion: true }) }; hojas.set(el, entrada); }
            var api = entrada.api;
            el.querySelectorAll(':scope > .ficha-bloque').forEach(function (w) {
                w.classList.add('pw-w');
                if (!w.dataset.w) w.dataset.w = 'base-' + (w.querySelector('.ficha-bloque-titulo')?.textContent || '').trim().replace(/\s*\(\d+\)/g, '');
                w.dataset.ancho = w.dataset.ancho || '6'; w.dataset.alto = w.dataset.alto || '5';
                w.dataset.minw = '3'; w.dataset.minh = '3';
                if (!w.querySelector(':scope > .pw-tirador')) { var t = document.createElement('span'); t.className = 'pw-tirador'; t.title = 'Tamaño'; w.appendChild(t); }
                w.querySelector(':scope > .pw-tirador').style.display = edicion ? '' : 'none';
            });
            api.aplicar(el, edicion);
        },
        restaurar: function (el) {
            var entrada = hojas.get(el);
            if (entrada) { entrada.api.cargar('[]'); entrada.api.aplicar(el, true); }
        },
        destruir: function (el) { var entrada = hojas.get(el); if (entrada) entrada.api.destruir(); hojas.delete(el); }
    };
})();
