// Capa de tendido sobre el visor de Línea 3D (Three.js). Módulo ES, lo usa AvanceObra.razor (vista Tendido).
// Un solo motor 3D: torres, modelos, terreno, cámara y zoom son los de Línea 3D (linea3d.js); esta capa dibuja encima,
// por tiro y según el avance de cada efecto, cadenas de aisladores, cables, poleas, separadores, cuadrillas y equipos.
// SUPUESTOS (visuales, no de diseño): amarres del modelo deducidos de su geometría (amarresDe en linea3d.js);
// cadenas en I de 5,5 m en todas las torres (no se sabe cuáles son de retención); un conductor por fase; flechas
// proporcionales al vano; cables y cuadrillas engrosados para que se vean a la escala del tiro.
import * as THREE from '../lib/three/three.module.min.js';
import { crear as crearLinea } from './linea3d.js';
export { pantallaCompleta } from './avance3d.js';

const COLOR_FASE = { A: 0xc8463e, B: 0x3d70c9, C: 0x8a55c2 };
const FASES = ['A', 'B', 'C'];
const CADENA = 5.5, NDISCOS = 14, ESC_OBRERO = 1.7, LIMITE_TUBOS = 140;
const ESCALA_LINEA = 10;   // línea completa: torres exageradas, como Línea 3D por defecto

// dotnet (opcional): recibe AlSeleccionarTorre al hacer clic en una torre (lo invoca el visor de Línea 3D).
export function crear(contenedor, dotnet) { return new Tendido(contenedor, dotnet); }

class Tendido {
    constructor(cont, dotnet) {
        this.cont = cont; this.dotnet = dotnet || null;
        this.capas = { conductor: true, guarda: true, obra: true, etiquetas: true };
        this.cuadros = {}; this.sel = null; this.tiros = []; this.etiquetas = []; this.animados = [];
        this.aspecto = 'satelite'; this.giro = false;
        // Estado de obra de las torres (id → { m: montaje 0–1 o -1, c: cimentación iniciada, p: % de la torre }),
        // simular = todas completas, escalaUsr = escala elegida (null = automática), color = { modo, hex }, familias ocultas.
        this.obra = {}; this.simulado = false; this.escalaUsr = null; this.color = { modo: 'avance', hex: '#c7ccd2' }; this.ocultas = [];
        this.mats = new Map();
        this.reduce = matchMedia('(prefers-reduced-motion: reduce)').matches;
        this._v = new THREE.Vector3(); this._t = 0;
    }

    mat(color, opc = {}) {
        const k = color + JSON.stringify(opc);
        if (!this.mats.has(k)) this.mats.set(k, new THREE.MeshStandardMaterial({ color, roughness: .55, metalness: .2, ...opc }));
        return this.mats.get(k);
    }
    matL(color) {
        const k = 'l' + color;
        if (!this.mats.has(k)) this.mats.set(k, new THREE.LineBasicMaterial({ color }));
        return this.mats.get(k);
    }

