import { navegacion3d } from './navegacion3d.js';
// Visor 3D del avance de obra de una torre (Three.js). Módulo ES, lo usa AvanceObra.razor.
// Diorama: bloque de terreno con las 4 excavaciones (cotas de la tabla de torres o genéricas), solado,
// acero, zapata y pedestal (o parrilla), relleno y la torre montada según el % de montaje, por peso de
// las piezas del modelo de Modelos 3D (o una silueta aproximada si la torre no tiene modelo).
// Trabajadores, equipos, pluma y aparejo son una SIMULACIÓN ilustrativa (sin escala ni procedimiento).
// Los ancladeros de los vientos siguen el plano DW-001 y su memoria de cálculo (ver ANCLA).
import * as THREE from '../lib/three/three.module.min.js';
import { OrbitControls } from '../lib/three/OrbitControls.js';
import { miembrosParametricos } from './linea3d.js';

const PATAS = ['A', 'B', 'C', 'D'];
// SUPUESTO: pata A = (-x,+z), B = (+x,+z), C = (+x,-z), D = (-x,-z) del modelo.
const SIGNO = { A: [-1, 1], B: [1, 1], C: [1, -1], D: [-1, -1] };
const KG_M_SIN_PESO = 6;   // SUPUESTO: pieza sin peso en la lista de materiales
const CLAVES_PATA = ['exc', 'sol', 'niv', 'ace', 'con', 'rel'];
const CLAVES_TORRE = ['pre', 'mon', 'ver', 'ves'];

// Ancladeros de los vientos de la pluma. Fuentes: plano DW-001 "Plano de sistemas de ancladeros" rev. 0 (2026-09-10) y su memoria "Ancladero con tubo enterrado", escenario T5A.
const ANCLA = {
    ang: 60,          // DW-001 "Ángulo máximo de inclinación" y memoria (θ = 60° con la horizontal). El dibujo del plano
                      // no está a escala (se ve ~30°); manda la cota y la memoria (H = T·cos θ, V = T·sin θ).
    angMin: 10,       // DW-001 "Ángulo mínimo de inclinación"
    prof: 1.8,        // DW-001 notas: profundidad de fondo de excavación 1,8 m
    tuboL: 2.0,       // DW-001 notas: longitud efectiva del tubo 2 m
    tuboD: .1524,     // memoria, entradas: D = 152,4 mm (6" OD)
    guayaD: .013,     // memoria, entradas: d_g = 13 mm
    vueltas: 3,       // memoria, entradas: n = 3 vueltas efectivas
    cargaKg: 2200,    // DW-001 notas: carga máxima 2,2 t (se usa como peso máximo de cada izaje)
    // SUPUESTOS (el plano no los acota): planta de la excavación 2,6 × 1,0 m, zanja de salida 0,3 m, ancladeros en las
    // diagonales de la torre (como la planta general del plano) y terreno plano.
    excL: 2.6, excA: 1.0, zanja: .3,
};
const MAX_IZAJES = 8;   // SUPUESTO visual: si el día suma más izajes, se agrupan en 8
const _p = new THREE.Vector3(), _q = new THREE.Quaternion(), _s = new THREE.Vector3(), _m4 = new THREE.Matrix4(), EJE_Y = new THREE.Vector3(0, 1, 0);

const cacheModelos = new Map();
function leerModelo(url) {
    if (!cacheModelos.has(url))
        cacheModelos.set(url, fetch(url).then(r => r.ok ? r.json() : null).catch(() => null));
    return cacheModelos.get(url);
}

// Piezas del modelo de planos (mm → m), con su peso. Los stubs van con la cimentación.
function desdeModelo(js) {
    const unit = new Map();
    for (const f of js.lista_materiales || []) {
        const cant = parseFloat(f.cant) || 1;
        if (f.peso_kg) unit.set(`${f.hoja}|${f.marca}`, f.peso_kg / cant);
    }
    const miembros = [], stubs = [];
    for (const m of js.miembros || []) {
        const a = m.a.map(v => v / 1000), b = m.b.map(v => v / 1000);
        if (m.tramo === 'Stub') { stubs.push(a[1] < b[1] ? { pie: a, cab: b } : { pie: b, cab: a }); continue; }
        const L = Math.hypot(b[0] - a[0], b[1] - a[1], b[2] - a[2]);
        let w = null;
        for (const k of m.marcas || []) if (unit.has(k)) { w = unit.get(k); break; }
        miembros.push({ a, b, w: w ?? KG_M_SIN_PESO * L, r: .07 });
    }
    return { miembros, stubs, nombre: js.meta?.nombre || '', plano: js.meta?.plano_base || '' };
}

function desdeSilueta(familia, cuerpo) {
    const d = miembrosParametricos(familia || 'AA', cuerpo || 1);
    const miembros = d.M.map(([a, b, r]) => {
        const L = Math.hypot(b[0] - a[0], b[1] - a[1], b[2] - a[2]);
        return { a, b, w: KG_M_SIN_PESO * L, r: Math.max(.06, r * .5) };
    });
    return { miembros, stubs: [], nombre: 'Silueta aproximada', plano: '' };
}

// ---------- Texturas ----------
function ruido(g, w, h, n, colores, tam = [1, 2.5]) {
    for (let i = 0; i < n; i++) {
        g.fillStyle = colores[(Math.random() * colores.length) | 0];
        const r = tam[0] + Math.random() * (tam[1] - tam[0]);
        g.beginPath(); g.ellipse(Math.random() * w, Math.random() * h, r, r * (.6 + Math.random() * .6), Math.random() * 3, 0, 7); g.fill();
    }
}
function texEstratos() {
    const W = 256, H = 512, c = document.createElement('canvas'); c.width = W; c.height = H; const g = c.getContext('2d');
    // Horizontes del suelo con contactos ondulados (capa vegetal, limos, arcillas, arenas, roca meteorizada).
    const capas = [['#4a3b2a', 40], ['#6e5439', 110], ['#8a6844', 130], ['#a5845b', 110], ['#7f7a72', 122]];
    let y0 = 0;
    for (const [col, h] of capas) {
        g.fillStyle = col; g.beginPath(); g.moveTo(0, y0);
        for (let x = 0; x <= W; x += 16) g.lineTo(x, y0 + Math.sin(x / 37 + y0) * 4);
        g.lineTo(W, H); g.lineTo(0, H); g.closePath(); g.fill(); y0 += h;
    }
    const sombra = g.createLinearGradient(0, 0, 0, H); sombra.addColorStop(0, 'rgba(0,0,0,0)'); sombra.addColorStop(1, 'rgba(0,0,0,.22)');
    g.fillStyle = sombra; g.fillRect(0, 0, W, H);
    ruido(g, W, H, 2600, ['rgba(0,0,0,.14)', 'rgba(255,255,255,.07)', 'rgba(40,25,10,.18)'], [.6, 1.8]);
    ruido(g, W, H, 140, ['rgba(160,150,135,.55)', 'rgba(110,100,90,.5)'], [1.5, 3.5]);   // gravas
    const t = new THREE.CanvasTexture(c); t.colorSpace = THREE.SRGBColorSpace; t.wrapS = THREE.RepeatWrapping; t.anisotropy = 4; return t;
}
function texPasto() {
    const c = document.createElement('canvas'); c.width = c.height = 512; const g = c.getContext('2d');
    g.fillStyle = '#5f7d3c'; g.fillRect(0, 0, 512, 512);
    // Manchas suaves de tono + briznas.
    for (let i = 0; i < 40; i++) {
        const x = Math.random() * 512, y = Math.random() * 512, r = 40 + Math.random() * 90, gr = g.createRadialGradient(x, y, 0, x, y, r);
        const col = Math.random() > .5 ? '95,118,62' : '150,160,98'; gr.addColorStop(0, `rgba(${col},.35)`); gr.addColorStop(1, `rgba(${col},0)`);
        g.fillStyle = gr; g.fillRect(x - r, y - r, 2 * r, 2 * r);
    }
    for (let i = 0; i < 9000; i++) {
        const x = Math.random() * 512, y = Math.random() * 512, l = 2 + Math.random() * 4;
        g.strokeStyle = Math.random() > .55 ? 'rgba(62,84,38,.45)' : 'rgba(176,186,120,.35)'; g.lineWidth = .8;
        g.beginPath(); g.moveTo(x, y); g.lineTo(x + (Math.random() - .5) * 2, y - l); g.stroke();
    }
    const t = new THREE.CanvasTexture(c); t.colorSpace = THREE.SRGBColorSpace; t.wrapS = t.wrapT = THREE.RepeatWrapping; t.repeat.set(4, 4); t.anisotropy = 8; return t;
}
function texRelleno() {
    const c = document.createElement('canvas'); c.width = 64; c.height = 256; const g = c.getContext('2d');
    for (let i = 0; i < 8; i++) { g.fillStyle = i % 2 ? '#ad8858' : '#a07c4f'; g.fillRect(0, i * 32, 64, 32); g.fillStyle = 'rgba(60,40,20,.35)'; g.fillRect(0, i * 32 + 31, 64, 1); }
    ruido(g, 64, 256, 500, ['rgba(0,0,0,.12)', 'rgba(255,255,255,.08)'], [.5, 1.4]);
    const t = new THREE.CanvasTexture(c); t.colorSpace = THREE.SRGBColorSpace; t.wrapS = t.wrapT = THREE.RepeatWrapping; t.repeat.set(1, 2); return t;
}
// Sombra de contacto (degradado radial) bajo el diorama.
function texSombra() {
    const c = document.createElement('canvas'); c.width = c.height = 256; const g = c.getContext('2d');
    const gr = g.createRadialGradient(128, 128, 30, 128, 128, 128); gr.addColorStop(0, 'rgba(0,0,0,.42)'); gr.addColorStop(.55, 'rgba(0,0,0,.16)'); gr.addColorStop(1, 'rgba(0,0,0,0)');
    g.fillStyle = gr; g.fillRect(0, 0, 256, 256);
    return new THREE.CanvasTexture(c);
}
function etiqueta(txt) {
    const c = document.createElement('canvas'); c.width = c.height = 128; const g = c.getContext('2d');
    g.shadowColor = 'rgba(0,0,0,.35)'; g.shadowBlur = 10; g.shadowOffsetY = 3;
    g.fillStyle = 'rgba(255,255,255,.94)'; g.beginPath(); g.arc(64, 62, 40, 0, 7); g.fill();
    g.shadowColor = 'transparent'; g.lineWidth = 4; g.strokeStyle = '#EC1C24'; g.stroke();
    g.fillStyle = '#1f2a3a'; g.font = '700 46px system-ui, sans-serif'; g.textAlign = 'center'; g.textBaseline = 'middle'; g.fillText(txt, 64, 65);
    const t = new THREE.CanvasTexture(c); t.colorSpace = THREE.SRGBColorSpace;
    const s = new THREE.Sprite(new THREE.SpriteMaterial({ map: t, depthTest: false })); s.scale.set(.8, .8, 1); s.renderOrder = 10; return s;
}

const pieza = (geo, mat, x = 0, y = 0, z = 0) => { const m = new THREE.Mesh(geo, mat); m.position.set(x, y, z); m.castShadow = true; return m; };
const piv = (x = 0, y = 0, z = 0) => { const g = new THREE.Group(); g.position.set(x, y, z); return g; };
const caja = (w, h, d, mat) => { const m = new THREE.Mesh(new THREE.BoxGeometry(w, h, d), mat); m.castShadow = m.receiveShadow = true; return m; };
function barra(a, b, r, mat) {
    const va = new THREE.Vector3(...a), vb = new THREE.Vector3(...b), L = va.distanceTo(vb);
    const m = caja(r, L, r, mat); m.position.copy(va).add(vb).multiplyScalar(.5);
    m.quaternion.setFromUnitVectors(new THREE.Vector3(0, 1, 0), vb.clone().sub(va).normalize()); return m;
}
// Plano con las coordenadas de textura recortadas (uS = fracción del ancho, vS = fracción del alto desde arriba), para
// que el césped y los estratos de un bloque pequeño tengan la misma escala que los del diorama.
function planoTierra(w, h, mat, uS, vS) {
    const geo = new THREE.PlaneGeometry(w, h), uv = geo.attributes.uv;
    for (let i = 0; i < uv.count; i++) uv.setXY(i, uv.getX(i) * uS, 1 - (1 - uv.getY(i)) * vS);
    const m = new THREE.Mesh(geo, mat); m.receiveShadow = true; return m;
}
const semi = (x, z) => new THREE.Vector3(x, 0, z).normalize();
const ease = (a, b, k) => a + (b - a) * k;

