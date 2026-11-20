import { navegacion3d } from './navegacion3d.js';
import { referenciaPlano, fichaPlano } from './planoDetalle.js';
// Visor de DETALLE por pieza (piloto "producto desmontable"): wwwroot/modelos3d/detalle/*.json, generado por
// tools/planos-dwg/detalle-bp6/armar-detalle.mjs. Cada ángulo L se arma con sus dos alas como sólidos extruidos con las
// perforaciones reales (agujeros en la malla) y el desaletado de extremo; cada platina con su contorno y perforaciones;
// cada tornillo como pieza (cabeza hexagonal, vástago, arandela plana, arandela de presión y tuerca) con InstancedMesh por
// tipo. Interacción: clic = seleccionar + ficha; mover (arrastrar) con "devolver a su sitio"; aislar/ocultar; vista
// explotada por conjunto o por pieza; mostrar/ocultar tornillería; acercar a la pieza. Se dibuja solo cuando algo cambia.
import * as THREE from '../lib/three/three.module.min.js';
import { OrbitControls } from '../lib/three/OrbitControls.js';
import { RoomEnvironment } from '../lib/three/addons/environments/RoomEnvironment.js';

const K = 0.001;                          // mm → m
const COLOR = { angulo: '#9aa3ad', platina: '#7f8c99', sel: '#ffa733', tornillo: '#c9ced4', tuerca: '#b9bec4' };
// Dimensiones de cabeza, tuerca y arandelas (mm): ASME B18.2.1/B18.2.2 (hexagonal), F436 y B18.21.1 típicas.
// SUPUESTO declarado en la ficha: la hoja no las da.
const HEX = { 15.9: { s: 24, k: 10, m: 14, dw: 33, tw: 3.5, dl: 27, tl: 4 }, 19.1: { s: 28.6, k: 12, m: 16.5, dw: 37, tw: 3.5, dl: 32, tl: 4.8 } };
const ICO = {
  iso: '<path d="M12 3 4 7.5v9L12 21l8-4.5v-9z"/><path d="M4 7.5 12 12l8-4.5M12 12v9"/>',
  encuadrar: '<path d="M4 9V4h5M20 9V4h-5M4 15v5h5M20 15v5h-5"/><circle cx="12" cy="12" r="2.5"/>',
  mover: '<path d="M12 3v18M3 12h18M12 3l-3 3M12 3l3 3M12 21l-3-3M12 21l3-3M3 12l3-3M3 12l3 3M21 12l-3-3M21 12l-3 3"/>',
  devolver: '<path d="M9 14 4 9l5-5"/><path d="M4 9h10a6 6 0 0 1 0 12h-3"/>',
  aislar: '<circle cx="12" cy="12" r="3.5"/><path d="M3 12h3M18 12h3M12 3v3M12 18v3"/>',
  ocultar: '<path d="M3 3l18 18"/><path d="M10.6 6.1A9.8 9.8 0 0 1 12 6c5 0 9 6 9 6a17 17 0 0 1-3 3.5M6.3 7.6C4.2 9.2 3 12 3 12s4 6 9 6a8.6 8.6 0 0 0 3.4-.7"/>',
  mostrar: '<path d="M3 12s4-6 9-6 9 6 9 6-4 6-9 6-9-6-9-6z"/><circle cx="12" cy="12" r="2.5"/>',
  explotar: '<path d="M12 12 6 6M12 12l6-6M12 12l-6 6M12 12l6 6"/><rect x="2" y="2" width="5" height="5" rx="1"/><rect x="17" y="2" width="5" height="5" rx="1"/><rect x="2" y="17" width="5" height="5" rx="1"/><rect x="17" y="17" width="5" height="5" rx="1"/>',
  tornillo: '<path d="M8 3h8l2 3H6z"/><path d="M10 6v15M14 6v15M10 9l4 2M10 13l4 2M10 17l4 2"/>',
  acercar: '<circle cx="10.5" cy="10.5" r="6.5"/><path d="M15.5 15.5 21 21M10.5 7.5v6M7.5 10.5h6"/>',
  conjuntos: '<path d="m12 3 9 5-9 5-9-5z"/><path d="m3 13 9 5 9-5"/>',
  info: '<circle cx="12" cy="12" r="9"/><path d="M12 11v5.5M12 7.6v.2"/>',
  cerrar: '<path d="M6 6l12 12M18 6 6 18"/>',
  pantalla: '<path d="M4 9V4h5M20 9V4h-5M4 15v5h5M20 15v5h-5"/>',
};
const svg = k => `<svg class="vdet-i" viewBox="0 0 24 24">${ICO[k]}</svg>`;
const v3 = a => new THREE.Vector3(a[0], a[1], a[2]);
const esc = s => String(s ?? '').replace(/[&<>"]/g, c => ({ '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;' }[c]));
const fmt = (n, d = 0) => (n ?? 0).toLocaleString('es-CO', { minimumFractionDigits: d, maximumFractionDigits: d });

export async function crear(contenedor, url, marco, escena) {
  const v = new VisorDetalle(contenedor, marco);
  await v.cargar(url);
  if (escena) v.aplicarEscena(escena);
  window.sigDetalle = v;               // para pruebas y capturas
  return v;
}

class VisorDetalle {
  constructor(cont, marco) {
    this.cont = cont; this.marco = marco || cont;
    cont.classList.add('vdet'); cont.replaceChildren();
    this.renderer = new THREE.WebGLRenderer({ antialias: true, preserveDrawingBuffer: true });
    this.renderer.setPixelRatio(Math.min(window.devicePixelRatio, 2));
    this.renderer.outputColorSpace = THREE.SRGBColorSpace;
    this.renderer.toneMapping = THREE.ACESFilmicToneMapping; this.renderer.toneMappingExposure = 1.05;
    cont.appendChild(this.renderer.domElement);
    this.scene = new THREE.Scene();
    const fondo = document.createElement('canvas'); fondo.width = 2; fondo.height = 256;
    const g = fondo.getContext('2d'), gr = g.createLinearGradient(0, 0, 0, 256); gr.addColorStop(0, '#dfe6ee'); gr.addColorStop(1, '#9aa7b6'); g.fillStyle = gr; g.fillRect(0, 0, 2, 256);
    this.scene.background = new THREE.CanvasTexture(fondo); this.scene.background.colorSpace = THREE.SRGBColorSpace;
    const pm = new THREE.PMREMGenerator(this.renderer);
    this.scene.environment = pm.fromScene(new RoomEnvironment(), 0.04).texture;
    const sol = new THREE.DirectionalLight('#ffffff', 1.4); sol.position.set(3, 6, 4); this.scene.add(sol, new THREE.AmbientLight('#ffffff', .25));
    this.camera = new THREE.PerspectiveCamera(40, 1, 0.01, 200);
    this.controls = new OrbitControls(this.camera, this.renderer.domElement);
    this.navegacion = navegacion3d(this.controls, this.renderer.domElement);
    this.controls.enableDamping = false; this.controls.zoomToCursor = true;
    this.controls.addEventListener('change', () => { this._planos(); this.pedir(); });
    this.raiz = new THREE.Group(); this.scene.add(this.raiz);
    this.piezas = []; this.tornillos = []; this.sel = null; this.modoMover = false; this.explosion = 0; this.modoExplosion = 'conjunto'; this.verTornillos = true;
    this._ui();
    this._eventos();
    this.ro = new ResizeObserver(() => this._tam()); this.ro.observe(cont); this._tam();
  }

  // ---------- carga y geometría ----------
  async cargar(url) {
    const r = await fetch(url, { cache: 'no-cache' }); if (!r.ok) throw new Error(`No se pudo leer ${url}`);
    const d = this.data = await r.json();
    // centro del conjunto (mm)
    const bb = new THREE.Box3();
    for (const p of d.piezas) bb.expandByPoint(v3(p.geo.o));
    this.centroMm = bb.getCenter(new THREE.Vector3());
    const matA = new THREE.MeshStandardMaterial({ color: COLOR.angulo, metalness: .55, roughness: .42 });
    const matP = new THREE.MeshStandardMaterial({ color: COLOR.platina, metalness: .5, roughness: .48 });
    for (const p of d.piezas) {
      const grupo = new THREE.Group(); grupo.userData.pieza = p;
      const mats = p.clase === 'angulo' ? matA : matP;
      for (const geo of p.clase === 'angulo' ? this._geoAngulo(p) : [this._geoPlatina(p)]) {
        const m = new THREE.Mesh(geo, mats); m.userData.pieza = p; grupo.add(m);
      }
      this.raiz.add(grupo);
      const caja = new THREE.Box3().setFromObject(grupo);
      p._grupo = grupo; p._centro = caja.getCenter(new THREE.Vector3()); p._mover = new THREE.Vector3(); p._visible = true;
      this.piezas.push(p);
    }
    this.porId = new Map(this.piezas.map(p => [p.id, p]));
    this._tornillos(d.tornillos);
    this._centros();
    this._llenarPanelConjuntos();
    this._ficha(null);
    this.encuadrar();
  }

  // Ala como sólido extruido con perforaciones: forma en (s, w) y espesor a lo largo de `nrm`.
  _ala(o, u, w, nrm, L, w0, w1, t, s0, s1, agujeros) {
    const sh = new THREE.Shape(); sh.moveTo(s0, w0); sh.lineTo(s1, w0); sh.lineTo(s1, w1); sh.lineTo(s0, w1); sh.lineTo(s0, w0);
    for (const a of agujeros) {
      const P = v3(a.p).sub(o), s = P.dot(u), ww = P.dot(w), r = a.d / 2;
      if (Math.abs(P.dot(nrm)) > t + 8) continue;                     // agujero de la otra ala
      if (s - r < s0 + 0.5 || s + r > s1 - 0.5 || ww - r < w0 + 0.3 || ww + r > w1 - 0.3) continue;
      const h = new THREE.Path(); h.absarc(s, ww, r, 0, Math.PI * 2, true); sh.holes.push(h);
    }
    const g = new THREE.ExtrudeGeometry(sh, { depth: t, bevelEnabled: false, curveSegments: 14 });
    const m = new THREE.Matrix4().makeBasis(u.clone().multiplyScalar(K), w.clone().multiplyScalar(K), nrm.clone().multiplyScalar(K));
    m.setPosition(o.clone().multiplyScalar(K));
    g.applyMatrix4(m);
    return g;
  }
  _geoAngulo(p) {
    const G = p.geo, o = v3(G.o), u = v3(G.u), a = v3(G.a), b = v3(G.b), L = G.L, ala = p.ala, t = p.espesor;
    // desaletado (D = mm): el ala saliente se recorta en el extremo
    let d0 = 0, d1 = 0; for (const c of G.cortes || []) if (c.tipo === 'D' && c.mm) { if (c.extremo === 0) d0 = Math.max(d0, c.mm); else d1 = Math.max(d1, c.mm); }
    const esCelosia = p.capa_n === 'fuera' || p.capa_n === 'dentro';
    const alaA = this._ala(o, u, a, b, L, 0, ala, t, 0, L, p.agujeros);
    const alaB = this._ala(o, u, b, a, L, t, ala, t, esCelosia ? d0 : 0, esCelosia ? L - d1 : L, p.agujeros);
    return [unir([alaA, alaB])];   // una malla por pieza (la mitad de llamadas de dibujo con la torre completa)
  }
  _geoPlatina(p) {
    const G = p.geo, o = v3(G.o), ex = v3(G.ex), ey = v3(G.ey), n = v3(G.n);
    const sh = new THREE.Shape(G.contorno.map(([x, y]) => new THREE.Vector2(x, y)));
    const nrm = G.fuera ? n : n.clone().negate();
    for (const a of p.agujeros) {
      const P = v3(a.p).sub(o); if (Math.abs(P.dot(n)) > G.t + 25) continue;
      const h = new THREE.Path(); h.absarc(P.dot(ex), P.dot(ey), a.d / 2, 0, Math.PI * 2, true); sh.holes.push(h);
    }
    const g = new THREE.ExtrudeGeometry(sh, { depth: G.t, bevelEnabled: false, curveSegments: 14 });
    const m = new THREE.Matrix4().makeBasis(ex.multiplyScalar(K), ey.multiplyScalar(K), nrm.multiplyScalar(K)); m.setPosition(o.multiplyScalar(K));
    g.applyMatrix4(m); return g;
  }

  // Tornillería: por tipo, un InstancedMesh "cabeza + vástago" y otro "arandela plana + arandela de presión + tuerca".
  _tornillos(lista) {
    const porTipo = Object.groupBy(lista, t => t.tipo);
    const matT = new THREE.MeshStandardMaterial({ color: COLOR.tornillo, metalness: .7, roughness: .35 });
    const matN = new THREE.MeshStandardMaterial({ color: COLOR.tuerca, metalness: .7, roughness: .38 });
    this.inst = [];
    for (const [tipo, ts] of Object.entries(porTipo)) {
      const d = ts[0].d, L = ts[0].L, h = HEX[d] || HEX[19.1];
      // eje local Y = hacia afuera (n). Cabeza sobre y ∈ [0, k]; vástago y ∈ [−L, 0] (con tuerca y arandelas aparte).
      const cab = new THREE.CylinderGeometry(h.s / Math.sqrt(3), h.s / Math.sqrt(3), h.k, 6); cab.translate(0, h.k / 2, 0);
      const vas = new THREE.CylinderGeometry(d / 2, d / 2, L, 16); vas.translate(0, -L / 2, 0);
      const g1 = unir([cab, vas]); g1.scale(K, K, K);
      // tuerca: arandela plana (contra la pieza), de presión y tuerca, hacia −y desde 0
      const aw = new THREE.CylinderGeometry(h.dw / 2, h.dw / 2, h.tw, 24); aw.translate(0, -h.tw / 2, 0);
      const al = new THREE.CylinderGeometry(h.dl / 2, h.dl / 2, h.tl, 20); al.translate(0, -h.tw - h.tl / 2, 0);
      const nu = new THREE.CylinderGeometry(h.s / Math.sqrt(3), h.s / Math.sqrt(3), h.m, 6); nu.translate(0, -h.tw - h.tl - h.m / 2, 0);
      const g2 = unir([aw, al, nu]); g2.scale(K, K, K);
      const m1 = new THREE.InstancedMesh(g1, matT.clone(), ts.length), m2 = new THREE.InstancedMesh(g2, matN.clone(), ts.length);
      m1.userData.tornillos = ts; m2.userData.tornillos = ts;
      m1.instanceColor = new THREE.InstancedBufferAttribute(new Float32Array(ts.length * 3).fill(1), 3);
      m2.instanceColor = new THREE.InstancedBufferAttribute(new Float32Array(ts.length * 3).fill(1), 3);
      ts.forEach((t, i) => {
        t._m1 = m1; t._m2 = m2; t._i = i; t._mover = new THREE.Vector3(); t._visible = true;
        // perno de escalera: cabeza por dentro, vástago hacia afuera (peldaño)
        let n = v3(t.n), p = v3(t.p);
        if (t.tipo === 'PE') { p = p.clone().addScaledVector(n, -5); n = n.clone().negate(); }
        t._n = n; t._p = p;
        t._centro = p.clone().addScaledVector(n, -t.L / 2);
      });
      m1.frustumCulled = false; m2.frustumCulled = false;
      this.raiz.add(m1, m2); this.inst.push(m1, m2);
    }
    this.tornillos = lista;
    this.porIdT = new Map(lista.map(t => [t.id, t]));
    this._matricesTornillos();
  }
  _matricesTornillos() {
    const q = new THREE.Quaternion(), up = new THREE.Vector3(0, 1, 0), M = new THREE.Matrix4(), S = new THREE.Vector3(1, 1, 1), Z = new THREE.Vector3(0, 0, 0);
    for (const t of this.tornillos) {
      const vis = this.verTornillos && t._visible;
      q.setFromUnitVectors(up, t._n);
      const off = this._offsetTornillo(t);
      const p1 = t._p.clone().add(off).multiplyScalar(K);
      t._m1.setMatrixAt(t._i, M.compose(p1, q, vis ? S : Z));
      // tuerca del lado interior: a "agarre" de la cabeza (más el arrastre de la vista explotada)
      const p2 = t._p.clone().addScaledVector(t._n, -(t.agarre || 0)).add(off).addScaledVector(t._n, -this.explosion * 60).multiplyScalar(K);
      t._m2.setMatrixAt(t._i, M.compose(p2, q, vis ? S : Z));
    }
    for (const m of this.inst) { m.instanceMatrix.needsUpdate = true; m.computeBoundingSphere(); }
  }

  // ---------- vista explotada ----------
  _centros() {
    const C = new THREE.Vector3(); this.piezas.forEach(p => C.add(p._centro)); C.divideScalar(this.piezas.length); this.C = C;
    this.cconj = {};
    for (const c of this.data.conjuntos) {
      const ps = this.piezas.filter(p => p.conjunto === c.id); if (!ps.length) continue;
      const cc = new THREE.Vector3(); ps.forEach(p => cc.add(p._centro)); cc.divideScalar(ps.length); this.cconj[c.id] = cc;
    }
  }
  _offsetExplosion(p) {
    const k = this.explosion; if (!k) return new THREE.Vector3();
    // centros en metros (cajas de las mallas)
    const C = this.C;
    if (this.modoExplosion === 'conjunto') {
      const cc = this.cconj[p.conjunto];
      // cada conjunto se separa la misma distancia (1,5 m a tope) en la dirección de su centro; montantes quietos
      const dir = cc.clone().sub(C); const l = dir.length();
      if (p.conjunto === 'montantes' || l < 1e-6) dir.set(0, 0, 0); else dir.multiplyScalar(1 / l);
      return dir.multiplyScalar(k * 1.5).add(p._centro.clone().sub(cc).multiplyScalar(k * .3));
    }
    return p._centro.clone().sub(C).multiplyScalar(k * 1.4);
  }
  _offsetTornillo(t) {
    const p = this.porId.get(t.piezas[0]);
    const off = p ? this._offsetExplosion(p).multiplyScalar(1 / K) : new THREE.Vector3();
    return off.add(t._mover).addScaledVector(t._n, this.explosion * 120);   // en explotada los tornillos salen de sus huecos
  }
  _aplicarPosiciones() {
    for (const p of this.piezas) p._grupo.position.copy(this._offsetExplosion(p)).add(p._mover.clone().multiplyScalar(K));
    this._matricesTornillos(); this.pedir();
  }
  setExplosion(k) { this.explosion = k; if (this.ui?.rango) this.ui.rango.value = String(Math.round(k * 100)); this._aplicarPosiciones(); }
  setModoExplosion(m) { this.modoExplosion = m; this.ui.modo.value = m; this._aplicarPosiciones(); }

  // ---------- selección y ficha ----------
  seleccionar(obj) {
    // limpiar
    if (this.sel?.tipo === 'pieza') this.sel.p._grupo.children.forEach(m => { m.material = m.userData.matOrig || m.material; });
    if (this.sel?.tipo === 'tornillo') this._colorTornillo(this.sel.t, 1, 1, 1);
    this.sel = obj;
    if (obj?.tipo === 'pieza') {
      const hl = new THREE.MeshStandardMaterial({ color: COLOR.sel, metalness: .3, roughness: .45, emissive: '#7a3d00', emissiveIntensity: .35 });
      obj.p._grupo.children.forEach(m => { m.userData.matOrig ??= m.material; m.material = hl; });
    }
    if (obj?.tipo === 'tornillo') this._colorTornillo(obj.t, 1.6, .9, .25);
    this._ficha(obj); this._estadoBotones(); this.pedir();
  }
  seleccionarId(id) { const p = this.porId.get(id); if (p) return this.seleccionar({ tipo: 'pieza', p }); const t = this.porIdT.get(id); if (t) this.seleccionar({ tipo: 'tornillo', t }); }
  _colorTornillo(t, r, g, b) { for (const m of [t._m1, t._m2]) { m.instanceColor.setXYZ(t._i, r, g, b); m.instanceColor.needsUpdate = true; } }

  referenciasPlanos() {
    return [...new Map(this.piezas.map(p => referenciaPlano(p, this.data.meta)).filter(Boolean).map(r => [r.clave, r])).values()];
  }
  setPlanos(enlaces, estado = 'listo') {
    this.enlacesPlanos = new Map(enlaces.map(e => [e.clave, e]));
    this.estadoPlanos = estado;
    this._ficha(this.sel);
  }
  _ficha(obj) {
    const f = this.ui.ficha, d = this.data;
    if (!obj) {
      const R = d.meta.resumen;
      f.innerHTML = `<div class="vdet-tit">${esc(d.meta.nombre)}</div><div class="vdet-sub">${esc(d.meta.plano.codigo)}-${esc(d.meta.plano.hoja)} · rev. ${esc(d.meta.plano.rev)}</div>
        <div class="vdet-kpis"><span><b>${R.piezas}</b>piezas</span><span><b>${R.platinas}</b>platinas</span><span><b>${R.tornillos}</b>tornillos</span><span><b>${R.perforaciones}</b>perforaciones</span><span><b>${fmt(R.peso_piezas_galv_kg, 1)}</b>kg galv.</span></div>
        <details class="vdet-d"><summary>Validación <span>${d.meta.validacion.length}</span></summary><table class="vdet-tab"><tr><th></th><th>Plano</th><th>Modelo</th></tr>${d.meta.validacion.map(x => `<tr><td>${esc(x.que)}</td><td>${esc(x.plano)}</td><td>${esc(x.modelo)}</td></tr>`).join('')}</table></details>
        <details class="vdet-d"><summary>Tornillos contra el listado</summary><ul>${Object.entries(R.tornillos_por_tipo).map(([k, n]) => `<li>${esc(k)}: ${n}${R.tornillos_plano[k] != null ? ` · listado ${R.tornillos_plano[k]}` : ''}</li>`).join('')}${(d.meta.diferencias_tornillos || []).map(x => `<li class="vdet-av">${esc(x)}</li>`).join('')}</ul></details>
        <details class="vdet-d"><summary>Supuestos <span>${d.meta.supuestos.length}</span></summary><ol>${d.meta.supuestos.map(s => `<li>${esc(s)}</li>`).join('')}</ol></details>`;
      return;
    }
    if (obj.tipo === 'pieza') {
      const p = obj.p, tor = p.tornillos.map(id => this.porIdT.get(id)).filter(Boolean);
      const plano = referenciaPlano(p, d.meta);
      const porTipo = {}; tor.forEach(t => porTipo[t.tipo] = (porTipo[t.tipo] || 0) + 1);
      f.innerHTML = `<div class="vdet-tit">Pieza ${esc(p.marca)}${p.lado ? ` <small>· lado ${esc(p.lado)}</small>` : ''}${p.parte ? ` <small>· ${esc(p.parte)}</small>` : ''}</div>
        <div class="vdet-sub">${esc(p.tipo)} · ${esc(p.cara)}</div>
        <dl class="vdet-dl"><dt>Perfil</dt><dd>${esc(p.perfil)}${p.acero ? ' · ' + esc(p.acero) : ''}${p.marca_leida === false ? ' <small>(supuesto)</small>' : ''}</dd>
        <dt>Longitud</dt><dd>${fmt(p.longitud_mm)} mm ${p.longitud_plano != null ? `<small>(plano ${fmt(p.longitud_plano)})</small>` : ''}</dd>
        <dt>Peso</dt><dd>${fmt(p.peso_kg, 2)} kg <small>(galv. ${fmt(p.peso_kg * 1.045, 2)})</small></dd>
        <dt>Plano de origen</dt><dd>${fichaPlano(plano, this.enlacesPlanos?.get(plano?.clave), this.estadoPlanos)}</dd>
        <dt>Perforaciones</dt><dd>${p.n_perforaciones}${p.agujeros.length ? ` · Ø ${[...new Set(p.agujeros.map(a => a.d))].join(' / ')} mm` : ''}${p.perforaciones_txt?.length ? `<small> · plano: ${esc(p.perforaciones_txt.join(' – '))}</small>` : ''}</dd>
        <dt>Tornillos</dt><dd>${tor.length ? Object.entries(porTipo).map(([k, n]) => `${n} × ${esc(k)}`).join(', ') : '—'}</dd>
        ${p.geo.cortes?.length ? `<dt>Cortes</dt><dd>${p.geo.cortes.map(c => `${esc(c.txt)} (extremo ${c.extremo + 1})`).join(', ')}</dd>` : ''}</dl>
        ${tor.length ? `<div class="vdet-chips">${tor.map(t => `<button type="button" class="vdet-chip" data-id="${t.id}" title="${esc(t.tipo)} · une ${esc(t.piezas.join(', '))}">${esc(t.id)}</button>`).join('')}</div>` : ''}
        <div class="vdet-fuente">${esc(p.fuente)}</div>
        ${p.supuestos?.length ? `<details class="vdet-d" open><summary>Supuestos <span>${p.supuestos.length}</span></summary><ol>${p.supuestos.map(s => `<li>${esc(s)}</li>`).join('')}</ol></details>` : ''}`;
    } else {
      const t = obj.t, T = d.meta.tipos_tornillo[t.tipo] || {};
      f.innerHTML = `<div class="vdet-tit">Tornillo ${esc(t.id)} <small>· ${esc(t.tipo)}</small></div><div class="vdet-sub">${esc(T.desc || '')}</div>
        <dl class="vdet-dl"><dt>Diámetro × largo</dt><dd>Ø${fmt(t.d, 1)} × ${t.L} mm</dd><dt>Hueco</dt><dd>Ø${fmt(T.hueco, 1)} mm</dd><dt>Agarre</dt><dd>${fmt(t.agarre, 1)} mm</dd>
        <dt>Conjunto</dt><dd>tuerca, arandela plana y de presión (nota 7)</dd>${t.union ? `<dt>Unión</dt><dd>${esc(t.union)}</dd>` : ''}${t.arandela_relleno ? '<dt>Arandela de relleno</dt><dd>sí (símbolo AR)</dd>' : ''}
        <dt>Vista</dt><dd>${esc(t.vista)}</dd></dl>
        <div class="vdet-chips">${t.piezas.map(id => `<button type="button" class="vdet-chip" data-id="${esc(id)}">${esc(id)}</button>`).join('')}</div>
        <div class="vdet-fuente">Símbolo «${esc(t.simbolo)}» de la hoja H1; cabeza, tuerca y arandelas con medidas típicas ASME/ASTM (SUPUESTO: la hoja no las da).</div>`;
    }
    f.querySelectorAll('.vdet-chip').forEach(b => b.onclick = () => this.seleccionarId(b.dataset.id));
  }

  // ---------- acciones ----------
  devolver(todo) {
    if (todo || !this.sel) { this.piezas.forEach(p => p._mover.set(0, 0, 0)); this.tornillos.forEach(t => t._mover.set(0, 0, 0)); }
    else if (this.sel.tipo === 'pieza') this.sel.p._mover.set(0, 0, 0); else this.sel.t._mover.set(0, 0, 0);
    this._aplicarPosiciones();
  }
  aislar() {
    if (!this.sel) return;
    const ids = new Set(this.sel.tipo === 'pieza' ? [this.sel.p.id] : this.sel.t.piezas);
    const tors = new Set(this.sel.tipo === 'pieza' ? this.sel.p.tornillos : [this.sel.t.id]);
    this.piezas.forEach(p => { p._visible = ids.has(p.id); p._grupo.visible = p._visible; });
    this.tornillos.forEach(t => t._visible = tors.has(t.id));
    this._matricesTornillos(); this.pedir(); this._llenarPanelConjuntos();
  }
  ocultar() {
    if (!this.sel) return;
    if (this.sel.tipo === 'pieza') { this.sel.p._visible = false; this.sel.p._grupo.visible = false; } else this.sel.t._visible = false;
    this.seleccionar(null); this._matricesTornillos(); this._llenarPanelConjuntos();
  }
  mostrarTodo() { this.piezas.forEach(p => { p._visible = true; p._grupo.visible = true; }); this.tornillos.forEach(t => t._visible = true); this._matricesTornillos(); this.pedir(); this._llenarPanelConjuntos(); }
  alternarTornillos(v) { this.verTornillos = v ?? !this.verTornillos; this.ui.bTor.classList.toggle('activo', this.verTornillos); this._matricesTornillos(); this.pedir(); }
  acercar() {
    if (!this.sel) return this.encuadrar();
    const caja = new THREE.Box3();
    if (this.sel.tipo === 'pieza') caja.setFromObject(this.sel.p._grupo);
    else { const p = this.sel.t._p.clone().add(this._offsetTornillo(this.sel.t)).multiplyScalar(K).add(this.raiz.position); caja.setFromCenterAndSize(p, new THREE.Vector3(.12, .12, .12)); }
    this._encuadrarCaja(caja, 1.6);
  }
  encuadrar() { const caja = new THREE.Box3(); this.piezas.forEach(p => p._visible && caja.expandByObject(p._grupo)); if (caja.isEmpty()) return; this._encuadrarCaja(caja, 1.15); }
  _encuadrarCaja(caja, f) {
    const c = caja.getCenter(new THREE.Vector3()), r = Math.max(caja.getSize(new THREE.Vector3()).length() / 2, .05);
    const dir = this.camera.position.clone().sub(this.controls.target); if (dir.lengthSq() < 1e-6) dir.set(1, .6, 1.4);
    dir.normalize();
    const dist = r * f / Math.sin(THREE.MathUtils.degToRad(this.camera.fov / 2));
    this.controls.target.copy(c); this.camera.position.copy(c).addScaledVector(dir, dist);
    this._radio = Math.max(this._radio || 0, r); this.controls.maxDistance = this._radio * 12; this._planos();
    this.controls.update(); this.pedir();
  }
  // Planos de recorte según la distancia de la cámara y el tamaño del modelo (antes far quedaba fijo y la torre desaparecía al alejarse).
  _planos() {
    const r = this._radio || 50, d = this.camera.position.distanceTo(this.controls.target);
    this.camera.near = Math.max(0.002, (d - r * 3) > 0 ? (d - r * 3) / 2 : d / 500);
    this.camera.far = d + r * 10; this.camera.updateProjectionMatrix();
  }
  vista(n) {
    const dirs = { iso: [-1, .55, 1.25], frente: [0, 0, 1], lado: [-1, 0, 0], arriba: [0, 1, .001], interior: [1, 0, 0] };
    const d = new THREE.Vector3(...dirs[n]).normalize();
    const r = this.camera.position.distanceTo(this.controls.target) || 5;
    this.camera.position.copy(this.controls.target).addScaledVector(d, r);
    this.controls.update(); this.encuadrar();
  }
  // Escenas para enlaces y capturas: "explotada", "explotada-pieza", "acercar:<id>", "aislar:<id>", "sin-tornillos", "frente"…
  aplicarEscena(e) {
    for (const parte of String(e).split(',')) {
      const [k, v] = parte.split(':');
      if (k === 'explotada') { this.setModoExplosion('conjunto'); this.setExplosion(v ? +v : .55); this.encuadrar(); }
      else if (k === 'explotada-pieza') { this.setModoExplosion('pieza'); this.setExplosion(v ? +v : .6); this.encuadrar(); }
      else if (k === 'sel') this.seleccionarId(v);
      else if (k === 'acercar') { this.seleccionarId(v); this.acercar(); }
      else if (k === 'aislar') { this.seleccionarId(v); this.aislar(); this.acercar(); }
      else if (k === 'sin-tornillos') this.alternarTornillos(false);
      else if (k === 'vista') this.vista(v);
      else if (k === 'zoom') { const d = this.camera.position.clone().sub(this.controls.target); this.camera.position.copy(this.controls.target).addScaledVector(d, 1 / +v); this.controls.update(); this.pedir(); }
      else if (k === 'orbita') { const [az, el] = v.split('/').map(Number); const r = this.camera.position.distanceTo(this.controls.target); const ph = THREE.MathUtils.degToRad(90 - el), th = THREE.MathUtils.degToRad(az); this.camera.position.copy(this.controls.target).add(new THREE.Vector3().setFromSphericalCoords(r, ph, th)); this.controls.update(); this.pedir(); }
    }
  }

  // ---------- interfaz ----------
  _ui() {
    const c = this.cont;
    const barra = document.createElement('div'); barra.className = 'vdet-barra';
    const b = (k, t, fn, cls = '') => `<button type="button" class="vdet-b ${cls}" data-k="${k}" title="${t}">${svg(fn)}</button>`;
    barra.innerHTML = `<div class="vdet-grp">${b('iso', 'Vista isométrica', 'iso')}${b('encuadrar', 'Encuadrar todo (F)', 'encuadrar')}${b('acercar', 'Acercar a la pieza seleccionada', 'acercar')}</div>
      <div class="vdet-grp">${b('mover', 'Mover pieza: arrastrar la pieza seleccionada (M)', 'mover')}${b('devolver', 'Devolver la pieza a su sitio (Mayús = todas)', 'devolver')}</div>
      <div class="vdet-grp">${b('aislar', 'Aislar la selección (I)', 'aislar')}${b('ocultar', 'Ocultar la selección (H)', 'ocultar')}${b('mostrar', 'Mostrar todo (Alt+H)', 'mostrar')}${b('tornillos', 'Mostrar u ocultar la tornillería (T)', 'tornillo', 'activo')}</div>
      <div class="vdet-grp vdet-exp" title="Vista explotada">${svg('explotar')}<input type="range" min="0" max="100" value="0" class="vdet-rango" title="Vista explotada"/><select class="vdet-modo" title="Explotar por"><option value="conjunto">por conjunto</option><option value="pieza">por pieza</option></select></div>
      <div class="vdet-grp">${b('conjuntos', 'Conjuntos', 'conjuntos')}${b('info', 'Ficha', 'info', 'activo')}${b('pantalla', 'Pantalla completa', 'pantalla')}</div>`;
    const ficha = document.createElement('div'); ficha.className = 'vdet-panel vdet-ficha';
    const conj = document.createElement('div'); conj.className = 'vdet-panel vdet-conj'; conj.hidden = true;
    const tip = document.createElement('div'); tip.className = 'vdet-tip'; tip.hidden = true;
    c.append(barra, ficha, conj, tip);
    this.ui = { barra, ficha, conj, tip, rango: barra.querySelector('.vdet-rango'), modo: barra.querySelector('.vdet-modo'), bTor: barra.querySelector('[data-k="tornillos"]'), bMover: barra.querySelector('[data-k="mover"]') };
    barra.addEventListener('click', e => {
      const k = e.target.closest('[data-k]')?.dataset.k; if (!k) return;
      if (k === 'iso') this.vista('iso');
      else if (k === 'encuadrar') this.encuadrar();
      else if (k === 'acercar') this.acercar();
      else if (k === 'mover') this.alternarMover();
      else if (k === 'devolver') this.devolver(e.shiftKey);
      else if (k === 'aislar') this.aislar();
      else if (k === 'ocultar') this.ocultar();
      else if (k === 'mostrar') this.mostrarTodo();
      else if (k === 'tornillos') this.alternarTornillos();
      else if (k === 'conjuntos') { conj.hidden = !conj.hidden; e.target.closest('button').classList.toggle('activo', !conj.hidden); }
      else if (k === 'info') { ficha.hidden = !ficha.hidden; e.target.closest('button').classList.toggle('activo', !ficha.hidden); }
      else if (k === 'pantalla') { if (document.fullscreenElement) document.exitFullscreen(); else this.marco.requestFullscreen?.(); }
    });
    this.ui.rango.addEventListener('input', () => this.setExplosion(+this.ui.rango.value / 100));
    this.ui.modo.addEventListener('change', () => this.setModoExplosion(this.ui.modo.value));
    this._estadoBotones();
  }
  alternarMover(v) { this.modoMover = v ?? !this.modoMover; this.ui.bMover.classList.toggle('activo', this.modoMover); this.renderer.domElement.style.cursor = this.modoMover ? 'move' : ''; }
  _estadoBotones() {
    for (const k of ['acercar', 'devolver', 'aislar', 'ocultar']) { const el = this.ui.barra.querySelector(`[data-k="${k}"]`); if (el) el.disabled = !this.sel && k !== 'devolver'; }
  }
  _llenarPanelConjuntos() {
    const d = this.data, el = this.ui.conj;
    el.innerHTML = `<div class="vdet-tit">Conjuntos</div>` + d.conjuntos.map(c => {
      const ps = this.piezas.filter(p => p.conjunto === c.id); if (!ps.length) return '';
      const vis = ps.filter(p => p._visible).length;
      return `<div class="vdet-fila"><button type="button" class="vdet-ojo ${vis ? 'activo' : ''}" data-c="${c.id}" title="${vis ? 'Ocultar' : 'Mostrar'} conjunto">${svg(vis ? 'mostrar' : 'ocultar')}</button><span>${esc(c.nombre)}</span><small>${vis}/${ps.length}</small><button type="button" class="vdet-ojo" data-ais="${c.id}" title="Aislar conjunto">${svg('aislar')}</button></div>`;
    }).join('');
    el.querySelectorAll('[data-c]').forEach(b => b.onclick = () => {
      const ps = this.piezas.filter(p => p.conjunto === b.dataset.c), vis = !ps.some(p => p._visible);
      ps.forEach(p => { p._visible = vis; p._grupo.visible = vis; });
      const ids = new Set(this.piezas.filter(p => p._visible).map(p => p.id)); this.tornillos.forEach(t => t._visible = t.piezas.some(i => ids.has(i)));
      this._matricesTornillos(); this.pedir(); this._llenarPanelConjuntos();
    });
    el.querySelectorAll('[data-ais]').forEach(b => b.onclick = () => {
      this.piezas.forEach(p => { p._visible = p.conjunto === b.dataset.ais; p._grupo.visible = p._visible; });
      const ids = new Set(this.piezas.filter(p => p._visible).map(p => p.id)); this.tornillos.forEach(t => t._visible = t.piezas.some(i => ids.has(i)));
      this._matricesTornillos(); this._llenarPanelConjuntos(); this.encuadrar();
    });
  }

  // ---------- eventos: clic, arrastre, atajos ----------
  _pick(ev) {
    const r = this.renderer.domElement.getBoundingClientRect();
    const m = new THREE.Vector2(((ev.clientX - r.left) / r.width) * 2 - 1, -((ev.clientY - r.top) / r.height) * 2 + 1);
    const rc = new THREE.Raycaster(); rc.setFromCamera(m, this.camera);
    const objs = this.piezas.filter(p => p._visible).flatMap(p => p._grupo.children);
    if (this.verTornillos) objs.push(...this.inst);
    const hit = rc.intersectObjects(objs, false)[0];
    if (!hit) return { rc };
    if (hit.object.isInstancedMesh) { const t = hit.object.userData.tornillos[hit.instanceId]; return t._visible ? { rc, hit, obj: { tipo: 'tornillo', t } } : { rc }; }
    return { rc, hit, obj: { tipo: 'pieza', p: hit.object.userData.pieza } };
  }
  _eventos() {
    const el = this.renderer.domElement;
    let abajo = null, arrastre = null;
    el.addEventListener('pointerdown', ev => {
      abajo = { x: ev.clientX, y: ev.clientY };
      if (!this.modoMover || this.navegacion.pan || !this.sel || ev.button !== 0) return;
      const { hit, obj } = this._pick(ev);
      const mismo = obj && ((obj.tipo === 'pieza' && this.sel.tipo === 'pieza' && obj.p === this.sel.p) || (obj.tipo === 'tornillo' && this.sel.tipo === 'tornillo' && obj.t === this.sel.t));
      if (!mismo) return;
      const normal = this.camera.getWorldDirection(new THREE.Vector3()).negate();
      arrastre = { plano: new THREE.Plane().setFromNormalAndCoplanarPoint(normal, hit.point), ini: hit.point.clone(), base: (this.sel.tipo === 'pieza' ? this.sel.p._mover : this.sel.t._mover).clone() };
      this.controls.enabled = false; el.setPointerCapture(ev.pointerId);
    });
    el.addEventListener('pointermove', ev => {
      if (!arrastre) { this._hover(ev); return; }
      const { rc } = this._pick(ev); const q = new THREE.Vector3();
      if (!rc.ray.intersectPlane(arrastre.plano, q)) return;
      const delta = q.sub(arrastre.ini).multiplyScalar(1 / K);
      const dest = this.sel.tipo === 'pieza' ? this.sel.p._mover : this.sel.t._mover;
      dest.copy(arrastre.base).add(delta);
      this._aplicarPosiciones();
    });
    el.addEventListener('pointerup', ev => {
      if (arrastre) { arrastre = null; this.controls.enabled = true; try { el.releasePointerCapture(ev.pointerId); } catch { } return; }
      if (ev.button !== 0 || this.navegacion.pan) return;
      if (!abajo || Math.hypot(ev.clientX - abajo.x, ev.clientY - abajo.y) > 4) return;
      const { obj } = this._pick(ev); this.seleccionar(obj || null);
    });
    el.addEventListener('dblclick', () => this.acercar());
    this._tecla = ev => {
      if (!this.cont.isConnected) return;
      if (ev.target.closest?.('input,select,textarea')) return;
      const k = ev.key.toLowerCase();
      if (k === 'f') this.encuadrar(); else if (k === 'm') this.alternarMover(); else if (k === 'i') this.aislar(); else if (k === 't') this.alternarTornillos();
      else if (k === 'h' && ev.altKey) this.mostrarTodo(); else if (k === 'h') this.ocultar(); else if (k === 'escape') { this.seleccionar(null); this.alternarMover(false); }
    };
    window.addEventListener('keydown', this._tecla);
    let ultimaRueda = 0;
    el.addEventListener('pointerdown', ev => { if (ev.button !== 1) return; ev.preventDefault(); const t = performance.now(); if (t - ultimaRueda < 350) this.encuadrar(); ultimaRueda = t; });
  }
  _hover(ev) {
    if (ev.buttons) return;
    clearTimeout(this._hT); this._hT = setTimeout(() => {
      const { obj } = this._pick(ev), tip = this.ui.tip;
      if (!obj) { tip.hidden = true; return; }
      tip.textContent = obj.tipo === 'pieza' ? `${obj.p.marca}${obj.p.lado ? ' · ' + obj.p.lado : ''} · ${obj.p.perfil}` : `${obj.t.id} · ${obj.t.tipo}`;
      const r = this.cont.getBoundingClientRect(); tip.style.left = (ev.clientX - r.left + 14) + 'px'; tip.style.top = (ev.clientY - r.top + 10) + 'px'; tip.hidden = false;
    }, 60);
  }

  // ---------- render ----------
  _tam() { const w = this.cont.clientWidth || 800, h = this.cont.clientHeight || 600; this.renderer.setSize(w, h); this.camera.aspect = w / h; this.camera.updateProjectionMatrix(); this.pedir(); }
  pedir() { if (this._raf) return; this._raf = requestAnimationFrame(() => { this._raf = 0; this.renderer.render(this.scene, this.camera); }); }
  renderAhora() { this.renderer.render(this.scene, this.camera); return true; }
  captura() { this.renderAhora(); return this.renderer.domElement.toDataURL('image/png'); }
  dispose() {
    this.navegacion.liberar(); this.controls.dispose();
    window.removeEventListener('keydown', this._tecla); this.ro?.disconnect();
    this.scene.traverse(o => { o.geometry?.dispose?.(); });
    this.renderer.dispose(); this.cont.replaceChildren(); if (window.sigDetalle === this) delete window.sigDetalle;
  }
}

// Une geometrías (no indexadas) en una sola: cabeza + vástago, o arandelas + tuerca.
function unir(gs) {
  const nn = gs.map(g => g.index ? g.toNonIndexed() : g);
  let n = 0; nn.forEach(g => n += g.attributes.position.count);
  const pos = new Float32Array(n * 3), nor = new Float32Array(n * 3); let o = 0;
  for (const g of nn) { g.computeVertexNormals(); pos.set(g.attributes.position.array, o * 3); nor.set(g.attributes.normal.array, o * 3); o += g.attributes.position.count; }
  const r = new THREE.BufferGeometry(); r.setAttribute('position', new THREE.BufferAttribute(pos, 3)); r.setAttribute('normal', new THREE.BufferAttribute(nor, 3)); return r;
}