    // datos = { torres: [{ id, nombre, x, z, lat, lon, familia, cuerpo, real, modelo, tiro }], tiros: [{ id, nombre, torres }],
    //           urls: { codigo: url }, origen: { lat0, lon0 } }. Las torres van en el orden de la línea (tiros en su orden).
    async cargar(datos) {
        const pedido = this._pedido = (this._pedido || 0) + 1;
        const porId = new Map(datos.torres.map(t => [t.id, t]));
        const seq = [];
        for (const ti of datos.tiros) for (const id of ti.torres) if (porId.has(id)) seq.push({ ...porId.get(id), tiro: ti.id });
        // El visor se crea de nuevo solo si cambian las torres (el terreno tarda en cargar).
        const clave = seq.map(t => `${t.id}:${t.modelo}:${t.familia}${t.cuerpo}`).join('|');
        if (!this.visor || clave !== this.clave) {
            this.soltarVisor();
            if (!seq.length) return;
            const v = await crearLinea(this.cont, seq, datos.urls || {}, datos.origen, this.dotnet, { barra: false, aspecto: this.aspecto, conservarVista: true });
            if (pedido !== this._pedido) { v.liberar(); return; }
            this.visor = v; this.clave = clave;
            v.setLinea(false, '#ec1c24');
            if (this.ocultas.length) v.ocultas = new Set(this.ocultas);
            this.pasarObra(); this.pasarColor();
            v.controls.autoRotate = this.giro; v.controls.autoRotateSpeed = .6;
            this.g = new THREE.Group(); v.scene.add(this.g);
            this.estaticos = new THREE.Group(); this.dinamico = new THREE.Group(); this.g.add(this.estaticos, this.dinamico);
            v.alCambiar.add(this._cambio = () => { this.armar(); this.construir(); });
            v.alPintar.add(this._pintar = dt => this.pintar(dt));
        }
        this.tiros = datos.tiros.map(t => ({ id: t.id, nombre: t.nombre, idx: this.visor.torres.map((x, i) => x.tiro === t.id ? i : -1).filter(i => i >= 0) }));
        this.rotular();
        this.aplicarEscala();
        this.encuadrar();
    }

    soltarVisor() {
        if (!this.visor) return;
        this.etiquetas.forEach(e => e.e.remove()); this.etiquetas = [];
        this.limpiar(this.dinamico); this.limpiar(this.estaticos);
        this.visor.liberar(); this.visor = null; this.clave = null;
    }

    aplicarEscala() {
        const e = this.escalaUsr ?? (this.sel == null ? ESCALA_LINEA : this.escalaTiro());
        if (this.visor.escala !== e) this.visor.setEscala(e);   // actualizar → alCambiar → armar + construir
        else { this.armar(); this.construir(); }
    }

    // SUPUESTO visual: en un tiro, torres y amarres exagerados según el vano medio (vano / 150 m, entre ×1 y ×4) para
    // que se lean de lado sin compactar los vanos; los vanos y el terreno quedan reales.
    escalaTiro() {
        const ti = this.tiros.find(t => t.id === this.sel), T = this.visor.torres;
        if (!ti) return 1;
        const idx = [...(ti.idx[0] > 0 ? [ti.idx[0] - 1] : []), ...ti.idx];
        let L = 0; for (let k = 1; k < idx.length; k++) L += Math.hypot(T[idx[k]].x - T[idx[k - 1]].x, T[idx[k]].z - T[idx[k - 1]].z);
        const vano = idx.length > 1 ? L / (idx.length - 1) : 150;
        return Math.round(Math.max(1, Math.min(4, vano / 150)) * 2) / 2;
    }

    // ---------- Puntos de amarre y cadenas (dependen de la posición, el relieve y la escala de las torres) ----------
    armar() {
        const v = this.visor; if (!v) return;
        this.limpiar(this.estaticos);
        const T = v.torres, n = T.length, s = v.escala;
        this.P = T.map(t => new THREE.Vector3(t.x, t.y || 0, t.z));
        this.DIR = T.map(t => new THREE.Vector3(Math.sin(t.rot), 0, Math.cos(t.rot)));
        const barras = new THREE.InstancedMesh(new THREE.CylinderGeometry(.06, .06, CADENA, 6), this.mat(0x8d969f, { metalness: .8, roughness: .35 }), n * 3);
        const discos = new THREE.InstancedMesh(new THREE.CylinderGeometry(.42, .42, .1, 14), this.mat(0x5b8f80, { roughness: .25, metalness: .1 }), n * 3 * NDISCOS);
        const m = new THREE.Matrix4(), q = new THREE.Quaternion(), Y = new THREE.Vector3(0, 1, 0), esc = new THREE.Vector3(s, s, s), cero = new THREE.Vector3();
        this.A = T.map((t, i) => {
            const am = t.amarre; q.setFromAxisAngle(Y, t.rot);
            const fase = {}, e = this.visible(t) && v.fraccionDe(t) >= 1 ? esc : cero;   // cadenas solo en torres montadas
            FASES.forEach((ph, j) => {
                const p = am.fases[j];
                barras.setMatrixAt(i * 3 + j, m.compose(v.mundo(i, p.x, p.y - CADENA / 2), q, e));
                for (let k = 0; k < NDISCOS; k++) discos.setMatrixAt((i * 3 + j) * NDISCOS + k, m.compose(v.mundo(i, p.x, p.y - .6 - k * (CADENA - 1.2) / (NDISCOS - 1)), q, e));
                fase[ph] = { grapa: v.mundo(i, p.x, p.y - CADENA), polea: v.mundo(i, p.x, p.y - CADENA - 1.2), brazo: v.mundo(i, p.x * .92, p.y + .25) };
            });
            return { fase, guarda: v.mundo(i, am.guarda[0].x, am.guarda[0].y), opgw: v.mundo(i, am.guarda[1].x, am.guarda[1].y), top: (t.y || 0) + (t.alto || 40) * s };
        });
        barras.castShadow = discos.castShadow = true;
        this.estaticos.add(barras, discos);
        this.marcar();
        this.etiquetas.forEach(et => et.p.setY(this.A[et.i].top + (et.esTiro ? 30 : 8) * Math.sqrt(s)));
    }

