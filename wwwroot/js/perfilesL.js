// Geometría de ángulos L reales a partir del JSON de una torre (wwwroot/modelos3d/torre-*.json).
// Misma regla que tools/modelos3d/generar_ifc.py (los IFC):
//  - perfil: marca del miembro → lista de materiales ("L AxT" o "L AxBxT"); sin perfil leído → perfil SUPUESTO
//    (perfilSupuesto.js: el de la pieza leída más parecida de su cara/grupo/función o el L más chico de la hoja);
//  - orientación de las alas (SUPUESTO, convención de montaje): montantes con talón en el eje y alas hacia el
//    interior; diagonales/horizontales con un ala en el plano de su cara y la otra hacia el interior;
//  - el talón va sobre la línea de trabajo del plano (sin gramil).
// Coordenadas del visor: Y arriba; el JSON viene en mm y se devuelve en metros.
import * as THREE from '../lib/three/three.module.min.js';
import { parsePerfil, listaPorMarca, filaLeida, perfilesSupuestos } from './perfilSupuesto.js';
export { parsePerfil };

const K = 0.001;

// El supuesto se calcula con la torre completa y queda en caché por lista de materiales (las torres a medio montar
// pasan solo parte de los miembros, con la misma lista).
const cacheSup = new WeakMap();
function supuestos(data) {
    const k = data.lista_materiales || data;
    let c = cacheSup.get(k);
    if (!c) { c = new Map(); cacheSup.set(k, c); }
    if (data.miembros.some(m => !c.has(m))) {
        const nuevos = perfilesSupuestos(data);
        for (const m of data.miembros) if (!c.has(m)) c.set(m, nuevos.get(m) || null);
    }
    return c;
}

function perp(v, u) {
    const p = v.clone().addScaledVector(u, -v.dot(u));
    return p.lengthSq() < 1e-10 ? null : p.normalize();
}

// Lista de piezas con su perfil (mm) y ejes locales.
export function piezas(data) {
    const lista = listaPorMarca(data), sup = supuestos(data);
    const out = [];
    for (const m of data.miembros) {
        const A = new THREE.Vector3(...m.a).multiplyScalar(K), B = new THREE.Vector3(...m.b).multiplyScalar(K);
        const L = A.distanceTo(B); if (L < 1e-6) continue;
        const u = B.clone().sub(A).normalize();
        const fila = filaLeida(m, lista), s = fila ? null : sup.get(m);
        const perfilTxt = fila ? fila.perfil : s ? s.perfil : null;
        const perfil = parsePerfil(perfilTxt);
        const mid = A.clone().add(B).multiplyScalar(.5);
        const sx = Math.sign(mid.x) || 1, sz = Math.sign(mid.z) || 1;
        let x;
        if (m.tipo === 'Montante') {
            x = perp(new THREE.Vector3(-sx, 0, 0), u) || perp(new THREE.Vector3(0, 1, 0), u);
            const q = perp(new THREE.Vector3(0, 0, -sz), u);
            if (q && new THREE.Vector3().crossVectors(u, x).dot(q) < 0) x = q;
        } else {
            const cara = m.cara || '';
            let n;
            if (cara.startsWith('Transversal')) n = new THREE.Vector3(0, 0, sz);
            else if (cara.startsWith('Longitudinal')) n = new THREE.Vector3(sx, 0, 0);
            else { n = new THREE.Vector3(mid.x, 0, mid.z); if (n.lengthSq() < 1e-8) n.set(1, 0, 0); n.normalize(); }
            x = perp(n.clone().negate(), u) || perp(new THREE.Vector3(0, 1, 0), u) || perp(new THREE.Vector3(1, 0, 0), u);
        }
        const y = new THREE.Vector3().crossVectors(u, x).normalize();
        out.push({ A, u, L, x, y, perfil, marca: m.marca, perfilTxt, supuesto: !fila && !!perfil, origen: s ? s.origen : null });
    }
    return out;
}

// Una sola geometría (no indexada, normales planas) con todas las piezas. Sin perfil leído ni supuesto (lista sin
// ningún ángulo L) → barra cuadrada de 40 mm.
export function geometriaL(data, opciones = {}) {
    const ps = piezas(data);
    const barra = (opciones.barraSinPerfil_mm ?? 40) * K;
    const tris = [];
    const P = (o, x, y, u, px, py, pz) => o.clone().addScaledVector(x, px).addScaledVector(y, py).addScaledVector(u, pz);
    for (const p of ps) {
        let poly;
        if (p.perfil) {
            const a = p.perfil.a * K, b = p.perfil.b * K, t = p.perfil.t * K;
            // Contorno de la L en el plano (x, y) local, talón en el origen: ala de ancho b sobre x, ala a sobre y.
            poly = [[0, 0], [b, 0], [b, t], [t, t], [t, a], [0, a]];
        } else {
            const h = barra / 2;
            poly = [[-h, -h], [h, -h], [h, h], [-h, h]];
        }
        const n = poly.length;
        const base = poly.map(([px, py]) => P(p.A, p.x, p.y, p.u, px, py, 0));
        const tope = poly.map(([px, py]) => P(p.A, p.x, p.y, p.u, px, py, p.L));
        for (let i = 0; i < n; i++) {
            const j = (i + 1) % n;
            tris.push(base[i], base[j], tope[j], base[i], tope[j], tope[i]);
        }
        // Tapas: la L se triangula como dos rectángulos; el cuadrado como dos triángulos.
        const cara = (v, inv) => {
            const tr = n === 6 ? [[0, 1, 2], [0, 2, 3], [0, 3, 4], [0, 4, 5]] : [[0, 1, 2], [0, 2, 3]];
            for (const [i, j, k] of tr) inv ? tris.push(v[i], v[k], v[j]) : tris.push(v[i], v[j], v[k]);
        };
        cara(base, true); cara(tope, false);
    }
    const pos = new Float32Array(tris.length * 3);
    tris.forEach((v, i) => { pos[i * 3] = v.x; pos[i * 3 + 1] = v.y; pos[i * 3 + 2] = v.z; });
    const g = new THREE.BufferGeometry();
    g.setAttribute('position', new THREE.BufferAttribute(pos, 3));
    g.computeVertexNormals();
    g.userData.conPerfil = ps.filter(p => p.perfil && !p.supuesto).length;
    g.userData.supuestos = ps.filter(p => p.supuesto).length;
    g.userData.total = ps.length;
    return g;
}