// Entorno simple (cielo claro, suelo oscuro y dos paneles de luz) para los reflejos del acero galvanizado.
function entorno(renderer) {
    const pm = new THREE.PMREMGenerator(renderer), esc = new THREE.Scene();
    const cielo = new THREE.Mesh(new THREE.SphereGeometry(50, 32, 16), new THREE.MeshBasicMaterial({ side: THREE.BackSide, vertexColors: true }));
    const pos = cielo.geometry.attributes.position, col = [];
    for (let i = 0; i < pos.count; i++) { const k = (pos.getY(i) / 50 + 1) / 2; col.push(.35 + .6 * k, .33 + .62 * k, .3 + .7 * k); }
    cielo.geometry.setAttribute('color', new THREE.Float32BufferAttribute(col, 3)); esc.add(cielo);
    for (const [x, y, z] of [[20, 25, 10], [-25, 18, -15]]) {
        const l = new THREE.Mesh(new THREE.PlaneGeometry(14, 8), new THREE.MeshBasicMaterial({ color: 0xffffff, side: THREE.DoubleSide }));
        l.position.set(x, y, z); l.lookAt(0, 0, 0); esc.add(l);
    }
    const tex = pm.fromScene(esc, .04).texture; pm.dispose();
    esc.traverse(o => { o.geometry?.dispose(); o.material?.dispose(); });
    return tex;
}

export function crear(contenedor) { const v = new VisorAvance(contenedor); contenedor.__visor = v; return v; }   // __visor: diagnóstico desde la consola

// Pantalla completa del marco del visor (si el navegador no la permite, lo maximiza en la ventana).
export async function pantallaCompleta(marco) {
    const doc = document;
    if (doc.fullscreenElement === marco) { await doc.exitFullscreen(); return false; }
    if (marco.classList.contains('av3d-max')) { marco.classList.remove('av3d-max'); return false; }
    try { await marco.requestFullscreen({ navigationUI: 'hide' }); }
    catch { marco.classList.add('av3d-max'); }
    return true;
}

class VisorAvance {
    constructor(cont) {
        this.cont = cont;
        this.renderer = new THREE.WebGLRenderer({ antialias: true });
        this.renderer.setPixelRatio(Math.min(2, devicePixelRatio));
        this.renderer.shadowMap.enabled = true; this.renderer.shadowMap.type = THREE.PCFSoftShadowMap;
        this.renderer.outputColorSpace = THREE.SRGBColorSpace;
        this.renderer.toneMapping = THREE.ACESFilmicToneMapping; this.renderer.toneMappingExposure = .95;
        this.renderer.domElement.style.cssText = 'position:absolute;inset:0;width:100%;height:100%;display:block;';
        cont.appendChild(this.renderer.domElement);
        this.scene = new THREE.Scene();
        this.cam = new THREE.PerspectiveCamera(38, 1, 0.1, 1200);
        this.ctl = new OrbitControls(this.cam, this.renderer.domElement);
        this.navegacion = navegacion3d(this.ctl, this.renderer.domElement);
        this.ctl.enableDamping = true; this.ctl.maxPolarAngle = Math.PI * .49; this.ctl.autoRotateSpeed = .6;
        // Zoom hacia donde apunta el mouse (el centro de giro lo sigue: no se queda "pegado" al acercarse)
        // y paneo en el plano de la pantalla. Rueda dentro del visor = solo zoom (la página no se desplaza).
        this.ctl.minDistance = 1.2; this.ctl.maxDistance = 600;
        this.ctl.addEventListener('start', () => { this._vuelo = null; });   // el usuario toma la cámara: se corta el vuelo
        this._rueda = e => e.preventDefault();
        cont.addEventListener('wheel', this._rueda, { passive: false });
        this.fondo();
        this._mq = matchMedia('(prefers-color-scheme: dark)'); this._mq.addEventListener('change', this._fondo = () => this.fondo());
        this._mo = new MutationObserver(this._fondo); this._mo.observe(document.documentElement, { attributes: true });

        this.scene.add(new THREE.HemisphereLight(0xeef4ff, 0x5d4b38, 1.0));
        this.sol = new THREE.DirectionalLight(0xfff1dc, 2.6);
        this.sol.castShadow = true; this.sol.shadow.mapSize.set(2048, 2048); this.sol.shadow.bias = -0.0004; this.sol.shadow.normalBias = .02; this.sol.shadow.radius = 3;
        const relleno = new THREE.DirectionalLight(0xcfe0ff, .55); relleno.position.set(-30, 18, -22); this.scene.add(relleno);
        this.scene.environment = entorno(this.renderer);
        this.scene.add(this.sol, this.sol.target);

        this.tEstr = texEstratos(); this.tRel = texRelleno(); this.tPasto = texPasto(); this.tSombra = texSombra();
        this.M = {
            pared: new THREE.MeshStandardMaterial({ map: this.tEstr, side: THREE.BackSide, roughness: 1, transparent: true }),
            lado: new THREE.MeshStandardMaterial({ map: this.tEstr, roughness: 1, transparent: true }),
            pasto: new THREE.MeshStandardMaterial({ map: this.tPasto, roughness: .95, transparent: true }),
            suelo: new THREE.MeshStandardMaterial({ color: 0x7d6142, roughness: 1, transparent: true }),
            relleno: new THREE.MeshStandardMaterial({ map: this.tRel, roughness: 1, transparent: true }),
            relTope: new THREE.MeshStandardMaterial({ color: 0xa88458, roughness: 1, transparent: true }),
            solado: new THREE.MeshStandardMaterial({ color: 0x8f8d88, roughness: .9 }),
            conc: new THREE.MeshStandardMaterial({ color: 0xc9c5bd, roughness: .9, envMapIntensity: .4 }),
            acero: new THREE.LineBasicMaterial({ color: 0x9c4a2c }),
            galv: new THREE.MeshStandardMaterial({ color: 0xa3acb5, metalness: .75, roughness: .38, envMapIntensity: .9 }),
            hoy: new THREE.MeshStandardMaterial({ color: 0xec1c24, metalness: .3, roughness: .5 }),
            base: new THREE.MeshStandardMaterial({ color: 0x2f2c29, roughness: .55, metalness: .1 }),
            borde: new THREE.MeshStandardMaterial({ color: 0x5f7243, roughness: .9 }),
            torre: new THREE.MeshStandardMaterial({ metalness: .72, roughness: .36, envMapIntensity: 1 }),
            aisl: new THREE.MeshStandardMaterial({ color: 0x3f7f73, roughness: .3, metalness: .1 }),
        };
        this.TIERRA = [this.M.pasto, this.M.lado, this.M.pared, this.M.suelo, this.M.relleno, this.M.relTope];
        this.MQ = {
            amarillo: new THREE.MeshStandardMaterial({ color: 0xe8b324, roughness: .55 }),
            negro: new THREE.MeshStandardMaterial({ color: 0x2a2a2a, roughness: .8 }),
            vidrio: new THREE.MeshStandardMaterial({ color: 0x8fb3c7, roughness: .1, metalness: .2, transparent: true, opacity: .55 }),
            naranja: new THREE.MeshStandardMaterial({ color: 0xe2672a, roughness: .5 }),
            piel: new THREE.MeshStandardMaterial({ color: 0xc28b62, roughness: .8 }),
            jean: new THREE.MeshStandardMaterial({ color: 0x34465e, roughness: .8 }),
            casco: new THREE.MeshStandardMaterial({ color: 0xf4f4f0, roughness: .4 }),
            chaleco: new THREE.MeshStandardMaterial({ color: 0xff7a1a, roughness: .5, emissive: 0x331100 }),
            gris: new THREE.MeshStandardMaterial({ color: 0x6d737a, roughness: .6, metalness: .3 }),
            viento: new THREE.LineBasicMaterial({ color: 0x3b3b3b }),
            tubo: new THREE.MeshStandardMaterial({ color: 0x3e4349, roughness: .55, metalness: .6 }),
        };
        this.crearEquipos();

        this.vis = null; this.obj = null; this.prev = null;
        this.transp = false;
        this.reduce = matchMedia('(prefers-reduced-motion: reduce)').matches;
        this._ro = new ResizeObserver(() => this.tam()); this._ro.observe(cont); this.tam();
        this._raf = requestAnimationFrame(this._bucle = t => this.bucle(t));
    }

    // Fondo en degradado (más claro arriba) a partir de --av3d-fondo del tema.
    fondo() {
        let base;
        try { base = new THREE.Color(getComputedStyle(this.cont).getPropertyValue('--av3d-fondo').trim() || '#e9ecef'); } catch { base = new THREE.Color('#e9ecef'); }
        const arriba = base.clone().lerp(new THREE.Color(0xffffff), base.getHSL({}).l > .5 ? .55 : .12);
        const c = document.createElement('canvas'); c.width = 2; c.height = 256; const g = c.getContext('2d');
        const gr = g.createLinearGradient(0, 0, 0, 256); gr.addColorStop(0, '#' + arriba.getHexString()); gr.addColorStop(1, '#' + base.getHexString());
        g.fillStyle = gr; g.fillRect(0, 0, 2, 256);
        const t = new THREE.CanvasTexture(c); t.colorSpace = THREE.SRGBColorSpace;
        this.scene.background?.dispose?.(); this.scene.background = t;
    }

    tam() {
        const r = this.cont.getBoundingClientRect();
        this.renderer.setSize(Math.max(1, r.width), Math.max(1, r.height), false);
        this.cam.aspect = r.width / Math.max(1, r.height); this.cam.updateProjectionMatrix();
    }

    // ---------- Torre y diorama ----------
    async cargarTorre(datos, cuadro, previo) {
        const pedido = this._pedido = (this._pedido || 0) + 1;
        let fuente = null;
        if (datos.modeloUrl) { const js = await leerModelo(datos.modeloUrl); if (js?.miembros?.length) fuente = desdeModelo(js); }
        if (pedido !== this._pedido) return;   // se pidió otra torre mientras cargaba
        if (!fuente) fuente = desdeSilueta(datos.familia, datos.cuerpo);
        this.limpiar();
        this.datos = datos; this.fuente = fuente;
        this.armar();
        this.vis = null;
        this.actualizar(cuadro, previo);
        // Sin montaje todavía: se encuadra la cimentación (la torre completa deja el diorama diminuto).
        const tq = cuadro?.torre || cuadro?.Torre || {};
        this.vista((tq.pre > 0 || tq.mon > 0) ? 'torre' : 'cimentacion');
    }

    limpiar() {
        this.quitarResalte(true);
        if (!this.grupo) return;
        this.scene.remove(this.grupo);
        this.grupo.traverse(o => {
            if (o.geometry) o.geometry.dispose();
            if (o.isSprite) { o.material.map?.dispose(); o.material.dispose(); }
        });
        this.inst?.dispose?.();
        this.grupo = null;
    }