    rotular() {
        this.etiquetas.forEach(e => e.e.remove()); this.etiquetas = [];
        const v = this.visor; if (!v) return;
        const et = (txt, i, tiro, esTiro) => {
            const e = document.createElement('div'); e.className = 'td-etq' + (esTiro ? ' td-etq-tiro' : ''); e.textContent = txt;
            this.cont.appendChild(e); this.etiquetas.push({ e, i, p: new THREE.Vector3(v.torres[i].x, 0, v.torres[i].z), tiro, esTiro });
        };
        v.torres.forEach((t, i) => et(t.nombre, i, t.tiro, false));
        this.tiros.forEach(ti => { if (ti.idx.length) et(ti.nombre, ti.idx[Math.floor(ti.idx.length / 2)], ti.id, true); });
    }

    visible(t) { return !this.visor?.ocultas?.has(t.familia); }

    // Torres sin montar: con cimentación iniciada, cuatro dados en las patas; sin nada, un anillo rojo en el sitio.
    marcar() {
        const v = this.visor, T = v.torres, s = v.escala, Y = new THREE.Vector3(0, 1, 0);
        const dados = [], vacias = [];
        T.forEach((t, i) => {
            if (!this.visible(t) || v.fraccionDe(t) > 0) return;
            (this.obra[t.id]?.c ? dados : vacias).push(i);
        });
        const m = new THREE.Matrix4(), q = new THREE.Quaternion(), esc = new THREE.Vector3(s, s, s);
        if (dados.length) {
            const im = new THREE.InstancedMesh(new THREE.BoxGeometry(1.4, 1, 1.4).translate(0, .5, 0), this.mat(0xc9c5bd, { roughness: .9 }), dados.length * 4);
            let k = 0;
            for (const i of dados) {
                const t = T[i], h = Math.max(2, (t.ancho || 8) / 2); q.setFromAxisAngle(Y, t.rot);
                for (const [x, z] of [[-h, -h], [h, -h], [h, h], [-h, h]]) im.setMatrixAt(k++, m.compose(v.mundo(i, x, 0, z), q, esc));
            }
            this.estaticos.add(im);
        }
        if (vacias.length) {
            const anillo = new THREE.InstancedMesh(new THREE.TorusGeometry(1, .07, 6, 32).rotateX(Math.PI / 2), this.mat(0xec1c24, { emissive: 0x550000 }), vacias.length);
            const poste = new THREE.InstancedMesh(new THREE.CylinderGeometry(.12, .12, 6, 6).translate(0, 3, 0), this.mat(0xec1c24, { emissive: 0x550000 }), vacias.length);
            vacias.forEach((i, k) => {
                const t = T[i], r = Math.max(3, (t.ancho || 8) * .7);
                anillo.setMatrixAt(k, m.compose(v.mundo(i, 0, .4), q.identity(), new THREE.Vector3(r * s, s, r * s)));
                poste.setMatrixAt(k, m.compose(v.mundo(i, 0, 0), q, esc));
            });
            this.estaticos.add(anillo, poste);
        }
    }

