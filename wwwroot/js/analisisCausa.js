// Editor de análisis de causa (Planes de acción). La ventana se arma sobre <body> (no la maneja Blazor, así ningún
// ancestro con transform/filter la descoloca y el formulario de fondo conserva lo que no se ha guardado).
//   sigCausas.abrir({ json, titulo }, dotnetRef)  → al guardar llama dotnetRef.Guardar(json, svg, resumen)
//   sigCausas.maximizar(dataUri, titulo)           → visor a pantalla completa (zoom, arrastrar, descargar)
// Un análisis = varias herramientas (pestañas) + conclusión. Se guarda la estructura (JSON) y un SVG que la app
// muestra como imagen (<img>: el SVG nunca se inserta como HTML).
(function () {
    'use strict';

    const FUENTE = "Inter, 'Segoe UI', Arial, sans-serif";

    // ---------------- paletas por tema ----------------
    // El editor dibuja con el tema de la app (claro, oscuro, cristal u otro leído de sus variables --t-*); la imagen que
    // se guarda (informes, Word, miniaturas) usa siempre «documento» (fondo claro, apto para imprimir).
    const BASE_CLARO = {
        rojo: '#EC1C24', rojoSuave: '#fdecec', tinta: '#1f2328', gris: '#6b7280', linea: '#8a929c', borde: '#cfd5dc',
        suave: '#f4f5f7', papel: '#ffffff', papel2: '#f6f7f9', nota: '#fff3b8', nota2: '#ffe789', notaBorde: '#e3cf73', cab: '#3a3f47', cab2: '#252a31',
        verde: '#2e7d32', verdeSuave: '#e8f3ea', ambar: '#a15c00', ambarSuave: '#fff1dc', azul: '#2f5f8a',
        fondo: '#ffffff', acento: '#2a78d6', acento2: '#1baf7a', textoAcento: '#ffffff', glow: false, oscuro: false,
        espinaA: '#4b5563', espinaB: '#1f2937', pez: '#2f5f8a', pez2: '#1d3f63',
        cielo1: '#e3f1fb', cielo2: '#f7fbff', hoja1: '#7ccf5a', hoja2: '#2f8f3c', hojaBorde: '#287a33', tronco1: '#8b5a2b', tronco2: '#5e3a1a',
        tierra1: '#c99a63', tierra2: '#8d5d32', pasto: '#4caf50', raiz: '#5b3a1e', causa1: '#2f80d1', causa2: '#1a5aa0', textoCausa: '#ffffff',
        problema1: '#ff3b3b', problema2: '#c4161c', efecto1: '#d9a520', efecto2: '#a8780a'
    };
    const PALETAS = {
        documento: BASE_CLARO,
        claro: BASE_CLARO,
        oscuro: Object.assign({}, BASE_CLARO, {
            rojo: '#ff6b70', rojoSuave: 'rgba(255,107,112,.16)', tinta: '#e8eaed', gris: '#9aa3ae', linea: '#7d8794', borde: '#3a414d',
            suave: '#2a303b', papel: '#262b35', papel2: '#20242d', nota: '#4a4320', nota2: '#3b351a', notaBorde: '#8a7a2e', cab: '#4a5361', cab2: '#353c47',
            verde: '#7fdca7', verdeSuave: 'rgba(31,157,85,.18)', ambar: '#ffd27a', ambarSuave: 'rgba(237,161,0,.18)',
            fondo: '#1b1f27', acento: '#3987e5', acento2: '#199e70', oscuro: true,
            espinaA: '#cfd5dc', espinaB: '#8a929c', pez: '#3d5a80', pez2: '#22364f',
            cielo1: '#1e2633', cielo2: '#1b1f27', hoja1: '#3f8f4a', hoja2: '#1f5a2a', hojaBorde: '#2c7a38', tronco1: '#7a5230', tronco2: '#4a3019',
            tierra1: '#4a3622', tierra2: '#2c2015', pasto: '#3f8f4a', raiz: '#c08a55', causa1: '#2b6cb8', causa2: '#1a4a85',
            problema1: '#e5484d', problema2: '#a61b20', efecto1: '#c99418', efecto2: '#8a6208'
        }),
        cristal: Object.assign({}, BASE_CLARO, {
            rojo: '#ff6b8b', rojoSuave: 'rgba(255,107,139,.16)', tinta: '#eafcff', gris: '#9fb8c8', linea: '#5fd8de', borde: 'rgba(110,240,240,.35)',
            suave: 'rgba(255,255,255,.06)', papel: 'rgba(255,255,255,.07)', papel2: 'rgba(110,240,240,.06)', nota: 'rgba(110,240,240,.14)', nota2: 'rgba(110,240,240,.06)',
            notaBorde: 'rgba(110,240,240,.55)', cab: '#36e6ea', cab2: '#1fb4c4', textoAcento: '#10203a',
            verde: '#7ff0c0', verdeSuave: 'rgba(127,240,192,.14)', ambar: '#ffd27a', ambarSuave: 'rgba(255,210,122,.14)',
            fondo: '#2b2f50', acento: '#36e6ea', acento2: '#7cc6bf', glow: true, oscuro: true,
            espinaA: '#ffffff', espinaB: '#36e6ea', pez: '#36e6ea', pez2: '#1c8fa3',
            cielo1: 'rgba(54,230,234,.08)', cielo2: 'rgba(54,230,234,0)', hoja1: 'rgba(54,230,234,.34)', hoja2: 'rgba(54,230,234,.10)', hojaBorde: 'rgba(110,245,245,.7)',
            tronco1: '#7ef3f3', tronco2: '#1c8fa3', tierra1: 'rgba(28,143,163,.20)', tierra2: 'rgba(16,32,58,.45)', pasto: '#36e6ea', raiz: '#5fe3e6',
            causa1: 'rgba(54,230,234,.28)', causa2: 'rgba(28,143,163,.18)', textoCausa: '#eafcff', problema1: '#ff6b8b', problema2: '#c2185b', efecto1: 'rgba(255,210,122,.42)', efecto2: 'rgba(214,150,30,.30)'
        })
    };
    // Otros temas (cálido, etc.): base clara u oscura según su fondo, con el acento y las superficies del tema.
    function paletaDeVariables() {
        const cs = getComputedStyle(document.documentElement), v = n => cs.getPropertyValue(n).trim();
        const fondo = v('--t-fondo') || v('--t-superficie');
        if (!fondo) return BASE_CLARO;
        const lum = (() => { const c = document.createElement('canvas').getContext('2d'); c.fillStyle = fondo; const h = c.fillStyle; if (!/^#/.test(h)) return 1; const n = parseInt(h.slice(1), 16); return (0.299 * (n >> 16) + 0.587 * ((n >> 8) & 255) + 0.114 * (n & 255)) / 255; })();
        const base = lum < 0.45 ? PALETAS.oscuro : BASE_CLARO;
        const acento = v('--t-acento') || base.rojo;
        return Object.assign({}, base, {
            fondo: v('--t-superficie') || base.fondo, papel: v('--t-superficie') || base.papel, suave: v('--t-superficie-2') || base.suave,
            tinta: v('--t-texto') || base.tinta, gris: v('--t-texto-2') || base.gris, borde: v('--t-borde') || base.borde,
            rojo: acento, acento: acento
        });
    }
    function paletaTema() {
        const t = (document.documentElement.getAttribute('data-tema') || '').toLowerCase();
        if (!t || t === 'claro') return PALETAS.claro;
        if (PALETAS[t]) return PALETAS[t];
        return paletaDeVariables();
    }
    let C = PALETAS.documento;
    let UID = 0;
    // Degradados, sombra y brillo de un dibujo (ids únicos: varios dibujos pueden convivir en la página).
    function defs(k) {
        return `<defs>
<linearGradient id="${k}-nodo" x1="0" y1="0" x2="0" y2="1"><stop offset="0" stop-color="${C.papel}"/><stop offset="1" stop-color="${C.papel2}"/></linearGradient>
<linearGradient id="${k}-prob" x1="0" y1="0" x2="0" y2="1"><stop offset="0" stop-color="${C.problema1}"/><stop offset="1" stop-color="${C.problema2}"/></linearGradient>
<linearGradient id="${k}-cab" x1="0" y1="0" x2="0" y2="1"><stop offset="0" stop-color="${C.cab}"/><stop offset="1" stop-color="${C.cab2}"/></linearGradient>
<linearGradient id="${k}-acento" x1="0" y1="0" x2="0" y2="1"><stop offset="0" stop-color="${C.acento}"/><stop offset="1" stop-color="${C.acento2}"/></linearGradient>
<linearGradient id="${k}-espina" x1="0" y1="0" x2="1" y2="0"><stop offset="0" stop-color="${C.espinaA}"/><stop offset="1" stop-color="${C.espinaB}"/></linearGradient>
<linearGradient id="${k}-pez" x1="0" y1="0" x2="1" y2="1"><stop offset="0" stop-color="${C.pez}"/><stop offset="1" stop-color="${C.pez2}"/></linearGradient>
<linearGradient id="${k}-nota" x1="0" y1="0" x2="0" y2="1"><stop offset="0" stop-color="${C.nota}"/><stop offset="1" stop-color="${C.nota2}"/></linearGradient>
<linearGradient id="${k}-cielo" x1="0" y1="0" x2="0" y2="1"><stop offset="0" stop-color="${C.cielo1}"/><stop offset="1" stop-color="${C.cielo2}"/></linearGradient>
<linearGradient id="${k}-tierra" x1="0" y1="0" x2="0" y2="1"><stop offset="0" stop-color="${C.tierra1}"/><stop offset="1" stop-color="${C.tierra2}"/></linearGradient>
<linearGradient id="${k}-tronco" x1="0" y1="0" x2="1" y2="0"><stop offset="0" stop-color="${C.tronco2}"/><stop offset=".45" stop-color="${C.tronco1}"/><stop offset="1" stop-color="${C.tronco2}"/></linearGradient>
<linearGradient id="${k}-causa" x1="0" y1="0" x2="0" y2="1"><stop offset="0" stop-color="${C.causa1}"/><stop offset="1" stop-color="${C.causa2}"/></linearGradient>
<linearGradient id="${k}-efecto" x1="0" y1="0" x2="0" y2="1"><stop offset="0" stop-color="${C.efecto1}"/><stop offset="1" stop-color="${C.efecto2}"/></linearGradient>
<radialGradient id="${k}-hoja" cx=".35" cy=".3" r=".8"><stop offset="0" stop-color="${C.hoja1}"/><stop offset="1" stop-color="${C.hoja2}"/></radialGradient>
<filter id="${k}-sombra" x="-20%" y="-20%" width="140%" height="160%"><feDropShadow dx="0" dy="2" stdDeviation="${C.oscuro ? 3 : 2.2}" flood-color="#000" flood-opacity="${C.oscuro ? .45 : .16}"/></filter>
<filter id="${k}-brillo" x="-30%" y="-30%" width="160%" height="160%"><feGaussianBlur stdDeviation="3" result="b"/><feMerge><feMergeNode in="b"/><feMergeNode in="SourceGraphic"/></feMerge></filter>
</defs>`;
    }
    // Efecto de las piezas: brillo de neón en Cristal, sombra suave en los demás.
    const efecto = k => C.glow ? `filter="url(#${k}-brillo)"` : `filter="url(#${k}-sombra)"`;

    // ---------------- utilidades ----------------
    const lienzoMedir = document.createElement('canvas').getContext('2d');
    function anchoTexto(t, tam, peso) { lienzoMedir.font = (peso || 400) + ' ' + tam + 'px ' + FUENTE; return lienzoMedir.measureText(t).width; }
    function envolver(texto, max, tam, peso) {
        const salida = [];
        String(texto ?? '').split('\n').forEach(parrafo => {
            const palabras = parrafo.split(/\s+/).filter(Boolean);
            if (!palabras.length) { salida.push(''); return; }
            let linea = '';
            palabras.forEach(p => {
                while (anchoTexto(p, tam, peso) > max && p.length > 1) {          // palabra más larga que la caja: se parte
                    let k = p.length - 1;
                    while (k > 1 && anchoTexto((linea ? linea + ' ' : '') + p.slice(0, k), tam, peso) > max) k--;
                    if (linea && anchoTexto(linea + ' ' + p.slice(0, k), tam, peso) > max) { salida.push(linea); linea = ''; continue; }
                    salida.push((linea ? linea + ' ' : '') + p.slice(0, k)); linea = ''; p = p.slice(k);
                }
                const prueba = linea ? linea + ' ' + p : p;
                if (anchoTexto(prueba, tam, peso) <= max) linea = prueba; else { if (linea) salida.push(linea); linea = p; }
            });
            salida.push(linea);
        });
        while (salida.length > 1 && salida[salida.length - 1] === '') salida.pop();
        return salida.length ? salida : [''];
    }
    function esc(s) { return String(s ?? '').replace(/&/g, '&amp;').replace(/</g, '&lt;').replace(/>/g, '&gt;').replace(/"/g, '&quot;'); }
    function nid() { return Math.random().toString(36).slice(2, 10); }
    function texto(x, y, lineas, tam, o) {
        o = o || {};
        const lh = o.lh || Math.round(tam * 1.3);
        const ts = lineas.map((l, i) => `<tspan x="${x}" y="${(y + i * lh).toFixed(1)}">${esc(l) || ' '}</tspan>`).join('');
        return `<text font-family="${FUENTE}" font-size="${tam}" font-weight="${o.peso || 400}" fill="${o.color || C.tinta}" text-anchor="${o.anchor || 'start'}"${o.italic ? ' font-style="italic"' : ''}>${ts}</text>`;
    }
    function rect(x, y, w, h, o) {
        o = o || {};
        return `<rect x="${x.toFixed(1)}" y="${y.toFixed(1)}" width="${w.toFixed(1)}" height="${h.toFixed(1)}" rx="${o.rx ?? 8}" fill="${o.fill || C.papel}" stroke="${o.stroke || C.borde}" stroke-width="${o.sw || 1.2}"${o.dash ? ` stroke-dasharray="${o.dash}"` : ''}${o.clase ? ` class="${o.clase}"` : ''}/>`;
    }
    const vacio = t => !String(t ?? '').trim();

    // ---------------- árbol genérico (5 porqués, árbol de causas, Ishikawa, afinidad) ----------------
    function nodo(t, extra) { return Object.assign({ id: nid(), texto: t || '', hijos: [], raiz: false }, extra || {}); }
    function buscar(n, id, padre, prof) {
        prof = prof || 0;
        if (n.id === id) return { n, padre: padre || null, prof };
        for (const h of n.hijos) { const r = buscar(h, id, n, prof + 1); if (r) return r; }
        return null;
    }
    function contiene(n, id) { return n.id === id || n.hijos.some(h => contiene(h, id)); }
    function profundidad(n) { return n.hijos.length ? 1 + Math.max(...n.hijos.map(profundidad)) : 0; }
    function recorrer(n, fn, prof, padre) { prof = prof || 0; fn(n, prof, padre); n.hijos.forEach(h => recorrer(h, fn, prof + 1, n)); }

    // ---------------- herramientas ----------------
    const H = {};

    // 5 porqués: el problema y cada "¿por qué?" como rama (una respuesta puede abrir varias ramas).
    H.porques = {
        nombre: '5 porqués', arbol: true, max: 9,
        desc: 'Preguntar «¿por qué?» hasta llegar a la causa raíz; cada respuesta puede abrir varias ramas',
        nuevo: () => { const r = nodo('', { problema: true }); r.hijos.push(nodo('')); return { raiz: r }; },
        rol: (prof) => prof === 0 ? 'Problema' : '¿Por qué? ' + prof,
        hijoNuevo: () => nodo(''),
        dibujar(d, m) {
            // Cadena de tarjetas: columnas numeradas (1 → 5…) con insignia circular, conectores curvos con flecha.
            const NW = 200, GX = 64, GY = 16, P = 22, CAB = 40, T = 13;
            const nodos = [];
            recorrer(d.raiz, n => { n._l = envolver(n.texto || (m.editor ? '' : '—'), NW - 26, T, n.problema ? 700 : 500); n._h = Math.max(46, n._l.length * 17 + 20); });
            const alto = n => n._sh = n.hijos.length ? Math.max(n._h, n.hijos.reduce((s, h) => s + alto(h), 0) + GY * (n.hijos.length - 1)) : n._h;
            alto(d.raiz);
            const colocar = (n, prof, top) => {
                n._x = P + prof * (NW + GX); n._y = top + (n._sh - n._h) / 2; n._p = prof; nodos.push(n);
                const suma = n.hijos.reduce((s, h) => s + h._sh, 0) + GY * Math.max(0, n.hijos.length - 1);
                let y = top + (n._sh - suma) / 2;
                n.hijos.forEach(h => { colocar(h, prof + 1, y); y += h._sh + GY; });
            };
            colocar(d.raiz, 0, P + CAB);
            const prof = profundidad(d.raiz);
            let s = '';
            for (let i = 0; i <= prof; i++) {
                const cxh = P + i * (NW + GX) + NW / 2;
                if (i === 0) s += `<rect x="${cxh - 44}" y="${P}" width="88" height="24" rx="12" fill="url(#${K}-prob)"/>` + texto(cxh, P + 16.5, ['Problema'], 11.5, { anchor: 'middle', peso: 700, color: '#fff' });
                else {
                    s += `<circle cx="${cxh - 40}" cy="${P + 12}" r="12" fill="url(#${K}-acento)" ${C.glow ? `filter="url(#${K}-brillo)"` : ''}/>` + texto(cxh - 40, P + 16.5, [String(i)], 12, { anchor: 'middle', peso: 800, color: C.textoAcento });
                    s += texto(cxh - 22, P + 16.5, ['¿Por qué?'], 11.5, { peso: 700, color: C.gris });
                }
            }
            nodos.forEach(n => n.hijos.forEach(h => {
                const x1 = n._x + NW, y1 = n._y + n._h / 2, x2 = h._x, y2 = h._y + h._h / 2, xm = x1 + GX / 2;
                s += `<path d="M${x1} ${y1} C${xm} ${y1} ${xm} ${y2} ${x2 - 8} ${y2}" fill="none" stroke="${C.acento}" stroke-width="2" stroke-linecap="round" opacity=".85" ${C.glow ? `filter="url(#${K}-brillo)"` : ''}/>`;
                s += `<path d="M${x2 - 9} ${y2 - 5} L${x2 - 1} ${y2} L${x2 - 9} ${y2 + 5} Z" fill="${C.acento}"/>`;
            }));
            nodos.forEach(n => s += cajaNodo(n, NW, T, m, n.problema ? 'problema' : null));
            const w = P * 2 + (prof + 1) * NW + prof * GX, h = Math.max(...nodos.map(n => n._y + n._h)) + P;
            if (m.editor) s += manijas(nodos, NW, 'h', m);
            return { svg: s, w, h };
        }
    };

    // Árbol de causas y efectos: abajo las causas (raíces, con compuertas Y / O opcionales), al centro el problema (tronco) y
    // arriba los efectos o consecuencias (copa). Los efectos son hijos del problema marcados «efecto» (y sus consecuencias).
    H.arbol = {
        nombre: 'Árbol de causas', arbol: true, max: 12,
        desc: 'Abajo las causas (raíces), al centro el problema (tronco) y arriba los efectos o consecuencias (copa)',
        nuevo: () => { const r = nodo('', { problema: true }); r.hijos.push(nodo('', { efecto: true }), nodo(''), nodo('')); return { raiz: r }; },
        rol: (prof, n) => prof === 0 ? 'Problema central' : n && n.efecto ? (prof === 1 ? 'Efecto' : 'Consecuencia (nivel ' + prof + ')') : 'Causa (nivel ' + prof + ')',
        // Un hijo hereda el lado: bajo un efecto (o junto a uno) nace otro efecto; bajo el problema o una causa, una causa.
        hijoNuevo: (padre, prof, hermano) => nodo('', (hermano && hermano.efecto) || (padre && padre.efecto) ? { efecto: true } : {}),
        dibujar(d, m) {
            const NW = 176, GX = 22, GY = 62, GYE = 36, P = 22, T = 12.5;
            const r = d.raiz, causas = r.hijos.filter(h => !h.efecto), efectos = r.hijos.filter(h => h.efecto);
            const nodos = [], filas = [], filasE = [];
            recorrer(r, (n, prof) => {
                n._l = envolver(n.texto || (m.editor ? '' : '—'), NW - 26, T, n.problema ? 700 : 600);
                n._h = Math.max(40, n._l.length * 16.5 + 18);
            });
            // Causas (hacia abajo) y efectos (hacia arriba): cada lado con sus propios anchos y filas.
            const hijosC = n => n === r ? causas : n.hijos, hijosE = n => n === r ? efectos : n.hijos;
            const medirFilas = (n, prof, hijos, arr) => { arr[prof] = Math.max(arr[prof] || 0, n._h); hijos(n).forEach(h => medirFilas(h, prof + 1, hijos, arr)); };
            medirFilas(r, 0, hijosC, filas); medirFilas(r, 0, hijosE, filasE);
            const anchoDe = (n, hijos) => {
                const hs = hijos(n);
                const v = hs.length ? Math.max(NW, hs.reduce((s, h) => s + anchoDe(h, hijos), 0) + GX * (hs.length - 1)) : NW;
                if (hijos === hijosC) n._sw = v; else n._swe = v;
                return v;
            };
            const sw = anchoDe(r, hijosC), swE = efectos.length ? anchoDe(r, hijosE) : 0;
            const nivE = filasE.length - 1;                                                        // niveles de efectos
            const HE = efectos.length ? filasE.slice(1).reduce((s, x) => s + x, 0) + GYE * (nivE - 1) : 0;
            const CW = Math.min(1400, Math.max(320, sw * 0.8, swE + 120));
            const CH = Math.max(150, Math.min(320, CW * 0.42), HE + 110);                          // copa: cabe los efectos
            const TR = 70;
            const Wt = Math.max(sw, CW + 40);
            const suelo = P * 2 + CH + TR;
            const yFila = [suelo - filas[0] / 2]; for (let i = 1; i < filas.length; i++) yFila[i] = yFila[i - 1] + filas[i - 1] + GY;
            const izq0 = P + (Wt - sw) / 2;
            const colocar = (n, prof, izq) => {
                n._x = izq + (n._sw - NW) / 2; n._y = yFila[prof]; n._p = prof; nodos.push(n);
                const hs = hijosC(n), suma = hs.reduce((s, h) => s + h._sw, 0) + GX * Math.max(0, hs.length - 1);
                let x = izq + (n._sw - suma) / 2;
                hs.forEach(h => { colocar(h, prof + 1, x); x += h._sw + GX; });
            };
            colocar(r, 0, izq0);
            const cx = r._x + NW / 2;
            const ccx = cx, ccy = P * 2 + CH * 0.5, rx = CW / 2, ry = CH / 2;
            // Efectos: el nivel 1 abajo en la copa (cerca del tronco) y sus consecuencias por encima.
            const baseE = ccy + ry * 0.55, yE = [];
            for (let k = 1; k <= nivE; k++) yE[k] = (k === 1 ? baseE : yE[k - 1] - GYE) - filasE[k];
            const colocarE = (n, prof, izq) => {
                n._x = izq + (n._swe - NW) / 2; n._y = yE[prof]; n._p = prof; nodos.push(n);
                const suma = n.hijos.reduce((s, h) => s + h._swe, 0) + GX * Math.max(0, n.hijos.length - 1);
                let x = izq + (n._swe - suma) / 2;
                n.hijos.forEach(h => { colocarE(h, prof + 1, x); x += h._swe + GX; });
            };
            if (efectos.length) {
                const suma = efectos.reduce((s, e) => s + e._swe, 0) + GX * (efectos.length - 1);
                let x = cx - suma / 2;
                efectos.forEach(e => { colocarE(e, 1, x); x += e._swe + GX; });
            }
            const w = P * 2 + Wt, h = Math.max(...nodos.map(n => n._y + n._h)) + P + 18;
            let s = '';
            s += `<rect x="0" y="0" width="${w}" height="${suelo}" fill="url(#${K}-cielo)"/>`;
            s += `<rect x="0" y="${suelo}" width="${w}" height="${h - suelo}" fill="url(#${K}-tierra)"/>`;
            // Tronco y ramas maestras (detrás de la copa).
            const tTop = ccy + ry * 0.2, tw1 = Math.max(16, CW * 0.035), tw2 = tw1 * 1.9;
            s += `<path d="M${cx - tw1} ${tTop} C${cx - tw1} ${suelo - 30} ${cx - tw2} ${suelo - 12} ${cx - tw2 * 1.25} ${suelo + 4} L${cx + tw2 * 1.25} ${suelo + 4} C${cx + tw2} ${suelo - 12} ${cx + tw1} ${suelo - 30} ${cx + tw1} ${tTop} Z" fill="url(#${K}-tronco)" ${C.glow ? `filter="url(#${K}-brillo)"` : ''}/>`;
            s += `<path d="M${cx - tw1 * 0.4} ${tTop + 26} Q${cx - rx * 0.28} ${ccy - ry * 0.05} ${cx - rx * 0.42} ${ccy - ry * 0.35}" stroke="url(#${K}-tronco)" stroke-width="${tw1 * 0.55}" stroke-linecap="round" fill="none"/>`;
            s += `<path d="M${cx + tw1 * 0.4} ${tTop + 18} Q${cx + rx * 0.25} ${ccy - ry * 0.1} ${cx + rx * 0.4} ${ccy - ry * 0.4}" stroke="url(#${K}-tronco)" stroke-width="${tw1 * 0.5}" stroke-linecap="round" fill="none"/>`;
            // Copa proporcional (más ancha y alta con más efectos).
            const blobs = [[0, -0.55, 0.42], [-0.45, -0.3, 0.36], [0.45, -0.3, 0.36], [-0.7, 0.15, 0.3], [0.7, 0.15, 0.3], [-0.3, 0.3, 0.38], [0.3, 0.3, 0.38], [0, 0, 0.5], [-0.15, -0.25, 0.34], [0.2, -0.1, 0.36]];
            s += `<g ${efecto(K)}>`;
            blobs.forEach(([bx, by, br]) => {
                const rr = br * Math.min(rx, ry * 1.6);
                s += `<ellipse cx="${(ccx + bx * rx * 0.9).toFixed(1)}" cy="${(ccy + by * ry).toFixed(1)}" rx="${(rr * 1.15).toFixed(1)}" ry="${rr.toFixed(1)}" fill="url(#${K}-hoja)" stroke="${C.hojaBorde}" stroke-width="${C.glow ? 1.2 : 0.8}" stroke-opacity="${C.glow ? .8 : .35}"/>`;
            });
            s += '</g>';
            // Ramas hacia los efectos (del tronco al efecto; de un efecto a sus consecuencias).
            const rama = (x1, y1, x2, y2, g) => `<path d="M${x1} ${y1} C${x1} ${(y1 + y2) / 2} ${x2} ${(y1 + y2) / 2} ${x2} ${y2}" stroke="${C.raiz}" stroke-width="${g}" stroke-linecap="round" fill="none" opacity=".9" ${C.glow ? `filter="url(#${K}-brillo)"` : ''}/>`;
            efectos.forEach(e => s += rama(cx, tTop + 10, e._x + NW / 2, e._y + e._h, Math.max(3, tw1 * 0.45)));
            nodos.filter(n => n.efecto).forEach(n => n.hijos.forEach(hh => { s += rama(n._x + NW / 2, n._y, hh._x + NW / 2, hh._y + hh._h, 2.5); }));
            // Pasto.
            let pasto = `M0 ${suelo}`;
            for (let x = 0; x <= w; x += 14) pasto += ` L${x + 4} ${suelo - 7 - (x % 28 === 0 ? 5 : 0)} L${x + 9} ${suelo}`;
            s += `<path d="${pasto} L${w} ${suelo} L${w} ${suelo + 5} L0 ${suelo + 5} Z" fill="${C.pasto}" opacity="${C.glow ? .7 : .9}" ${C.glow ? `filter="url(#${K}-brillo)"` : ''}/>`;
            // Raíces: curvas que se adelgazan con la profundidad, con raicillas.
            nodos.filter(n => !n.efecto).forEach(n => {
                const hs = hijosC(n); if (!hs.length) return;
                const x1 = n._x + NW / 2, y1 = n._y + n._h, grueso = Math.max(1.8, 7 - n._p * 1.6);
                hs.forEach((hh, i) => {
                    const x2 = hh._x + NW / 2, y2 = hh._y, ym = (y1 + y2) / 2;
                    s += `<path d="M${x1} ${y1} C${x1} ${ym + 6} ${x2} ${ym - 6} ${x2} ${y2}" stroke="${C.raiz}" stroke-width="${grueso}" stroke-linecap="round" fill="none" opacity="${C.glow ? .85 : .9}" ${C.glow ? `filter="url(#${K}-brillo)"` : ''}/>`;
                    const lado = i % 2 ? 1 : -1;
                    s += `<path d="M${(x1 + x2) / 2} ${ym} q${lado * 14} 6 ${lado * 22} 18" stroke="${C.raiz}" stroke-width="${Math.max(1, grueso * 0.4)}" stroke-linecap="round" fill="none" opacity=".55"/>`;
                });
                if (n.compuerta) s += compuerta(x1, y1 + 15, n.compuerta);
            });
            nodos.forEach(n => s += pildora(n, NW, T, m, n.problema));
            if (m.editor) {
                const n = nodos.find(x => x.id === m.sel);
                if (n) {
                    if (n.problema) {
                        s += manija(n._x + NW / 2 - 16, n._y + n._h + 12, 'hijo', 'Agregar causa debajo (Tab)');
                        s += manija(n._x + NW / 2 + 16, n._y - 12, 'efecto', 'Agregar efecto o consecuencia arriba');
                    } else if (n.efecto) {
                        s += manija(n._x + NW / 2, n._y - 12, 'hijo', 'Agregar consecuencia encima (Tab)');
                        s += manija(n._x + NW + 11, n._y + n._h / 2, 'hermano', 'Agregar efecto al mismo nivel (Enter)');
                    } else s += manijas([n], NW, 'v', m);
                }
            }
            return { svg: s, w, h };
        }
    };
    function compuerta(x, y, tipo) {
        // Y = semicírculo plano abajo (AND), O = escudo curvo (OR), como en árboles de fallas.
        const d = tipo === 'Y'
            ? `M${x - 10} ${y + 9} V${y - 1} A10 10 0 0 1 ${x + 10} ${y - 1} V${y + 9} Z`
            : `M${x - 10} ${y + 9} Q${x - 9} ${y - 4} ${x} ${y - 10} Q${x + 9} ${y - 4} ${x + 10} ${y + 9} Q${x} ${y + 4} ${x - 10} ${y + 9} Z`;
        return `<path d="${d}" fill="${C.papel}" stroke="${C.cab}" stroke-width="1.4"/>` + texto(x, y + 5, [tipo], 9.5, { anchor: 'middle', peso: 700, color: C.cab });
    }

    // Ishikawa (espina de pescado): problema → categorías (6M por defecto) → causas → subcausas.
    const PRESETS = {
        '6M': ['Mano de obra', 'Método', 'Máquina', 'Material', 'Medición', 'Medio ambiente'],
        '4M': ['Mano de obra', 'Método', 'Máquina', 'Material'],
        '8M': ['Mano de obra', 'Método', 'Máquina', 'Material', 'Medición', 'Medio ambiente', 'Gestión', 'Mantenimiento']
    };
    H.ishikawa = {
        nombre: 'Diagrama de Ishikawa', arbol: true, max: 3,
        desc: 'Espina de pescado: causas agrupadas por categorías (6M, 4M u 8M) con subcausas',
        nuevo: (preset) => { const r = nodo('', { problema: true }); (PRESETS[preset || '6M']).forEach(c => r.hijos.push(nodo(c, { categoria: true }))); return { raiz: r }; },
        rol: (prof) => ['Problema (efecto)', 'Categoría', 'Causa', 'Subcausa'][prof] || 'Subcausa',
        hijoNuevo: (padre, prof) => prof === 0 ? nodo('Categoría', { categoria: true }) : nodo(''),
        dibujar(d, m) {
            // Pez de verdad: cola a la izquierda, espina con degradado, espinas curvas por categoría con su marcador (rombo),
            // causas en líneas punteadas hacia la espina y el problema dentro de la cabeza (con ojo y agalla).
            const P = 22, T = 12.5, TS = 11.5, TW = 190, SL = 0.3, LH = 34, HW = 210, COLA = 78;
            const cats = d.raiz.hijos, nCol = Math.max(1, Math.ceil(cats.length / 2));
            const medir = c => {
                let h = 26;
                c.hijos.forEach(ca => {
                    ca._l = envolver(ca.texto || (m.editor ? '' : '—'), TW, T); ca._h = ca._l.length * 16 + 6; h += ca._h;
                    ca.hijos.forEach(sc => { sc._l = envolver(sc.texto || (m.editor ? '' : '—'), TW - 22, TS); sc._h = sc._l.length * 14.5 + 4; h += sc._h; });
                    h += 8;
                });
                return c._alto = Math.max(80, h + 8);
            };
            cats.forEach(medir);
            const arriba = cats.filter((c, i) => i % 2 === 0), abajo = cats.filter((c, i) => i % 2 === 1);
            const HA = Math.max(80, ...arriba.map(c => c._alto)), HB = Math.max(80, ...abajo.map(c => c._alto));
            const cw = TW + 34 + Math.max(HA, HB) * SL + 16;
            const espY = P + LH + HA + 10;
            const x0 = P + COLA, cabX = x0 + nCol * cw;
            d.raiz._l = envolver(d.raiz.texto || (m.editor ? '' : '—'), HW - 70, 13.5, 600);
            const cabH = Math.max(120, d.raiz._l.length * 18 + 60);
            let s = '';
            // Cola (dos lóbulos) y espina ahusada.
            s += `<path d="M${x0 + 6} ${espY} C${x0 - 14} ${espY - 8} ${x0 - 46} ${espY - 46} ${x0 - COLA + 6} ${espY - 62} Q${x0 - COLA + 34} ${espY} ${x0 - COLA + 6} ${espY + 62} C${x0 - 46} ${espY + 46} ${x0 - 14} ${espY + 8} ${x0 + 6} ${espY} Z" fill="url(#${K}-pez)" ${efecto(K)}/>`;
            s += `<path d="M${x0} ${espY - 3} L${cabX + 8} ${espY - 7} L${cabX + 8} ${espY + 7} L${x0} ${espY + 3} Z" fill="url(#${K}-espina)" ${C.glow ? `filter="url(#${K}-brillo)"` : ''}/>`;
            const hit = [];
            const dibujarCat = (c, col, sup) => {
                const finX = x0 + col * cw + cw - 12, H0 = sup ? HA : HB;
                const iniX = finX - H0 * SL, iniY = sup ? espY - H0 : espY + H0;
                // Espina curva (como la costilla del pez), más gruesa junto a la espina central.
                const cx = (finX + iniX) / 2 + 16, cy = (espY + iniY) / 2;
                s += `<path d="M${finX} ${espY} Q${cx} ${cy} ${iniX} ${iniY}" stroke="url(#${K}-espina)" stroke-width="4" stroke-linecap="round" fill="none" ${C.glow ? `filter="url(#${K}-brillo)"` : ''}/>`;
                s += `<rect x="${iniX - 7}" y="${iniY - 7}" width="14" height="14" rx="2" transform="rotate(45 ${iniX} ${iniY})" fill="${C.acento}" ${efecto(K)}/>`;
                c._l = envolver(c.texto || 'Categoría', TW - 10, 13, 700);
                const lw = Math.min(TW, Math.max(...c._l.map(l => anchoTexto(l, 13, 700))) + 28), lh = c._l.length * 16 + 12;
                const lx = iniX - lw / 2, ly = sup ? iniY - lh - 14 : iniY + 14;
                c._x = lx; c._y = ly; c._w = lw; c._h = lh;
                const selc = m.sel === c.id;
                s += grupoIni(c, m) + `<rect x="${lx}" y="${ly}" width="${lw}" height="${lh}" rx="${lh / 2}" fill="${C.glow ? 'rgba(54,230,234,.10)' : `url(#${K}-cab)`}" stroke="${selc ? C.rojo : (C.glow ? C.acento : 'none')}" stroke-width="${selc ? 2.5 : 1.2}"/>`
                    + texto(iniX, ly + 19, c._l, 13, { anchor: 'middle', peso: 700, color: C.glow ? C.acento : '#fff', lh: 16 }) + '</g>';
                hit.push(c);
                let y = sup ? iniY + 24 : espY + 26;
                const bx = yy => iniX + (sup ? (yy - iniY) : (iniY - yy)) * SL + 8;
                c.hijos.forEach(ca => {
                    const yb = y + 12, xb = bx(yb + (ca._l.length - 1) * 16);
                    const tw = Math.max(...ca._l.map(l => anchoTexto(l, T)));
                    ca._x = xb - 10 - Math.min(TW, tw) - 6; ca._y = y - 2; ca._w = Math.min(TW, tw) + 14;
                    const ylinea = yb + (ca._l.length - 1) * 16 + 4;
                    s += grupoIni(ca, m);
                    if (ca.raiz || m.sel === ca.id) s += rect(ca._x - 2, ca._y, ca._w + 4, ca._h, { fill: ca.raiz ? C.rojoSuave : 'none', stroke: m.sel === ca.id ? C.rojo : (ca.raiz ? C.rojo : 'none'), sw: m.sel === ca.id ? 2 : 1, rx: 6 });
                    else s += `<rect x="${ca._x - 2}" y="${ca._y}" width="${ca._w + 4}" height="${ca._h}" fill="transparent"/>`;
                    s += `<path d="M${xb - 10 - Math.min(TW, tw)} ${ylinea} H${bx(ylinea)}" stroke="${C.gris}" stroke-width="1.3" stroke-dasharray="6 5" fill="none" opacity=".8"/>`;
                    s += `<circle cx="${bx(ylinea)}" cy="${ylinea}" r="2.6" fill="${C.acento}"/>`;
                    s += texto(xb - 10, yb, ca._l, T, { anchor: 'end', peso: ca.raiz ? 700 : 500, color: ca.raiz ? C.rojo : C.tinta, lh: 16 });
                    s += '</g>'; hit.push(ca);
                    y += ca._h;
                    ca.hijos.forEach(sc => {
                        const ys = y + 10, xs = bx(ys) - 34;
                        const sw = Math.max(...sc._l.map(l => anchoTexto(l, TS)));
                        sc._x = xs - sw - 4; sc._y = y - 1; sc._w = sw + 14;
                        s += grupoIni(sc, m);
                        if (sc.raiz || m.sel === sc.id) s += rect(sc._x - 2, sc._y, sc._w + 4, sc._h, { fill: sc.raiz ? C.rojoSuave : 'none', stroke: m.sel === sc.id ? C.rojo : (sc.raiz ? C.rojo : 'none'), sw: m.sel === sc.id ? 2 : 1, rx: 6 });
                        else s += `<rect x="${sc._x - 2}" y="${sc._y}" width="${sc._w + 4}" height="${sc._h}" fill="transparent"/>`;
                        s += `<circle cx="${xs + 7}" cy="${ys - 4}" r="2" fill="${C.gris}"/>`;
                        s += texto(xs, ys, sc._l, TS, { anchor: 'end', color: sc.raiz ? C.rojo : C.gris, peso: sc.raiz ? 600 : 400, lh: 14.5 });
                        s += '</g>'; hit.push(sc);
                        y += sc._h;
                    });
                    y += 8;
                });
            };
            arriba.forEach((c, i) => dibujarCat(c, i, true));
            abajo.forEach((c, i) => dibujarCat(c, i, false));
            // Cabeza: cuerpo redondeado con agalla y ojo; el problema va dentro.
            const r = d.raiz, hh = cabH, hx = cabX, hw = HW + 34;
            s += `<path d="M${hx} ${espY - hh * 0.36} C${hx + hw * 0.35} ${espY - hh * 0.62} ${hx + hw * 0.82} ${espY - hh * 0.5} ${hx + hw} ${espY} C${hx + hw * 0.82} ${espY + hh * 0.5} ${hx + hw * 0.35} ${espY + hh * 0.62} ${hx} ${espY + hh * 0.36} Q${hx - 10} ${espY} ${hx} ${espY - hh * 0.36} Z" fill="url(#${K}-pez)" stroke="${m.sel === r.id ? C.rojo : (C.glow ? C.acento : 'none')}" stroke-width="${m.sel === r.id ? 3 : 1.4}" ${efecto(K)}/>`;
            s += `<path d="M${hx + 22} ${espY - hh * 0.3} Q${hx + 40} ${espY} ${hx + 22} ${espY + hh * 0.3}" stroke="${C.glow ? 'rgba(255,255,255,.55)' : 'rgba(255,255,255,.45)'}" stroke-width="2.5" fill="none" stroke-linecap="round"/>`;
            const ojoX = hx + hw * 0.76, ojoY = espY - hh * 0.2;
            s += `<circle cx="${ojoX}" cy="${ojoY}" r="9" fill="#fff"/><circle cx="${ojoX + 2}" cy="${ojoY}" r="4.5" fill="${C.glow ? '#10203a' : C.cab2}"/>`;
            r._x = hx; r._y = espY - hh / 2; r._w = hw; r._h = hh;
            const ty = espY - (r._l.length * 18) / 2 + 13;
            s += grupoIni(r, m) + `<rect x="${hx}" y="${r._y}" width="${hw}" height="${hh}" fill="transparent"/>`
                + texto(hx + 44 + (HW - 70) / 2, ty, r._l, 13.5, { anchor: 'middle', peso: 700, color: '#fff', lh: 18 }) + '</g>';
            hit.push(r);
            const w = cabX + hw + P, h = Math.max(espY + HB + LH + P + 10, espY + hh / 2 + P);
            if (m.editor) {
                const sel = hit.find(n => n.id === m.sel);
                if (sel) {
                    const info = buscar(d.raiz, sel.id);
                    if (info && info.prof < 3) s += manija(info.prof === 0 ? sel._x + 30 : sel._x + sel._w + 10, info.prof === 0 ? sel._y + sel._h - 14 : sel._y + sel._h / 2, 'hijo', info.prof === 0 ? 'Agregar categoría (Tab)' : info.prof === 1 ? 'Agregar causa (Tab)' : 'Agregar subcausa (Tab)');
                    if (info && info.prof > 0) s += manija(sel._x - 14, sel._y + sel._h / 2, 'hermano', 'Agregar al mismo nivel (Enter)');
                }
            }
            return { svg: s, w, h };
        }
    };

    // Lluvia de ideas / diagrama de afinidad: ideas en notas, agrupadas por afinidad (se arrastran entre grupos).
    H.afinidad = {
        nombre: 'Lluvia de ideas y afinidad', arbol: true, max: 2,
        desc: 'Ideas en notas que se arrastran a grupos afines; marcar las causas más probables',
        nuevo: () => { const r = nodo('', { problema: true }); r.hijos.push(nodo('Sin agrupar', { categoria: true }, ), nodo('Grupo 1', { categoria: true })); r.hijos[0].hijos.push(nodo('')); return { raiz: r }; },
        rol: (prof) => ['Tema', 'Grupo', 'Idea'][prof] || 'Idea',
        hijoNuevo: (padre, prof) => prof === 0 ? nodo('Grupo', { categoria: true }) : nodo(''),
        dibujar(d, m) {
            // Tablero de notas: tema arriba, columnas de grupo (paneles de vidrio/papel) con notas adhesivas de esquina doblada.
            const P = 22, GW = 200, GX = 18, T = 12.5;
            const r = d.raiz;
            const anchoTot = Math.max(GW, r.hijos.length * (GW + GX) - GX);
            r._l = envolver(r.texto || (m.editor ? '' : '—'), anchoTot - 40, 14, 700);
            r._x = P; r._y = P; r._w = anchoTot; r._h = Math.max(48, r._l.length * 18 + 24);
            let s = grupoIni(r, m) + `<rect x="${P}" y="${P}" width="${anchoTot}" height="${r._h}" rx="14" fill="url(#${K}-prob)" stroke="${m.sel === r.id ? C.tinta : 'none'}" stroke-width="2.5" ${efecto(K)}/>`
                + texto(P + anchoTot / 2, P + 28, r._l, 14, { anchor: 'middle', peso: 700, color: '#fff', lh: 18 }) + '</g>';
            let hMax = 0; const y0 = P + r._h + 20;
            r.hijos.forEach((g, i) => {
                const x = P + i * (GW + GX);
                g._l = envolver(g.texto || 'Grupo', GW - 50, 12.5, 700); g._x = x; g._y = y0; g._w = GW; g._h = g._l.length * 15 + 16;
                let y = y0 + g._h + 12;
                const ideas = g.hijos.map((idea, j) => {
                    idea._l = envolver(idea.texto || (m.editor ? '' : '—'), GW - 40, T); idea._x = x + 10; idea._y = y; idea._w = GW - 20; idea._h = Math.max(44, idea._l.length * 16 + 20);
                    y += idea._h + 10; return idea;
                });
                const altoG = Math.max(y - y0 + 4, 100);
                s += `<rect data-id="${g.id}" x="${x}" y="${y0}" width="${GW}" height="${altoG}" rx="14" fill="${C.suave}" stroke="${C.borde}" stroke-width="1"${g.hijos.length ? '' : ' stroke-dasharray="5 4"'} class="ac-destino"/>`;
                s += grupoIni(g, m) + `<rect x="${x}" y="${y0}" width="${GW}" height="${g._h}" rx="14" fill="url(#${K}-cab)" stroke="${m.sel === g.id ? C.rojo : 'none'}" stroke-width="2.5"/>`
                    + texto(x + GW / 2 - 8, y0 + 19, g._l, 12.5, { anchor: 'middle', peso: 700, color: C.glow ? C.textoAcento : '#fff', lh: 15 })
                    + `<circle cx="${x + GW - 18}" cy="${y0 + g._h / 2}" r="10" fill="rgba(255,255,255,.25)"/>` + texto(x + GW - 18, y0 + g._h / 2 + 4, [String(g.hijos.length)], 10.5, { anchor: 'middle', peso: 800, color: C.glow ? C.textoAcento : '#fff' }) + '</g>';
                ideas.forEach(idea => {
                    const raiz = idea.raiz, sel = m.sel === idea.id, X = idea._x, Y = idea._y, W = idea._w, Hh = idea._h, D = 12;
                    s += grupoIni(idea, m)
                        + `<path d="M${X} ${Y} H${X + W} V${Y + Hh - D} L${X + W - D} ${Y + Hh} H${X} Z" fill="${raiz ? C.rojoSuave : `url(#${K}-nota)`}" stroke="${sel ? C.rojo : raiz ? C.rojo : C.notaBorde}" stroke-width="${sel ? 2.4 : 1}" ${efecto(K)}/>`
                        + `<path d="M${X + W} ${Y + Hh - D} L${X + W - D} ${Y + Hh - D} L${X + W - D} ${Y + Hh} Z" fill="${C.notaBorde}" opacity=".7"/>`
                        + `<rect x="${X + W / 2 - 16}" y="${Y - 5}" width="32" height="10" rx="2" fill="${C.glow ? 'rgba(255,255,255,.25)' : 'rgba(255,255,255,.65)'}" stroke="${C.notaBorde}" stroke-width=".6"/>`
                        + texto(X + 12, Y + 22, idea._l, T, { lh: 16, peso: raiz ? 700 : 500, color: raiz ? C.rojo : C.tinta }) + '</g>';
                });
                hMax = Math.max(hMax, y0 + altoG);
            });
            if (m.editor) {
                const info = m.sel ? buscar(r, m.sel) : null;
                if (info) {
                    const n = info.n;
                    if (info.prof < 2) s += manija(info.prof === 0 ? n._x + n._w + 12 : n._x + n._w - 12, info.prof === 0 ? n._y + n._h / 2 : n._y + n._h + 12, 'hijo', info.prof === 0 ? 'Agregar grupo (Tab)' : 'Agregar idea al grupo (Tab)');
                    if (info.prof > 0) s += manija(info.prof === 1 ? n._x + n._w + 8 : n._x + n._w / 2, info.prof === 1 ? n._y + n._h / 2 : n._y + n._h + 4, 'hermano', info.prof === 1 ? 'Agregar grupo (Enter)' : 'Agregar idea (Enter)');
                }
            }
            return { svg: s, w: P * 2 + anchoTot, h: hMax + P };
        }
    };

    // Pareto: causas con su frecuencia (o costo), barras de mayor a menor, % acumulado y línea del 80 %.
    H.pareto = {
        nombre: 'Diagrama de Pareto', tabla: 'pareto',
        desc: 'Priorizar: pocas causas explican la mayoría de los casos (regla 80/20)',
        nuevo: () => ({ unidad: 'Frecuencia', items: [{ id: nid(), causa: '', valor: 0 }, { id: nid(), causa: '', valor: 0 }, { id: nid(), causa: '', valor: 0 }] }),
        ordenados: d => d.items.filter(i => !vacio(i.causa) || Number(i.valor) > 0).toSorted((a, b) => (Number(b.valor) || 0) - (Number(a.valor) || 0)),
        dibujar(d, m) {
            // Barras con degradado y esquinas redondeadas (las «pocas vitales» en el acento), zona del 80 % sombreada y curva
            // acumulada con puntos. Las dos escalas se rotulan a cada lado (es la forma estándar del diagrama de Pareto).
            const items = this.ordenados(d);
            const P = 22, IZ = 58, DE = 88, AR = 38, CH = 260, BW = 60, BG = 24, T = 11;
            const total = items.reduce((s, i) => s + (Number(i.valor) || 0), 0);
            const n = Math.max(items.length, 1), ancho = IZ + n * (BW + BG) + BG + DE + P;
            const etqs = items.map(i => envolver(i.causa || '—', BW + BG - 6, T).slice(0, 4));
            const hEtq = Math.max(1, ...etqs.map(e => e.length)) * 14 + 12;
            const x0 = P + IZ, y0 = P + AR, yB = y0 + CH, xF = x0 + n * (BW + BG) + BG;
            let s = `<rect x="${x0}" y="${y0 - 6}" width="${xF - x0}" height="${CH + 6}" rx="10" fill="${C.suave}"/>`;
            s += texto(x0 - 8, P + 14, [d.unidad || 'Frecuencia'], 10.5, { anchor: 'end', color: C.gris, peso: 700 }) + texto(xF + 8, P + 14, ['% acumulado'], 10.5, { color: C.gris, peso: 700 });
            const max = total || 1;
            for (let k = 0; k <= 5; k++) {
                const y = yB - CH * k / 5;
                s += `<path d="M${x0} ${y} H${xF}" stroke="${C.borde}" stroke-width="${k ? 0.8 : 1.4}" ${k ? 'stroke-dasharray="2 4"' : ''}/>`;
                s += texto(x0 - 6, y + 4, [fmt(max * k / 5)], 10, { anchor: 'end', color: C.gris });
                s += texto(xF + 6, y + 4, [(20 * k) + ' %'], 10, { color: C.gris });
            }
            const y80 = yB - CH * 0.8;
            s += `<rect x="${x0}" y="${y0 - 6}" width="${xF - x0}" height="${y80 - y0 + 6}" fill="${C.rojo}" opacity=".06"/>`;
            s += `<path d="M${x0} ${y80} H${xF}" stroke="${C.rojo}" stroke-width="1.4" stroke-dasharray="7 5"/>` + `<rect x="${xF - 44}" y="${y80 - 20}" width="40" height="16" rx="8" fill="${C.rojo}"/>` + texto(xF - 24, y80 - 8.5, ['80 %'], 9.5, { anchor: 'middle', color: '#fff', peso: 800 });
            let acum = 0; const pts = [];
            items.forEach((it, i) => {
                const v = Number(it.valor) || 0, antes = total ? acum / total : 0; acum += v;
                const vital = antes < 0.8 && v > 0;
                const x = x0 + BG + i * (BW + BG), h = Math.max(CH * v / max, 0.5), rr = Math.min(8, h / 2);
                const barra = `M${x} ${yB} V${yB - h + rr} Q${x} ${yB - h} ${x + rr} ${yB - h} H${x + BW - rr} Q${x + BW} ${yB - h} ${x + BW} ${yB - h + rr} V${yB} Z`;
                s += `<g${m.editor ? ` data-id="${it.id}" class="ac-n"` : ''}>` + `<path d="${barra}" fill="${vital ? `url(#${K}-prob)` : `url(#${K}-nodo)`}" stroke="${m.sel === it.id ? C.tinta : (vital ? 'none' : C.borde)}" stroke-width="${m.sel === it.id ? 2.4 : 1}" ${efecto(K)}/>`
                    + (h > 26 ? texto(x + BW / 2, yB - h + 17, [fmt(v)], 11, { anchor: 'middle', peso: 800, color: vital ? '#fff' : C.tinta })
                              : texto(x + BW / 2, yB - h - 7, [fmt(v)], 10.5, { anchor: 'middle', peso: 700, color: C.tinta })) + '</g>';
                s += texto(x + BW / 2, yB + 17, etqs[i], T, { anchor: 'middle', lh: 14, peso: it.raiz ? 800 : 500, color: it.raiz ? C.rojo : C.tinta });
                pts.push([x + BW / 2, yB - CH * (total ? acum / total : 0)]);
            });
            if (pts.length) {
                let dpath = `M${pts[0][0]} ${pts[0][1]}`;
                for (let i = 1; i < pts.length; i++) { const [a1, b1] = pts[i - 1], [a2, b2] = pts[i], mx = (a1 + a2) / 2; dpath += ` C${mx} ${b1} ${mx} ${b2} ${a2} ${b2}`; }
                s += `<path d="${dpath}" fill="none" stroke="${C.acento}" stroke-width="2.6" stroke-linecap="round" ${C.glow ? `filter="url(#${K}-brillo)"` : ''}/>`;
                pts.forEach(p => { s += `<circle cx="${p[0]}" cy="${p[1]}" r="4.5" fill="${C.fondo}" stroke="${C.acento}" stroke-width="2.2"/>`; s += texto(p[0] + 8, p[1] - 9, [Math.round((p[1] - yB) / -CH * 100) + ' %'], 9.5, { color: C.acento, peso: 700 }); });
            } else s += texto((x0 + xF) / 2, yB - CH / 2, ['Sin datos'], 12, { anchor: 'middle', color: C.gris });
            return { svg: s, w: ancho, h: yB + hEtq + P };
        }
    };
    function fmt(v) { return (Math.round(v * 100) / 100).toLocaleString('es-CO'); }

    // AMEF: modo de falla, efecto, causa, S × O × D = NPR.
    const COL_AMEF = [
        ['funcion', 'Función / paso', 150], ['modo', 'Modo de falla', 150], ['efecto', 'Efecto', 150], ['s', 'S', 34],
        ['causa', 'Causa', 160], ['o', 'O', 34], ['controles', 'Controles actuales', 140], ['d', 'D', 34], ['npr', 'NPR', 50], ['accion', 'Acción recomendada', 170]
    ];
    H.amef = {
        nombre: 'AMEF (modo y efecto de falla)', tabla: 'amef',
        desc: 'Modo de falla, efecto y causa; severidad × ocurrencia × detección = NPR',
        nuevo: () => ({ filas: [filaAmef()] }),
        npr: f => (Number(f.s) || 0) * (Number(f.o) || 0) * (Number(f.d) || 0),
        nivel: v => v >= 200 ? 'alto' : v >= 100 ? 'medio' : v > 0 ? 'bajo' : '',
        dibujar(d) {
            const filas = d.filas.filter(f => COL_AMEF.some(c => !['s', 'o', 'd', 'npr'].includes(c[0]) && !vacio(f[c[0]])) || this.npr(f) > 0);
            return tablaSvg(COL_AMEF, filas, (f, k) => k === 'npr' ? (this.npr(f) || '') : f[k], (f, k) => {
                if (k !== 'npr') return f.raiz && k === 'causa' ? { fill: C.rojoSuave, color: C.rojo, peso: 600 } : null;
                const n = this.nivel(this.npr(f));
                return n === 'alto' ? { fill: C.rojoSuave, color: C.rojo, peso: 700 } : n === 'medio' ? { fill: C.ambarSuave, color: C.ambar, peso: 700 } : n === 'bajo' ? { fill: C.verdeSuave, color: C.verde, peso: 700 } : null;
            });
        }
    };
    function filaAmef() { return { id: nid(), funcion: '', modo: '', efecto: '', s: '', causa: '', o: '', controles: '', d: '', accion: '', raiz: false }; }

    // Es / No es (Kepner-Tregoe): delimitar el problema para encontrar lo distintivo.
    const DIM_ESNOES = ['Qué (objeto / defecto)', 'Dónde', 'Cuándo', 'Cuánto / extensión'];
    const COL_ESNOES = [['dim', 'Dimensión', 150], ['es', 'Es', 210], ['noes', 'No es', 210], ['dist', 'Lo distintivo / cambios', 220]];
    H.esnoes = {
        nombre: 'Es / No es', tabla: 'esnoes',
        desc: 'Delimitar el problema: qué, dónde, cuándo y cuánto es y no es, y qué lo distingue',
        nuevo: () => ({ filas: DIM_ESNOES.map(x => ({ id: nid(), dim: x, es: '', noes: '', dist: '' })), causa: '' }),
        dibujar(d) {
            const t = tablaSvg(COL_ESNOES, d.filas, (f, k) => f[k], (f, k) => k === 'dim' ? { fill: C.suave, peso: 600 } : null);
            if (vacio(d.causa)) return t;
            const l = envolver(d.causa, t.w - 60, 12.5, 600);
            const y = t.h + 4, h = l.length * 16 + 34;
            t.svg += rect(22, y, t.w - 44, h, { fill: C.rojoSuave, stroke: C.rojo, rx: 12 }) + texto(34, y + 18, ['Causa más probable'], 10.5, { color: C.rojo, peso: 700 }) + texto(34, y + 36, l, 12.5, { peso: 600, lh: 16 });
            t.h = y + h + 20;
            return t;
        }
    };

    function tablaSvg(cols, filas, valor, estilo) {
        // Tabla con marco redondeado, encabezado en degradado, filas alternas y celdas de nivel en píldora.
        const P = 22, T = 11.5, LH = 15;
        const anchoT = cols.reduce((s, c) => s + c[2], 0);
        const cabL = cols.map(c => envolver(c[1], c[2] - 10, 11, 700));
        const cabH = Math.max(...cabL.map(l => l.length)) * 14 + 16;
        const cuerpo = []; let y = P + cabH;
        if (!filas.length) { cuerpo.push(texto(P + anchoT / 2, y + 26, ['Sin datos'], 12, { anchor: 'middle', color: C.gris })); y += 42; }
        filas.forEach((f, fi) => {
            const celdas = cols.map(c => envolver(String(valor(f, c[0]) ?? ''), c[2] - 14, T));
            const h = Math.max(...celdas.map(l => l.length)) * LH + 16;
            let x = P;
            cuerpo.push(`<rect x="${P}" y="${y}" width="${anchoT}" height="${h}" fill="${fi % 2 ? C.suave : 'transparent'}"/>`);
            cols.forEach((c, i) => {
                const e = estilo(f, c[0]) || {};
                const corta = c[2] <= 54;
                if (e.fill) cuerpo.push(corta && celdas[i][0]
                    ? `<rect x="${x + 5}" y="${y + h / 2 - 11}" width="${c[2] - 10}" height="22" rx="11" fill="${e.fill}"/>`
                    : `<rect x="${x + 2}" y="${y + 2}" width="${c[2] - 4}" height="${h - 4}" rx="6" fill="${e.fill}"/>`);
                cuerpo.push(texto(corta ? x + c[2] / 2 : x + 8, corta ? y + h / 2 + 4 : y + 19, celdas[i], T, { anchor: corta ? 'middle' : 'start', color: e.color || C.tinta, peso: e.peso || 500, lh: LH }));
                if (i) cuerpo.push(`<path d="M${x} ${y + 6} V${y + h - 6}" stroke="${C.borde}" stroke-width=".8"/>`);
                x += c[2];
            });
            y += h;
            cuerpo.push(`<path d="M${P} ${y} H${P + anchoT}" stroke="${C.borde}" stroke-width=".7"/>`);
        });
        let s = `<rect x="${P}" y="${P}" width="${anchoT}" height="${y - P}" rx="12" fill="url(#${K}-nodo)" stroke="${C.borde}" stroke-width="1" ${efecto(K)}/>`;
        s += `<path d="M${P} ${P + cabH} V${P + 12} a12 12 0 0 1 12 -12 H${P + anchoT - 12} a12 12 0 0 1 12 12 V${P + cabH} Z" fill="url(#${K}-cab)"/>`;
        let x = P;
        cols.forEach((c, i) => { s += texto(x + c[2] / 2, P + 20, cabL[i], 11, { anchor: 'middle', color: C.glow ? C.textoAcento : '#fff', peso: 700, lh: 14 }); x += c[2]; });
        return { svg: s + cuerpo.join(''), w: anchoT + P * 2, h: y + P };
    }

    // ---------------- piezas de dibujo compartidas ----------------
    function grupoIni(n, m) { return m.editor ? `<g data-id="${n.id}" class="ac-n${m.sel === n.id ? ' ac-sel' : ''}">` : '<g>'; }
    // Tarjeta de un nodo: degradado suave, sombra (o brillo en Cristal), franja de causa raíz y sello «RAÍZ».
    function cajaNodo(n, NW, T, m, tipo) {
        const sel = m.sel === n.id, prob = tipo === 'problema';
        let s = grupoIni(n, m);
        if (prob) {
            s += `<rect x="${n._x}" y="${n._y}" width="${NW}" height="${n._h}" rx="12" fill="url(#${K}-prob)" stroke="${sel ? C.tinta : 'none'}" stroke-width="2.5" ${efecto(K)}/>`;
        } else {
            const fill = n.raiz ? C.rojoSuave : `url(#${K}-nodo)`, stroke = sel ? C.rojo : n.raiz ? C.rojo : C.borde;
            s += `<rect x="${n._x}" y="${n._y}" width="${NW}" height="${n._h}" rx="12" fill="${fill}" stroke="${stroke}" stroke-width="${sel ? 2.6 : n.raiz ? 1.6 : 1.1}" ${efecto(K)}/>`;
            if (n.raiz) s += `<path d="M${n._x} ${n._y + 12} a12 12 0 0 1 12 -12 h-6 v${n._h} h6 a12 12 0 0 1 -12 -12 z" fill="${C.rojo}"/><rect x="${n._x}" y="${n._y + 8}" width="5" height="${n._h - 16}" fill="${C.rojo}"/>`;
        }
        const y = n._y + (n._h - n._l.length * 17) / 2 + 13;
        s += texto(n._x + 14, y, n._l, T, { lh: 17, peso: prob ? 700 : n.raiz ? 600 : 500, color: prob ? '#fff' : n.raiz ? C.rojo : C.tinta });
        if (n.raiz && !prob) s += `<rect x="${n._x + NW - 44}" y="${n._y - 8}" width="38" height="16" rx="8" fill="${C.rojo}"/>` + texto(n._x + NW - 25, n._y + 3.5, ['RAÍZ'], 8.5, { anchor: 'middle', peso: 800, color: '#fff' });
        return s + '</g>';
    }
    // Píldora del árbol de causas (como en las láminas: problema rojo en el tronco, causas azules / de cristal en las raíces).
    function pildora(n, NW, T, m, prob) {
        const sel = m.sel === n.id;
        const fill = prob || n.raiz ? `url(#${K}-prob)` : n.efecto ? `url(#${K}-efecto)` : `url(#${K}-causa)`;
        const borde = sel ? (C.glow ? '#fff' : C.tinta) : (C.glow ? (prob || n.raiz ? C.rojo : n.efecto ? '#ffd27a' : C.acento) : 'rgba(255,255,255,.55)');
        let s = grupoIni(n, m) + `<rect x="${n._x}" y="${n._y}" width="${NW}" height="${n._h}" rx="${Math.min(22, n._h / 2)}" fill="${fill}" stroke="${borde}" stroke-width="${sel ? 2.6 : 1.4}" ${efecto(K)}/>`;
        s += `<rect x="${n._x + 8}" y="${n._y + 3}" width="${NW - 16}" height="${Math.min(12, n._h / 3)}" rx="6" fill="#fff" opacity="${C.glow ? .08 : .18}"/>`;   // reflejo
        const y = n._y + (n._h - n._l.length * 16.5) / 2 + 12.5;
        s += texto(n._x + NW / 2, y, n._l, T, { anchor: 'middle', lh: 16.5, peso: prob ? 800 : 600, color: prob || n.raiz || n.efecto ? '#fff' : C.textoCausa });
        if (n.raiz && !prob) s += texto(n._x + NW - 12, n._y + 12, ['RAÍZ'], 8, { anchor: 'end', peso: 800, color: '#fff' });
        return s + '</g>';
    }
    function manija(x, y, accion, titulo) {
        return `<g class="ac-manija" data-accion="${accion}"><title>${esc(titulo)}</title><circle cx="${x}" cy="${y}" r="10" fill="${C.glow ? C.acento : C.rojo}" stroke="${C.glow ? '#10203a' : '#fff'}" stroke-width="2"/><path d="M${x - 4.5} ${y} H${x + 4.5} M${x} ${y - 4.5} V${y + 4.5}" stroke="${C.glow ? '#10203a' : '#fff'}" stroke-width="2" stroke-linecap="round"/></g>`;
    }
    function manijas(nodos, NW, dir, m) {
        const n = nodos.find(x => x.id === m.sel); if (!n) return '';
        let s = dir === 'h' ? manija(n._x + NW + 12, n._y + n._h / 2, 'hijo', '¿Por qué? Agregar rama (Tab)') : manija(n._x + NW / 2, n._y + n._h + 12, 'hijo', 'Agregar causa debajo (Tab)');
        if (n._p > 0) s += dir === 'h' ? manija(n._x + NW / 2, n._y + n._h + 9, 'hermano', 'Agregar otra respuesta al mismo nivel (Enter)') : manija(n._x + NW + 11, n._y + n._h / 2, 'hermano', 'Agregar causa al mismo nivel (Enter)');
        return s;
    }

    // ---------------- documento completo → SVG ----------------
    function causasRaiz(doc) {
        const r = [];
        doc.herramientas.forEach(h => {
            const d = h.datos;
            if (H[h.tipo]?.arbol) recorrer(d.raiz, (n, prof) => { if (n.raiz && prof > 0 && !vacio(n.texto)) r.push(n.texto.trim()); });
            else if (h.tipo === 'pareto') d.items.forEach(i => { if (i.raiz && !vacio(i.causa)) r.push(i.causa.trim()); });
            else if (h.tipo === 'amef') d.filas.forEach(f => { if (f.raiz && !vacio(f.causa)) r.push(f.causa.trim()); });
            else if (h.tipo === 'esnoes' && !vacio(d.causa)) r.push(d.causa.trim());
        });
        return [...new Set(r)];
    }
    // En la imagen final no van las ramas vacías (nodos sin texto ni hijos) ni los grupos de afinidad vacíos.
    function podar(n, tipo, prof) {
        n.hijos = n.hijos.map(h => podar(h, tipo, prof + 1)).filter(Boolean);
        if (prof === 0) return n;
        if (n.categoria) return tipo === 'afinidad' && !n.hijos.length ? null : n;
        return vacio(n.texto) && !n.hijos.length ? null : n;
    }
    // Cada dibujo lleva sus propios degradados/filtros (K = prefijo único) y la paleta del tema (editor) o del documento.
    let K = 'ac0';
    function dibujarHerramienta(h, m, paleta) {
        C = paleta || (m && m.editor ? paletaTema() : PALETAS.documento);
        K = 'ac' + (++UID);
        const r = dibujarCrudo(h, m);
        r.svg = defs(K) + r.svg;
        r.fondo = C.fondo;
        return r;
    }
    function dibujarCrudo(h, m) {
        if (m && m.editor) return H[h.tipo].dibujar(h.datos, m);
        let datos = h.datos;
        if (H[h.tipo].arbol) { datos = structuredClone(h.datos); podar(datos.raiz, h.tipo, 0); }
        else if (h.tipo === 'esnoes') { datos = Object.assign({}, h.datos, { filas: h.datos.filas.filter(f => !vacio(f.es) || !vacio(f.noes) || !vacio(f.dist)) }); }
        return H[h.tipo].dibujar(datos, { editor: false });
    }
    function svgCompleto(doc, paleta) {
        if (!doc.herramientas.length && vacio(doc.conclusion)) return null;
        const pal = paleta || PALETAS.documento;
        const partes = doc.herramientas.map(h => ({ h, r: dibujarHerramienta(h, null, pal) }));
        C = pal;
        const raices = causasRaiz(doc);
        let W = Math.max(640, ...partes.map(p => p.r.w));
        let s = '', y = 0;
        partes.forEach((p, i) => {
            const tit = (p.h.titulo && p.h.titulo.trim()) || H[p.h.tipo].nombre;
            s += `<rect x="0" y="${y}" width="${W}" height="36" fill="${C.suave}"/><rect x="0" y="${y}" width="5" height="36" fill="${C.rojo}"/>`;
            s += texto(18, y + 23, [tit], 14, { peso: 700 });
            if (tit !== H[p.h.tipo].nombre) s += texto(W - 16, y + 23, [H[p.h.tipo].nombre], 11, { anchor: 'end', color: C.gris });
            y += 36;
            s += `<g transform="translate(${((W - p.r.w) / 2).toFixed(1)},${y})">${p.r.svg}</g>`;
            y += p.r.h + (i < partes.length - 1 ? 10 : 0);
        });
        if (raices.length || !vacio(doc.conclusion)) {
            y += 6;
            let b = '', yy = y + 26;
            b += texto(20, yy, ['Causa raíz'], 12, { peso: 700, color: C.rojo }); yy += 20;
            raices.forEach(rz => { const l = envolver(rz, W - 70, 12.5, 600); b += `<circle cx="28" cy="${yy - 4}" r="3.5" fill="${C.rojo}"/>` + texto(40, yy, l, 12.5, { peso: 600, lh: 16 }); yy += l.length * 16 + 4; });
            if (!vacio(doc.conclusion)) { const l = envolver(doc.conclusion, W - 40, 12.5); yy += raices.length ? 6 : 0; b += texto(20, yy, l, 12.5, { lh: 17 }); yy += l.length * 17; }
            s += `<rect x="12" y="${y}" width="${W - 24}" height="${yy - y + 6}" rx="10" fill="${C.rojoSuave}" stroke="${C.rojo}" stroke-width="1"/>` + b;
            y = yy + 18;
        }
        const Ht = Math.ceil(y + 4); W = Math.ceil(W);
        return `<svg xmlns="http://www.w3.org/2000/svg" width="${W}" height="${Ht}" viewBox="0 0 ${W} ${Ht}"><rect width="100%" height="100%" fill="${C.fondo}"/>${s}</svg>`;
    }
    function resumen(doc) {
        const r = causasRaiz(doc), partes = [];
        if (doc.herramientas.length) partes.push('Herramientas: ' + doc.herramientas.map(h => (h.titulo && h.titulo.trim()) || H[h.tipo].nombre).join(', ') + '.');
        if (r.length) partes.push('Causa raíz: ' + r.join('; ') + '.');
        if (!vacio(doc.conclusion)) partes.push(doc.conclusion.trim());
        return partes.join('\n');
    }
    function limpiar(doc) { return JSON.stringify(doc, (k, v) => k.startsWith('_') ? undefined : v); }
    function leer(json) {
        let d = null;
        try { d = json ? JSON.parse(json) : null; } catch { d = null; }
        if (!d || !Array.isArray(d.herramientas)) d = { v: 1, herramientas: [], activa: 0, conclusion: '' };
        d.herramientas = d.herramientas.filter(h => H[h.tipo] && h.datos);
        return d;
    }

    // ---------------- íconos de la ventana (mismo trazo que Ico.razor) ----------------
    const IC = {
        atras: '<path d="M15 18l-6-6 6-6"/>',
        guardar: '<path d="M5 3h11l3 3v13a2 2 0 0 1-2 2H7a2 2 0 0 1-2-2z"/><path d="M8 3v6h8V3M8 21v-7h8v7"/>',
        agregar: '<path d="M12 5v14M5 12h14"/>',
        deshacer: '<path d="M9 14 4 9l5-5"/><path d="M4 9h10a6 6 0 0 1 0 12h-3"/>',
        rehacer: '<path d="m15 14 5-5-5-5"/><path d="M20 9H10a6 6 0 0 0 0 12h3"/>',
        acercar: '<circle cx="11" cy="11" r="7"/><path d="m20 20-4-4M8 11h6M11 8v6"/>',
        alejar: '<circle cx="11" cy="11" r="7"/><path d="m20 20-4-4M8 11h6"/>',
        ajustar: '<path d="M4 9V4h5M20 9V4h-5M4 15v5h5M20 15v5h-5"/>',
        png: '<rect x="3" y="4" width="18" height="16" rx="2"/><circle cx="9" cy="10" r="2"/><path d="m21 16-5-5-8 9"/>',
        svg: '<path d="M12 3v12M7 10l5 5 5-5"/><path d="M5 19h14"/>',
        eliminar: '<path d="M4 7h16M10 11v6M14 11v6M6 7l1 13h10l1-13M9 7V4h6v3"/>',
        cerrar: '<path d="M6 6l12 12M18 6 6 18"/>',
        hijo: '<path d="M5 4v8a3 3 0 0 0 3 3h11"/><path d="m15 11 4 4-4 4"/>',
        hermano: '<path d="M5 6h14M5 12h14"/><path d="M12 15v6M9 18h6"/>',
        subir: '<path d="m6 15 6-6 6 6"/>',
        bajar: '<path d="m6 9 6 6 6-6"/>',
        raiz: '<circle cx="12" cy="12" r="8"/><circle cx="12" cy="12" r="3"/>',
        editar: '<path d="M4 20h4L19 9l-4-4L4 16z"/><path d="m14 6 4 4"/>',
        // herramientas
        porques: '<rect x="2" y="9" width="6" height="6" rx="1"/><rect x="16" y="3" width="6" height="5" rx="1"/><rect x="16" y="16" width="6" height="5" rx="1"/><path d="M8 12h4V5.5h4M12 12v6.5h4"/>',
        arbol: '<rect x="8" y="2" width="8" height="5" rx="1"/><rect x="2" y="17" width="7" height="5" rx="1"/><rect x="15" y="17" width="7" height="5" rx="1"/><path d="M12 7v5M5.5 17v-5h13v5"/>',
        ishikawa: '<path d="M2 12h15"/><path d="M17 8h5v8h-5z"/><path d="M5 5l3 7M11 5l3 7M5 19l3-7M11 19l3-7"/>',
        afinidad: '<rect x="3" y="3" width="7" height="7" rx="1"/><rect x="14" y="3" width="7" height="7" rx="1"/><rect x="3" y="14" width="7" height="7" rx="1"/><rect x="14" y="14" width="7" height="7" rx="1"/>',
        pareto: '<path d="M3 21h18"/><rect x="4" y="8" width="4" height="13"/><rect x="10" y="13" width="4" height="8"/><rect x="16" y="17" width="4" height="4"/><path d="M6 8l6-4 6-1.5"/>',
        amef: '<rect x="3" y="4" width="18" height="16" rx="2"/><path d="M3 10h18M3 15h18M9 4v16M15 4v16"/>',
        esnoes: '<rect x="3" y="4" width="18" height="16" rx="2"/><path d="M12 4v16"/><path d="m6 12 1.6 1.6L10 11M15 10l3 3M18 10l-3 3"/>'
    };
    const ico = n => `<svg viewBox="0 0 24 24" width="18" height="18" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round" aria-hidden="true">${IC[n] || ''}</svg>`;
    const btn = (accion, icono, titulo, extra) => `<button type="button" class="ac-btn ${extra || ''}" data-accion="${accion}" title="${esc(titulo)}">${ico(icono)}</button>`;
    const ORDEN = ['porques', 'ishikawa', 'arbol', 'afinidad', 'pareto', 'amef', 'esnoes'];

    // ---------------- ventana del editor ----------------
    let actual = null;

    function abrir(opc, dotnet) {
        if (actual) actual.cerrar(true);
        const doc = leer(opc && opc.json);
        const est = { doc, dotnet, sel: null, zoom: 1, pila: [], rehacer: [], inicial: '', galeria: doc.herramientas.length === 0, ultimoMedido: null };
        est.doc.activa = Math.min(est.doc.activa || 0, Math.max(0, doc.herramientas.length - 1));
        // «Cambios sin guardar» = contenido distinto; cambiar de pestaña (activa) no cuenta como cambio.
        const firma = d => limpiar(Object.assign({}, d, { activa: 0 }));
        est.inicial = firma(est.doc);
        const raizEl = document.createElement('div');
        raizEl.className = 'ac-ventana';
        raizEl.setAttribute('role', 'dialog');
        raizEl.setAttribute('aria-label', 'Editor de análisis de causa');
        raizEl.innerHTML = `
            <div class="ac-cab">
                ${btn('cerrar', 'atras', 'Volver (Esc)')}
                <div class="ac-tit"><span>Análisis de causa</span>${opc && opc.titulo ? `<small>${esc(opc.titulo)}</small>` : ''}</div>
                <div class="ac-pestanas" role="tablist"></div>
                <div class="ac-acc">
                    ${btn('deshacer', 'deshacer', 'Deshacer (Ctrl + Z)')}${btn('rehacer', 'rehacer', 'Rehacer (Ctrl + Y)')}
                    <span class="ac-sep"></span>
                    ${btn('alejar', 'alejar', 'Alejar (Ctrl + rueda)')}${btn('acercar', 'acercar', 'Acercar (Ctrl + rueda)')}${btn('ajustar', 'ajustar', 'Ajustar a la pantalla')}
                    <span class="ac-sep"></span>
                    ${btn('png', 'png', 'Descargar imagen PNG')}${btn('svg', 'svg', 'Descargar SVG')}
                    <span class="ac-sep"></span>
                    ${btn('guardar', 'guardar', 'Guardar en el campo', 'ac-primario')}
                </div>
            </div>
            <div class="ac-aviso" hidden role="alertdialog" aria-modal="true" aria-labelledby="ac-aviso-tit"><div class="ac-aviso-caja">
                <b class="ac-aviso-tit" id="ac-aviso-tit">Cambios sin guardar</b>
                <p class="ac-aviso-txt">Este análisis tiene cambios que aún no están en el plan. Guárdelos, salga sin guardarlos o siga editando.</p>
                <div class="ac-aviso-btns">${btn('seguir', 'editar', 'Seguir editando (Esc)')}${btn('descartar', 'atras', 'Salir sin guardar')}${btn('guardar', 'guardar', 'Guardar y salir', 'ac-primario')}</div>
            </div></div>
            <div class="ac-cuerpo">
                <div class="ac-lienzo" tabindex="0"><div class="ac-host"></div><textarea class="ac-inline" hidden rows="2"></textarea></div>
                <aside class="ac-panel"></aside>
                <div class="ac-galeria" hidden></div>
            </div>
            <div class="ac-pie">
                <div class="ac-raices"></div>
                <div class="ac-campo-concl"><textarea id="ac-conclusion" rows="2" placeholder=" "></textarea><label for="ac-conclusion">Conclusión del análisis</label></div>
            </div>`;
        document.body.appendChild(raizEl);
        document.documentElement.classList.add('ac-abierto');
        const $ = s => raizEl.querySelector(s);
        const lienzo = $('.ac-lienzo'), host = $('.ac-host'), panel = $('.ac-panel'), galeria = $('.ac-galeria'), inline = $('.ac-inline');
        const conclusion = $('#ac-conclusion');
        conclusion.value = doc.conclusion || '';

        const H0 = () => est.doc.herramientas[est.doc.activa] || null;
        const sucio = () => firma(est.doc) !== est.inicial;

        // ---- historial ----
        let instantanea = null;
        function antes() { est.pila.push(limpiar(est.doc)); if (est.pila.length > 120) est.pila.shift(); est.rehacer = []; }
        function cambio(fn, mantener) { antes(); fn(); pintar(mantener); }
        function deshacer() { if (!est.pila.length) return; est.rehacer.push(limpiar(est.doc)); est.doc = leer(est.pila.pop()); est.sel = null; conclusion.value = est.doc.conclusion || ''; pintar(); }
        function rehacer() { if (!est.rehacer.length) return; est.pila.push(limpiar(est.doc)); est.doc = leer(est.rehacer.pop()); est.sel = null; conclusion.value = est.doc.conclusion || ''; pintar(); }
        // Escribir en un campo: una sola entrada de historial por foco.
        function alEscribir(el, fn) {
            el.addEventListener('focus', () => { instantanea = limpiar(est.doc); });
            el.addEventListener('input', () => {
                if (instantanea !== null) { est.pila.push(instantanea); est.rehacer = []; instantanea = null; }
                fn(el.value); pintarLienzo(); pintarRaices();
            });
        }
        alEscribir(conclusion, v => est.doc.conclusion = v);

        // ---- pestañas ----
        function pintarPestanas() {
            const p = $('.ac-pestanas');
            p.innerHTML = est.doc.herramientas.map((h, i) => `
                <span class="ac-pest ${i === est.doc.activa && !est.galeria ? 'activa' : ''}" data-i="${i}" role="tab" title="${esc(H[h.tipo].nombre)}">
                    ${ico(h.tipo)}<span class="ac-pest-txt">${esc((h.titulo && h.titulo.trim()) || H[h.tipo].nombre)}</span>
                    <button type="button" class="ac-pest-x" data-quitar="${i}" title="Quitar esta herramienta">${ico('cerrar')}</button>
                </span>`).join('') + `<button type="button" class="ac-btn ac-pest-mas ${est.galeria ? 'activa' : ''}" data-accion="galeria" title="Agregar herramienta">${ico('agregar')}</button>`;
        }

        // ---- galería de herramientas ----
        function pintarGaleria() {
            galeria.hidden = !est.galeria;
            if (!est.galeria) return;
            galeria.innerHTML = `<div class="ac-gal-rejilla">${ORDEN.map(t => `
                <button type="button" class="ac-gal-tarjeta" data-nueva="${t}" title="${esc(H[t].desc)}">
                    <span class="ac-gal-ico">${ico(t).replace('width="18" height="18"', 'width="42" height="42"').replace('stroke-width="2"', 'stroke-width="1.4"')}</span>
                    <span class="ac-gal-nom">${esc(H[t].nombre)}</span>
                    ${t === 'ishikawa' ? `<span class="ac-gal-var">${Object.keys(PRESETS).map(k => `<span class="ac-chip" data-nueva="ishikawa" data-preset="${k}" title="${esc(PRESETS[k].join(', '))}">${k}</span>`).join('')}</span>` : ''}
                </button>`).join('')}</div>`;
        }
        function agregarHerramienta(tipo, preset) {
            cambio(() => {
                est.doc.herramientas.push({ id: nid(), tipo, titulo: '', datos: H[tipo].nuevo(preset) });
                est.doc.activa = est.doc.herramientas.length - 1; est.galeria = false;
                const h = H0(); est.sel = H[tipo].arbol ? h.datos.raiz.id : null;
            });
            ajustar();
            const h = H0();
            lienzo.focus({ preventScroll: true });
            if (H[tipo].arbol) editarEnLinea(h.datos.raiz.id);
        }

        // ---- lienzo ----
        function pintarLienzo() {
            const h = H0();
            host.classList.toggle('ac-host-tabla', !!(h && (h.tipo === 'amef' || h.tipo === 'esnoes')));
            if (!h || est.galeria) { host.replaceChildren(); return; }
            if (h.tipo === 'amef' || h.tipo === 'esnoes') { if (!host.querySelector('.ac-tabla-ed') || host.dataset.h !== h.id) pintarTablaEd(h); else refrescarTablaEd(h); host.dataset.h = h.id; return; }
            host.dataset.h = h.id;
            let r;
            try { r = dibujarHerramienta(h, { editor: true, sel: est.sel }); }
            catch (err) {
                console.error('Análisis de causa: no se pudo dibujar la herramienta', err);
                host.innerHTML = `<div class="ac-error-dibujo">No se pudo dibujar esta herramienta: ${esc(err && err.message || err)}. Los demás botones siguen funcionando; deshaga el último cambio o avise del error.</div>`;
                return;
            }
            est.ultimoMedido = r;
            const z = est.zoom;
            host.innerHTML = `<svg xmlns="http://www.w3.org/2000/svg" width="${Math.ceil(r.w * z)}" height="${Math.ceil(r.h * z)}" viewBox="0 0 ${Math.ceil(r.w)} ${Math.ceil(r.h)}" class="ac-svg" style="background:${r.fondo}">${r.svg}</svg>`;
        }
        function ajustar() {
            const h = H0(); if (!h || est.galeria || h.tipo === 'amef' || h.tipo === 'esnoes') return;
            const r = dibujarHerramienta(h, { editor: true, sel: est.sel });
            const z = Math.min((lienzo.clientWidth - 40) / r.w, (lienzo.clientHeight - 40) / r.h, 1.4);
            est.zoom = Math.max(0.25, Math.min(z, 2.5)); pintarLienzo();
        }
        function zoom(f) { est.zoom = Math.max(0.25, Math.min(est.zoom * f, 3)); pintarLienzo(); }

        // ---- tabla editable (AMEF, Es / No es) ----
        function pintarTablaEd(h) {
            const amef = h.tipo === 'amef';
            const cols = amef ? COL_AMEF : COL_ESNOES;
            const filas = h.datos.filas;
            const celda = (f, c) => {
                if (amef && c[0] === 'npr') { const v = H.amef.npr(f); return `<td class="ac-npr ac-npr-${H.amef.nivel(v)}" data-npr="${f.id}">${v || ''}</td>`; }
                if (amef && ['s', 'o', 'd'].includes(c[0])) return `<td><select data-f="${f.id}" data-k="${c[0]}" title="${c[0] === 's' ? 'Severidad' : c[0] === 'o' ? 'Ocurrencia' : 'Detección'} (1 a 10)"><option value=""></option>${[1, 2, 3, 4, 5, 6, 7, 8, 9, 10].map(n => `<option ${String(f[c[0]]) === String(n) ? 'selected' : ''}>${n}</option>`).join('')}</select></td>`;
                return `<td><textarea rows="2" data-f="${f.id}" data-k="${c[0]}">${esc(f[c[0]])}</textarea></td>`;
            };
            host.innerHTML = `<div class="ac-tabla-ed"><table><thead><tr>${cols.map(c => `<th style="min-width:${c[2]}px">${esc(c[1])}</th>`).join('')}${amef ? '<th title="Causa raíz">' + ico('raiz') + '</th>' : ''}<th></th></tr></thead><tbody>
                ${filas.map(f => `<tr>${cols.map(c => celda(f, c)).join('')}${amef ? `<td class="text-center"><input type="checkbox" data-raiz="${f.id}" ${f.raiz ? 'checked' : ''} title="Causa raíz"></td>` : ''}<td>${btn('quitar-fila', 'eliminar', 'Quitar fila', 'ac-btn-mini" data-fila="' + f.id)}</td></tr>`).join('')}
                </tbody></table>${btn('agregar-fila', 'agregar', 'Agregar fila', 'ac-btn-mini')}
                ${amef ? '' : `<div class="ac-campo-concl ac-causa-prob"><textarea id="ac-causa-prob" rows="2" placeholder=" ">${esc(h.datos.causa)}</textarea><label for="ac-causa-prob">Causa más probable</label></div>`}</div>`;
            host.querySelectorAll('textarea[data-f], select[data-f]').forEach(el => {
                const f = h.datos.filas.find(x => x.id === el.dataset.f);
                alEscribir(el, v => { f[el.dataset.k] = v; if (amef) refrescarTablaEd(h); });
                if (el.tagName === 'SELECT') el.addEventListener('change', () => { antes(); f[el.dataset.k] = el.value; refrescarTablaEd(h); pintarRaices(); });
            });
            host.querySelectorAll('[data-raiz]').forEach(el => el.addEventListener('change', () => { antes(); h.datos.filas.find(x => x.id === el.dataset.raiz).raiz = el.checked; pintarRaices(); }));
            const cp = host.querySelector('#ac-causa-prob'); if (cp) alEscribir(cp, v => h.datos.causa = v);
        }
        function refrescarTablaEd(h) {
            if (h.tipo !== 'amef') return;
            host.querySelectorAll('[data-npr]').forEach(td => { const f = h.datos.filas.find(x => x.id === td.dataset.npr); if (!f) return; const v = H.amef.npr(f); td.textContent = v || ''; td.className = 'ac-npr ac-npr-' + H.amef.nivel(v); });
        }

        // ---- panel lateral ----
        function pintarPanel() {
            const h = H0();
            panel.hidden = !h || est.galeria;
            if (!h || est.galeria) return;
            const def = H[h.tipo];
            let html = `<div class="ac-panel-tit">${ico(h.tipo)}<span>${esc(def.nombre)}</span></div>
                <div class="ac-campo-concl"><input id="ac-h-titulo" placeholder=" " value="${esc(h.titulo)}"><label for="ac-h-titulo">Título de la herramienta</label></div>`;
            if (def.arbol) {
                const info = est.sel ? buscar(h.datos.raiz, est.sel) : null;
                if (info) {
                    const n = info.n, prof = info.prof, esCat = !!n.categoria, esRaizArbol = prof === 0;
                    html += `<div class="ac-sel">
                        <div class="ac-sel-rol">${esc(def.rol(prof, n))}</div>
                        <div class="ac-campo-concl"><textarea id="ac-n-texto" rows="4" placeholder=" ">${esc(n.texto)}</textarea><label for="ac-n-texto">Texto</label></div>
                        ${!esRaizArbol && !esCat ? `<label class="ac-check"><input type="checkbox" id="ac-n-raiz" ${n.raiz ? 'checked' : ''}><span>Causa raíz</span></label>` : ''}
                        ${h.tipo === 'arbol' && n.hijos.length > 1 ? `<div class="ac-seg" title="Compuerta lógica entre las causas de este nodo">${[['', 'Sin compuerta'], ['Y', 'Y (todas)'], ['O', 'O (alguna)']].map(o => `<button type="button" class="${(n.compuerta || '') === o[0] ? 'activa' : ''}" data-compuerta="${o[0]}">${o[1]}</button>`).join('')}</div>` : ''}
                        <div class="ac-sel-btns">
                            ${prof < def.max ? btn('hijo', 'hijo', 'Agregar debajo (Tab)') : ''}
                            ${prof > 0 ? btn('hermano', 'hermano', 'Agregar al mismo nivel (Enter)') : ''}
                            ${btn('editar', 'editar', 'Editar texto en el diagrama (F2 o doble clic)')}
                            ${prof > 0 ? btn('subir', 'subir', 'Subir (Alt + ↑)') + btn('bajar', 'bajar', 'Bajar (Alt + ↓)') : ''}
                            ${prof > 0 ? btn('borrar', 'eliminar', 'Quitar con lo que tiene debajo (Supr)') : ''}
                        </div></div>`;
                } else {
                    html += `<div class="ac-sel-vacio">${btn('sel-raiz', 'editar', 'Seleccionar el problema')}</div>`;
                }
                if (h.tipo === 'ishikawa') html += `<div class="ac-panel-sub">${btn('cat', 'agregar', 'Agregar categoría')}${Object.keys(PRESETS).map(k => `<button type="button" class="ac-chip" data-preset="${k}" title="Cambiar las categorías vacías por ${esc(PRESETS[k].join(', '))}">${k}</button>`).join('')}</div>`;
                if (h.tipo === 'afinidad') html += `<div class="ac-panel-sub">${btn('cat', 'agregar', 'Agregar grupo')}</div>`;
                if (h.tipo === 'arbol') html += `<div class="ac-panel-sub">${btn('efecto', 'hijo', 'Agregar efecto o consecuencia (copa del árbol)')}</div>`;
            } else if (h.tipo === 'pareto') {
                const d = h.datos;
                const orden = def.ordenados(d), total = orden.reduce((s, i) => s + (Number(i.valor) || 0), 0);
                let acum = 0; const pct = {};
                orden.forEach(i => { acum += Number(i.valor) || 0; pct[i.id] = total ? Math.round(acum / total * 100) : 0; });
                html += `<div class="ac-campo-concl"><input id="ac-p-unidad" placeholder=" " value="${esc(d.unidad)}"><label for="ac-p-unidad">Medida (frecuencia, costo, horas…)</label></div>
                    <div class="ac-pareto-lista">${d.items.map(i => `
                        <div class="ac-pareto-fila ${est.sel === i.id ? 'activa' : ''}" data-item="${i.id}">
                            <input class="ac-p-causa" data-item="${i.id}" value="${esc(i.causa)}" placeholder="Causa" title="Causa">
                            <input class="ac-p-valor" data-item="${i.id}" type="number" min="0" step="any" value="${esc(i.valor)}" title="${esc(d.unidad || 'Valor')}">
                            <span class="ac-p-pct" title="% acumulado">${pct[i.id] != null ? pct[i.id] + '%' : ''}</span>
                            <label class="ac-p-raiz" title="Causa raíz"><input type="checkbox" data-praiz="${i.id}" ${i.raiz ? 'checked' : ''}>${ico('raiz')}</label>
                            ${btn('quitar-item', 'eliminar', 'Quitar', 'ac-btn-mini" data-fila="' + i.id)}
                        </div>`).join('')}</div>
                    ${btn('agregar-item', 'agregar', 'Agregar causa', 'ac-btn-mini')}`;
            }
            panel.innerHTML = html;
            const t = panel.querySelector('#ac-h-titulo'); alEscribir(t, v => { h.titulo = v; pintarPestanas(); });
            const nt = panel.querySelector('#ac-n-texto');
            if (nt) alEscribir(nt, v => { const i = buscar(h.datos.raiz, est.sel); if (i) i.n.texto = v; });
            const nr = panel.querySelector('#ac-n-raiz');
            if (nr) nr.addEventListener('change', () => cambio(() => { buscar(h.datos.raiz, est.sel).n.raiz = nr.checked; }, true));
            panel.querySelectorAll('[data-compuerta]').forEach(b => b.addEventListener('click', () => cambio(() => { buscar(h.datos.raiz, est.sel).n.compuerta = b.dataset.compuerta || null; })));
            const pu = panel.querySelector('#ac-p-unidad'); if (pu) alEscribir(pu, v => h.datos.unidad = v);
            panel.querySelectorAll('.ac-p-causa').forEach(el => { alEscribir(el, v => { h.datos.items.find(x => x.id === el.dataset.item).causa = v; }); el.addEventListener('focus', () => { est.sel = el.dataset.item; pintarLienzo(); }); });
            panel.querySelectorAll('.ac-p-valor').forEach(el => { alEscribir(el, v => { h.datos.items.find(x => x.id === el.dataset.item).valor = v === '' ? '' : Number(v); actualizarPct(h); }); el.addEventListener('focus', () => { est.sel = el.dataset.item; pintarLienzo(); }); });
            panel.querySelectorAll('[data-praiz]').forEach(el => el.addEventListener('change', () => { antes(); h.datos.items.find(x => x.id === el.dataset.praiz).raiz = el.checked; pintarLienzo(); pintarRaices(); }));
        }
        function actualizarPct(h) {
            const orden = H.pareto.ordenados(h.datos), total = orden.reduce((s, i) => s + (Number(i.valor) || 0), 0);
            let acum = 0;
            panel.querySelectorAll('.ac-p-pct').forEach(el => el.textContent = '');
            orden.forEach(i => { acum += Number(i.valor) || 0; const el = panel.querySelector(`.ac-pareto-fila[data-item="${i.id}"] .ac-p-pct`); if (el) el.textContent = (total ? Math.round(acum / total * 100) : 0) + '%'; });
        }

        function pintarRaices() {
            const r = causasRaiz(est.doc);
            $('.ac-raices').innerHTML = r.length ? `<span class="ac-raices-tit" title="Marcadas como causa raíz en las herramientas">${ico('raiz')}</span>` + r.map(x => `<span class="ac-chip ac-chip-raiz">${esc(x)}</span>`).join('') : '';
        }

        function pintar(mantenerPanel) {
            host.dataset.h = '';
            pintarPestanas(); pintarGaleria(); pintarLienzo();
            if (!mantenerPanel) pintarPanel();
            pintarRaices();
            $('[data-accion="deshacer"]').disabled = !est.pila.length;
            $('[data-accion="rehacer"]').disabled = !est.rehacer.length;
        }

        // ---- operaciones sobre el árbol ----
        function seleccionar(id) { cerrarInline(true); est.sel = id; pintarLienzo(); pintarPanel(); }
        function agregar(tipo) {
            cerrarInline(true);
            const h = H0(); if (!h || !H[h.tipo].arbol) return;
            if (!est.sel) est.sel = h.datos.raiz.id;
            const def = H[h.tipo], info = buscar(h.datos.raiz, est.sel); if (!info) return;
            let nuevo;
            cambio(() => {
                if (tipo === 'hijo' && info.prof < def.max) { nuevo = def.hijoNuevo(info.n, info.prof); info.n.hijos.push(nuevo); }
                else if (tipo === 'efecto' && h.tipo === 'arbol') { nuevo = nodo('', { efecto: true }); h.datos.raiz.hijos.push(nuevo); }
                else if (info.padre) { nuevo = def.hijoNuevo(info.padre, info.prof - 1, info.n); info.padre.hijos.splice(info.padre.hijos.indexOf(info.n) + 1, 0, nuevo); }
                if (nuevo) est.sel = nuevo.id;
            });
            if (nuevo) editarEnLinea(nuevo.id);
        }
        function borrar() {
            cerrarInline(false);
            const h = H0(); if (!h || !est.sel) return;
            const info = buscar(h.datos.raiz, est.sel); if (!info || !info.padre) return;
            cambio(() => { const i = info.padre.hijos.indexOf(info.n); info.padre.hijos.splice(i, 1); est.sel = (info.padre.hijos[i] || info.padre.hijos[i - 1] || info.padre).id; });
        }
        function mover(dir) {
            const h = H0(); if (!h || !est.sel) return;
            const info = buscar(h.datos.raiz, est.sel); if (!info || !info.padre) return;
            const l = info.padre.hijos, i = l.indexOf(info.n), j = i + dir;
            if (j < 0 || j >= l.length) return;
            cambio(() => { l.splice(i, 1); l.splice(j, 0, info.n); });
        }
        // Arrastrar un nodo sobre otro: pasa a ser su hijo (si cabe en la profundidad de la herramienta).
        function reubicar(origen, destino) {
            const h = H0(); if (!h || origen === destino) return false;
            const def = H[h.tipo], raiz = h.datos.raiz;
            const o = buscar(raiz, origen), d = buscar(raiz, destino);
            if (!o || !d || !o.padre || contiene(o.n, destino)) return false;
            let padre = d.n, profPadre = d.prof;
            // Ishikawa/afinidad: soltar una causa sobre una causa del mismo nivel la deja como hermana si no cabe debajo.
            if (profPadre + 1 + profundidad(o.n) > def.max) { if (!d.padre) return false; padre = d.padre; profPadre = d.prof - 1; }
            if (profPadre + 1 + profundidad(o.n) > def.max || (o.n.categoria && profPadre !== 0)) return false;
            if (def.max <= 3 && !o.n.categoria && profPadre === 0 && (h.tipo === 'ishikawa' || h.tipo === 'afinidad')) return false;
            cambio(() => {
                o.padre.hijos.splice(o.padre.hijos.indexOf(o.n), 1);
                if (padre === d.padre) padre.hijos.splice(padre.hijos.indexOf(d.n) + 1, 0, o.n); else padre.hijos.push(o.n);
                est.sel = o.n.id;
            });
            return true;
        }
        function editarEnLinea(id) {
            const h = H0(); if (!h || !H[h.tipo].arbol) return;
            const info = buscar(h.datos.raiz, id); if (!info) return;
            const g = host.querySelector(`[data-id="${id}"].ac-n`); if (!g) return;
            const caja = (g.querySelector('rect') || g).getBoundingClientRect(), base = lienzo.getBoundingClientRect();
            inline.hidden = false;
            inline.style.left = (caja.left - base.left + lienzo.scrollLeft) + 'px';
            inline.style.top = (caja.top - base.top + lienzo.scrollTop) + 'px';
            inline.style.width = Math.max(caja.width, 170) + 'px';
            inline.style.minHeight = Math.max(caja.height, 40) + 'px';
            inline.value = info.n.texto;
            inline.dataset.id = id;
            inline.focus(); inline.select();
        }
        function cerrarInline(guardar) {
            if (inline.hidden) return;
            const id = inline.dataset.id, h = H0();
            inline.hidden = true;
            if (guardar && h) {
                const info = buscar(h.datos.raiz, id);
                if (info && info.n.texto !== inline.value) cambio(() => { info.n.texto = inline.value; });
            }
            lienzo.focus({ preventScroll: true });
        }
        inline.addEventListener('keydown', e => {
            if (e.key === 'Enter' && !e.shiftKey && !e.isComposing && e.keyCode !== 229) { e.preventDefault(); cerrarInline(true); }   // no cortar la escritura con IME
            else if (e.key === 'Escape') { e.preventDefault(); e.stopPropagation(); cerrarInline(false); }
            else if (e.key === 'Tab') { e.preventDefault(); cerrarInline(true); agregar(e.shiftKey ? 'hermano' : 'hijo'); }
        });
        inline.addEventListener('blur', () => cerrarInline(true));

        // ---- puntero en el lienzo: seleccionar, arrastrar nodos, desplazar el fondo ----
        let arr = null;
        lienzo.addEventListener('pointerdown', e => {
            if (e.button !== 0 || e.target === inline || e.target.closest('.ac-tabla-ed')) return;
            const man = e.target.closest('.ac-manija');
            if (man) { e.preventDefault(); agregar(man.dataset.accion); return; }
            const g = e.target.closest('.ac-n');
            const h = H0();
            if (g && h) {
                const id = g.dataset.id;
                if (h.tipo === 'pareto') { seleccionar(id); const inp = panel.querySelector(`.ac-p-causa[data-item="${id}"]`); if (inp) inp.focus(); return; }
                arr = { id, x: e.clientX, y: e.clientY, mov: false, fantasma: null };
            } else arr = { fondo: true, x: e.clientX, y: e.clientY, sl: lienzo.scrollLeft, st: lienzo.scrollTop };
            lienzo.setPointerCapture(e.pointerId);
        });
        lienzo.addEventListener('pointermove', e => {
            if (!arr) return;
            if (arr.fondo) { lienzo.scrollLeft = arr.sl - (e.clientX - arr.x); lienzo.scrollTop = arr.st - (e.clientY - arr.y); lienzo.classList.add('ac-mano'); return; }
            if (!arr.mov && Math.hypot(e.clientX - arr.x, e.clientY - arr.y) < 6) return;
            const h = H0(); const info = buscar(h.datos.raiz, arr.id);
            if (!info || !info.padre) return;
            if (!arr.mov) {
                arr.mov = true;
                arr.fantasma = document.createElement('div'); arr.fantasma.className = 'ac-fantasma';
                arr.fantasma.textContent = info.n.texto || H[h.tipo].rol(info.prof, info.n);
                raizEl.appendChild(arr.fantasma);
                host.querySelector(`[data-id="${arr.id}"].ac-n`)?.classList.add('ac-arrastrado');
            }
            arr.fantasma.style.left = (e.clientX + 12) + 'px'; arr.fantasma.style.top = (e.clientY + 10) + 'px';
            arr.fantasma.hidden = true;
            const bajo = document.elementFromPoint(e.clientX, e.clientY);
            arr.fantasma.hidden = false;
            host.querySelectorAll('.ac-destino-ok').forEach(x => x.classList.remove('ac-destino-ok'));
            const dest = bajo && bajo.closest('[data-id]');
            arr.destino = dest && dest.dataset.id !== arr.id && !contiene(info.n, dest.dataset.id) ? dest.dataset.id : null;
            if (arr.destino) dest.classList.add('ac-destino-ok');
        });
        const soltar = e => {
            if (!arr) return;
            const a = arr; arr = null;
            lienzo.classList.remove('ac-mano');
            if (a.fondo) { if (Math.hypot(e.clientX - a.x, e.clientY - a.y) < 4 && est.sel) seleccionar(null); return; }
            if (a.fantasma) a.fantasma.remove();
            if (a.mov) { if (!(a.destino && reubicar(a.id, a.destino))) pintarLienzo(); }
            else seleccionar(a.id);
        };
        lienzo.addEventListener('pointerup', soltar);
        lienzo.addEventListener('pointercancel', soltar);
        lienzo.addEventListener('dblclick', e => { const g = e.target.closest('.ac-n'); if (g && H0() && H[H0().tipo].arbol) { seleccionar(g.dataset.id); setTimeout(() => editarEnLinea(g.dataset.id), 10); } });
        lienzo.addEventListener('wheel', e => { if (!e.ctrlKey) return; e.preventDefault(); zoom(e.deltaY < 0 ? 1.1 : 1 / 1.1); }, { passive: false });

        // ---- teclado ----
        function teclado(e) {
            if (!document.body.contains(raizEl)) return;
            // Con el aviso de cambios abierto: Esc = seguir editando; el resto de atajos no actúa por debajo.
            if (!$('.ac-aviso').hidden) { if (e.key === 'Escape') { e.preventDefault(); e.stopPropagation(); $('.ac-aviso').hidden = true; lienzo.focus({ preventScroll: true }); } return; }
            const escribiendo = e.target.closest && e.target.closest('input, textarea, select');
            if ((e.ctrlKey || e.metaKey) && !escribiendo && e.key.toLowerCase() === 'z') { e.preventDefault(); e.shiftKey ? rehacer() : deshacer(); return; }
            if ((e.ctrlKey || e.metaKey) && !escribiendo && e.key.toLowerCase() === 'y') { e.preventDefault(); rehacer(); return; }
            if ((e.ctrlKey || e.metaKey) && e.key.toLowerCase() === 's') { e.preventDefault(); guardar(); return; }
            if (e.key === 'Escape' && !escribiendo) { e.preventDefault(); if (est.galeria && est.doc.herramientas.length) { est.galeria = false; pintar(); } else if (est.sel) seleccionar(null); else intentarCerrar(); return; }
            if (escribiendo) return;
            const h = H0(); if (!h || !H[h.tipo].arbol) return;
            if (e.key === 'Tab') { e.preventDefault(); if (est.sel) agregar(e.shiftKey ? 'hermano' : 'hijo'); else seleccionar(h.datos.raiz.id); }
            else if (e.key === 'Enter' && est.sel) { e.preventDefault(); agregar('hermano'); }
            else if (e.key === 'F2' && est.sel) { e.preventDefault(); editarEnLinea(est.sel); }
            else if ((e.key === 'Delete' || e.key === 'Backspace') && est.sel) { e.preventDefault(); borrar(); }
            else if (e.altKey && e.key === 'ArrowUp') { e.preventDefault(); mover(-1); }
            else if (e.altKey && e.key === 'ArrowDown') { e.preventDefault(); mover(1); }
            else if (/^[\p{L}\p{N}]$/u.test(e.key) && est.sel && !e.ctrlKey && !e.altKey && !e.metaKey) { editarEnLinea(est.sel); inline.value = e.key; e.preventDefault(); }
        }
        document.addEventListener('keydown', teclado, true);

        // ---- clics en botones ----
        raizEl.addEventListener('click', e => {
            const q = e.target.closest('[data-quitar]');
            if (q) { e.stopPropagation(); const i = Number(q.dataset.quitar); cambio(() => { est.doc.herramientas.splice(i, 1); est.doc.activa = Math.max(0, Math.min(est.doc.activa, est.doc.herramientas.length - 1)); est.sel = null; if (!est.doc.herramientas.length) est.galeria = true; }); return; }
            const nueva = e.target.closest('[data-nueva]');
            if (nueva) { e.stopPropagation(); agregarHerramienta(nueva.dataset.nueva, nueva.dataset.preset); return; }
            const pest = e.target.closest('.ac-pest');
            if (pest) { est.doc.activa = Number(pest.dataset.i); est.galeria = false; est.sel = null; pintar(); ajustar(); return; }
            const pre = e.target.closest('.ac-panel [data-preset]');
            if (pre) {
                const h = H0();
                cambio(() => {
                    const r = h.datos.raiz, nombres = PRESETS[pre.dataset.preset];
                    const conCausas = r.hijos.filter(c => c.hijos.length);
                    const usadas = new Set(conCausas.map(c => c.texto.trim().toLowerCase()));
                    r.hijos = conCausas.concat(nombres.filter(n => !usadas.has(n.toLowerCase())).map(n => nodo(n, { categoria: true })));
                });
                return;
            }
            const b = e.target.closest('[data-accion]'); if (!b || b.classList.contains('ac-manija')) return;
            const h = H0();
            switch (b.dataset.accion) {
                case 'cerrar': intentarCerrar(); break;
                case 'guardar': guardar(); break;
                case 'descartar': cerrar(true); break;
                case 'seguir': $('.ac-aviso').hidden = true; lienzo.focus({ preventScroll: true }); break;
                case 'galeria': est.galeria = !est.galeria || !est.doc.herramientas.length; est.sel = null; pintar(); if (!est.galeria) ajustar(); break;
                case 'deshacer': deshacer(); break;
                case 'rehacer': rehacer(); break;
                case 'acercar': zoom(1.2); break;
                case 'alejar': zoom(1 / 1.2); break;
                case 'ajustar': ajustar(); break;
                case 'png': descargar('png', b); break;
                case 'svg': descargar('svg', b); break;
                case 'hijo': agregar('hijo'); break;
                case 'efecto': agregar('efecto'); break;
                case 'hermano': agregar('hermano'); break;
                case 'editar': if (est.sel) editarEnLinea(est.sel); break;
                case 'subir': mover(-1); break;
                case 'bajar': mover(1); break;
                case 'borrar': borrar(); break;
                case 'sel-raiz': if (h) seleccionar(h.datos.raiz.id); break;
                case 'cat': if (h) cambio(() => { const n = H[h.tipo].hijoNuevo(h.datos.raiz, 0); h.datos.raiz.hijos.push(n); est.sel = n.id; }); if (est.sel) editarEnLinea(est.sel); break;
                case 'agregar-fila': if (h) { cambio(() => h.datos.filas.push(h.tipo === 'amef' ? filaAmef() : { id: nid(), dim: '', es: '', noes: '', dist: '' })); host.dataset.h = ''; pintarLienzo(); } break;
                case 'quitar-fila': if (h) { cambio(() => { h.datos.filas = h.datos.filas.filter(f => f.id !== b.dataset.fila); }); host.dataset.h = ''; pintarLienzo(); } break;
                case 'agregar-item': if (h) { cambio(() => h.datos.items.push({ id: nid(), causa: '', valor: 0 })); const l = panel.querySelectorAll('.ac-p-causa'); if (l.length) l[l.length - 1].focus(); } break;
                case 'quitar-item': if (h) cambio(() => { h.datos.items = h.datos.items.filter(i => i.id !== b.dataset.fila); }); break;
            }
        });

        // ---- descargar ----
        async function descargar(fmtSalida, boton) {
            const fondo = await elegirFondo(boton); if (!fondo) return;
            const svg = svgCompleto(est.doc, fondo === 'tema' ? paletaTema() : PALETAS.documento); if (!svg) return;
            const nombre = 'analisis-causa' + (opc && opc.titulo ? '-' + String(opc.titulo).replace(/[^\w\-]+/g, '_') : '') + (fondo === 'tema' ? '-tema' : '');
            if (fmtSalida === 'svg') return bajar(new Blob([svg], { type: 'image/svg+xml' }), nombre + '.svg');
            svgAPng(svg, nombre);
        }

        // ---- guardar / cerrar ----
        async function guardar() {
            cerrarInline(true);
            const svg = svgCompleto(est.doc);
            const json = est.doc.herramientas.length || !vacio(est.doc.conclusion) ? limpiar(est.doc) : null;
            try { await est.dotnet.invokeMethodAsync('Guardar', json, svg, resumen(est.doc)); }
            catch (err) { console.error(err); mostrarAviso('No se pudo guardar', 'El plan no recibió el análisis. Intente guardar de nuevo, salga sin guardar o siga editando.'); return; }
            cerrar(true);
        }
        function mostrarAviso(tit, txt) {
            $('.ac-aviso-tit').textContent = tit; $('.ac-aviso-txt').textContent = txt;
            $('.ac-aviso').hidden = false;
            requestAnimationFrame(() => $('.ac-aviso [data-accion="guardar"]').focus());
        }
        function intentarCerrar() {
            cerrarInline(true);
            if (sucio()) mostrarAviso('Cambios sin guardar', 'Este análisis tiene cambios que aún no están en el plan. Guárdelos, salga sin guardarlos o siga editando.');
            else cerrar(true);
        }
        function cerrar() {
            document.removeEventListener('keydown', teclado, true);
            raizEl.remove();
            document.documentElement.classList.remove('ac-abierto');
            actual = null;
            try { est.dotnet.invokeMethodAsync('Cerrado'); } catch { }
        }

        actual = { cerrar };
        pintar();
        requestAnimationFrame(() => { ajustar(); lienzo.focus({ preventScroll: true }); });
    }

    function b64(s) { return btoa(unescape(encodeURIComponent(s))); }
    // Menú «¿Con qué fondo?» junto a un botón de descarga. Devuelve 'tema', 'blanco' o null (cerrado sin elegir).
    function elegirFondo(boton) {
        return new Promise(res => {
            document.querySelectorAll('.ac-fondo-menu').forEach(x => x.remove());
            const m = document.createElement('div');
            m.className = 'ac-fondo-menu';
            m.innerHTML = `<div class="ac-fondo-tit">Descargar con</div>
                <button type="button" data-fondo="tema" title="Los colores del tema que está usando"><span class="ac-fondo-muestra ac-fm-tema"></span>Acorde al tema</button>
                <button type="button" data-fondo="blanco" title="Fondo blanco, para documentos e impresión"><span class="ac-fondo-muestra ac-fm-blanco"></span>Fondo blanco</button>`;
            document.body.appendChild(m);
            const b = boton.getBoundingClientRect();
            m.style.top = (b.bottom + 6) + 'px';
            m.style.left = Math.max(8, Math.min(b.left, window.innerWidth - m.offsetWidth - 8)) + 'px';
            const fin = v => { document.removeEventListener('pointerdown', fuera, true); document.removeEventListener('keydown', tecla, true); m.remove(); res(v); };
            const fuera = e => { if (!m.contains(e.target)) fin(null); };
            const tecla = e => { if (e.key === 'Escape') { e.preventDefault(); e.stopPropagation(); fin(null); } };
            m.addEventListener('click', e => { const x = e.target.closest('[data-fondo]'); if (x) fin(x.dataset.fondo); });
            setTimeout(() => { document.addEventListener('pointerdown', fuera, true); document.addEventListener('keydown', tecla, true); }, 0);
        });
    }
    function svgAPng(svg, nombre) {
        const img = new Image();
        img.onload = () => {
            const c = document.createElement('canvas'), k = 2;
            c.width = img.width * k; c.height = img.height * k;
            const g = c.getContext('2d'); g.scale(k, k); g.drawImage(img, 0, 0);
            c.toBlob(b => bajar(b, nombre + '.png'), 'image/png');
        };
        img.src = 'data:image/svg+xml;base64,' + b64(svg);
    }

    // ---------------- miniaturas acordes al tema (la app las pinta desde la estructura, dentro de <img>) ----------------
    const minis = new Map();   // img → json
    function pintarMini(img, json) {
        if (!img) return;
        if (json === null || json === undefined) { minis.delete(img); return; }
        minis.set(img, json);
        try {
            const svg = svgCompleto(leer(json), paletaTema());
            if (svg) img.src = 'data:image/svg+xml;base64,' + b64(svg);
        } catch (e) { console.error('Análisis de causa: no se pudo dibujar la miniatura', e); }
    }
    // Al cambiar de tema (html[data-tema]) se vuelven a pintar las miniaturas a la vista.
    new MutationObserver(() => {
        minis.forEach((json, img) => { if (!img.isConnected) minis.delete(img); else pintarMini(img, json); });
    }).observe(document.documentElement, { attributes: true, attributeFilter: ['data-tema'] });
    function bajar(blob, nombre) { const a = document.createElement('a'); a.href = URL.createObjectURL(blob); a.download = nombre; document.body.appendChild(a); a.click(); setTimeout(() => { URL.revokeObjectURL(a.href); a.remove(); }, 500); }

    // ---------------- visor a pantalla completa (miniaturas de tabla, ficha y widget) ----------------
    function maximizar(uri, titulo, json) {
        let docV = null;
        if (json) { try { docV = leer(json); const svgT = svgCompleto(docV, paletaTema()); if (svgT) uri = 'data:image/svg+xml;base64,' + b64(svgT); } catch (e) { docV = null; } }
        if (!uri) return;
        const v = document.createElement('div');
        v.className = 'ac-visor';
        v.innerHTML = `<div class="ac-visor-cab"><span class="ac-visor-tit">${esc(titulo || 'Análisis de causa')}</span>
            ${btn('alejar', 'alejar', 'Alejar')}${btn('acercar', 'acercar', 'Acercar')}${btn('ajustar', 'ajustar', 'Ajustar')}${btn('png', 'png', 'Descargar imagen PNG')}${btn('cerrar', 'cerrar', 'Cerrar (Esc)')}</div>
            <div class="ac-visor-area"><img alt="Análisis de causa" draggable="false"></div>`;
        document.body.appendChild(v);
        const img = v.querySelector('img'), area = v.querySelector('.ac-visor-area');
        let z = 1, nat = { w: 0, h: 0 };
        const aplicar = () => { img.style.width = (nat.w * z) + 'px'; img.style.height = (nat.h * z) + 'px'; };
        const ajustarV = () => { z = Math.min((area.clientWidth - 40) / nat.w, (area.clientHeight - 40) / nat.h, 2.5); aplicar(); };
        img.onload = () => { nat = { w: img.naturalWidth || 800, h: img.naturalHeight || 600 }; ajustarV(); };
        img.src = uri;
        const cerrarV = () => { document.removeEventListener('keydown', tecla, true); v.remove(); };
        const tecla = e => { if (e.key === 'Escape') { e.preventDefault(); e.stopPropagation(); cerrarV(); } };
        document.addEventListener('keydown', tecla, true);
        v.addEventListener('click', e => {
            const b = e.target.closest('[data-accion]');
            if (!b) { if (e.target === area) cerrarV(); return; }
            ({ cerrar: cerrarV, acercar: () => { z *= 1.25; aplicar(); }, alejar: () => { z /= 1.25; aplicar(); }, ajustar: ajustarV,
               png: () => docV ? elegirFondo(b).then(f => { if (!f) return; const sv = svgCompleto(docV, f === 'tema' ? paletaTema() : PALETAS.documento); if (sv) svgAPng(sv, 'analisis-causa' + (f === 'tema' ? '-tema' : '')); }) : (() => { const c = document.createElement('canvas'), k = 2; c.width = nat.w * k; c.height = nat.h * k; const g = c.getContext('2d'); g.scale(k, k); g.drawImage(img, 0, 0, nat.w, nat.h); c.toBlob(bl => bajar(bl, 'analisis-causa.png'), 'image/png'); })() })[b.dataset.accion]?.();
        });
        area.addEventListener('wheel', e => { e.preventDefault(); z *= e.deltaY < 0 ? 1.1 : 1 / 1.1; z = Math.max(0.1, Math.min(z, 8)); aplicar(); }, { passive: false });
        let pan = null;
        area.addEventListener('pointerdown', e => { if (e.target !== img) return; pan = { x: e.clientX, y: e.clientY, sl: area.scrollLeft, st: area.scrollTop }; area.setPointerCapture(e.pointerId); area.classList.add('ac-mano'); });
        area.addEventListener('pointermove', e => { if (!pan) return; area.scrollLeft = pan.sl - (e.clientX - pan.x); area.scrollTop = pan.st - (e.clientY - pan.y); });
        area.addEventListener('pointerup', () => { pan = null; area.classList.remove('ac-mano'); });
    }

    window.sigCausas = {
        abrir, maximizar, pintarMini,
        // Para pruebas y para regenerar la imagen desde la estructura guardada.
        svg: json => svgCompleto(leer(json)),
        resumen: json => resumen(leer(json)),
        herramientas: () => ORDEN.map(t => ({ tipo: t, nombre: H[t].nombre }))
    };
})();