    armar() {
        const g = this.grupo = new THREE.Group(); this.scene.add(g);
        const F = this.fuente, M = this.M;

        // Miembros de la torre ordenados de abajo hacia arriba, con su peso acumulado.
        const mm = F.miembros.map(m => {
            let va = new THREE.Vector3(...m.a), vb = new THREE.Vector3(...m.b);
            if (va.y > vb.y) [va, vb] = [vb, va];
            const L = va.distanceTo(vb);
            return { va, vb, L, dir: vb.clone().sub(va).normalize(), w: m.w, r: m.r };
        }).filter(m => m.L > 1e-3).sort((p, q) => (p.va.y + p.vb.y) - (q.va.y + q.vb.y));   // por cota media: crece de abajo hacia arriba
        let acum = 0; for (const m of mm) { acum += m.w; m.cum = acum; }
        // Tono del acero: piernas y montantes un poco más oscuros que celosía y crucetas (la torre se lee por planos).
        const tPierna = new THREE.Color(0x8d969e), tCelosia = new THREE.Color(0xa6aeb6);
        for (const m of mm) m.tono = Math.abs(m.dir.y) > .85 ? tPierna : tCelosia;
        this.miembros = mm; this.peso = acum || 1; this._f = new Float32Array(mm.length); this._lotes = null;
        this.alto = Math.max(1, ...mm.map(m => m.vb.y));
        this.yMin = Math.min(0, ...mm.map(m => m.va.y));

        // Pie de cada pata: el stub del modelo o, si no hay, el punto más bajo de la esquina.
        this.POS = {};
        for (const p of PATAS) {
            const [sx, sz] = SIGNO[p];
            const st = F.stubs.filter(s => Math.sign(s.pie[0]) === sx && Math.sign(s.pie[2]) === sz)[0];
            if (st) { this.POS[p] = { x: st.pie[0], z: st.pie[2], stub: st }; continue; }
            let best = null, sc = -Infinity;
            for (const m of mm) {
                if (m.va.y > this.yMin + .6) continue;
                const s = Math.min(sx * m.va.x, sz * m.va.z);
                if (s > sc) { sc = s; best = m.va; }
            }
            const x = best ? best.x : sx * 3.5, z = best ? best.z : sz * 3.5;
            this.POS[p] = { x, z, stub: { pie: [x, -.4, z], cab: [x * .97, .6, z * .97] } };
        }

        // Cotas por pata (m) — vienen de la página (tabla de torres o genéricas).
        this.C = {};
        for (const p of PATAS) {
            const c = (this.datos.patas || []).find(x => x.letra === p)?.cotas || this.datos.cotas || {};
            const H = c.excProf || 3.2, Lx = c.excLargo || 3, Lz = c.excAncho || 3;
            const ZL = Math.min(c.zapLado || 2.4, Math.min(Lx, Lz) * .95), ZH = c.zapAlto || .45;
            const PL = Math.min(c.pedLado || .7, ZL), PA = c.pedAlto || (H - ZH + .3);
            this.C[p] = { H, Lx, Lz, ZL, ZH, PL, PA, parrilla: !!c.parrilla, pesada: !!c.pesada, pila: !!c.pila };
            if (c.pila) Object.assign(this.C[p], { Lx: Lx, Lz: Lx, R: Lx / 2, NA: Math.max(1, Math.round(H / .5)) });   // pila: diámetro = largo
        }

        // Bloque de terreno: medio lado según el ancho de la base y las fosas.
        const maxX = Math.max(...PATAS.map(p => Math.abs(this.POS[p].x) + this.C[p].Lx / 2));
        const maxZ = Math.max(...PATAS.map(p => Math.abs(this.POS[p].z) + this.C[p].Lz / 2));
        const B = this.B = Math.max(11, Math.max(maxX, maxZ) + 6.5);
        const PROF = Math.max(5, ...PATAS.map(p => this.C[p].H + 1.5));
        const forma = new THREE.Shape([new THREE.Vector2(-B, -B), new THREE.Vector2(B, -B), new THREE.Vector2(B, B), new THREE.Vector2(-B, B)]);
        for (const p of PATAS) {
            const { x, z } = this.POS[p], { Lx, Lz, pila, R } = this.C[p], h = new THREE.Path();
            if (pila) h.absarc(x, -z, R, 0, Math.PI * 2, true);
            else { h.moveTo(x - Lx / 2, -z - Lz / 2); h.lineTo(x + Lx / 2, -z - Lz / 2); h.lineTo(x + Lx / 2, -z + Lz / 2); h.lineTo(x - Lx / 2, -z + Lz / 2); }
            forma.holes.push(h);
        }
        this.tPasto.repeat.set(B / 2.75, B / 2.75);
        const sup = new THREE.Mesh(new THREE.ShapeGeometry(forma), M.pasto);
        sup.rotation.x = -Math.PI / 2; sup.receiveShadow = true; g.add(sup);
        for (let i = 0; i < 4; i++) {
            const l = new THREE.Mesh(new THREE.PlaneGeometry(2 * B, PROF), M.lado);
            const ang = i * Math.PI / 2; l.position.set(Math.sin(ang) * B, -PROF / 2, Math.cos(ang) * B); l.rotation.y = ang; l.receiveShadow = true; g.add(l);
        }
        // Zócalo oscuro, ribete del césped y sombra de contacto: el diorama se apoya en el piso de la escena.
        const base = new THREE.Mesh(new THREE.BoxGeometry(2 * B + .5, .45, 2 * B + .5), M.base);
        base.position.y = -PROF - .225; base.castShadow = base.receiveShadow = true; g.add(base);
        for (let i = 0; i < 4; i++) {
            const r = caja(i % 2 ? .12 : 2 * B + .12, .1, i % 2 ? 2 * B + .12 : .12, M.borde);
            r.position.set(i === 1 ? B : i === 3 ? -B : 0, -.03, i === 0 ? B : i === 2 ? -B : 0); g.add(r);
        }
        const sombra = new THREE.Mesh(new THREE.PlaneGeometry(B * 3.6, B * 3.6), new THREE.MeshBasicMaterial({ map: this.tSombra, transparent: true, depthWrite: false, toneMapped: false }));
        sombra.rotation.x = -Math.PI / 2; sombra.position.y = -PROF - .449; g.add(sombra);

        // Cimentación de cada pata.
        this.CIM = {};
        for (const p of PATAS) {
            const { x, z, stub } = this.POS[p], C = this.C[p], gp = new THREE.Group(); gp.position.set(x, 0, z); g.add(gp);
            if (C.pila) { this.CIM[p] = this.armarPila(gp, C, stub, x, z); const e = etiqueta(p); e.position.set(x + Math.sign(x) * (C.R + .9), .7, z + Math.sign(z) * (C.R + .9)); g.add(e); continue; }
            const pared = new THREE.Mesh(new THREE.BoxGeometry(C.Lx, C.H, C.Lz), M.pared); pared.position.y = -C.H / 2; pared.receiveShadow = true; gp.add(pared);
            const c = { g: gp };
            c.suelo = caja(C.Lx - .01, 1, C.Lz - .01, M.suelo); gp.add(c.suelo);
            c.solado = caja(C.Lx - .1, .07, C.Lz - .1, M.solado); c.solado.position.y = -C.H + .035; gp.add(c.solado);
            const yTope = -C.H + .07 + C.ZH + C.PA;   // corona del pedestal
            if (!C.parrilla) {
                const pts = [], n = 7, Z = C.ZL, P = C.PL, H = C.H;
                for (let i = 0; i < n; i++) { const t = -Z / 2 + .1 + i * (Z - .2) / (n - 1); pts.push(t, -H + .12, -Z / 2 + .1, t, -H + .12, Z / 2 - .1, -Z / 2 + .1, -H + .16, t, Z / 2 - .1, -H + .16, t); }
                for (const [sx, sz] of [[-1, -1], [1, -1], [1, 1], [-1, 1], [0, -1], [0, 1], [-1, 0], [1, 0]])
                    pts.push(sx * (P / 2 - .06), -H + .12, sz * (P / 2 - .06), sx * (P / 2 - .06), yTope - .05, sz * (P / 2 - .06));
                for (let k = 0; k < 6; k++) { const y = -H + C.ZH + .2 + k * ((yTope - (-H + C.ZH) - .3) / 5), h = P / 2 - .05; pts.push(-h, y, -h, h, y, -h, h, y, -h, h, y, h, h, y, h, -h, y, h, -h, y, h, -h, y, -h); }
                const gg = new THREE.BufferGeometry(); gg.setAttribute('position', new THREE.Float32BufferAttribute(pts, 3));
                c.acero = new THREE.LineSegments(gg, M.acero); c.nAcero = pts.length / 6; gp.add(c.acero);
                c.zap = caja(C.ZL, C.ZH, C.ZL, M.conc); gp.add(c.zap);
                c.ped = caja(C.PL, 1, C.PL, M.conc); gp.add(c.ped);
                c.stub = barra([stub.pie[0] - x, Math.min(stub.pie[1], yTope - .8), stub.pie[2] - z], [stub.cab[0] - x, stub.cab[1] + .1, stub.cab[2] - z], .1, M.galv); gp.add(c.stub);
            } else {
                // Parrilla (como en obra): emparrillado de perfiles en el fondo, stub con la pendiente de la pierna y
                // empalme arriba, diagonales del emparrillado al stub y riostra horizontal. Todo se arma junto con la
                // instalación de la parrilla (Nivelación), en este orden. Pesada: más vigas, más gruesas, tercera capa
                // y cuatro diagonales.
                c.parr = [];
                const Z = C.ZL, n = C.pesada ? 9 : 6, e = C.pesada ? .14 : .1, y0 = -C.H + .2 + (C.pesada ? 3 : 2) * e;
                const cab = new THREE.Vector3(stub.cab[0] - x, stub.cab[1] + .1, stub.cab[2] - z);
                const s = new THREE.Vector3(stub.cab[0] - stub.pie[0], stub.cab[1] - stub.pie[1], stub.cab[2] - stub.pie[2]).normalize();
                if (s.y < .5) s.set(0, 1, 0);
                const lim = Math.max(0, (Math.min(C.Lx, C.Lz) - Z) / 2 - .05);
                const pie = cab.clone().addScaledVector(s, -(cab.y - y0) / s.y);
                pie.x = Math.max(-lim, Math.min(lim, pie.x)); pie.z = Math.max(-lim, Math.min(lim, pie.z));
                for (let i = 0; i < n; i++) {
                    const t = -Z / 2 + .1 + i * (Z - .2) / (n - 1);
                    const b1 = caja(Z, e, e, M.galv); b1.position.set(pie.x, -C.H + .2, pie.z + t); c.parr.push(b1);
                }
                for (let i = 0; i < n; i++) {
                    const t = -Z / 2 + .1 + i * (Z - .2) / (n - 1);
                    const b2 = caja(e, e, Z, M.galv); b2.position.set(pie.x + t, -C.H + .2 + e, pie.z); c.parr.push(b2);
                }
                if (C.pesada) for (let i = 0; i < n; i += 2) {
                    const t = -Z / 2 + .1 + i * (Z - .2) / (n - 1);
                    const b3 = caja(Z, e, e, M.galv); b3.position.set(pie.x, -C.H + .2 + 2 * e, pie.z + t); c.parr.push(b3);
                }
                c.parr.push(barra([pie.x, y0, pie.z], cab.toArray(), C.pesada ? .2 : .16, M.galv));
                const emp = barra(cab.clone().addScaledVector(s, -.55).toArray(), cab.clone().addScaledVector(s, -.08).toArray(), C.pesada ? .3 : .26, M.galv);
                c.parr.push(emp);
                const enStub = f => pie.clone().addScaledVector(s, (cab.y - y0) / s.y * f);
                const sx = Math.sign(x) || 1, sz = Math.sign(z) || 1;
                const esq = C.pesada ? [[1, 1], [-1, 1], [1, -1], [-1, -1]] : [[sx, -sz], [-sx, sz]];
                const nudo = enStub(C.pesada ? .5 : .55), r = C.pesada ? .1 : .08;
                const bases = esq.map(([a, b]) => new THREE.Vector3(pie.x + a * Z * .4, y0, pie.z + b * Z * .4));
                bases.forEach(b => c.parr.push(barra(b.toArray(), nudo.toArray(), r, M.galv)));
                // Riostra horizontal entre las diagonales (a un tercio de su altura).
                const at = (b, f) => b.clone().lerp(nudo, f);
                if (C.pesada) for (let k = 0; k < 4; k++) c.parr.push(barra(at(bases[k], .35).toArray(), at(bases[[1, 3, 0, 2][k]], .35).toArray(), .06, M.galv));
                else c.parr.push(barra(at(bases[0], .35).toArray(), at(bases[1], .35).toArray(), .06, M.galv));
                c.parr.forEach(m => gp.add(m));
            }
            c.rel = caja(C.Lx - .02, 1, C.Lz - .02, [M.relleno, M.relleno, M.relTope, M.relleno, M.relleno, M.relleno]); gp.add(c.rel);
            this.CIM[p] = c;
            const e = etiqueta(p); e.position.set(x + Math.sign(x) * (C.Lx / 2 + .8), .7, z + Math.sign(z) * (C.Lz / 2 + .8)); g.add(e);
        }

        // Torre: una caja instanciada por pieza (se escala según lo montado).
        this.inst = new THREE.InstancedMesh(new THREE.BoxGeometry(1, 1, 1), M.torre, Math.max(1, mm.length));
        this.inst.castShadow = true; this.inst.receiveShadow = true; g.add(this.inst);
        for (let i = 0; i < mm.length; i++) this.inst.setColorAt(i, mm[i].tono);

        // Pluma de izaje (ilustrativa): cuatro cordones, celosía, placa de pie, polea de cabeza y polea de reenvío.
        // Fija desde el inicio, apoyada en el suelo y más alta que la torre completa
        // (antes subía con el montaje y dejaba ver el cable desde su pie hasta el suelo).
        const hP = Math.max(7, this.alto + 3), MQ = this.MQ;
        this.pluma = new THREE.Group(); g.add(this.pluma);
        {
            const pts = [], a = .18, esq = [[-a, -a], [a, -a], [a, a], [-a, a]];
            for (const [x, z] of esq) this.pluma.add(barra([x, 0, z], [x, hP, z], .06, MQ.amarillo));
            for (let y = 0; y < hP - .3; y += .6) for (let i = 0; i < 4; i++) { const [x1, z1] = esq[i], [x2, z2] = esq[(i + 1) % 4]; pts.push(x1, y, z1, x2, y + .6, z2); }
            const gg = new THREE.BufferGeometry(); gg.setAttribute('position', new THREE.Float32BufferAttribute(pts, 3));
            this.pluma.add(new THREE.LineSegments(gg, new THREE.LineBasicMaterial({ color: 0xb07a12 })));
            this.pluma.add(pieza(new THREE.BoxGeometry(.6, .05, .6), MQ.gris, 0, .025, 0));
            this.pluma.add(pieza(new THREE.BoxGeometry(.5, .08, .5), MQ.amarillo, 0, hP, 0));
            const cab = pieza(new THREE.CylinderGeometry(.15, .15, .06, 20), MQ.gris, 0, hP - .22, 0); cab.rotation.x = Math.PI / 2; this.pluma.add(cab);
            this.pluma.add(pieza(new THREE.BoxGeometry(.05, .36, .12), MQ.negro, 0, hP - .2, .05));
            const ree = pieza(new THREE.CylinderGeometry(.12, .12, .06, 18), MQ.gris, 0, .3, .26); ree.rotation.x = Math.PI / 2; this.pluma.add(ree);
            this.pluma.userData.h = hP;
        }
        // Gancho: motón (bloque y roldana) y gancho; los cables (dos ramales y eslingas) se redibujan por cuadro.
        this.gancho = new THREE.Group(); g.add(this.gancho);
        this.gancho.add(pieza(new THREE.BoxGeometry(.22, .32, .12), MQ.amarillo, 0, .42, 0));
        const rold = pieza(new THREE.CylinderGeometry(.09, .09, .14, 16), MQ.gris, 0, .48, 0); rold.rotation.x = Math.PI / 2; this.gancho.add(rold);
        this.gancho.add(pieza(new THREE.CylinderGeometry(.025, .025, .14, 8), MQ.gris, 0, .2, 0));
        const gch = pieza(new THREE.TorusGeometry(.07, .022, 8, 18, Math.PI * 1.45), MQ.gris, 0, .06, 0); gch.rotation.z = Math.PI * .8; this.gancho.add(gch);
        this.cable = new THREE.LineSegments(new THREE.BufferGeometry(), new THREE.LineBasicMaterial({ color: 0x2b2b2b })); g.add(this.cable);

        // Cadenas de aisladores (vestida, esquemáticas) en las puntas de los brazos.
        this.aisl = new THREE.Group(); g.add(this.aisl);
        const top = mm.filter(m => m.vb.y > this.alto * .6).map(m => m.vb);
        const puntas = [];
        for (const s of [-1, 1]) { const v = top.filter(p => Math.sign(p.x) === s).sort((p, q) => Math.abs(q.x) - Math.abs(p.x))[0]; if (v) puntas.push(v); }
        for (const p of puntas) for (let k = 0; k < 14; k++) {
            const d = new THREE.Mesh(new THREE.CylinderGeometry(.16, .16, .05, 14), M.aisl);
            d.position.set(p.x * .97, p.y - .3 - k * .2, p.z); d.castShadow = true; this.aisl.add(d);
        }

        // Ancladeros de los vientos (DW-001): en las diagonales, a la distancia Lh en que el viento queda a 60° con la
        // horizontal cuando la pluma está en lo más alto del montaje; más abajo el ángulo es menor (el plano admite
        // de 10° a 60°). Si así cayeran sobre el diorama, se retiran hasta quedar fuera (el ángulo baja).
        const tipMax = this.puntaPluma(this.alto).tip;
        const LhDis = tipMax.y / Math.tan(ANCLA.ang * Math.PI / 180), Lh = Math.max(LhDis, B * Math.SQRT2 + 3);
        this.vientos = { lh: Lh, angulo: Math.atan2(tipMax.y, Lh) * 180 / Math.PI, retirado: Lh > LhDis + .01 };
        this.ancladeros = []; this.OJOS = [];
        for (const [sx, sz] of [[1, 1], [-1, 1], [-1, -1], [1, -1]]) {
            const dir = new THREE.Vector3(sx, 0, sz).normalize();
            const { grupo, ojo } = this.armarAncladero(B, PROF);
            const pos = new THREE.Vector3(tipMax.x, 0, tipMax.z).addScaledVector(dir, Lh + ojo.z);   // el ojo queda a Lh
            grupo.position.copy(pos); grupo.rotation.y = Math.atan2(-dir.x, -dir.z);                 // +z local hacia la torre
            grupo.visible = false; g.add(grupo); grupo.updateMatrixWorld(true);
            this.ancladeros.push(grupo); this.OJOS.push(grupo.localToWorld(ojo.clone()));
        }
        this.lineas = new THREE.LineSegments(new THREE.BufferGeometry(), this.MQ.viento); g.add(this.lineas);

        // Sol y sombras a la escala del diorama (y de los ancladeros).
        const R = Math.max(B, this.alto * .6, Lh + 3);
        this.sol.position.set(R * .8, R * 1.6, R * .65); this.sol.target.position.set(0, 0, 0);
        Object.assign(this.sol.shadow.camera, { left: -R * 1.1, right: R * 1.1, top: R * 1.1, bottom: -R * 1.1, near: 1, far: R * 5 });
        this.sol.shadow.camera.updateProjectionMatrix();
        this.ctl.minDistance = 4; this.ctl.maxDistance = Math.max(120, this.alto * 4);
        this.aplicarTransparencia();
    }