    // ---------- Estado y controles ----------
    // cuadros = avance de tendido por tiro; torres = estado de obra por torre a la misma fecha (ver this.obra).
    estado(cuadros, torres) {
        this.cuadros = cuadros || {};
        if (torres) { this.obra = torres; if (this.visor) { this.pasarObra(); return; } }   // pasarObra → alCambiar → armar + construir
        this.construir();
    }
    pasarObra() {
        const v = this.visor; if (!v) return;
        v.torres.forEach(t => { t.pctObra = this.obra[t.id]?.p ?? null; });
        v.setMontaje(this.simulado ? null : Object.fromEntries(v.torres.map(t => [t.id, this.obra[t.id]?.m ?? 0])));
    }
    simular(on) { this.simulado = !!on; this.pasarObra(); }
    escala(e) { this.escalaUsr = e > 0 ? e : null; if (this.visor) this.aplicarEscala(); }
    // modo: 'avance' (rojo → verde por %), 'plateado', 'tipo' (color de la familia) o 'fijo' (hex).
    colores(modo, hex) { this.color = { modo: modo || 'avance', hex: hex || this.color.hex }; this.pasarColor(); }
    pasarColor() {
        const v = this.visor; if (!v) return;
        const { modo, hex } = this.color;
        if (modo === 'plateado') v.setColorFijo('#c7ccd2'); else if (modo === 'fijo') v.setColorFijo(hex);
        v.setModo(modo === 'plateado' || modo === 'fijo' ? 'fijo' : modo);
    }
    familias(ocultas) { this.ocultas = ocultas || []; this.visor?.setFamiliasOcultas(this.ocultas); }
    seleccionar(id) { this.sel = id ?? null; if (!this.visor) return; this.aplicarEscala(); this.encuadrar(); }
    capa(k, on) { this.capas[k] = !!on; this.construir(); }
    girar(on) { this.giro = !!on; if (this.visor) { this.visor.controls.autoRotate = this.giro; this.visor.controls.autoRotateSpeed = .6; } }
    recentrar() { this.encuadrar(); }
    // Buscar torre: la cámara va a la torre y queda seleccionada (amarilla); deseleccionar = cerrar su ficha.
    enfocar(id) { this.visor?.enfocar(id); }
    deseleccionar() { if (this.visor && this.visor.sel >= 0) this.visor.seleccionar(-1, false); }
    terreno(a) { this.aspecto = a; this.visor?.mostrarTerreno(a, false); }

    // Mira el tiro de lado (perpendicular a su eje), un poco desde arriba; con la línea completa, la vista general de Línea 3D.
    encuadrar() {
        const v = this.visor; if (!v || !this.P?.length) return;
        const ti = this.tiros.find(t => t.id === this.sel);
        if (!ti) { v.vista('todo'); return; }
        const idx = [...(ti.idx[0] > 0 ? [ti.idx[0] - 1] : []), ...ti.idx];
        const box = new THREE.Box3();
        idx.forEach(i => { box.expandByPoint(this.P[i]); box.expandByPoint(this.P[i].clone().setY(this.A[i].top)); });
        const c = box.getCenter(new THREE.Vector3());
        const ax = this.P[idx[idx.length - 1]].clone().sub(this.P[idx[0]]).setY(0);
        if (ax.lengthSq() < 1) ax.set(1, 0, 0); ax.normalize();
        const lado = new THREE.Vector3(-ax.z, 0, ax.x);
        let ancho = 0, fondo = 0;
        idx.forEach(i => { const d = this.P[i].clone().sub(c); ancho = Math.max(ancho, Math.abs(d.dot(ax))); fondo = Math.max(fondo, Math.abs(d.dot(lado))); });
        const cam = v.camera, vf = THREE.MathUtils.degToRad(cam.fov) / 2, hf = Math.atan(Math.tan(vf) * cam.aspect);
        const d = Math.max((ancho + 25) / Math.tan(hf), box.getSize(new THREE.Vector3()).y * .7 / Math.tan(vf)) * 1.08 + fondo;
        v.controls.target.copy(c);
        cam.position.copy(c).add(lado.multiplyScalar(.9).add(new THREE.Vector3(0, .3, 0)).normalize().multiplyScalar(d));
        v.controls.update();
    }

