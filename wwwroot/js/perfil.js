// Vista Perfil de Avance de obra: planta-perfil de la línea a partir de la tabla de torres (SVG, sin dependencias).
// Datos: torres { id, n, tipo, fn ('Suspensión'|'Retención'|'Pórtico'|''), x (abscisa), z (cota diseño), zc (cota construcción),
// h (altura total), hc (amarre conductor inferior), defl (°, signo de la tabla), lat, lon } y tiros { id, nombre, torres }.
// No dibuja catenarias ni terreno entre torres: no hay datos del conductor ni perfil del terreno (se avisa en la ficha).
const NS = 'http://www.w3.org/2000/svg';
// Nombres y tipos vienen de la tabla de torres (texto del usuario): se escapan antes de ir a innerHTML.
const esc = (v) => String(v ?? '').replace(/[&<>"']/g, c => ({ '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;', "'": '&#39;' }[c]));
const ICO = {
    cuerda: '<path d="M2 8l20 4"/><circle cx="2" cy="8" r="1.5"/><circle cx="22" cy="12" r="1.5"/>',
    secc: '<path d="M4 3v18M20 3v18"/><path d="M4 12h16" stroke-dasharray="3 2"/>',
    tiros: '<path d="M3 20 6 6l3 14M15 20l3-14 3 14"/><path d="M6 7q6 5 12 0"/>',
    etq: '<path d="M4 5h16v10H9l-5 4z"/>',
    cons: '<path d="M3 17l5-5 4 3 9-8"/><path d="M3 20l5-4 4 2 9-6" stroke-dasharray="2 2"/>',
    guit: '<rect x="3" y="4" width="18" height="16" rx="1"/><path d="M3 10h18M3 15h18M9 10v10M15 10v10"/>',
    planta: '<path d="M3 16l6-6 5 4 7-7"/><circle cx="9" cy="10" r="1.5"/><circle cx="14" cy="14" r="1.5"/>',
    avance: '<circle cx="12" cy="12" r="8"/><path d="M12 4a8 8 0 0 1 0 16z" fill="currentColor" stroke="none"/>',
    todo: '<path d="M3 9V3h6M21 9V3h-6M3 15v6h6M21 15v6h-6"/>',
    pc: '<path d="M4 9V4h5M20 9V4h-5M4 15v5h5M20 15v5h-5"/><rect x="8" y="8" width="8" height="8" rx="1"/>',
    exag: '<path d="M12 3v18M8 7l4-4 4 4M8 17l4 4 4-4"/>',
    buscar: '<circle cx="11" cy="11" r="6.5"/><path d="m16 16 4.5 4.5"/>',
    torre: '<path d="M12 2 7 22M12 2l5 20M8.6 15h6.8M10 9h4"/>',
    tiro: '<path d="M3 8q9 8 18 0"/><path d="M3 4v16M21 4v16"/>',
    hdv: '<path d="M6 3h9l4 4v14H6z"/><path d="M9 12h7M9 16h5"/>',
};
const svgIco = (k, s = 16) => `<svg viewBox="0 0 24 24" width="${s}" height="${s}" fill="none" stroke="currentColor" stroke-width="1.8" stroke-linecap="round" stroke-linejoin="round">${ICO[k]}</svg>`;
const el = (n, a, p) => { const e = document.createElementNS(NS, n); for (const k in a) e.setAttribute(k, a[k]); if (p) p.appendChild(e); return e; };
const tx = (p, x, y, s, c, anc) => { const e = el('text', { x, y, class: c, 'text-anchor': anc || 'middle' }, p); e.textContent = s; return e; };
const nf = d => n => n.toLocaleString('es-CO', { minimumFractionDigits: d, maximumFractionDigits: d });
const f0 = nf(0), f1 = nf(1), f2 = nf(2);
const km = x => 'K' + Math.floor(x / 1000) + '+' + String(Math.round(x % 1000)).padStart(3, '0');
const sg = v => (v > 0 ? '+' : '') + f2(v);
const colAv = p => p == null ? '#a6aeb8' : `hsl(${Math.round(Math.max(0, Math.min(100, p)) * 1.2)} 62% 42%)`;
const ML = 56, MR = 14;

export function crear(raiz, dotnet) {
    raiz.innerHTML = `
<div class="pf-visor">
  <div class="pf-barra">
    <label class="av3d-grupo av3d-buscar-caja pf-buscar" title="Ir a torre">${svgIco('buscar', 15)}<input class="av3d-buscar" placeholder="Torre…" list="pf-lt"><datalist id="pf-lt"></datalist></label>
    <div class="av3d-grupo">
      <button type="button" class="av3d-btn activo" data-k="cuerda" title="Conductor inferior (cuerda entre amarres)">${svgIco('cuerda')}</button>
      <button type="button" class="av3d-btn activo" data-k="etq" title="Nombres de torres">${svgIco('etq')}</button>
      <button type="button" class="av3d-btn" data-k="cons" title="Cota de construcción contra cota de diseño">${svgIco('cons')}</button>
    </div>
    <div class="av3d-grupo">
      <button type="button" class="av3d-btn activo" data-b="secc" title="Secciones entre retenciones">${svgIco('secc')}</button>
      <button type="button" class="av3d-btn" data-b="tiros" title="Tiros de tendido">${svgIco('tiros')}</button>
      <button type="button" class="av3d-btn" data-k="avance" title="Color por avance de obra">${svgIco('avance')}</button>
    </div>
    <div class="av3d-grupo">
      <button type="button" class="av3d-btn activo" data-k="guit" title="Guitarra de datos">${svgIco('guit')}</button>
      <button type="button" class="av3d-btn activo" data-k="planta" title="Planta del trazado">${svgIco('planta')}</button>
      <label class="pf-exag" title="Exageración vertical">${svgIco('exag', 15)}<select><option value="0">Auto</option><option value="5">×5</option><option value="10">×10</option><option value="20">×20</option></select></label>
    </div>
    <div class="av3d-grupo">
      <button type="button" class="av3d-btn" data-a="todo" title="Ver toda la línea">${svgIco('todo')}</button>
      <button type="button" class="av3d-btn" data-a="pc" title="Pantalla completa">${svgIco('pc')}</button>
    </div>
  </div>
  <svg class="pf-svg"></svg>
  <svg class="pf-mini"></svg>
  <div class="pf-ley"></div>
</div>
<aside class="pf-lado">
  <div class="pf-ficha"><div class="pf-vacio">—</div></div>
  <div class="pf-tabla-caja">
    <div class="pf-tabs"><button type="button" class="on" data-t="sec">Secciones <small></small></button><button type="button" data-t="dif">Δ cota <small></small></button></div>
    <div class="pf-tabla" data-p="sec"><table><thead><tr><th>Sección</th><th title="Vanos">N</th><th>Long. m</th><th title="Vano regulador">VR m</th></tr></thead><tbody></tbody></table></div>
    <div class="pf-tabla" data-p="dif" hidden><table><thead><tr><th>Torre</th><th title="Cota de diseño">Diseño</th><th title="Cota de construcción">Constr.</th><th>Δ m</th></tr></thead><tbody></tbody></table></div>
  </div>
</aside>
<div class="pf-tip"></div>`;
    const q = s => raiz.querySelector(s);
    const sv = q('.pf-svg'), mi = q('.pf-mini'), tip = q('.pf-tip'), ficha = q('.pf-ficha');
    let T = [], S = [], TI = [], X0 = 0, X1 = 1, w0 = 0, w1 = 1, sel = null, exag = 0, pct = {}, vivo = true;
    const ver = { cuerda: 1, etq: 1, cons: 0, guit: 1, planta: 1, avance: 0, banda: 'secc' };

    function preparar(datos) {
        T = (datos.torres || []).filter(t => t.x != null && t.z != null).sort((a, b) => a.x - b.x);
        const lat0 = T.length ? T.reduce((s, t) => s + (t.lat || 0), 0) / T.length : 0, kLon = 111320 * Math.cos(lat0 * Math.PI / 180);
        T.forEach((t, i) => {
            t.i = i; t.ret = t.fn !== 'Suspensión'; t.za = t.hc != null ? t.z + t.hc : t.z; t.zt = t.h != null ? t.z + t.h : t.z;
            t.dz = t.zc != null ? t.zc - t.z : null; t.E = (t.lon || 0) * kLon; t.N = (t.lat || 0) * 110574;
        });
        S = []; let ini = 0;
        for (let i = 1; i < T.length; i++) if (T[i].ret || i === T.length - 1) {
            let s1 = 0, s3 = 0; for (let k = ini; k < i; k++) { const a = T[k + 1].x - T[k].x; s1 += a; s3 += a ** 3; }
            S.push({ id: S.length + 1, a: ini, b: i, n: i - ini, L: s1, vr: s1 > 0 ? Math.sqrt(s3 / s1) : 0 }); ini = i;
        }
        T.forEach(t => t.sec = S.find(s => t.i >= s.a && t.i < s.b) || S[S.length - 1]);
        const pos = new Map(T.map(t => [t.id, t]));
        TI = (datos.tiros || []).map(r => { const ts = r.torres.map(id => pos.get(id)).filter(Boolean); return ts.length ? { nombre: r.nombre, a: Math.min(...ts.map(t => t.x)), b: Math.max(...ts.map(t => t.x)) } : null; }).filter(Boolean);
        X0 = T.length ? T[0].x : 0; X1 = T.length ? T[T.length - 1].x : 1; if (X1 <= X0) X1 = X0 + 1;
        w0 = X0; w1 = Math.min(X1, X0 + 5000);
        tablas();
        q('#pf-lt').innerHTML = T.map(t => `<option value="${esc(t.n)}"></option>`).join('');
        ficha.innerHTML = `<div class="pf-vacio">${T.length ? '—' : 'Sin torres con abscisa y cota en la tabla de torres'}</div>`;
    }

    function dibujar() {
        if (!vivo) return;
        const W = sv.clientWidth, H = sv.clientHeight; sv.replaceChildren();
        if (!T.length || !W) { leyenda(1); return; }
        const GH = ver.guit ? 6 * 15 + 6 : 0, PH = ver.planta ? 110 : 0, mt = 48, pb = H - 24 - GH - PH;
        const vis = T.filter(t => t.x >= w0 - 1500 && t.x <= w1 + 1500), inw = vis.filter(t => t.x >= w0 && t.x <= w1), base = inw.length ? inw : vis;
        const sxk = (W - ML - MR) / (w1 - w0), sx = x => ML + (x - w0) * sxk;
        let zmin = Math.min(...base.map(t => Math.min(t.z, ver.cons && t.zc != null ? t.zc : t.z))) - 15, zmax = Math.max(...base.map(t => t.zt)) + 15;
        if (exag) { const need = (pb - mt) / (sxk * exag), c = (zmin + zmax) / 2; if (need > zmax - zmin) { zmin = c - need / 2; zmax = c + need / 2; } }
        const syk = (pb - mt) / (zmax - zmin), sy = z => mt + (zmax - z) * syk;
        el('clipPath', { id: 'pf-cp' }, el('defs', {}, sv)).appendChild(el('rect', { x: ML, y: 0, width: W - ML - MR, height: H }));
        const g = el('g', { 'clip-path': 'url(#pf-cp)' }, sv);
        const bandas = ver.banda === 'secc' ? S.map(s => ({ a: T[s.a].x, b: T[s.b].x, t: 'S' + s.id + ' · VR ' + f0(s.vr) + ' m' }))
            : ver.banda === 'tiros' ? TI.map(r => ({ a: r.a, b: r.b, t: r.nombre })) : [];
        bandas.forEach((b, k) => {
            const a = sx(b.a), c = sx(b.b); if (c < ML || a > W) return;
            el('rect', { x: a, y: mt - 26, width: Math.max(0, c - a), height: pb - mt + 26, fill: k % 2 ? 'var(--pf-banda)' : 'transparent' }, g);
            if (c - a > 80) tx(g, (a + c) / 2, mt - 14, b.t, 'pf-ax');
        });
        const paso = [5, 10, 20, 25, 50, 100, 200, 250, 500].find(p => (zmax - zmin) / p <= 7) || 1000;
        for (let z = Math.ceil(zmin / paso) * paso; z <= zmax; z += paso) { el('line', { x1: ML, x2: W - MR, y1: sy(z), y2: sy(z), class: 'pf-rej' }, sv); tx(sv, ML - 6, sy(z) + 3, f0(z), 'pf-ax', 'end'); }
        const px = [100, 200, 250, 500, 1000, 2000, 5000, 10000, 20000].find(p => (w1 - w0) / p <= 8) || 50000;
        for (let x = Math.ceil(w0 / px) * px; x <= w1; x += px) tx(sv, sx(x), pb + 14, km(x), 'pf-ax');
        const d = vis.map((t, k) => (k ? 'L' : 'M') + sx(t.x) + ',' + sy(t.z)).join('');
        el('path', { d: d + `L${sx(vis[vis.length - 1].x)},${pb}L${sx(vis[0].x)},${pb}Z`, class: 'pf-suelo' }, g);
        el('path', { d, class: 'pf-suelo-l' }, g);
        for (let k = 0; k < vis.length - 1; k++) {
            const a = vis[k], b = vis[k + 1], on = sel && sel.v === a.i;
            const hit = el('line', { x1: sx(a.x), y1: sy(a.za), x2: sx(b.x), y2: sy(b.za), stroke: 'transparent', 'stroke-width': 12, style: 'cursor:pointer' }, g);
            if (ver.cuerda || on) el('line', { x1: sx(a.x), y1: sy(a.za), x2: sx(b.x), y2: sy(b.za), class: on ? 'pf-cuerda on' : 'pf-cuerda', 'pointer-events': 'none' }, g);
            hit.onclick = () => elegir({ v: a.i }); hit.onmousemove = e => verTip(e, `Vano ${a.n} – ${b.n}: ${f0(b.x - a.x)} m`); hit.onmouseleave = () => tip.style.display = 'none';
        }
        const dens = (W - ML - MR) / Math.max(1, inw.length);
        vis.forEach(t => {
            const x = sx(t.x), on = sel && sel.t === t.i, c = ver.avance ? colAv(pct[t.id]) : t.ret ? 'var(--pf-ret)' : 'var(--pf-sus)';
            const gt = el('g', { style: 'cursor:pointer' }, g);
            el('rect', { x: x - 7, y: sy(t.zt) - 4, width: 14, height: sy(t.z) - sy(t.zt) + 8, fill: 'transparent' }, gt);
            el('line', { x1: x, x2: x, y1: sy(t.z), y2: sy(t.zt), stroke: c, 'stroke-width': on ? 4 : t.ret ? 2.6 : 1.8 }, gt);
            if (t.hc != null) {
                if (t.ret) el('rect', { x: x - (on ? 4 : 3), y: sy(t.za) - (on ? 4 : 3), width: on ? 8 : 6, height: on ? 8 : 6, fill: c }, gt);
                else el('circle', { cx: x, cy: sy(t.za), r: on ? 4 : 2.6, class: 'pf-amarre', stroke: c }, gt);
            }
            if (ver.cons && t.dz != null && Math.abs(t.dz) >= 0.01) {
                el('line', { x1: x - 6, x2: x + 6, y1: sy(t.zc), y2: sy(t.zc), class: 'pf-cons' }, gt);
                if (Math.abs(t.dz) >= 1 && dens > 30) tx(gt, x + 8, sy(t.zc) + 3, (t.dz > 0 ? '+' : '') + f1(t.dz), 'pf-ax pf-cons-t', 'start');
            }
            if (ver.etq && (dens > 26 || on)) tx(gt, x, sy(t.zt) - 6, t.n, 'pf-etq');
            gt.onclick = () => elegir({ t: t.i });
            gt.onmousemove = e => verTip(e, `${t.n} · ${t.tipo || '?'}${t.fn ? ' · ' + t.fn : ''}${ver.avance && pct[t.id] != null ? ' · ' + f1(pct[t.id]) + ' %' : ''}`);
            gt.onmouseleave = () => tip.style.display = 'none';
        });
        el('line', { x1: ML, x2: W - MR, y1: pb, y2: pb, class: 'pf-base' }, sv);
        let y = pb + 24;
        if (ver.guit) {
            const filas = [['Torre', t => t.n], ['Tipo', t => t.tipo || '—'], ['Cota (m)', t => f2(t.z)], ['Abscisa', t => km(t.x)], ['Vano (m)', null],
                ['Deflexión', t => t.defl == null ? '—' : Math.abs(t.defl) < 0.005 ? '0°' : sg(t.defl) + '°']];
            const gg = el('g', {}, sv); el('rect', { x: 0, y: y - 4, width: W, height: GH, class: 'pf-guit' }, gg);
            filas.forEach((f, r) => { tx(gg, 6, y + r * 15 + 8, f[0], 'pf-gl', 'start'); el('line', { x1: 0, x2: W, y1: y + r * 15 - 3, y2: y + r * 15 - 3, class: 'pf-rej' }, gg); });
            const gc = el('g', { 'clip-path': 'url(#pf-cp)' }, gg), show = dens > 44;
            inw.forEach(t => { const x = sx(t.x); el('line', { x1: x, x2: x, y1: y - 3, y2: y + 4 * 15 - 3, class: 'pf-rej' }, gc); if (show) filas.forEach((f, r) => { if (f[1]) tx(gc, x, y + r * 15 + 8, f[1](t), 'pf-gv'); }); });
            vis.forEach((t, k) => { const b = vis[k + 1]; if (b && sx(b.x) - sx(t.x) > 34) tx(gc, (sx(t.x) + sx(b.x)) / 2, y + 4 * 15 + 8, f0(b.x - t.x), 'pf-gv'); });
            if (!show) tx(gg, W / 2, y + 30, '···', 'pf-ax');
            y += GH;
        }
        // Planta rectificada: cada torre en su abscisa (alineada con el perfil) y su desvío lateral respecto a la recta
        // entre los extremos visibles; si no cabe, la escala transversal se reduce y se indica.
        if (ver.planta && base.length > 1 && base.some(t => t.lat)) {
            const gp = el('g', {}, sv); el('rect', { x: 0, y, width: W, height: PH - 4, class: 'pf-planta' }, gp);
            const a = base[0], b = base[base.length - 1], ang = Math.atan2(b.N - a.N, b.E - a.E);
            const V = t => -(t.E - a.E) * Math.sin(ang) + (t.N - a.N) * Math.cos(ang);
            const vs = vis.map(V), bv = base.map(V), vmin = Math.min(...bv), vmax = Math.max(...bv);
            const kv = Math.min(sxk, (PH - 34) / Math.max(1, vmax - vmin)), cy = y + (PH - 4) / 2, vc = (vmax + vmin) / 2;
            tx(gp, 6, y + 12, 'Planta' + (kv < sxk * 0.98 ? ' · transversal ×' + f1(kv / sxk) : ''), 'pf-gl', 'start');
            const gc = el('g', { 'clip-path': 'url(#pf-cp)' }, gp), Y = k => cy - (vs[k] - vc) * kv;
            el('path', { d: vis.map((t, k) => (k ? 'L' : 'M') + sx(t.x) + ',' + Y(k)).join(''), class: 'pf-eje' }, gc);
            vis.forEach((t, k) => {
                const c = ver.avance ? colAv(pct[t.id]) : t.ret ? 'var(--pf-ret)' : 'var(--pf-sus)', on = sel && sel.t === t.i;
                const m = t.ret ? el('rect', { x: sx(t.x) - 3.5, y: Y(k) - 3.5, width: 7, height: 7, fill: c }, gc)
                    : el('circle', { cx: sx(t.x), cy: Y(k), r: on ? 4.5 : 3, class: 'pf-amarre', stroke: c }, gc);
                m.style.cursor = 'pointer'; m.onclick = () => elegir({ t: t.i });
                if (t.defl && Math.abs(t.defl) >= 0.5 && dens > 36) tx(gc, sx(t.x), Y(k) + 15, (t.defl > 0 ? '+' : '') + f1(t.defl) + '°', 'pf-ax');
            });
        }
        leyenda(syk / sxk);
        miniDib();
    }

    function leyenda(ex) {
        q('.pf-ley').innerHTML = (ver.avance
            ? '<span><i class="pf-rampa"></i>Avance 0 → 100 %</span><span><i class="pf-sq"></i>Retención*</span><span><i class="pf-ci"></i>Suspensión*</span>'
            : '<span><i style="background:var(--pf-sus)"></i>Suspensión*</span><span><i style="background:var(--pf-ret)"></i>Retención*</span>')
            + '<span><i style="background:var(--pf-cuerda)"></i>Conductor inferior (cuerda, sin catenaria)</span>'
            + '<span><i class="pf-l-suelo"></i>Unión de cotas de torre (no es el terreno)</span>'
            + (ver.cons ? '<span><i style="background:#d9871b"></i>Cota de construcción</span>' : '')
            + `<span>Exageración vertical ×${f1(ex)}</span><span title="Deducido del tipo de torre; por confirmar con diseño">*supuesto</span>`;
    }

    function miniDib() {
        const W = mi.clientWidth, H = mi.clientHeight; mi.replaceChildren(); if (!W) return;
        const zl = Math.min(...T.map(t => t.z)), zh = Math.max(...T.map(t => t.zt));
        const sx = x => (x - X0) / (X1 - X0) * W, sy = z => 4 + (zh - z) / Math.max(1, zh - zl) * (H - 8);
        el('path', { d: T.map((t, k) => (k ? 'L' : 'M') + sx(t.x) + ',' + sy(t.z)).join('') + `L${W},${H}L0,${H}Z`, class: 'pf-suelo pf-suelo-m' }, mi);
        T.filter(t => t.ret).forEach(t => el('line', { x1: sx(t.x), x2: sx(t.x), y1: sy(t.zt), y2: sy(t.z), stroke: ver.avance ? colAv(pct[t.id]) : 'var(--pf-ret)', 'stroke-width': 1 }, mi));
        el('rect', { x: sx(w0), y: 1, width: Math.max(3, sx(w1) - sx(w0)), height: H - 2, class: 'pf-ventana', rx: 3 }, mi);
    }

    function verTip(e, s) { const r = raiz.getBoundingClientRect(); tip.textContent = s; tip.style.display = 'block'; tip.style.left = (e.clientX - r.left + 12) + 'px'; tip.style.top = (e.clientY - r.top - 26) + 'px'; }
    function ventana(a, w) { w = Math.max(300, Math.min(X1 - X0, w)); a = Math.max(X0, Math.min(X1 - w, a)); w0 = a; w1 = a + w; }
    function enfocar(t) { const w = Math.min(w1 - w0, 3000); ventana(t.x - w / 2, w); }

    function elegir(s, desdeFuera) {
        sel = s;
        if (s.t != null) {
            const t = T[s.t], p = T[s.t - 1], n = T[s.t + 1], vv = p && n ? ((t.x - p.x) + (n.x - t.x)) / 2 : null;
            ficha.innerHTML = `<div class="pf-f-cab"><b>${esc(t.n)}</b>${t.fn ? `<span class="pf-tag ${t.ret ? 'ret' : ''}">${esc(t.fn)}</span>` : ''}</div>
<dl>
<dt>Tipo</dt><dd>${esc(t.tipo || '—')}</dd><dt>Abscisa</dt><dd>${km(t.x)}</dd>
<dt>Cota de diseño</dt><dd>${f2(t.z)} m</dd><dt>Cota de construcción</dt><dd>${t.zc != null ? f2(t.zc) + ' m' : '—'}</dd>
${t.dz != null ? `<dt>Δ construcción − diseño</dt><dd>${sg(t.dz)} m</dd>` : ''}
<dt>Altura total</dt><dd>${t.h != null ? f1(t.h) + ' m' : '—'}</dd><dt>Amarre conductor inferior</dt><dd>${t.hc != null ? f2(t.hc) + ' m' : '—'}</dd>
<dt>Deflexión</dt><dd>${t.defl != null ? sg(t.defl) + '°' : '—'}</dd>
<dt>Vano atrás / adelante</dt><dd>${p ? f0(t.x - p.x) : '—'} / ${n ? f0(n.x - t.x) : '—'} m</dd>
<dt>Vano de viento</dt><dd>${vv ? f0(vv) + ' m' : '—'}</dd><dt>Sección</dt><dd>S${t.sec.id} · VR ${f0(t.sec.vr)} m</dd>
${pct[t.id] != null ? `<dt>Avance de obra</dt><dd>${f1(pct[t.id])} %</dd>` : ''}
</dl>
<div class="pf-f-acc">
<button type="button" class="av3d-btn" data-acc="torre" title="Torre por torre">${svgIco('torre')}</button>
<button type="button" class="av3d-btn" data-acc="tendido" title="Ver en el tendido">${svgIco('tiro')}</button>
<button type="button" class="av3d-btn" data-acc="hdv" title="Hoja de vida">${svgIco('hdv')}</button>
</div>`;
            ficha.querySelectorAll('[data-acc]').forEach(b => b.onclick = () => dotnet && dotnet.invokeMethodAsync('PerfilAccion', b.dataset.acc, t.id).catch(() => { }));
            if (desdeFuera || t.x < w0 || t.x > w1) enfocar(t);
            dotnet && dotnet.invokeMethodAsync('PerfilSeleccion', t.id).catch(() => { });   // para volver al 3D en esta torre
        } else {
            const a = T[s.v], b = T[s.v + 1], L = b.x - a.x, dz = b.za - a.za;
            ficha.innerHTML = `<div class="pf-f-cab"><b>Vano ${esc(a.n)} – ${esc(b.n)}</b><span class="pf-tag">S${a.sec.id}</span></div>
<dl>
<dt>Longitud horizontal</dt><dd>${f2(L)} m</dd><dt>Desnivel entre amarres</dt><dd>${sg(dz)} m</dd>
<dt>Pendiente de la cuerda</dt><dd>${f1(dz / L * 100)} %</dd><dt>Vano regulador (sección)</dt><dd>${f0(a.sec.vr)} m</dd>
<dt>Vano / regulador</dt><dd>${f2(L / a.sec.vr)}</dd><dt>Flecha</dt><dd>—</dd><dt>Distancia mínima al suelo</dt><dd>—</dd>
</dl>
<div class="pf-nota" title="Faltan datos del conductor, tensión de diseño y perfil del terreno entre torres">Flecha y distancia al suelo: sin datos</div>`;
        }
        const sid = s.t != null ? T[s.t].sec.id : T[s.v].sec.id;
        raiz.querySelectorAll('[data-p=sec] tr').forEach(r => r.classList.toggle('sel', +r.dataset.s === sid));
        raiz.querySelectorAll('[data-p=dif] tr').forEach(r => r.classList.toggle('sel', s.t != null && +r.dataset.t === s.t));
        dibujar();
    }

    function tablas() {
        const ts = raiz.querySelector('[data-p=sec] tbody'), td = raiz.querySelector('[data-p=dif] tbody');
        ts.replaceChildren(); td.replaceChildren();
        S.forEach(s => {
            const r = document.createElement('tr'); r.dataset.s = s.id;
            r.innerHTML = `<td>S${s.id} · ${esc(T[s.a].n)}–${esc(T[s.b].n)}</td><td>${s.n}</td><td>${f0(s.L)}</td><td>${f0(s.vr)}</td>`;
            r.onclick = () => { const m = (T[s.b].x - T[s.a].x) * .08 + 80; ventana(T[s.a].x - m, T[s.b].x - T[s.a].x + 2 * m); sel = null; ts.querySelectorAll('tr').forEach(x => x.classList.toggle('sel', x === r)); dibujar(); };
            ts.appendChild(r);
        });
        const difs = T.filter(t => t.dz != null && Math.abs(t.dz) >= 0.01).sort((a, b) => Math.abs(b.dz) - Math.abs(a.dz));
        difs.forEach(t => {
            const r = document.createElement('tr'); r.dataset.t = t.i;
            r.innerHTML = `<td>${esc(t.n)}</td><td>${f2(t.z)}</td><td>${f2(t.zc)}</td><td class="${Math.abs(t.dz) >= 1 ? 'pf-dif' : ''}">${sg(t.dz)}</td>`;
            r.onclick = () => { if (!ver.cons) alternar('cons'); enfocar(t); elegir({ t: t.i }); };
            td.appendChild(r);
        });
        raiz.querySelector('[data-t=sec] small').textContent = S.length;
        raiz.querySelector('[data-t=dif] small').textContent = difs.length;
    }

    function alternar(k) { ver[k] = ver[k] ? 0 : 1; raiz.querySelector(`[data-k=${k}]`)?.classList.toggle('activo', !!ver[k]); dibujar(); }
    raiz.querySelectorAll('[data-k]').forEach(b => b.onclick = () => alternar(b.dataset.k));
    raiz.querySelectorAll('[data-b]').forEach(b => b.onclick = () => {
        ver.banda = ver.banda === b.dataset.b ? '' : b.dataset.b;
        raiz.querySelectorAll('[data-b]').forEach(x => x.classList.toggle('activo', x.dataset.b === ver.banda)); dibujar();
    });
    q('.pf-exag select').onchange = e => { exag = +e.target.value; dibujar(); };
    q('[data-a=todo]').onclick = () => { w0 = X0; w1 = X1; dibujar(); };
    q('[data-a=pc]').onclick = async () => {
        try { if (document.fullscreenElement) await document.exitFullscreen(); else await raiz.requestFullscreen(); }
        catch { raiz.classList.toggle('pf-max'); }
        setTimeout(dibujar, 120);
    };
    raiz.querySelectorAll('.pf-tabs button').forEach(b => b.onclick = () => {
        raiz.querySelectorAll('.pf-tabs button').forEach(x => x.classList.toggle('on', x === b));
        raiz.querySelectorAll('.pf-tabla').forEach(p => p.hidden = p.dataset.p !== b.dataset.t);
    });
    q('.pf-buscar input').onchange = e => {
        const v = e.target.value.trim().toLowerCase(), t = T.find(x => String(x.n).toLowerCase() === v);
        if (t) elegir({ t: t.i }, true);
    };
    sv.addEventListener('wheel', e => {
        e.preventDefault(); const r = sv.getBoundingClientRect(), f = Math.max(0, Math.min(1, (e.clientX - r.left - ML) / (r.width - ML - MR)));
        const xm = w0 + f * (w1 - w0), w = (w1 - w0) * (e.deltaY > 0 ? 1.2 : 1 / 1.2); ventana(xm - f * Math.max(300, Math.min(X1 - X0, w)), w); dibujar();
    }, { passive: false });
    let dr = null;
    const mover = e => { if (!dr) return; const dx = (e.clientX - dr.x) / (sv.clientWidth - ML - MR) * (dr.w1 - dr.w0); ventana(dr.w0 - dx, dr.w1 - dr.w0); dibujar(); };
    const soltar = () => { dr = null; sv.classList.remove('pf-arrastre'); };
    sv.addEventListener('pointerdown', e => { dr = { x: e.clientX, w0, w1 }; sv.classList.add('pf-arrastre'); });
    window.addEventListener('pointermove', mover); window.addEventListener('pointerup', soltar);
    const miIr = e => { const r = mi.getBoundingClientRect(), w = w1 - w0; ventana(X0 + (e.clientX - r.left) / r.width * (X1 - X0) - w / 2, w); dibujar(); };
    mi.addEventListener('pointerdown', e => {
        miIr(e); const mv = ev => miIr(ev), up = () => { window.removeEventListener('pointermove', mv); window.removeEventListener('pointerup', up); };
        window.addEventListener('pointermove', mv); window.addEventListener('pointerup', up);
    });
    const ro = new ResizeObserver(() => dibujar()); ro.observe(raiz);

    return {
        cargar(datos) { preparar(datos); sel = null; dibujar(); },
        estado(p) { pct = p || {}; if (sel && sel.t != null) elegir(sel); else dibujar(); },
        seleccionar(id) { const t = T.find(x => x.id === id); if (t) elegir({ t: t.i }, true); },
        liberar() { vivo = false; ro.disconnect(); window.removeEventListener('pointermove', mover); window.removeEventListener('pointerup', soltar); raiz.replaceChildren(); },
    };
}