    // Posición de la pluma: fija en el suelo (no depende de lo montado), algo inclinada hacia +x (el lado por donde
    // sube la carga). La inclinación es pequeña para que la punta quede sobre la torre aunque la pluma sea alta.
    puntaPluma(_alto) {
        const h = this.pluma.userData.h, pos = new THREE.Vector3(.6, 0, .4), rot = new THREE.Euler(.03, 0, -.04);
        return { pos, rot, tip: new THREE.Vector3(0, h, 0).applyEuler(rot).add(pos) };
    }

    // Ancladero con tubo enterrado (DW-001, vistas 1 a 4): bloque de terreno en corte con la excavación rellena, el tubo
    // Schedule "tipo muerto" horizontal y perpendicular al viento, la guaya con sus vueltas al centro del tubo, la zanja
    // de salida hacia el viento y el ojo de la guaya en superficie. +z local = hacia la torre. Devuelve el ojo en local.
    armarAncladero(B, PROF) {
        const M = this.M, A = ANCLA, gr = new THREE.Group();
        const yEje = -(A.prof - A.tuboD / 2);                               // eje del tubo apoyado en el fondo
        const ySal = yEje + A.tuboD / 2;                                    // la guaya sale por encima del tubo
        const zOjo = -ySal / Math.tan(A.ang * Math.PI / 180);               // sale hacia el viento con su ángulo
        const xM = A.excL / 2 + .6, z0 = -(A.excA / 2 + .7), z1 = zOjo + .9, DP = A.prof + .7, W = 2 * xM, D = z1 - z0, zc = (z0 + z1) / 2;
        // Bloque: césped, cuatro caras en corte con los mismos estratos del diorama y zócalo.
        const sup = planoTierra(W, D, M.pasto, W / (2 * B), D / (2 * B)); sup.rotation.x = -Math.PI / 2; sup.position.set(0, 0, zc); gr.add(sup);
        for (const [w, x, z, ry] of [[W, 0, z1, 0], [W, 0, z0, Math.PI], [D, xM, zc, Math.PI / 2], [D, -xM, zc, -Math.PI / 2]]) {
            const l = planoTierra(w, DP, M.lado, w / (2 * B), DP / PROF); l.position.set(x, -DP / 2, z); l.rotation.y = ry; gr.add(l);
        }
        const zo = caja(W + .3, .3, D + .3, M.base); zo.position.set(0, -DP - .15, zc); gr.add(zo);
        // Excavación rellena y compactada (DW-001, nota), con su huella y la de la zanja en superficie.
        const rel = caja(A.excL, A.prof, A.excA, M.relleno); rel.position.y = -A.prof / 2; rel.castShadow = false; gr.add(rel);
        const hue = caja(A.excL, .025, A.excA, M.relTope); hue.position.y = .012; gr.add(hue);
        const zj = caja(A.zanja, .02, zOjo + .15 - A.excA / 2, M.relTope); zj.position.set(0, .01, (A.excA / 2 + zOjo + .15) / 2); gr.add(zj);
        // Tubo y vueltas de la guaya al centro.
        const tubo = pieza(new THREE.CylinderGeometry(A.tuboD / 2, A.tuboD / 2, A.tuboL, 24), this.MQ.tubo, 0, yEje, 0); tubo.rotation.z = Math.PI / 2; gr.add(tubo);
        for (let k = 0; k < A.vueltas; k++) {
            const v = pieza(new THREE.TorusGeometry(A.tuboD / 2 + A.guayaD / 2, A.guayaD / 2, 6, 24), this.MQ.gris, (k - (A.vueltas - 1) / 2) * A.guayaD * 1.6, yEje, 0);
            v.rotation.y = Math.PI / 2; gr.add(v);
        }
        // Guaya hasta la superficie y ojo para la fijación del viento.
        const yOjo = .12, ojo = new THREE.Vector3(0, yOjo + .07, zOjo + .02);
        gr.add(new THREE.Line(new THREE.BufferGeometry().setFromPoints([new THREE.Vector3(0, ySal, 0), new THREE.Vector3(0, 0, zOjo), new THREE.Vector3(0, yOjo - .07, zOjo + .01)]), this.MQ.viento));
        const ar = pieza(new THREE.TorusGeometry(.07, .012, 8, 20), this.MQ.gris, 0, yOjo, zOjo + .01); ar.rotation.y = Math.PI / 2; gr.add(ar);
        return { grupo: gr, ojo };
    }

    // Pila excavada (APROXIMACIÓN): fuste cilíndrico revestido con anillos de concreto de 0,5 m que se funden de
    // arriba hacia abajo a medida que avanza la excavación; luego canasta de acero, concreto del fuste y stub.
    armarPila(gp, C, stub, x, z) {
        const M = this.M, R = C.R, H = C.H, c = { g: gp, anillos: [] };
        const pared = new THREE.Mesh(new THREE.CylinderGeometry(R, R, H, 40, 1, true), M.pared); pared.position.y = -H / 2; pared.receiveShadow = true; gp.add(pared);
        c.suelo = new THREE.Mesh(new THREE.CylinderGeometry(R - .01, R - .01, 1, 40), M.suelo); c.suelo.receiveShadow = true; gp.add(c.suelo);
        const e = .12, alto = H / C.NA;
        const perfil = [new THREE.Vector2(R - e, 0), new THREE.Vector2(R - .005, 0), new THREE.Vector2(R - .005, alto - .03), new THREE.Vector2(R - e, alto - .03)];
        const geoAnillo = new THREE.LatheGeometry(perfil, 40);
        for (let k = 0; k < C.NA; k++) {
            const a = new THREE.Mesh(geoAnillo, M.conc); a.position.y = -(k + 1) * alto; a.castShadow = a.receiveShadow = true; a.visible = false; gp.add(a); c.anillos.push(a);
        }
        const pts = [], rr = R - e - .08;
        for (let i = 0; i < 10; i++) { const t = i / 10 * Math.PI * 2; pts.push(Math.cos(t) * rr, -H + .1, Math.sin(t) * rr, Math.cos(t) * rr, .1, Math.sin(t) * rr); }
        for (let y = -H + .3; y < 0; y += .5) for (let i = 0; i < 16; i++) {
            const t1 = i / 16 * Math.PI * 2, t2 = (i + 1) / 16 * Math.PI * 2;
            pts.push(Math.cos(t1) * rr, y, Math.sin(t1) * rr, Math.cos(t2) * rr, y, Math.sin(t2) * rr);
        }
        const gg = new THREE.BufferGeometry(); gg.setAttribute('position', new THREE.Float32BufferAttribute(pts, 3));
        c.acero = new THREE.LineSegments(gg, M.acero); c.nAcero = pts.length / 6; gp.add(c.acero);
        c.fuste = new THREE.Mesh(new THREE.CylinderGeometry(R - e, R - e, 1, 40), M.conc); c.fuste.castShadow = c.fuste.receiveShadow = true; gp.add(c.fuste);
        c.dado = caja(Math.min(C.PL + .3, 1.1), .5, Math.min(C.PL + .3, 1.1), M.conc); c.dado.position.y = .25; gp.add(c.dado);
        c.stub = barra([stub.pie[0] - x, -1.2, stub.pie[2] - z], [stub.cab[0] - x, stub.cab[1] + .1, stub.cab[2] - z], .1, M.galv); gp.add(c.stub);
        return c;
    }