    limpiar(g) {
        if (!g) return;
        for (const o of [...g.children]) { g.remove(o); o.traverse(x => x.geometry?.dispose()); if (o.isInstancedMesh) o.dispose(); }
    }

    cable(a, b, flecha, r, color, hasta = 1, opc) {
        if (hasta <= .001) return;
        const pts = [], n = this.ligero ? 10 : 24;
        for (let k = 0; k <= n; k++) { const t = hasta * k / n; const p = a.clone().lerp(b, t); p.y -= 4 * flecha * t * (1 - t); pts.push(p); }
        if (this.ligero) { this.dinamico.add(new THREE.Line(new THREE.BufferGeometry().setFromPoints(pts), this.matL(color))); return; }
        const m = new THREE.Mesh(new THREE.TubeGeometry(new THREE.CatmullRomCurve3(pts), 32, r, 8, false), this.mat(color, opc || { metalness: .55, roughness: .38 }));
        m.castShadow = true; this.dinamico.add(m);
    }
    punto(a, b, flecha, t) { const p = a.clone().lerp(b, t); p.y -= 4 * flecha * t * (1 - t); return p; }

    // Liniero: piernas, torso con chaleco, cabeza y casco (como en Torre por torre), escalado para la vista del tiro.
    obrero(pos, chaleco = 0xff7a1a, rotY = 0, tipo = 'mirar') {
        const g = new THREE.Group(), pz = (geo, m, x, y, z) => { const o = new THREE.Mesh(geo, m); o.position.set(x, y, z); return o; };
        const jean = this.mat(0x34465e, { roughness: .8 }), ch = this.mat(chaleco, { emissive: 0x331100 }), piel = this.mat(0xc28b62, { roughness: .8 }), casco = this.mat(0xf4f4f0, { roughness: .4 });
        for (const x of [-1, 1]) g.add(pz(new THREE.CapsuleGeometry(.07, .62, 4, 8), jean, x * .09, .43, 0));
        const torso = new THREE.Group(); torso.position.y = .85; g.add(torso);
        torso.add(pz(new THREE.CapsuleGeometry(.16, .34, 4, 10), ch, 0, .24, 0));
        torso.add(pz(new THREE.SphereGeometry(.11, 12, 10), piel, 0, .62, 0));
        torso.add(pz(new THREE.SphereGeometry(.125, 14, 8, 0, Math.PI * 2, 0, Math.PI / 2), casco, 0, .66, 0));
        const brazos = [];
        for (const x of [-1, 1]) { const h = new THREE.Group(); h.position.set(x * .21, .42, 0); h.add(pz(new THREE.CapsuleGeometry(.05, .44, 4, 8), ch, 0, -.24, 0)); torso.add(h); brazos.push(h); }
        g.scale.setScalar(ESC_OBRERO); g.position.copy(pos); g.rotation.y = rotY; this.dinamico.add(g);
        this.animados.push({ tipo, torso, brazos, f: Math.random() * 6 });
    }
    lineaDeVida(a, b) { this.dinamico.add(new THREE.Line(new THREE.BufferGeometry().setFromPoints([a, b]), this.matL(0xffb000))); }

