// Tablas configurables de la app (toda tabla con clase "tabla-pin").
//  - Congelar: clic en la chincheta del encabezado (zona derecha) fija o suelta
//    cualquier columna; las fijas pasan al inicio. Por defecto, la primera.
//  - Mover: arrastrar el encabezado a otra posición. Soltarla dentro de la zona
//    fija la congela; sacarla de esa zona la suelta.
//  - Densidad (Amplia / Normal / Compacta) y Restablecer: botón del encabezado
//    oscuro (.pg-tabla-cfg, EncabezadoPagina).
// Las columnas se identifican por el texto de su encabezado (o data-col), así
// el orden se conserva aunque el visor de Campos muestre u oculte columnas.
// Todo va por DOM/atributos (data-fija, data-densidad), sin tocar el estado de
// Blazor; se reaplica tras cada re-render con un MutationObserver. El orden
// "natural" de cada fila se lee del árbol lógico de Blazor, por eso mover las
// celdas no rompe sus actualizaciones.
// Tablas con encabezados agrupados (colspan/rowspan) solo admiten congelar.
(function () {
    var estado = {};          // clave tabla -> { orden: [claves] | null, fijas: [claves] | null }
    var ocupado = false;
    var DENSIDADES = ['amplia', 'normal', 'compacta'];

    // ---------- Almacenamiento ----------
    function leer(k) { try { return localStorage.getItem(k); } catch (e) { return null; } }
    function escribir(k, v) { try { if (v === null) localStorage.removeItem(k); else localStorage.setItem(k, v); } catch (e) { } }

    function clave(t) {
        var tablas = Array.prototype.slice.call(document.querySelectorAll('table.tabla-pin'));
        return location.pathname + '#' + tablas.indexOf(t);
    }
    function config(t, claves) {
        var k = clave(t);
        if (!estado[k]) {
            var c = null;
            try { var g = leer('sigwebCols:' + k); if (g) c = JSON.parse(g); } catch (e) { }
            if (!c) {
                c = { orden: null, fijas: null };
                // Formato anterior: índices de columnas fijas.
                try {
                    var v = leer('sigwebPin:' + k);
                    if (v) { var idx = JSON.parse(v); if (Array.isArray(idx)) c.fijas = idx.filter(function (i) { return i < claves.length; }).map(function (i) { return claves[i]; }); }
                } catch (e) { }
            }
            estado[k] = c;
        }
        if (!estado[k].fijas) estado[k].fijas = claves.length ? [claves[0]] : [];
        return estado[k];
    }
    function guardar(t) { var k = clave(t); escribir('sigwebCols:' + k, JSON.stringify(estado[k])); escribir('sigwebPin:' + k, null); }

    function densidad() { var d = leer('sigwebDensidad'); return DENSIDADES.indexOf(d) >= 0 ? d : 'normal'; }

    // ---------- Orden natural (árbol lógico de Blazor) ----------
    function hijosLogicos(n) {
        var s = Object.getOwnPropertySymbols ? Object.getOwnPropertySymbols(n) : [];
        for (var i = 0; i < s.length; i++) { var v = n[s[i]]; if (Array.isArray(v)) return v; }
        return null;
    }
    function celdasNaturales(tr) {
        var arr = hijosLogicos(tr), out = [];
        if (arr) {
            (function rec(a, prof) {
                for (var i = 0; i < a.length; i++) {
                    var c = a[i];
                    if (!c) continue;
                    if (c.nodeType === 1) { if (c.parentNode === tr) out.push(c); }
                    else if (c.nodeType === 8 && prof < 6) { var sub = hijosLogicos(c); if (sub) rec(sub, prof + 1); }
                }
            })(arr, 0);
            if (out.length === tr.children.length) return out;
        }
        // Sin árbol lógico: orden recordado antes de mover, si sigue siendo válido.
        var act = Array.prototype.slice.call(tr.children);
        var cache = tr.__swNat;
        if (cache && cache.length === act.length && cache.every(function (c) { return c.parentNode === tr; })) return cache;
        if (!tr.__swMovida) { tr.__swNat = act; return act; }
        return null;
    }

    function claveTh(th) {
        var d = th.getAttribute('data-col');
        if (d) return 'c:' + d;
        var txt = (th.textContent || '').replace(/[^\p{L}\p{N}]+/gu, ' ').trim().toLowerCase();
        return txt;
    }
    function clavesDe(ths) {
        var vistas = {};
        return ths.map(function (th) {
            var k = claveTh(th) || '∅';
            vistas[k] = (vistas[k] || 0) + 1;
            return vistas[k] > 1 ? k + '·' + vistas[k] : k;
        });
    }
    function esRegular(t, nat) {
        for (var i = 0; i < nat.length; i++) if (nat[i].colSpan > 1 || nat[i].rowSpan > 1) return false;
        if (t.tHead) for (var r = 1; r < t.tHead.rows.length; r++) { var cs = t.tHead.rows[r].cells; if (cs.length && cs.length !== nat.length) return false; }
        if (t.querySelector('tbody > tr > [rowspan]:not([rowspan="1"])')) return false;
        return true;
    }

    // Orden visual: el guardado (claves que existan) + columnas nuevas junto a su vecina natural; fijas primero.
    function ordenVisual(claves, cfg) {
        var res = (cfg.orden || []).filter(function (k) { return claves.indexOf(k) >= 0; });
        claves.forEach(function (k, i) {
            if (res.indexOf(k) >= 0) return;
            var pos = 0;
            for (var j = i - 1; j >= 0; j--) { var p = res.indexOf(claves[j]); if (p >= 0) { pos = p + 1; break; } }
            res.splice(pos, 0, k);
        });
        var fij = res.filter(function (k) { return cfg.fijas.indexOf(k) >= 0; });
        var resto = res.filter(function (k) { return cfg.fijas.indexOf(k) < 0; });
        return { claves: fij.concat(resto), nFijas: fij.length };
    }

    // ---------- Aplicar orden, congelado y densidad ----------
    function aplicar(t) {
        var trCab = t.tHead && t.tHead.rows[0];
        if (!trCab) return;
        var d = densidad();
        if (t.getAttribute('data-densidad') !== d) t.setAttribute('data-densidad', d);
        var nat = celdasNaturales(trCab);
        if (!nat || !nat.length) return;
        var n = nat.length;
        var claves = clavesDe(nat);
        nat.forEach(function (th, i) { th.__swClave = claves[i]; });
        var cfg = config(t, claves);
        var regular = esRegular(t, nat);
        t.__swRegular = regular;
        if (regular) { if (!t.hasAttribute('data-sw-mover')) t.setAttribute('data-sw-mover', ''); }
        else if (t.hasAttribute('data-sw-mover')) t.removeAttribute('data-sw-mover');

        var filas = t.querySelectorAll('tr');
        var posFijas = {};
        if (regular) {
            var ov = ordenVisual(claves, cfg);
            var perm = ov.claves.map(function (k) { return claves.indexOf(k); });
            var identidad = perm.every(function (p, i) { return p === i; });
            if (!identidad || t.__swMovida) {
                var foco = document.activeElement, selIni = null, selFin = null;
                try { selIni = foco && foco.selectionStart; selFin = foco && foco.selectionEnd; } catch (e) { }
                var movioFoco = false;
                filas.forEach(function (tr) {
                    if (tr.children.length !== n) return;
                    var cel = celdasNaturales(tr);
                    if (!cel) return;
                    var ok = true;
                    for (var i = 0; i < n; i++) if (tr.children[i] !== cel[perm[i]]) { ok = false; break; }
                    if (ok) return;
                    if (foco && tr.contains(foco)) movioFoco = true;
                    for (var j = 0; j < n; j++) tr.appendChild(cel[perm[j]]);
                    tr.__swMovida = true;
                    t.__swMovida = true;
                });
                if (movioFoco && foco && document.activeElement !== foco) {
                    try { foco.focus({ preventScroll: true }); if (selIni !== null) foco.setSelectionRange(selIni, selFin); } catch (e) { }
                }
            }
            for (var f = 0; f < ov.nFijas; f++) posFijas[f] = true;
        } else {
            cfg.fijas.forEach(function (k) { var i = claves.indexOf(k); if (i >= 0) posFijas[i] = true; });
        }

        // Congelado: "left" acumulado con el ancho real de cada columna fija, en orden visual.
        var ths = trCab.children;
        var left = 0, lefts = {}, fin = -1;
        for (var c = 0; c < ths.length; c++) if (posFijas[c]) { lefts[c] = left; left += ths[c].getBoundingClientRect().width; fin = c; }
        filas.forEach(function (tr) {
            var celdas = tr.children;
            if (celdas.length !== n) return;
            for (var i = 0; i < celdas.length; i++) {
                var x = celdas[i];
                if (lefts.hasOwnProperty(i)) {
                    if (x.getAttribute('data-fija') !== '1') x.setAttribute('data-fija', '1');
                    if (x.style.left !== lefts[i] + 'px') x.style.left = lefts[i] + 'px';
                } else if (x.hasAttribute('data-fija')) {
                    x.removeAttribute('data-fija'); x.style.left = '';
                }
                if (i === fin) { if (!x.hasAttribute('data-fija-fin')) x.setAttribute('data-fija-fin', ''); }
                else if (x.hasAttribute('data-fija-fin')) x.removeAttribute('data-fija-fin');
            }
        });
    }
    function aplicarTodas() {
        if (ocupado) return;
        ocupado = true;
        try { document.querySelectorAll('table.tabla-pin').forEach(aplicar); } finally { setTimeout(function () { ocupado = false; }, 0); }
    }

    var temporizador = null;
    new MutationObserver(function () {
        clearTimeout(temporizador);
        temporizador = setTimeout(aplicarTodas, 60);
    }).observe(document.documentElement, { childList: true, subtree: true });
    setTimeout(aplicarTodas, 100);
    window.addEventListener('resize', function () { clearTimeout(temporizador); temporizador = setTimeout(aplicarTodas, 100); });

    function marcarColumna(t, k) {
        var trCab = t.tHead && t.tHead.rows[0];
        if (!trCab) return;
        var idx = -1;
        for (var i = 0; i < trCab.children.length; i++) if (trCab.children[i].__swClave === k) idx = i;
        if (idx < 0) return;
        var n = trCab.children.length, marcadas = [];
        t.querySelectorAll('tr').forEach(function (tr) { if (tr.children.length === n) { var c = tr.children[idx]; c.setAttribute('data-sw-movida', ''); marcadas.push(c); } });
        setTimeout(function () { marcadas.forEach(function (c) { c.removeAttribute('data-sw-movida'); }); }, 700);
    }

    // ---------- Chincheta: congelar / soltar ----------
    function enChincheta(th, x) { return x >= th.getBoundingClientRect().right - 26; }
    document.addEventListener('click', function (e) {
        if (suprimirClic) { suprimirClic = false; e.stopImmediatePropagation(); e.preventDefault(); return; }
        var th = e.target.closest && e.target.closest('table.tabla-pin thead th');
        if (!th || !enChincheta(th, e.clientX)) return;
        var t = th.closest('table');
        aplicar(t);
        var k = th.__swClave;
        if (k === undefined) return;
        var cfg = estado[clave(t)];
        var pos = cfg.fijas.indexOf(k);
        if (pos >= 0) cfg.fijas.splice(pos, 1); else cfg.fijas.push(k);
        guardar(t);
        aplicar(t);
        if (t.__swRegular) marcarColumna(t, k);
        e.stopImmediatePropagation();
        e.preventDefault();
    }, true);

    // ---------- Arrastrar encabezados para mover columnas ----------
    var arr = null, suprimirClic = false;
    function marcoDe(t) { return t.closest('.tabla-scroll, .ens-tabla-marco, .tt-scroll, .dat-scroll') || t.parentElement; }

    document.addEventListener('pointerdown', function (e) {
        if (e.button !== 0 || e.pointerType === 'touch') return;
        var th = e.target.closest && e.target.closest('table.tabla-pin > thead > tr:first-child > th');
        if (!th) return;
        var t = th.closest('table');
        if (!t.__swRegular || th.parentNode !== t.tHead.rows[0]) return;
        if (enChincheta(th, e.clientX)) return;
        if (e.target.closest('input, select, textarea, button, a, label, [contenteditable]')) return;
        var redim = e.target.closest('.th-redim');
        if (redim) { var rr = redim.getBoundingClientRect(); if (e.clientX > rr.right - 18 && e.clientY > rr.bottom - 18) return; }
        arr = { t: t, th: th, x0: e.clientX, y0: e.clientY, iniciado: false };
    }, true);

    function iniciarArrastre() {
        aplicar(arr.t);
        arr.iniciado = true;
        arr.clave = arr.th.__swClave;
        var g = document.createElement('div');
        g.className = 'sw-col-fantasma';
        g.textContent = (arr.th.textContent || '').trim() || '·';
        document.body.appendChild(g);
        var ind = document.createElement('div');
        ind.className = 'sw-col-indicador';
        document.body.appendChild(ind);
        arr.fantasma = g; arr.indicador = ind;
        arr.th.setAttribute('data-sw-origen', '');
        document.documentElement.classList.add('sw-arrastrando-col');
        try { window.getSelection().removeAllRanges(); } catch (e) { }
        arr.giro = setInterval(function () {
            if (!arr || !arr.iniciado) return;
            var m = marcoDe(arr.t), r = m.getBoundingClientRect();
            if (arr.x < r.left + 40) m.scrollLeft -= 14;
            else if (arr.x > r.right - 40) m.scrollLeft += 14;
            else return;
            ubicar();
        }, 30);
    }

    function ubicar() {
        var trCab = arr.t.tHead.rows[0], ths = trCab.children, x = arr.x;
        var origen = Array.prototype.indexOf.call(ths, arr.th);
        var dest = 0;
        for (var i = 0; i < ths.length; i++) {
            if (i === origen) continue;
            var r = ths[i].getBoundingClientRect();
            if (x > r.left + r.width / 2) dest++;
        }
        arr.dest = dest;
        // Posición del indicador: borde entre columnas (sin contar la de origen).
        var otros = Array.prototype.filter.call(ths, function (th, i) { return i !== origen; });
        var xl;
        if (!otros.length) xl = arr.th.getBoundingClientRect().left;
        else if (dest >= otros.length) xl = otros[otros.length - 1].getBoundingClientRect().right;
        else xl = otros[dest].getBoundingClientRect().left;
        var m = marcoDe(arr.t).getBoundingClientRect(), tr = arr.t.getBoundingClientRect();
        var top = Math.max(m.top, tr.top), bot = Math.min(m.bottom, tr.bottom);
        xl = Math.min(Math.max(xl, m.left), m.right);
        arr.indicador.style.left = (xl - 1.5) + 'px';
        arr.indicador.style.top = top + 'px';
        arr.indicador.style.height = Math.max(0, bot - top) + 'px';
    }

    document.addEventListener('pointermove', function (e) {
        if (!arr) return;
        if (!arr.iniciado) {
            if (Math.abs(e.clientX - arr.x0) < 6) { if (Math.abs(e.clientY - arr.y0) > 12) arr = null; return; }
            iniciarArrastre();
        }
        e.preventDefault();
        arr.x = e.clientX;
        arr.fantasma.style.left = (e.clientX + 12) + 'px';
        arr.fantasma.style.top = (e.clientY + 10) + 'px';
        ubicar();
    }, true);

    function terminar(soltar) {
        var a = arr;
        arr = null;
        if (!a || !a.iniciado) return;
        clearInterval(a.giro);
        a.fantasma.remove(); a.indicador.remove();
        a.th.removeAttribute('data-sw-origen');
        document.documentElement.classList.remove('sw-arrastrando-col');
        suprimirClic = true;
        setTimeout(function () { suprimirClic = false; }, 0);
        if (!soltar || a.dest === undefined) return;

        var t = a.t, trCab = t.tHead.rows[0];
        var vis = Array.prototype.map.call(trCab.children, function (th) { return th.__swClave; });
        var k = a.clave, cfg = estado[clave(t)];
        var estabaFija = cfg.fijas.indexOf(k) >= 0;
        var sin = vis.filter(function (x) { return x !== k; });
        var nF = sin.filter(function (x) { return cfg.fijas.indexOf(x) >= 0; }).length;
        sin.splice(a.dest, 0, k);
        var fija = estabaFija ? a.dest <= nF : a.dest < nF;
        cfg.orden = sin;
        cfg.fijas = sin.filter(function (x) { return x === k ? fija : cfg.fijas.indexOf(x) >= 0; });
        guardar(t);
        aplicar(t);
        marcarColumna(t, k);
    }
    document.addEventListener('pointerup', function () { terminar(true); }, true);
    document.addEventListener('pointercancel', function () { terminar(false); }, true);
    document.addEventListener('keydown', function (e) { if (e.key === 'Escape' && arr) terminar(false); }, true);

    // ---------- Menú del encabezado: densidad y restablecer ----------
    var ICONOS = {
        amplia: '<path d="M4 4.5h16M4 12h16M4 19.5h16"/>',
        normal: '<path d="M4 6h16M4 10h16M4 14h16M4 18h16"/>',
        compacta: '<path d="M4 5h16M4 8.5h16M4 12h16M4 15.5h16M4 19h16"/>',
        restablecer: '<path d="M3.5 12a8.5 8.5 0 1 0 2.6-6.1"/><path d="M3.5 4v4.5H8"/>'
    };
    var TITULOS = { amplia: 'Amplia', normal: 'Normal', compacta: 'Compacta', restablecer: 'Restablecer columnas' };
    function svg(k) { return '<svg viewBox="0 0 24 24" width="18" height="18" fill="none" stroke="currentColor" stroke-width="1.8" stroke-linecap="round" stroke-linejoin="round" aria-hidden="true">' + ICONOS[k] + '</svg>'; }

    var menu = null;
    function cerrarMenu() { if (menu) { menu.remove(); menu = null; } }
    function abrirMenu(btn) {
        cerrarMenu();
        menu = document.createElement('div');
        menu.className = 'sw-tabla-menu';
        var d = densidad();
        var html = '<div class="sw-tm-grupo" role="group">';
        DENSIDADES.forEach(function (k) {
            html += '<button type="button" class="sw-tm-btn' + (k === d ? ' activo' : '') + '" data-densidad="' + k + '" title="' + TITULOS[k] + '" aria-label="' + TITULOS[k] + '">' + svg(k) + '</button>';
        });
        html += '</div><span class="sw-tm-sep"></span>';
        html += '<button type="button" class="sw-tm-btn" data-accion="restablecer" title="' + TITULOS.restablecer + '" aria-label="' + TITULOS.restablecer + '">' + svg('restablecer') + '</button>';
        menu.innerHTML = html;
        document.body.appendChild(menu);
        var r = btn.getBoundingClientRect(), w = menu.offsetWidth;
        menu.style.top = (r.bottom + 6) + 'px';
        menu.style.left = Math.max(8, Math.min(r.right - w, window.innerWidth - w - 8)) + 'px';
    }
    document.addEventListener('click', function (e) {
        var btn = e.target.closest && e.target.closest('.pg-tabla-cfg');
        if (btn) { if (menu) cerrarMenu(); else abrirMenu(btn); return; }
        if (!menu) return;
        var b = e.target.closest('.sw-tabla-menu .sw-tm-btn');
        if (!b) { if (!e.target.closest('.sw-tabla-menu')) cerrarMenu(); return; }
        if (b.hasAttribute('data-densidad')) {
            escribir('sigwebDensidad', b.getAttribute('data-densidad'));
            menu.querySelectorAll('[data-densidad]').forEach(function (x) { x.classList.toggle('activo', x === b); });
            aplicarTodas();
            setTimeout(aplicarTodas, 30);   // recalcula el "left" de las fijas con el alto/ancho nuevo
        } else if (b.getAttribute('data-accion') === 'restablecer') {
            document.querySelectorAll('table.tabla-pin').forEach(function (t) {
                var k = clave(t);
                delete estado[k];
                escribir('sigwebCols:' + k, null);
                escribir('sigwebPin:' + k, null);
            });
            aplicarTodas();
            cerrarMenu();
        }
    });
    document.addEventListener('keydown', function (e) { if (e.key === 'Escape') cerrarMenu(); });
    window.addEventListener('resize', cerrarMenu);
})();