    // ---------- Trabajadores y equipos ----------
    crearEquipos() {
        const MQ = this.MQ, s = this.scene;
        const obrero = () => {
            const g = new THREE.Group(), cuerpo = piv(0, .85, 0); g.add(cuerpo);
            for (const x of [-1, 1]) cuerpo.add(pieza(new THREE.CapsuleGeometry(.07, .62, 4, 8), MQ.jean, x * .09, -.42, 0));
            const torso = piv(0, 0, 0); cuerpo.add(torso);
            torso.add(pieza(new THREE.CapsuleGeometry(.16, .34, 4, 10), MQ.chaleco, 0, .24, 0));
            torso.add(pieza(new THREE.SphereGeometry(.11, 12, 10), MQ.piel, 0, .62, 0));
            torso.add(pieza(new THREE.SphereGeometry(.125, 14, 8, 0, Math.PI * 2, 0, Math.PI / 2), MQ.casco, 0, .66, 0));
            const brazos = [];
            for (const x of [-1, 1]) { const h = piv(x * .21, .42, 0); h.add(pieza(new THREE.CapsuleGeometry(.05, .44, 4, 8), MQ.chaleco, 0, -.24, 0)); torso.add(h); brazos.push(h); }
            const herr = piv(0, .1, .32); torso.add(herr);
            g.userData = { cuerpo, torso, brazos, herr, fase: Math.random() * 6 };
            g.visible = false; s.add(g); return g;
        };
        const pala = () => { const h = new THREE.Group(); h.add(pieza(new THREE.CylinderGeometry(.02, .02, 1.1, 6), MQ.negro)); h.add(pieza(new THREE.BoxGeometry(.22, .02, .28), MQ.gris, 0, -.55, .08)); h.rotation.x = .5; return h; };
        // Rastrillo (regar el solado) y vibrador de inmersión (concreto), en la mano como la pala.
        const rastrillo = () => { const h = new THREE.Group(); h.add(pieza(new THREE.CylinderGeometry(.018, .018, 1.4, 6), MQ.negro)); h.add(pieza(new THREE.BoxGeometry(.45, .05, .05), MQ.gris, 0, -.7, .03)); h.rotation.x = .9; return h; };
        const vibrador = () => { const h = new THREE.Group(); h.add(pieza(new THREE.CylinderGeometry(.035, .035, .55, 8), MQ.gris, 0, -.42, .05)); h.add(pieza(new THREE.CylinderGeometry(.015, .015, .5, 6), MQ.negro, 0, .05, -.05)); h.rotation.x = .25; return h; };
        this.cuadrilla = Array.from({ length: 20 }, obrero);
        this.palas = this.cuadrilla.map(o => { const p = pala(); p.visible = false; o.userData.herr.add(p); return p; });
        this.cuadrilla.forEach(o => {
            const u = o.userData;
            u.tools = { pala: this.palas[this.cuadrilla.indexOf(o)], rastrillo: rastrillo(), vibrador: vibrador() };
            u.herr.add(u.tools.rastrillo); u.herr.add(u.tools.vibrador); u.tools.rastrillo.visible = u.tools.vibrador.visible = false;
        });
        // Casco de color por oficio (topógrafo blanco, maestro azul, obreros amarillos) y chaleco reflectivo con franjas.
        const cascos = [0xf2c230, 0xf2c230, 0x2f6fd0, 0xf2c230, 0xffffff, 0xf2c230, 0xe2672a, 0xf2c230];
        const franja = new THREE.MeshStandardMaterial({ color: 0xe8eef2, roughness: .25, metalness: .4, emissive: 0x2a2a2a });
        this.cuadrilla.forEach((o, i) => {
            const t = o.userData.torso;
            t.children[2].material = new THREE.MeshStandardMaterial({ color: cascos[i % cascos.length], roughness: .4 });
            for (const y of [.14, .32]) t.add(pieza(new THREE.CylinderGeometry(.163, .163, .03, 14), franja, 0, y, 0));
        });

        // Utilería de cada pata (una por pata, se reutiliza): carretilla, trípode con estación total, mira con prisma y viga.
        const carretilla = () => {
            const g = new THREE.Group();
            const tolva = new THREE.Mesh(new THREE.CylinderGeometry(.42, .26, .3, 4, 1, true), MQ.naranja); tolva.rotation.y = Math.PI / 4; tolva.scale.set(1, 1, 1.5); tolva.position.set(0, .55, 0); tolva.castShadow = true; g.add(tolva);
            const mezcla = pieza(new THREE.BoxGeometry(.5, .05, .75), this.M.conc, 0, .66, 0); g.add(mezcla);
            const rueda = pieza(new THREE.CylinderGeometry(.17, .17, .08, 16), MQ.negro, 0, .17, .62); rueda.rotation.z = Math.PI / 2; g.add(rueda);
            for (const x of [-.22, .22]) { g.add(barra([x, .45, .5], [x, .6, -.75], .018, MQ.gris)); g.add(barra([x, .45, -.15], [x, 0, -.2], .015, MQ.gris)); }
            g.userData = { tolva, mezcla }; g.visible = false; s.add(g); return g;
        };
        const tripode = () => {
            const g = new THREE.Group(), h = 1.45;
            for (let i = 0; i < 3; i++) { const a = i * Math.PI * 2 / 3; g.add(barra([Math.cos(a) * .42, 0, Math.sin(a) * .42], [0, h, 0], .015, MQ.amarillo)); }
            g.add(pieza(new THREE.CylinderGeometry(.09, .09, .04, 14), MQ.gris, 0, h + .02, 0));
            const est = piv(0, h + .04, 0); g.add(est);
            est.add(pieza(new THREE.BoxGeometry(.16, .2, .12), MQ.amarillo, 0, .12, 0));
            const lente = pieza(new THREE.CylinderGeometry(.04, .045, .2, 12), MQ.negro, 0, .17, .02); lente.rotation.x = Math.PI / 2; est.add(lente);
            g.userData = { est }; g.visible = false; s.add(g); return g;
        };
        const mira = () => {
            const g = new THREE.Group(), blanco = new THREE.MeshStandardMaterial({ color: 0xf5f5f0, roughness: .5 }), rojo = new THREE.MeshStandardMaterial({ color: 0xd2312a, roughness: .5 });
            for (let k = 0; k < 5; k++) g.add(pieza(new THREE.CylinderGeometry(.02, .02, .5, 8), k % 2 ? rojo : blanco, 0, .25 + k * .5, 0));
            g.add(pieza(new THREE.SphereGeometry(.06, 12, 10), new THREE.MeshStandardMaterial({ color: 0x9ad0ff, roughness: .05, metalness: .6 }), 0, 2.55, 0));
            g.add(pieza(new THREE.CylinderGeometry(.08, .08, .03, 14), MQ.amarillo, 0, 2.48, 0));
            g.visible = false; s.add(g); return g;
        };
        const viga = () => { const g = new THREE.Group(); g.add(pieza(new THREE.BoxGeometry(2.1, .1, .1), this.M.galv)); g.visible = false; s.add(g); return g; };
        this.util = { carretilla: PATAS.map(carretilla), tripode: PATAS.map(tripode), mira: PATAS.map(mira), viga: PATAS.map(viga) };

        // Prearmado: una sección de celosía tendida en el piso sobre durmientes, un arrume de
        // ángulos al lado y un ángulo suelto que carga un obrero. ILUSTRATIVO: no corresponde a una sección real del modelo.
        {
            const g = this.prearm = new THREE.Group(), LP = 5, AN = 1.5, Y = .2, madera = new THREE.MeshStandardMaterial({ color: 0x8a6239, roughness: .9 });
            for (let k = 0; k < 4; k++) g.add(pieza(new THREE.BoxGeometry(AN + .5, .14, .16), madera, 0, .07, -LP / 2 + .3 + k * (LP - .6) / 3));
            for (const x of [-AN / 2, AN / 2]) g.add(barra([x, Y, -LP / 2], [x, Y, LP / 2], .055, this.M.galv));
            const n = 5;
            for (let k = 0; k <= n; k++) {
                const z = -LP / 2 + k * LP / n;
                g.add(barra([-AN / 2, Y, z], [AN / 2, Y, z], .035, this.M.galv));
                if (k < n) g.add(barra([k % 2 ? -AN / 2 : AN / 2, Y, z], [k % 2 ? AN / 2 : -AN / 2, Y, z + LP / n], .03, this.M.galv));
            }
            // Arrume de ángulos sueltos junto a la sección.
            for (let k = 0; k < 6; k++) g.add(barra([AN / 2 + .7 + (k % 3) * .12, .06 + Math.floor(k / 3) * .09, -1.8], [AN / 2 + .7 + (k % 3) * .12, .06 + Math.floor(k / 3) * .09, 1.6], .04, this.M.galv));
            // Ángulo que lleva un obrero (se mueve con él) y llave de los que pernan.
            g.userData.angulo = barra([0, 0, -1.1], [0, 0, 1.1], .04, this.M.galv); g.add(g.userData.angulo);
            g.userData.LP = LP; g.userData.AN = AN; g.userData.Y = Y;
            g.visible = false; s.add(g);
        }

        const retro = this.retro = new THREE.Group(); s.add(retro); retro.visible = false;
        retro.add(pieza(new THREE.BoxGeometry(2.6, .7, 1.5), MQ.amarillo, 0, .95, 0));
        retro.add(pieza(new THREE.BoxGeometry(1.1, .5, 1.3), MQ.amarillo, 1.05, 1.5, 0));
        retro.add(pieza(new THREE.BoxGeometry(1.05, 1.05, 1.25), MQ.vidrio, -.15, 1.85, 0));
        retro.add(pieza(new THREE.BoxGeometry(1.15, .08, 1.35), MQ.amarillo, -.15, 2.4, 0));
        for (const [x, r, w] of [[-.75, .62, .45], [1.0, .42, .35]]) for (const z of [-.72, .72]) { const rr = pieza(new THREE.CylinderGeometry(r, r, w, 18), MQ.negro, x, r, z); rr.rotation.x = Math.PI / 2; retro.add(rr); }
        retro.add(pieza(new THREE.BoxGeometry(.35, .6, 1.9), MQ.amarillo, 2.15, .55, 0));
        const b0 = piv(-1.35, 1.2, 0); retro.add(b0);
        b0.add(pieza(new THREE.BoxGeometry(2.2, .22, .22), MQ.amarillo, -1.1, 0, 0));
        const b1 = piv(-2.2, 0, 0); b0.add(b1);
        b1.add(pieza(new THREE.BoxGeometry(.2, 1.7, .2), MQ.amarillo, 0, -.85, 0));
        const b2 = piv(0, -1.7, 0); b1.add(b2);
        b2.add(pieza(new THREE.BoxGeometry(.5, .35, .55), MQ.gris, -.15, -.12, 0));
        retro.userData = { b0, b1, b2 }; retro.scale.setScalar(.85);

        const trompo = this.trompo = new THREE.Group(); s.add(trompo); trompo.visible = false;
        trompo.add(pieza(new THREE.BoxGeometry(1.6, .12, .9), MQ.gris, 0, .55, 0));
        for (const z of [-.5, .5]) { const r = pieza(new THREE.CylinderGeometry(.32, .32, .16, 16), MQ.negro, -.3, .32, z); r.rotation.x = Math.PI / 2; trompo.add(r); }
        trompo.add(pieza(new THREE.BoxGeometry(.12, .6, .12), MQ.gris, .7, .3, 0));
        const eje = piv(.1, 1.15, 0); eje.rotation.z = .55; trompo.add(eje);
        const tambor = new THREE.Mesh(new THREE.LatheGeometry([[0, -.55], [.38, -.5], [.55, -.15], [.55, .1], [.3, .55], [.22, .62]].map(([r, y]) => new THREE.Vector2(r, y)), 20), MQ.naranja);
        tambor.castShadow = true; eje.add(tambor); tambor.add(pieza(new THREE.BoxGeometry(.04, .5, .2), MQ.gris, .56, 0, 0));
        trompo.userData = { tambor };

        const rana = this.rana = new THREE.Group(); s.add(rana); rana.visible = false;
        rana.add(pieza(new THREE.BoxGeometry(.35, .08, .45), MQ.gris, 0, .04, 0)); rana.add(pieza(new THREE.CylinderGeometry(.09, .12, .6, 10), MQ.amarillo, 0, .38, 0)); rana.add(pieza(new THREE.BoxGeometry(.5, .05, .05), MQ.negro, 0, .78, -.15));
        const malacate = this.malacate = new THREE.Group(); s.add(malacate); malacate.visible = false;
        malacate.add(pieza(new THREE.BoxGeometry(1.2, .5, .8), MQ.naranja, 0, .25, 0));
        const t = pieza(new THREE.CylinderGeometry(.22, .22, .7, 16), MQ.gris, 0, .7, 0); t.rotation.x = Math.PI / 2; malacate.add(t);
    }