    // Equipo de tendido: camión con tambor (winche) o tensionadora con carretes de conductor. Se apoya en el terreno.
    camion(pos, dir, tipo) {
        const g = new THREE.Group(), pz = (geo, m, x, y, z) => { const o = new THREE.Mesh(geo, m); o.position.set(x, y, z); return o; };
        const cuerpo = this.mat(tipo === 'winche' ? 0xc8402a : 0xe8b324), negro = this.mat(0x2a2a2a, { roughness: .8 }), gris = this.mat(0x6d737a, { metalness: .3 });
        const vidrio = this.mat(0x8fb3c7, { roughness: .1, metalness: .2, transparent: true, opacity: .6 });
        g.add(pz(new THREE.BoxGeometry(7.2, .5, 2.5), gris, 0, 1.0, 0));
        g.add(pz(new THREE.BoxGeometry(2.3, 2.1, 2.5), cuerpo, 3.6, 2.2, 0));
        g.add(pz(new THREE.BoxGeometry(.05, .9, 2.1), vidrio, 4.76, 2.6, 0));
        for (const x of [-2.6, -1.2, 3.4]) for (const z of [-1.1, 1.1]) { const w = pz(new THREE.CylinderGeometry(.55, .55, .45, 16), negro, x, .55, z); w.rotation.x = Math.PI / 2; g.add(w); }
        if (tipo === 'winche') {
            g.add(pz(new THREE.BoxGeometry(3.6, 1.6, 2.3), cuerpo, -1, 2.05, 0));
            const t = pz(new THREE.CylinderGeometry(1.0, 1.0, 1.9, 20), gris, -1.2, 3.4, 0); t.rotation.x = Math.PI / 2; g.add(t); this.animados.push({ rot: t });
        } else {
            g.add(pz(new THREE.BoxGeometry(3.6, 1.2, 2.3), cuerpo, -1, 1.85, 0));
            for (const x of [-2.2, .2]) { const t = pz(new THREE.CylinderGeometry(.75, .75, 2.0, 20), gris, x, 3.0, 0); t.rotation.x = Math.PI / 2; g.add(t); this.animados.push({ rot: t }); }
            for (let k = 0; k < 3; k++) {
                const carrete = new THREE.Group(); carrete.position.set(-9 - k * 3.4, 1.7, 0);
                for (const z of [-.7, .7]) { const d = pz(new THREE.CylinderGeometry(1.6, 1.6, .12, 24), this.mat(0x8a5a2e, { roughness: .9 }), 0, 0, z); d.rotation.x = Math.PI / 2; carrete.add(d); }
                const nucleo = pz(new THREE.CylinderGeometry(1.2, 1.2, 1.3, 24), this.mat(0xb7bcc2, { metalness: .6, roughness: .4 }), 0, 0, 0); nucleo.rotation.x = Math.PI / 2; carrete.add(nucleo);
                g.add(carrete); this.animados.push({ rot: carrete });
            }
            g.add(pz(new THREE.BoxGeometry(11, .3, 2.4), gris, -12.4, .55, 0));
        }
        g.position.copy(pos); g.position.y = this.suelo(pos); g.rotation.y = Math.atan2(-dir.z, dir.x); this.dinamico.add(g);
    }
    suelo(p) { return this.visor.alturaTerreno(p.x, p.z); }
    enSuelo(p) { return p.clone().setY(this.suelo(p)); }