    postura(o, tipo, t) {
        const u = o.userData, k = t / 1000 + u.fase;
        u.cuerpo.position.y = .85; u.torso.rotation.set(0, 0, 0); u.brazos.forEach(b => b.rotation.set(0, 0, 0));
        if (tipo === 'pala') { const c = (Math.sin(k * 2.4) + 1) / 2; u.torso.rotation.x = .2 + .55 * c; u.brazos.forEach(b => b.rotation.x = -.6 - .9 * c); }
        else if (tipo === 'agachado') { u.cuerpo.position.y = .55; u.torso.rotation.x = .9; u.brazos.forEach((b, i) => b.rotation.x = -1.2 + Math.sin(k * 3 + i * 1.7) * .45); }
        else if (tipo === 'vaciar') { u.torso.rotation.x = .35; u.brazos.forEach(b => b.rotation.x = -1.1 + Math.sin(k * 1.5) * .15); }
        else if (tipo === 'regar') { u.torso.rotation.x = .55; u.torso.rotation.y = Math.sin(k * 1.8) * .35; u.brazos.forEach(b => { b.rotation.x = -1.0; b.rotation.z = Math.sin(k * 1.8) * .2; }); }
        else if (tipo === 'carretilla') { u.torso.rotation.x = .3; u.brazos.forEach(b => b.rotation.x = -.45); }
        else if (tipo === 'topo') { u.torso.rotation.x = .38; u.brazos.forEach((b, i) => b.rotation.x = -1.75 + (i ? Math.sin(k * .8) * .08 : 0)); }
        else if (tipo === 'mira') { u.brazos[0].rotation.x = -2.5; u.brazos[1].rotation.x = -1.5; u.torso.rotation.y = Math.sin(k * .4) * .06; }
        else if (tipo === 'amarrar') { u.cuerpo.position.y = .52; u.torso.rotation.x = 1.0; u.brazos.forEach((b, i) => { b.rotation.x = -1.35; b.rotation.z = (i ? 1 : -1) * (.25 + Math.sin(k * 9 + i) * .22); }); }
        else if (tipo === 'cargar') { u.cuerpo.position.y = .85 + Math.abs(Math.sin(k * 4)) * .02; u.torso.rotation.x = .12; u.brazos.forEach(b => b.rotation.x = -.95); }
        else if (tipo === 'vibrar') { u.cuerpo.position.y = .8; u.torso.rotation.x = .55 + Math.sin(k * 40) * .015; u.brazos.forEach(b => b.rotation.x = -1.05 + Math.sin(k * 1.2) * .1); }
        else if (tipo === 'rana') { u.cuerpo.position.y = .85 + Math.abs(Math.sin(k * 14)) * .03; u.brazos.forEach(b => b.rotation.x = -.9); }
        else if (tipo === 'trepar') { u.torso.rotation.x = -.15; u.brazos.forEach((b, i) => b.rotation.x = -2.6 + Math.sin(k * 2 + i * Math.PI) * .35); }
        else if (tipo === 'halar') { u.torso.rotation.x = -.25; u.brazos.forEach((b, i) => b.rotation.x = -1.3 + Math.sin(k * 2.2 + i) * .25); }
        else if (tipo === 'mirar') { u.torso.rotation.y = Math.sin(k * .6) * .5; u.brazos[1].rotation.x = Math.sin(k * .9) > .6 ? -2.2 : 0; }
    }

    // Punto de la pierna de la torre (esquina sx, sz) a la cota y: entre las piezas montadas que cruzan esa
    // cota, la que está más hacia la esquina (min(sx·x, sz·z)). Así el liniero queda sobre la pierna y no
    // en el aire frente a un brazo.
    puntoPierna(y, sx, sz, hasta) {
        let mejor = null, sc = -Infinity;
        for (let i = 0; i < hasta; i++) {
            const m = this.miembros[i];
            if (m.va.y > y || m.vb.y < y || m.dir.y < .85 || m.L < .8) continue;   // solo piezas casi verticales: piernas y montantes
            const f = (y - m.va.y) / (m.vb.y - m.va.y), x = m.va.x + (m.vb.x - m.va.x) * f, z = m.va.z + (m.vb.z - m.va.z) * f;
            const s = Math.min(sx * x, sz * z);
            if (s > sc) { sc = s; mejor = new THREE.Vector3(x, y, z); }
        }
        return sc > 0 ? mejor : null;
    }

    equipos(d, t, alto, nMont, plumaActiva) {
        const obj = this.obj, prev = this.prev;
        const v = (c, p, k) => Math.max(0, c?.patas?.[p]?.[k] ?? 0);
        const trabajo = (p, k) => { const a = obj?.patas?.[p]?.[k] ?? 0, b = prev?.patas?.[p]?.[k] ?? 0; return a > 0 && a > b + 1e-4; };
        let i = 0;
        // Actividad en curso por pata ese día (la página la manda con trabajar()): también los días sin registro nuevo.
        // Manda la que envía la página (la actividad elegida va primero); si no, la que avanzó ese día.
        const tarea = p => this._trab?.[p] || ['exc', 'con', 'rel', 'niv', 'ace', 'sol'].find(k => trabajo(p, k)) || null;
        const usar = (tipo, x, y, z, rotY, herr) => {
            const o = this.cuadrilla[i]; if (!o) return;
            o.visible = true; o.position.set(x, y, z); o.rotation.y = rotY;
            const nombre = herr === true ? 'pala' : herr || null;
            for (const [k, h] of Object.entries(o.userData.tools)) h.visible = k === nombre;
            this.postura(o, tipo, t); i++;
        };
        this.retro.visible = this.trompo.visible = this.rana.visible = this.malacate.visible = false;
        for (const l of Object.values(this.util)) l.forEach(o => o.visible = false);
        if (prev || this._trab) for (const p of PATAS) {
            const { x, z } = this.POS[p], C = this.C[p], r = semi(x, z), mira = Math.atan2(-r.x, -r.z), lado = new THREE.Vector3(-r.z, 0, r.x);
            const borde = Math.max(C.Lx, C.Lz) / 2 * Math.SQRT2;   // distancia al borde de la fosa en la diagonal
            const fondo = -C.H + (v(d, p, 'sol') > 0 ? .07 : 0);
            const dentro = Math.min(.7, Math.min(C.Lx, C.Lz) / 2 - .35);
            const k = tarea(p), enCurso = (_p, c) => k === c;
            if (enCurso(p, 'exc') && C.pila) {
                // Pila: excavación manual dentro del anillo y un ayudante en el borde.
                usar('pala', x, -C.H * v(d, p, 'exc'), z, mira, true);
                usar('mirar', x + r.x * (C.R + .6), 0, z + r.z * (C.R + .6), mira, false);
            } else if (enCurso(p, 'exc')) {
                if (!this.retro.visible) {
                    this.retro.visible = true; this.retro.position.set(x + r.x * (borde + 2.4), 0, z + r.z * (borde + 2.4)); this.retro.rotation.y = Math.atan2(r.z, -r.x) + Math.PI;
                    const c = (Math.sin(t / 900) + 1) / 2, u = this.retro.userData;
                    u.b0.rotation.z = -.35 + .45 * c; u.b1.rotation.z = .5 - .9 * c; u.b2.rotation.z = -.6 + 1.2 * c;
                }
                usar('pala', x + lado.x * dentro, -C.H * v(d, p, 'exc'), z + lado.z * dentro, mira + .4, true);
            } else if (enCurso(p, 'con')) {
                this.trompo.visible = true; this.trompo.position.set(x + r.x * (borde + 1.4) + lado.x * 1.6, 0, z + r.z * (borde + 1.4) + lado.z * 1.6); this.trompo.rotation.y = mira;
                this.trompo.userData.tambor.rotation.y = t / 260;
                usar('vaciar', x + r.x * (borde + .4), 0, z + r.z * (borde + .4), mira, false);
                usar('vibrar', x - lado.x * dentro, -C.H + .07 + C.ZH * Math.min(1, v(d, p, 'con') / .55), z - lado.z * dentro, mira, 'vibrador');
            } else if (enCurso(p, 'rel')) {
                const yr = -C.H + C.H * v(d, p, 'rel');
                this.rana.visible = true; this.rana.position.set(x + lado.x * dentro, yr + Math.abs(Math.sin(t / 70)) * .04, z + lado.z * dentro);
                usar('rana', x + lado.x * dentro + r.x * .45, yr, z + lado.z * dentro + r.z * .45, mira + Math.PI, false);
                usar('pala', x + r.x * (borde + .6), 0, z + r.z * (borde + .6), mira, true);
            } else if (k === 'parrilla' || (C.parrilla && k === 'niv')) {
                // Instalación de parrillas: dos obreros bajan/acomodan una viga del emparrillado y otro la nivela.
                const vg = this.util.viga[PATAS.indexOf(p)], va = Math.sin(t / 1400) * .25;
                vg.visible = true; vg.position.set(x + lado.x * va, fondo + .95, z + lado.z * va); vg.rotation.y = Math.atan2(-lado.z, lado.x);
                usar('cargar', vg.position.x + lado.x * .85, fondo, vg.position.z + lado.z * .85, mira + Math.PI / 2, false);
                usar('cargar', vg.position.x - lado.x * .85, fondo, vg.position.z - lado.z * .85, mira - Math.PI / 2, false);
                usar('agachado', x - r.x * dentro * .6, fondo, z - r.z * dentro * .6, mira + Math.PI, false);
            } else if (enCurso(p, 'sol')) {
                // Solado: concreto de limpieza en la cota de fondo; uno vierte desde la carretilla en el borde y otro lo riega.
                const ca = this.util.carretilla[PATAS.indexOf(p)], vaciando = (Math.sin(t / 900) + 1) / 2;
                ca.visible = true; ca.position.set(x + r.x * (borde + .5), 0, z + r.z * (borde + .5)); ca.rotation.y = mira;
                ca.userData.tolva.rotation.x = -vaciando * .5; ca.userData.mezcla.visible = vaciando < .7;
                usar('carretilla', x + r.x * (borde + 1.45), 0, z + r.z * (borde + 1.45), mira, false);
                usar('regar', x + lado.x * dentro, fondo, z + lado.z * dentro, mira + .3, 'rastrillo');
                usar('regar', x - lado.x * dentro, fondo, z - lado.z * dentro, mira - .3, 'rastrillo');
            } else if (enCurso(p, 'niv')) {
                // Nivelación: topógrafo con estación total en el borde y ayudante con la mira (prisma) en el fondo.
                const tr = this.util.tripode[PATAS.indexOf(p)], mi = this.util.mira[PATAS.indexOf(p)];
                const ex = x + r.x * (borde + 1.3) + lado.x * .9, ez = z + r.z * (borde + 1.3) + lado.z * .9;
                tr.visible = true; tr.position.set(ex, 0, ez);
                const px = x + lado.x * dentro * .4, pz = z + lado.z * dentro * .4;
                tr.userData.est.rotation.y = Math.atan2(px - ex, pz - ez);
                mi.visible = true; mi.position.set(px + r.x * .25, fondo, pz + r.z * .25);
                usar('topo', ex + r.x * .55, 0, ez + r.z * .55, Math.atan2(px - ex, pz - ez), false);
                usar('mira', px, fondo, pz, Math.atan2(ex - px, ez - pz), false);
            } else if (enCurso(p, 'ace')) {
                // Armado de acero: obreros agachados sobre la parrilla de varillas amarrando con alambre.
                usar('amarrar', x + lado.x * dentro, fondo + .15, z + lado.z * dentro, mira, false);
                usar('amarrar', x - lado.x * dentro, fondo + .15, z - lado.z * dentro, mira + Math.PI, false);
                usar('agachado', x - r.x * dentro * .5, fondo + .15, z - r.z * dentro * .5, mira + Math.PI / 2, false);
            } else if (k === 'otro') {
                usar('pala', x + r.x * (borde + .6), 0, z + r.z * (borde + .6), mira, true);
                usar('mirar', x + r.x * (borde + 1.6) + lado.x, 0, z + r.z * (borde + 1.6) + lado.z, mira, false);
            }
        }
        // Prearmado en curso: la cuadrilla alista la estructura en el piso, fuera de la huella de la torre (lado −x).
        const pre = this._pre, pa = this.prearm;
        pa.visible = !!pre;
        if (pre) {
            const xb = Math.max(...PATAS.map(p => Math.abs(this.POS[p].x) + this.C[p].Lx / 2));
            const cx = -(xb + Math.max(2.2, (this.B - xb) / 2)), u = pa.userData, Y = u.Y;
            pa.position.set(cx, 0, 0);
            // Dos pernando agachados sobre nudos de la sección, uno trae un ángulo del arrume y el maestro revisa.
            usar('amarrar', cx - u.AN / 2 - .45, 0, -1, Math.PI / 2, false);
            usar('amarrar', cx + u.AN / 2 + .45, 0, 1, -Math.PI / 2, false);
            usar('agachado', cx, Y, u.LP / 2 + .55, Math.PI, false);
            const va = Math.sin(t / 2200);                       // va y viene entre el arrume y la sección
            const ox = u.AN / 2 + 1.25 + .35 * va;
            u.angulo.position.set(ox - .32, .95, 0);             // en las manos (el obrero mira hacia la sección, −x)
            usar('cargar', cx + ox, 0, 0, -Math.PI / 2, false);
            usar('mirar', cx - u.AN / 2 - 1.6, 0, u.LP / 2 + .8, Math.PI * .75, false);
        }
        const pts = [];
        this.ancladeros.forEach(a => a.visible = !!plumaActiva);
        if (plumaActiva) {
            const pl = this.pluma, tip = new THREE.Vector3(0, pl.userData.h, 0).applyEuler(pl.rotation).add(pl.position);
            for (const o of this.OJOS) pts.push(tip, o);
            const w = this.POS.A, rA = semi(w.x, w.z), dA = Math.max(this.C.A.Lx, this.C.A.Lz) / 2 * Math.SQRT2 + 2.2;
            this.malacate.visible = true; this.malacate.position.set(w.x + rA.x * dA, 0, w.z + rA.z * dA); this.malacate.rotation.y = Math.atan2(rA.x, rA.z);
            pts.push(tip, pl.position.clone(), pl.position.clone(), new THREE.Vector3(0, .3, 0), new THREE.Vector3(0, .3, 0), this.malacate.position.clone().setY(.7));
            if (!pre?.elegida) usar('halar', this.malacate.position.x + rA.x * .9, 0, this.malacate.position.z + rA.z * .9, Math.atan2(-rA.x, -rA.z), false);
            const ret = this.POS.C, rc = semi(ret.x, ret.z), dC = Math.max(this.C.C.Lx, this.C.C.Lz) / 2 * Math.SQRT2 + 1.2;
            const pie = new THREE.Vector3(ret.x + rc.x * dC, 1, ret.z + rc.z * dC);
            // Manila guía de la carga (solo mientras sube algo).
            if (this._izando) { pts.push(this._cargaPos.clone(), pie); if (!pre?.elegida) usar('halar', pie.x, 0, pie.z, Math.atan2(-rc.x, -rc.z), false); }
            if (!pre?.elegida) usar('mirar', -1.2, 0, Math.min(this.B - 1, Math.abs(this.POS.A.z) + this.C.A.Lz + 2.5), Math.PI, false);
            // Linieros apoyados en las piernas montadas (pies sobre la pierna, manos por debajo del borde montado).
            // Si a esa cota no hay pierna (cabeza o crucetas), se baja hasta encontrar una.
            for (const [sx, sz] of (pre?.elegida ? [] : [[1, 1], [-1, -1]])) {
                let q = null;
                for (let yPie = alto - 2.1; yPie > .6 && !q; yPie -= .5) q = this.puntoPierna(yPie, sx, sz, Math.max(0, nMont - 1));
                if (!q) continue;
                const o = semi(sx, sz);
                usar('trepar', q.x + o.x * .3, q.y, q.z + o.z * .3, Math.atan2(-sx, -sz), false);
            }
        }
        this.lineas.geometry.dispose(); this.lineas.geometry = new THREE.BufferGeometry().setFromPoints(pts); this.lineas.visible = pts.length > 0;
        for (; i < this.cuadrilla.length; i++) this.cuadrilla[i].visible = false;
    }

    // ---------- Estado ----------
    // cuadro = { patas: { A: { exc, sol, niv, ace, con, rel } }, torre: { pre, mon, ver, ves } } (0–1; -1 = no aplica).
    actualizar(cuadro, previo) {
        const limpio = c => {
            const r = { patas: {}, torre: {} };
            for (const p of PATAS) { r.patas[p] = {}; for (const k of CLAVES_PATA) r.patas[p][k] = Math.max(0, c?.patas?.[p]?.[k] ?? 0); }
            for (const k of CLAVES_TORRE) r.torre[k] = Math.max(0, c?.torre?.[k] ?? 0);
            return r;
        };
        this.obj = limpio(cuadro);
        this.prev = previo ? limpio(previo) : null;
        if (!this.vis || this.reduce) this.vis = JSON.parse(JSON.stringify(this.obj));
    }

    pintarTorre(p, pHoy) {
        const meta = p * this.peso, desde = pHoy * this.peso, inst = this.inst;
        const rojo = new THREE.Color(0xec1c24);
        let alto = 0, n = 0;
        this.miembros.forEach((m, i) => {
            const pr = m.cum - m.w, f = meta >= m.cum ? 1 : meta <= pr ? 0 : (meta - pr) / m.w;
            if (f > 0) { alto = Math.max(alto, m.va.y + m.dir.y * m.L * f); n = i + 1; }
            this._f[i] = f; this.matrizMiembro(i, f, null);
            inst.setColorAt(i, m.cum > desde && f > 0 && p < 1 ? rojo : m.tono);
        });
        inst.instanceMatrix.needsUpdate = true; if (inst.instanceColor) inst.instanceColor.needsUpdate = true;
        return { alto, n };
    }

    // Matriz de una pieza montada en la fracción f de su largo, desplazada off (pieza colgando del gancho).
    matrizMiembro(i, f, off) {
        const m = this.miembros[i], L = Math.max(1e-4, m.L * f);
        _p.copy(m.va).addScaledVector(m.dir, L / 2); if (off) _p.add(off);
        _q.setFromUnitVectors(EJE_Y, m.dir); _s.set(f ? m.r : 0, L, f ? m.r : 0);
        this.inst.setMatrixAt(i, _m4.compose(_p, _q, _s));
    }

    // Izajes del día: las piezas entre el montaje anterior y el del registro, en el orden de montaje, en lotes de hasta
    // 2,2 t (carga máxima por izar del plano DW-001). SUPUESTO: cada izaje sale con la carga máxima.
    lotesIzado(desde, meta) {
        const clave = desde.toFixed(1) + '|' + meta.toFixed(1);
        if (this._lotes?.clave === clave) return this._lotes.v;
        const mm = this.miembros, hoy = [];
        mm.forEach((m, i) => { if (m.cum > desde + 1e-6 && m.cum - m.w < meta - 1e-6) hoy.push(i); });
        let grupos = [], cur = [], kg = 0;
        for (const i of hoy) {
            if (cur.length && kg + mm[i].w > ANCLA.cargaKg) { grupos.push(cur); cur = []; kg = 0; }
            cur.push(i); kg += mm[i].w;
        }
        if (cur.length) grupos.push(cur);
        if (grupos.length > MAX_IZAJES) { const k = Math.ceil(hoy.length / MAX_IZAJES); grupos = []; for (let j = 0; j < hoy.length; j += k) grupos.push(hoy.slice(j, j + k)); }
        const v = grupos.map(ix => {
            const caja3 = new THREE.Box3(); for (const i of ix) caja3.expandByPoint(mm[i].va).expandByPoint(mm[i].vb);
            const c = caja3.getCenter(new THREE.Vector3()), min = caja3.min, max = caja3.max;
            // Sube por fuera de la cara +x: lo justo para no tocar lo ya montado a esa altura.
            let xMax = max.x;
            for (let i = 0; i < ix[0]; i++) { const m = mm[i]; if (m.vb.y >= min.y - 1 && m.va.y <= max.y + 1) xMax = Math.max(xMax, m.va.x, m.vb.x); }
            const dx = xMax + .8 - min.x;
            // Eslingas a los dos extremos más altos del lote.
            const altos = []; for (const i of ix) for (const q of [mm[i].va, mm[i].vb]) if (q.y >= max.y - .3) altos.push(q);
            altos.sort((a, b) => a.x - b.x);
            const ganchos = altos.length ? [altos[0], altos[altos.length - 1]] : [c];
            return { ix, c, min: min.clone(), max: max.clone(), dx, ganchos };
        });
        this._lotes = { clave, v }; return v;
    }

    // Fase del izaje en curso (ciclo de 7 s por lote y 3 s con todo asentado): en el suelo junto a la cara, sube,
    // entra sobre su sitio, baja y asienta. Movimiento suavizado (sin saltos).
    estadoIzado(t, lotes) {
        if (!lotes.length) return null;
        const D = 7000, HOLD = 3000, u = t % (lotes.length * D + HOLD), k = Math.floor(u / D);
        if (k >= lotes.length) return { k, off: null };
        const ph = (u - k * D) / D, L = lotes[k], suave = x => x * x * (3 - 2 * x), tramo = (a, b) => suave(Math.min(1, Math.max(0, (ph - a) / (b - a))));
        const o0 = new THREE.Vector3(L.dx, .05 - L.min.y, 0), o1 = new THREE.Vector3(L.dx, .8, 0), o2 = new THREE.Vector3(0, .8, 0), o3 = new THREE.Vector3();
        let off;
        if (ph < .6) off = o0.lerp(o1, tramo(.12, .6));
        else if (ph < .78) off = o1.lerp(o2, tramo(.6, .78));
        else off = o2.lerp(o3, tramo(.78, .92));
        return { k, off };
    }

    // Lotes posteriores al que sube: aún no están; el que sube va desplazado.
    aplicarIzado(st, lotes) {
        for (let j = st.k; j < lotes.length; j++) for (const i of lotes[j].ix) this.matrizMiembro(i, j > st.k ? 0 : this._f[i], j === st.k ? st.off : null);
        this.inst.instanceMatrix.needsUpdate = true;
    }

    // Aparejo: polea de cabeza → motón del gancho en dos ramales, eslingas a la carga.
    aparejo(tip, st, lotes) {
        const polea = tip.clone().add(new THREE.Vector3(0, -.28, 0)), pts = [];
        let apex;
        this._izando = !!(st && st.off && st.k < lotes.length);
        const L = this._izando ? lotes[st.k] : null;
        if (L) {
            this._cargaPos = L.c.clone().add(st.off);
            apex = this._cargaPos.clone().add(new THREE.Vector3(0, L.max.y - L.c.y + 1.2, 0));
        } else { apex = tip.clone().add(new THREE.Vector3(.9, -3, 0)); this._cargaPos = apex.clone(); }
        if (apex.y > polea.y - .7) apex.y = polea.y - .7;
        if (L) for (const q of L.ganchos) pts.push(apex, q.clone().add(st.off));
        this.gancho.position.copy(apex);
        for (const s of [-.06, .06]) pts.push(polea.clone().add(new THREE.Vector3(s, 0, 0)), apex.clone().add(new THREE.Vector3(s, .5, 0)));
        this.cable.geometry.dispose(); this.cable.geometry = new THREE.BufferGeometry().setFromPoints(pts);
    }