    // Cables, poleas, cuadrillas y equipos del día (se rehacen al cambiar fecha, tiro, capas, relieve o escala).
    construir() {
        if (!this.visor || !this.A) return;
        this.limpiar(this.dinamico); this.animados.length = 0;
        const P = this.P, A = this.A, cl = x => Math.max(0, Math.min(1, x)), s = this.visor.escala, rs = Math.sqrt(s);
        const nVanos = this.sel == null ? P.length : (this.tiros.find(t => t.id === this.sel)?.idx.length || 0);
        const polea = this.mat(0xffc400, { metalness: .3 }), separador = this.mat(0x9aa1a8, { metalness: .7, roughness: .35 });
        for (const ti of this.tiros) {
            if (!ti.idx.length) continue;
            const q = this.cuadros[ti.id] || this.cuadros[String(ti.id)] || {};
            const v = k => +(q[k] ?? 0);
            const pPol = v('Poleas'), pGuia = v('CableGuia'), pGua = v('Guarda'), pOp = v('Opgw'), pCond = v('Conductor'), pFl = v('Flechado'), pGr = v('Engrapado'), pSep = v('Separadores');
            const elegido = this.sel == null || this.sel === ti.id;
            this.ligero = !elegido || nVanos > LIMITE_TUBOS;
            const T = ti.idx[0] > 0 ? [ti.idx[0] - 1, ...ti.idx] : [...ti.idx];
            const n = T.length - 1;
            if (n < 1) continue;
            const torresCon = x => Math.floor(x * (n + 1) + 1e-6);
            for (let k = 0; k < n; k++) {
                const i = T[k], j = T[k + 1], L = P[i].clone().setY(0).distanceTo(P[j].clone().setY(0));
                const engrapado = torresCon(pGr) >= k + 2, flechado = pFl * n >= k + 1;
                const fl = L * (flechado ? .038 : .058), flg = L * (flechado ? .03 : .045);
                if (this.capas.guarda) {
                    this.cable(A[i].guarda, A[j].guarda, flg, .12 * rs, 0xa3acb5, cl(pGua * n - k), { metalness: .6, roughness: .38 });
                    this.cable(A[i].opgw, A[j].opgw, flg, .16 * rs, 0x3b4148, cl(pOp * n - k), { metalness: .3, roughness: .5 });
                }
                if (!this.capas.conductor) continue;
                const fGuia = cl(pGuia * n - k), fCond = cl(pCond * n - k), fs = cl(pSep * n - k);
                if (fGuia > 0 && fCond < 1) this.cable(A[i].fase.B.polea, A[j].fase.B.polea, L * .07, .08 * rs, 0xf2c230, fGuia, { roughness: .6 });
                if (fCond <= 0) continue;
                for (const ph of FASES) {
                    const a = engrapado ? A[i].fase[ph].grapa : A[i].fase[ph].polea, b = engrapado ? A[j].fase[ph].grapa : A[j].fase[ph].polea;
                    this.cable(a, b, fl, .2 * rs, COLOR_FASE[ph], fCond);
                    if (fs > 0 && elegido && !this.ligero) for (let k2 = 1; k2 < 8 && k2 / 8 <= fs; k2++) {
                        const sp = new THREE.Mesh(new THREE.BoxGeometry(.22, .9, .9), separador); sp.position.copy(this.punto(a, b, fl, k2 / 8)); this.dinamico.add(sp);
                    }
                    if (fs > 0 && fs < 1 && pSep < 1 && this.capas.obra && elegido && !this.ligero && ph === 'B') {
                        const pc = this.punto(a, b, fl, fs), dir = b.clone().sub(a).setY(0).normalize();
                        const carro = new THREE.Mesh(new THREE.BoxGeometry(1.8, .6, 1.3), this.mat(0xffd000)); carro.position.copy(pc).add(new THREE.Vector3(0, -.7, 0)); carro.rotation.y = Math.atan2(-dir.z, dir.x); this.dinamico.add(carro);
                        this.obrero(pc.clone().add(new THREE.Vector3(0, -1.0 - 1.7 * ESC_OBRERO, 0)), 0xff7a1a, Math.atan2(dir.x, dir.z), 'halar');
                    }
                }
            }
            if (!elegido || this.ligero) continue;
            // Poleas colgadas mientras la torre no esté engrapada
            T.forEach((t2, k) => {
                if (!(torresCon(pPol) > k) || torresCon(pGr) > k) return;
                for (const ph of FASES) {
                    const pol = new THREE.Mesh(new THREE.TorusGeometry(.75, .2, 8, 18), polea);
                    pol.position.copy(A[t2].fase[ph].polea); pol.position.y += .75; pol.rotation.y = Math.atan2(this.DIR[t2].x, this.DIR[t2].z) + Math.PI / 2;
                    this.dinamico.add(pol);
                }
            });
            if (!this.capas.obra) continue;
            // Cuadrillas en alturas (poleas y engrapado) en la torre en la que van, con línea de vida, y apoyo en tierra.
            const enAltura = x => {
                if (x <= 0 || x >= 1) return;
                const t2 = T[Math.min(n, torresCon(x))], rot = Math.atan2(this.DIR[t2].x, this.DIR[t2].z);
                for (const ph of ['A', 'C']) { const br = A[t2].fase[ph].brazo; this.obrero(br, 0xff7a1a, rot, 'trepar'); this.lineaDeVida(br, A[t2].fase[ph].grapa); }
                this.obrero(this.enSuelo(P[t2].clone().add(this.DIR[t2].clone().multiplyScalar(9)).add(new THREE.Vector3(6, 0, 0))), 0x2e9e5b, rot + Math.PI / 2, 'mirar');
            };
            enAltura(pPol); enAltura(pGr);
            if (pFl > 0 && pFl < 1) { const t2 = T[Math.min(n, Math.floor(pFl * n) + 1)]; this.obrero(A[t2].fase.B.brazo, 0x2f6fdb, Math.atan2(this.DIR[t2].x, this.DIR[t2].z), 'mirar'); }
            // Frente de tendido: tensionadora con carretes en la primera torre, winche en la última
            if ([pGuia, pGua, pOp, pCond].some(x => x > 0 && x < 1)) {
                const d0 = this.DIR[T[0]], dn = this.DIR[T[n]];
                this.camion(P[T[0]].clone().sub(d0.clone().multiplyScalar(45)), d0, 'freno');
                this.camion(P[T[n]].clone().add(dn.clone().multiplyScalar(40)), dn.clone().negate(), 'winche');
                const lat0 = new THREE.Vector3(-d0.z, 0, d0.x).multiplyScalar(5), latn = new THREE.Vector3(-dn.z, 0, dn.x).multiplyScalar(5);
                this.obrero(this.enSuelo(P[T[0]].clone().sub(d0.clone().multiplyScalar(36)).add(lat0)), 0xff7a1a, Math.atan2(d0.x, d0.z), 'halar');
                this.obrero(this.enSuelo(P[T[n]].clone().add(dn.clone().multiplyScalar(31)).add(latn)), 0xff7a1a, Math.atan2(-dn.x, -dn.z), 'mirar');
            }
        }
    }

    // Cada cuadro (lo llama el visor de Línea 3D): animación de cuadrillas y equipos, y etiquetas sobre las torres.
    pintar(dt) {
        this._t += dt;
        const t = this._t;
        if (!this.reduce) for (const a of this.animados) {
            if (a.rot) { a.rot.rotation.z += dt * .8; continue; }
            const k = t + a.f;
            if (a.tipo === 'trepar') a.brazos.forEach((b, i) => b.rotation.x = -2.6 + Math.sin(k * 2 + i * Math.PI) * .35);
            else if (a.tipo === 'halar') { a.torso.rotation.x = -.25; a.brazos.forEach((b, i) => b.rotation.x = -1.3 + Math.sin(k * 2.2 + i) * .25); }
            else { a.torso.rotation.y = Math.sin(k * .6) * .5; a.brazos[1].rotation.x = Math.sin(k * .9) > .6 ? -2.2 : 0; }
        }
        const cam = this.visor.camera, w = this.cont.clientWidth, h = this.cont.clientHeight, v = this._v;
        for (const et of this.etiquetas) {
            const ver = this.capas.etiquetas && (et.esTiro ? this.sel == null : this.sel === et.tiro);
            let vis = false;
            if (ver) { v.copy(et.p).project(cam); vis = v.z < 1 && Math.abs(v.x) < 1.05 && Math.abs(v.y) < 1.05; }
            et.e.style.display = vis ? '' : 'none';
            if (vis) { et.e.style.left = ((v.x + 1) / 2 * w) + 'px'; et.e.style.top = ((1 - v.y) / 2 * h) + 'px'; }
        }
    }

    liberar() {
        this._pedido = (this._pedido || 0) + 1;
        this.soltarVisor();
        for (const m of this.mats.values()) m.dispose();
        this.mats.clear();
    }
}