    aplicar(t) {
        const d = this.vis;
        for (const p of PATAS) {
            const c = this.CIM[p], C = this.C[p], v = k => d.patas[p][k];
            const exc = v('exc'), rel = v('rel');
            // Stub de zapata o pila: se pone al terminar el amarre del acero, justo antes del vaciado (según el registro,
            // no la animación). Sin acero que aplique, aparece con el concreto.
            const o = k => this.obj?.patas?.[p]?.[k] ?? v(k), stubListo = o('ace') >= .999 || o('con') > 0;
            if (C.pila) {
                // Un anillo por cada 0,5 m excavado (la excavación del día se reparte en partes iguales).
                const hx = C.H * Math.max(0, exc), na = Math.min(C.NA, Math.floor(exc * C.NA + 1e-3));
                c.suelo.scale.y = Math.max(.001, C.H - hx); c.suelo.position.y = -C.H + c.suelo.scale.y / 2; c.suelo.visible = exc < .999;
                c.anillos.forEach((a, i) => a.visible = i < na);
                c.acero.visible = v('ace') > 0; c.acero.geometry.setDrawRange(0, Math.ceil(c.nAcero * Math.max(0, v('ace'))) * 2);
                const cc = Math.max(0, v('con')), fz = Math.min(1, cc / .9), dz = Math.max(0, (cc - .9) / .1);
                c.fuste.visible = fz > 0; c.fuste.scale.y = Math.max(.001, C.H * fz); c.fuste.position.y = -C.H + C.H * fz / 2;
                c.dado.visible = dz > 0; c.dado.scale.y = Math.max(.001, dz); c.dado.position.y = .25 * dz;
                c.stub.visible = stubListo;
                continue;
            }
            c.suelo.scale.y = Math.max(.001, C.H * (1 - exc)); c.suelo.position.y = -C.H + c.suelo.scale.y / 2; c.suelo.visible = exc < .999;
            c.solado.visible = v('sol') > 0; c.solado.scale.set(Math.max(.01, v('sol')), 1, 1);
            if (!C.parrilla) {
                c.stub.visible = stubListo;
                c.acero.visible = v('ace') > 0; c.acero.geometry.setDrawRange(0, Math.ceil(c.nAcero * v('ace')) * 2);
                const cc = v('con'), z1 = Math.min(1, cc / .55), z2 = Math.max(0, (cc - .55) / .45);
                c.zap.visible = z1 > 0; c.zap.scale.y = Math.max(.001, z1); c.zap.position.y = -C.H + .07 + C.ZH * z1 / 2;
                c.ped.visible = z2 > 0; c.ped.scale.y = Math.max(.001, C.PA * z2); c.ped.position.y = -C.H + .07 + C.ZH + C.PA * z2 / 2;
            } else {
                const n = Math.round(c.parr.length * v('niv')); c.parr.forEach((m, i) => m.visible = i < n);
            }
            const hr = C.H * rel; c.rel.visible = rel > 0; c.rel.scale.y = Math.max(.001, hr); c.rel.position.y = -C.H + hr / 2;
        }
        // Montaje: el prearmado no levanta piezas; lo del día (sobre el cuadro anterior) va en rojo.
        const pm = d.torre.mon, pHoy = this.prev ? this.prev.torre.mon : pm;
        const { alto, n } = this.pintarTorre(pm, Math.min(pm, pHoy));
        // Pluma, vientos y ancladeros: solo mientras el montaje está en curso según el registro (no según la animación,
        // que nunca llega exacto a 0 ni a 1).
        const pmReg = this.obj?.torre?.mon ?? pm, activa = pmReg > 0.001 && pmReg < 0.999 && pm > 0.001;
        this.pluma.visible = this.gancho.visible = this.cable.visible = activa;
        this._izando = false;
        if (activa) {
            const { pos, rot, tip } = this.puntaPluma(alto);
            this.pluma.position.copy(pos); this.pluma.rotation.copy(rot);
            // Lo izado hoy según el registro (no la animación del cuadro).
            const pPrev = this.prev ? this.prev.torre.mon : pmReg;
            const lotes = this.lotesIzado(Math.min(pmReg, pPrev) * this.peso, pmReg * this.peso);
            const st = this.reduce ? null : this.estadoIzado(t, lotes);
            if (st) this.aplicarIzado(st, lotes);
            this.aparejo(tip, st, lotes);
        }
        const ves = d.torre.ves;
        this.aisl.visible = ves > 0;
        this.aisl.children.forEach((m, i) => m.visible = i < this.aisl.children.length * ves);
        this.equipos(d, t, alto, n, activa ? this.pluma : null);
    }

    bucle(t) {
        this._raf = requestAnimationFrame(this._bucle);
        if (this.grupo && this.obj && this.vis) {
            const dt = this._t0 ? Math.min(1000, t - this._t0) : 16;
            const k = this.reduce ? 1 : 1 - Math.exp(-dt / 220);   // ~1 s para llegar, sin depender de los fps
            for (const p of PATAS) for (const c of CLAVES_PATA) this.vis.patas[p][c] = ease(this.vis.patas[p][c], this.obj.patas[p][c], k);
            for (const c of CLAVES_TORRE) this.vis.torre[c] = ease(this.vis.torre[c], this.obj.torre[c], k);
            this.aplicar(t);
        }
        this._t0 = t;
        if (this._vuelo) {
            const v = this._vuelo, u = Math.min(1, (t - (v.ini ??= t)) / v.dur), k = u * u * (3 - 2 * u);
            this.cam.position.lerpVectors(v.p0, v.p1, k); this.ctl.target.lerpVectors(v.t0, v.t1, k);
            if (u >= 1) this._vuelo = null;
        }
        if (this._pulso?.length && !this.reduce) { const o = .13 + .07 * Math.sin(t / 320); for (const m of this._pulso) m.opacity = o; }
        this.ctl.update();
        this.renderer.render(this.scene, this.cam);
    }

    // Actividades en curso el día mostrado: [{ tipo: 'exc'|'sol'|'niv'|'ace'|'parrilla'|'con'|'rel'|'pre'|'mon'|'otro', pata: 'A'…|null, elegida }].
    // Los obreros de cada pata trabajan en esa tarea aunque ese día no haya un registro nuevo; se van al terminar su lapso.
    trabajar(lista) {
        const m = {};
        let pre = null;
        for (const x of lista || []) {
            const p = x.pata ?? x.Pata, k = x.tipo ?? x.Tipo;
            // Prearmado: escena propia en el piso (no ocupa una pata); elegida = es la actividad seleccionada en el panel.
            if (k === 'pre') { pre = { elegida: !!(x.elegida ?? x.Elegida) || !!pre?.elegida }; continue; }
            if (p && k && k !== 'mon' && !m[p]) m[p] = k;
        }
        this._trab = Object.keys(m).length ? m : null;
        this._pre = pre;
    }

    // ---------- Controles ----------
    aplicarTransparencia() {
        for (const m of this.TIERRA) { m.opacity = this.transp ? (m === this.M.pasto ? .3 : .2) : 1; m.depthWrite = !this.transp; m.needsUpdate = true; }
    }
    transparente(v) { this.transp = !!v; this._resTransp = undefined; this.aplicarTransparencia(); }
    girar(v) { this.ctl.autoRotate = !!v; }
    recentrar() { this.vista(this._vista || 'torre'); }
    vista(nombre, animar = false) {
        this._vista = nombre;
        const A = this.alto || 30, B = this.B || 11, pos = new THREE.Vector3(), obj = new THREE.Vector3();
        if (nombre === 'cimentacion') { pos.set(B * 1.05, B * .75, B * 1.2); obj.set(0, -1.5, 0); }
        else {
            // Con el montaje en curso se ven también los ancladeros de los vientos.
            const mon = this.obj?.torre?.mon ?? 0, conVientos = !!this.vientos && mon > .001 && mon < .999, r = Math.max(A * 1.65, B * 3, conVientos ? this.vientos.lh * 2.3 : 0);
            pos.set(r * .85, A * .55 + 4, r); obj.set(0, A * (conVientos ? .2 : .45), 0);
        }
        this.volar(pos, obj, animar);
    }
    // Lleva la cámara a pos mirando a obj (suave en ~0,7 s, o de una vez).
    volar(pos, obj, animar = true) {
        if (!animar || this.reduce) { this._vuelo = null; this.cam.position.copy(pos); this.ctl.target.copy(obj); this.ctl.update(); return; }
        this._vuelo = { p0: this.cam.position.clone(), t0: this.ctl.target.clone(), p1: pos.clone(), t1: obj.clone(), ini: null, dur: 700 };
    }

    // ---------- Resalte desde el panel de actividades ----------
    // d = { clave, elementos: 'patas' | 'torre', pata: 'A'…|null, patas: { A: 0–1 | null (N/A) }, pct: 0–1 | null } o null (quitar).
    // Patas: caja de contorno por fosa con el color del % de la actividad en esa pata (rojo 0 → verde 100; gris N/A),
    // suelo transparente y torre atenuada para ver lo enterrado. Torre: contorno de la torre y relleno hasta el %.
    // La cámara solo se mueve cuando cambia la actividad o la pata (no al mover la línea de tiempo).
    resaltar(d) {
        const mover = (d?.clave || '') !== (this._resClave || '');
        this.quitarResalte(!d || d.elementos !== 'patas');
        this._resClave = d?.clave || '';
        if (!d || !this.grupo) return;
        const g = this.res = new THREE.Group(); this.grupo.add(g); this._pulso = [];
        const color = v => v == null ? new THREE.Color(0x8a9099) : new THREE.Color().setHSL(Math.min(1, Math.max(0, v)) / 3, .62, .46, THREE.SRGBColorSpace);
        const caja3 = (w, h, l, x, y, z, col, op, foco) => {
            const m = new THREE.Mesh(new THREE.BoxGeometry(w, h, l), new THREE.MeshBasicMaterial({ color: col, transparent: true, opacity: op, depthWrite: false, toneMapped: false }));
            m.position.set(x, y, z); m.renderOrder = 5; g.add(m);
            const b = new THREE.LineSegments(new THREE.EdgesGeometry(m.geometry), new THREE.LineBasicMaterial({ color: col, transparent: true, opacity: foco ? 1 : .35, depthTest: false, toneMapped: false }));
            b.position.copy(m.position); b.renderOrder = 6; g.add(b);
            return m;
        };
        if (d.elementos === 'patas') {
            for (const p of PATAS) {
                if (!this.POS[p] || !(p in (d.patas || {}))) continue;
                const { x, z } = this.POS[p], C = this.C[p], v = d.patas[p], foco = !d.pata || d.pata === p;
                const w = C.Lx + .35, l = (C.pila ? C.Lx : C.Lz) + .35, h = C.H + .5;
                const m = caja3(w, h, l, x, -C.H / 2 + .25, z, color(v), foco ? .16 : .04, foco);
                if (foco && v != null) this._pulso.push(m.material);
            }
            if (this._resTransp === undefined) this._resTransp = this.transp;
            this.transp = true; this.aplicarTransparencia(); this.atenuarTorre(true);
            if (mover) {
                if (d.pata && this.POS[d.pata]) {
                    const { x, z } = this.POS[d.pata], C = this.C[d.pata], sx = Math.sign(x) || 1, sz = Math.sign(z) || 1, r = Math.max(C.Lx, C.Lz) * 1.5 + 4.5;
                    this._vista = 'cimentacion';
                    this.volar(new THREE.Vector3(x + sx * r * .75, r * .85, z + sz * r * .75), new THREE.Vector3(x, -C.H * .45, z));
                } else this.vista('cimentacion', true);
            }
        } else {
            let mx = 1;
            for (const m of this.miembros || []) mx = Math.max(mx, Math.abs(m.va.x), Math.abs(m.va.z), Math.abs(m.vb.x), Math.abs(m.vb.z));
            const A = this.alto || 30, w = 2 * mx + .8, col = color(d.pct);
            caja3(w, A + .4, w, 0, A / 2, 0, col, .03, true);
            const f = Math.max(0, Math.min(1, d.pct ?? 0));
            if (f > 0) this._pulso.push(caja3(w - .1, A * f, w - .1, 0, A * f / 2, 0, col, .14, true).material);
            if (mover) this.vista('torre', true);
        }
    }
    quitarResalte(restaurar) {
        if (this.res) {
            this.res.parent?.remove(this.res);
            this.res.traverse(o => { o.geometry?.dispose(); o.material?.dispose(); });
            this.res = null;
        }
        this._pulso = [];
        if (restaurar) {
            if (this._resTransp !== undefined) { this.transp = this._resTransp; this._resTransp = undefined; this.aplicarTransparencia(); }
            this.atenuarTorre(false);
        }
    }
    atenuarTorre(si) {
        const m = this.M.torre;
        if (!!m.transparent === !!si) return;
        m.transparent = !!si; m.opacity = si ? .22 : 1; m.depthWrite = !si; m.needsUpdate = true;
    }
    info() {
        const v = this.vientos;
        return {
            nombre: this.fuente?.nombre || '', plano: this.fuente?.plano || '', pesoKg: Math.round(this.peso || 0), alto: Math.round((this.alto || 0) * 10) / 10,
            vientos: v ? { lhM: Math.round(v.lh * 10) / 10, anguloMax: Math.round(v.angulo * 10) / 10, retirado: v.retirado } : null,
        };
    }

    liberar() {
        this.navegacion.liberar();
        cancelAnimationFrame(this._raf);
        this.cont.removeEventListener('wheel', this._rueda);
        this._ro.disconnect(); this._mo.disconnect(); this._mq.removeEventListener('change', this._fondo);
        this.limpiar();
        this.scene.traverse(o => { if (o.geometry) o.geometry.dispose(); });
        for (const m of [...Object.values(this.M), ...Object.values(this.MQ)]) m.dispose();
        this.tEstr.dispose(); this.tRel.dispose(); this.tPasto.dispose(); this.tSombra.dispose();
        this.scene.environment?.dispose(); this.scene.background?.dispose?.();
        this.ctl.dispose(); this.renderer.dispose(); this.renderer.domElement.remove();
    }
}
