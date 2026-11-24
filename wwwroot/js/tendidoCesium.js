import { navegacionCesium } from './navegacion3d.js';
// Visor del tendido por tiros sobre CesiumJS (globo 3D). Módulo ES, lo usa AvanceObra.razor (vista Tendido).
// Misma interfaz que la capa anterior (tendido3d.js): crear, cargar, estado, seleccionar, capa, escala, colores, familias,
// simular, girar, recentrar, enfocar, deseleccionar, terreno, liberar.
// Terreno real a escala sin token: alturas Terrarium (AWS, libres) e imagen satelital World Imagery (Esri, con su crédito).
// Torres: la geometría de Línea 3D (modelo de planos con ángulos L o silueta paramétrica) se convierte a glTF en el
// navegador y Cesium la dibuja con luz del sol, sombras y contorno al seleccionarla.
// SUPUESTOS (visuales, no de diseño): amarres deducidos de la geometría (amarresDe), cadenas en I de 5,5 m en todas,
// un conductor por fase, flecha proporcional al vano, torres exageradas según la escala (línea ×10, tiro = vano / 150 m).
import * as THREE from '../lib/three/three.module.min.js';
import { miembrosParametricos, amarresDe, amarresParametricos, geometria, ordenMontaje, colorAvance } from './linea3d.js';
import { geometriaL } from './perfilesL.js';
export { pantallaCompleta } from './avance3d.js';

const K = 0.001;
const COLOR_FASE = { A: '#e0564b', B: '#4f8ae0', C: '#a06ddb' };
const FASES = ['A', 'B', 'C'];
const CADENA = 5.5, ESCALA_LINEA = 10, PASOS = 20;
const IMAGEN_MS = 'https://services.arcgisonline.com/ArcGIS/rest/services/World_Imagery/MapServer';
const IMAGEN = 'https://server.arcgisonline.com/ArcGIS/rest/services/World_Imagery/MapServer/tile/{z}/{y}/{x}';
const ALTURAS = z => `https://s3.amazonaws.com/elevation-tiles-prod/terrarium/${z}/`;
const Z_ALTURAS = 14;     // nivel máximo de las alturas; más cerca se interpola dentro de la baldosa de ese nivel

// ---------- Carga de Cesium (vendorizado en lib/cesium, global window.Cesium) ----------
// CSP de la app (script-src 'self', sin 'unsafe-eval' ni blob:): lib/cesium/Cesium.js va parcheado (el Knockout embebido
// usaba (0,eval)("this") y protobuf eval("require"); ahora globalThis y null; repetir al actualizar Cesium). Además se
// borra CESIUM_WORKERS para que Cesium cree sus workers desde lib/cesium/Workers/*.js (módulos same-origin) y no
// desde un blob: con importScripts, que la CSP bloquea (sin workers no hay terreno ni geometrías).
let cargaCesium = null;
function cesium() {
    if (window.Cesium) return Promise.resolve(window.Cesium);
    return cargaCesium ??= new Promise((res, rej) => {
        const base = new URL('../lib/cesium/', import.meta.url).href;
        window.CESIUM_BASE_URL = base;
        const l = document.createElement('link'); l.rel = 'stylesheet'; l.href = base + 'Widgets/widgets.css'; document.head.appendChild(l);
        const s = document.createElement('script'); s.src = base + 'Cesium.js';
        const falla = m => { cargaCesium = null; s.remove(); rej(new Error(m)); };
        s.onload = () => {
            // onload llega aunque el script falle al evaluarse (p. ej. bloqueado por la CSP): window.Cesium queda vacío.
            if (!window.Cesium?.Viewer) return falla('Cesium se descargó pero no se pudo ejecutar (¿política de seguridad de contenido?). Revise la consola.');
            try { delete globalThis.CESIUM_WORKERS; } catch { globalThis.CESIUM_WORKERS = undefined; }
            res(window.Cesium);
        };
        s.onerror = () => falla('No se pudo cargar Cesium');
        document.head.appendChild(s);
    });
}

// ---------- Terreno Terrarium (PNG: alto = R·256 + G + B/256 − 32768) ----------
const cacheAlturas = new Map();
function baldosaAlturas(z, x, y) {
    const k = `${z}/${x}/${y}`;
    if (!cacheAlturas.has(k)) {
        if (cacheAlturas.size > 400) cacheAlturas.delete(cacheAlturas.keys().next().value);
        cacheAlturas.set(k, new Promise(res => {
            const im = new Image(); im.crossOrigin = 'anonymous';
            im.onload = () => {
                const c = document.createElement('canvas'); c.width = c.height = 256;
                const g = c.getContext('2d', { willReadFrequently: true }); g.drawImage(im, 0, 0);
                const d = g.getImageData(0, 0, 256, 256).data, h = new Float32Array(256 * 256);
                for (let i = 0; i < h.length; i++) h[i] = d[i * 4] * 256 + d[i * 4 + 1] + d[i * 4 + 2] / 256 - 32768;
                res(h);
            };
            im.onerror = () => res(null);
            im.src = `${ALTURAS(z)}${x}/${y}.png`;
        }));
    }
    return cacheAlturas.get(k);
}
function terrenoTerrarium(C) {
    const N = 65;
    return new C.CustomHeightmapTerrainProvider({
        width: N, height: N, tilingScheme: new C.WebMercatorTilingScheme(),
        credit: 'Alturas: Mapzen Terrarium (AWS Open Data)',
        callback: async (x, y, level) => {
            const z = Math.min(level, Z_ALTURAS), f = 2 ** (level - z);
            const px = Math.floor(x / f), py = Math.floor(y / f), ox = (x - px * f) / f, oy = (y - py * f) / f;
            const h = await baldosaAlturas(z, px, py), out = new Float32Array(N * N);
            if (!h) return out;
            for (let j = 0; j < N; j++) for (let i = 0; i < N; i++) {
                const u = Math.min(255, (ox + i / (N - 1) / f) * 255), v = Math.min(255, (oy + j / (N - 1) / f) * 255);
                const i0 = Math.floor(u), j0 = Math.floor(v), i1 = Math.min(255, i0 + 1), j1 = Math.min(255, j0 + 1), a = u - i0, b = v - j0;
                out[j * N + i] = (h[j0 * 256 + i0] * (1 - a) + h[j0 * 256 + i1] * a) * (1 - b) + (h[j1 * 256 + i0] * (1 - a) + h[j1 * 256 + i1] * a) * b;
            }
            return out;
        }
    });
}

// ---------- Geometría de Three.js → GLB (Z arriba, Y a lo largo de la línea; X = −x de Línea 3D para no reflejar) ----------
function glb(geo) {
    let g = geo.index ? geo : geo;
    if (!g.attributes.normal) g.computeVertexNormals();
    const P = g.attributes.position, Nn = g.attributes.normal, n = P.count;
    const pos = new Float32Array(n * 3), nor = new Float32Array(n * 3), min = [Infinity, Infinity, Infinity], max = [-Infinity, -Infinity, -Infinity];
    for (let i = 0; i < n; i++) {
        const v = [-P.getX(i), P.getZ(i), P.getY(i)];
        pos.set(v, i * 3); nor.set([-Nn.getX(i), Nn.getZ(i), Nn.getY(i)], i * 3);
        for (let k = 0; k < 3; k++) { if (v[k] < min[k]) min[k] = v[k]; if (v[k] > max[k]) max[k] = v[k]; }
    }
    const idx = g.index ? Uint32Array.from(g.index.array) : Uint32Array.from({ length: n }, (_, i) => i);
    const bl = [pos.byteLength, nor.byteLength, idx.byteLength], total = bl[0] + bl[1] + bl[2];
    const json = {
        asset: { version: '2.0', generator: 'SigWeb' }, scene: 0, scenes: [{ nodes: [0] }], nodes: [{ mesh: 0 }],
        meshes: [{ primitives: [{ attributes: { POSITION: 0, NORMAL: 1 }, indices: 2, material: 0 }] }],
        materials: [{ pbrMetallicRoughness: { baseColorFactor: [.86, .88, .9, 1], metallicFactor: .55, roughnessFactor: .42 }, doubleSided: true }],
        buffers: [{ byteLength: total }],
        bufferViews: [{ buffer: 0, byteOffset: 0, byteLength: bl[0], target: 34962 }, { buffer: 0, byteOffset: bl[0], byteLength: bl[1], target: 34962 }, { buffer: 0, byteOffset: bl[0] + bl[1], byteLength: bl[2], target: 34963 }],
        accessors: [
            { bufferView: 0, componentType: 5126, count: n, type: 'VEC3', min, max },
            { bufferView: 1, componentType: 5126, count: n, type: 'VEC3' },
            { bufferView: 2, componentType: 5125, count: idx.length, type: 'SCALAR' }]
    };
    let js = new TextEncoder().encode(JSON.stringify(json));
    const jl = Math.ceil(js.length / 4) * 4, jp = new Uint8Array(jl).fill(0x20); jp.set(js); js = jp;
    const out = new ArrayBuffer(12 + 8 + jl + 8 + total), dv = new DataView(out), u8 = new Uint8Array(out);
    dv.setUint32(0, 0x46546C67, true); dv.setUint32(4, 2, true); dv.setUint32(8, out.byteLength, true);
    dv.setUint32(12, jl, true); dv.setUint32(16, 0x4E4F534A, true); u8.set(js, 20);
    let o = 20 + jl; dv.setUint32(o, total, true); dv.setUint32(o + 4, 0x004E4942, true); o += 8;
    u8.set(new Uint8Array(pos.buffer), o); u8.set(new Uint8Array(nor.buffer), o + bl[0]); u8.set(new Uint8Array(idx.buffer), o + bl[0] + bl[1]);
    return URL.createObjectURL(new Blob([out], { type: 'model/gltf-binary' }));
}

const css = c => (c instanceof THREE.Color ? '#' + c.getHexString() : c);
const icono = (svg) => 'data:image/svg+xml;utf8,' + encodeURIComponent(svg);
const ICO_OBRERO = c => icono(`<svg xmlns="http://www.w3.org/2000/svg" width="32" height="32" viewBox="0 0 32 32"><circle cx="16" cy="16" r="15" fill="rgba(17,21,27,.78)"/><circle cx="16" cy="11" r="4.2" fill="#f2c9a5"/><path d="M10.5 10.2a5.5 5.5 0 0 1 11 0z" fill="#f4f4f0"/><path d="M9 25c.6-5 3.4-7.5 7-7.5s6.4 2.5 7 7.5z" fill="${c}"/></svg>`);
const ICO_EQUIPO = c => icono(`<svg xmlns="http://www.w3.org/2000/svg" width="36" height="36" viewBox="0 0 36 36"><circle cx="18" cy="18" r="17" fill="rgba(17,21,27,.78)"/><rect x="6" y="14" width="15" height="8" rx="1.5" fill="${c}"/><path d="M21 16h5l3 3.5V22h-8z" fill="${c}"/><circle cx="11" cy="23.5" r="2.4" fill="#222" stroke="#ddd" stroke-width="1"/><circle cx="25" cy="23.5" r="2.4" fill="#222" stroke="#ddd" stroke-width="1"/><circle cx="13" cy="11.5" r="3.5" fill="none" stroke="#ddd" stroke-width="1.6"/></svg>`);

// dotnet (opcional): recibe AlSeleccionarTorre al hacer clic en una torre.
export function crear(contenedor, dotnet) { return new TendidoCesium(contenedor, dotnet); }

class TendidoCesium {
    constructor(cont, dotnet) {
        this.cont = cont; this.dotnet = dotnet || null;
        this.capas = { conductor: true, guarda: true, obra: true, etiquetas: true };
        this.cuadros = {}; this.sel = null; this.tiros = []; this.torres = [];
        this.aspecto = 'satelite'; this.giro = false;
        this.obra = {}; this.simulado = false; this.escalaUsr = null; this.color = { modo: 'avance', hex: '#c7ccd2' }; this.ocultas = new Set();
        this.selTorre = -1; this.urls = new Map(); this.modelos = []; this._pedido = 0;
    }

    async iniciar() {
        if (this.viewer) return;
        const C = this.C = await cesium();
        const creditos = document.createElement('div'); creditos.className = 'td-cz-creditos'; this.cont.appendChild(creditos);
        const v = this.viewer = new C.Viewer(this.cont, {
            animation: false, timeline: false, baseLayerPicker: false, geocoder: false, homeButton: false, sceneModePicker: false,
            navigationHelpButton: false, fullscreenButton: false, infoBox: false, selectionIndicator: false, scene3DOnly: true,
            baseLayer: false, terrainProvider: terrenoTerrarium(C), creditContainer: creditos, msaaSamples: 4, shadows: true,
            terrainShadows: C.ShadowMode.RECEIVE_ONLY
        });
        const sc = this.scene = v.scene, gl = sc.globe;
        // Tamaño: al entrar/salir de pantalla completa o cambiar la ventana (también con escala de Windows ≠ 100 %), el
        // lienzo de Cesium se ajusta de una vez al contenedor; si no, quedaba una franja vacía abajo.
        this._ajustar = () => { if (!this.viewer) return; try { this.viewer.resize(); this.scene.requestRender(); } catch { } };
        this._ro = new ResizeObserver(this._ajustar); this._ro.observe(this.cont);
        this._fs = () => { this._ajustar(); setTimeout(this._ajustar, 120); setTimeout(this._ajustar, 450); };
        document.addEventListener('fullscreenchange', this._fs); window.addEventListener('resize', this._fs);
        // Imagen satelital: el servicio de Esri devuelve una baldosa gris ("sin datos") cuando se pide más detalle del que
        // hay en la zona; el proveedor ArcGIS la descarta y Cesium sigue ampliando la última baldosa real (sin límite de zoom).
        // Si el servicio no responde a la consulta inicial, plantilla de URL hasta el nivel 17 (más cerca se amplía).
        this.capaImagen = C.ImageryLayer.fromProviderAsync(C.ArcGisMapServerImageryProvider.fromUrl(IMAGEN_MS));
        this.capaImagen.errorEvent.addEventListener(() => {
            if (!this.viewer) return;
            const show = this.capaImagen.show;
            v.imageryLayers.remove(this.capaImagen);
            this.capaImagen = v.imageryLayers.addImageryProvider(new C.UrlTemplateImageryProvider({ url: IMAGEN, maximumLevel: 17, credit: 'Imagen: Esri, Maxar, Earthstar Geographics' }));
            this.capaImagen.show = show;
        });
        v.imageryLayers.add(this.capaImagen);
        gl.depthTestAgainstTerrain = true; gl.enableLighting = true; gl.showGroundAtmosphere = true; gl.maximumScreenSpaceError = 1.6;
        gl.baseColor = C.Color.fromCssColorString('#2b323b');
        sc.fog.enabled = true; sc.fog.density = 1.4e-4; sc.highDynamicRange = false;
        sc.postProcessStages.fxaa.enabled = true;
        v.shadowMap.softShadows = true; v.shadowMap.size = 2048; v.shadowMap.darkness = .45; v.shadowMap.maximumDistance = 6000;
        // Sol de media mañana en Colombia (UTC−5): luz lateral que marca el relieve y las celosías.
        const hoy = new Date(); hoy.setUTCHours(15, 0, 0, 0);
        v.clock.currentTime = C.JulianDate.fromDate(hoy); v.clock.shouldAnimate = false;
        this.etq = sc.primitives.add(new C.LabelCollection({ scene: sc }));
        this.puntos = sc.primitives.add(new C.PointPrimitiveCollection());
        this.iconos = sc.primitives.add(new C.BillboardCollection({ scene: sc }));
        this.lineas = sc.primitives.add(new C.PolylineCollection());
        this.estaticas = sc.primitives.add(new C.PolylineCollection());
        this.navegacion = navegacionCesium(v, C);
        this.clic = new C.ScreenSpaceEventHandler(sc.canvas);
        this.clic.setInputAction(e => {
            if (this.navegacion.pan) return;
            const p = sc.pick(e.position), i = p && p.id && typeof p.id.torre === 'number' ? p.id.torre : -1;
            this.elegir(i, true);
        }, C.ScreenSpaceEventType.LEFT_CLICK);
        sc.preRender.addEventListener(() => this.girarCuadro());
        if (this.aspecto !== 'satelite') this.aplicarAspecto();
    }

    // datos = { torres: [{ id, nombre, x, z, lat, lon, familia, cuerpo, real, modelo, color, tiro }], tiros: [{ id, nombre, torres }],
    //           urls: { codigo: url }, origen }. Torres en el orden de la línea (tiros en su orden).
    async cargar(datos) {
        const pedido = ++this._pedido;
        await this.iniciar();
        if (pedido !== this._pedido || !this.viewer) return;
        const C = this.C, porId = new Map(datos.torres.map(t => [t.id, t])), seq = [];
        for (const ti of datos.tiros) for (const id of ti.torres) if (porId.has(id)) seq.push({ ...porId.get(id), tiro: ti.id });
        const clave = seq.map(t => `${t.id}:${t.modelo}:${t.familia}${t.cuerpo}`).join('|');
        if (clave !== this.clave) {
            this.clave = clave; this.quitarModelos();
            await this.prepararTorres(seq, datos.urls || {});
            if (pedido !== this._pedido) return;
            await this.alturas();
            if (pedido !== this._pedido) return;
        }
        this.tiros = datos.tiros.map(t => ({ id: t.id, nombre: t.nombre, idx: this.torres.map((x, i) => x.tiro === t.id ? i : -1).filter(i => i >= 0) }));
        this.rotular();
        this.redibujar();
        this.encuadrar(true);
    }

    // Modelos (uno por tipo + cuerpo) con su geometría, alto, ancho y amarres; orientación de cada torre según sus vanos.
    async prepararTorres(seq, urls) {
        const urlsMin = Object.fromEntries(Object.entries(urls).map(([k, v]) => [k.toLowerCase(), v]));
        const reales = {};
        await Promise.all([...new Set(seq.filter(t => t.real && t.modelo).map(t => t.modelo))].map(async k => {
            const u = urlsMin[k.toLowerCase()]; if (!u) return;
            try {
                const data = await (await fetch(u)).json();
                const P = data.miembros.filter(m => m.tramo !== 'Stub').flatMap(m => [m.a.map(v => v * K), m.b.map(v => v * K)]);
                reales[k] = { data, alto: data.meta.alto_total_mm * K, ancho: data.meta.ancho_base_mm * K, amarre: P.length ? amarresDe(P) : null };
            } catch { }
        }));
        const grupos = new Map(), n = seq.length;
        seq.forEach((t, i) => {
            const p = seq[Math.max(0, i - 1)], q = seq[Math.min(n - 1, i + 1)];
            let dE = (q.lon - p.lon) * Math.cos(t.lat * Math.PI / 180), dN = q.lat - p.lat;
            if (Math.hypot(dE, dN) < 1e-12) { dE = 0; dN = 1; }
            t.giro = Math.atan2(-dE, dN);    // eje Y local → dirección de la línea
            const real = t.real && reales[t.modelo];
            const k = real ? 'real|' + t.modelo : `${t.familia}|${t.cuerpo}`;
            if (!grupos.has(k)) {
                const d = real ? reales[t.modelo] : miembrosParametricos(t.familia, t.cuerpo);
                grupos.set(k, { clave: k, usaReal: !!real, real: real ? reales[t.modelo] : null, familia: t.familia, cuerpo: t.cuerpo, alto: d.alto, ancho: d.ancho,
                    amarre: (real && real.amarre) || amarresParametricos(t.familia, t.cuerpo), M: real ? null : d.M });
            }
            const g = grupos.get(k);
            Object.assign(t, { grupo: g, alto: g.alto, ancho: g.ancho, amarre: g.amarre, modeloTxt: real ? 'real' : 'aprox' });
        });
        this.torres = seq;
    }

    // Altura del terreno en cada torre (plano = 0).
    async alturas() {
        const C = this.C, T = this.torres;
        if (this.aspecto === 'plano') { T.forEach(t => t.h = 0); return; }
        const pts = T.map(t => C.Cartographic.fromDegrees(t.lon, t.lat));
        try { await C.sampleTerrain(this.viewer.terrainProvider, Z_ALTURAS, pts); } catch { }
        T.forEach((t, i) => t.h = Number.isFinite(pts[i].height) ? pts[i].height : 0);
    }

    // ---------- Torres ----------
    escalaActual() { return this.escalaUsr ?? (this.sel == null ? ESCALA_LINEA : this.escalaTiro()); }
    escalaTiro() {
        const ti = this.tiros.find(t => t.id === this.sel), T = this.torres;
        if (!ti) return 1;
        const idx = [...(ti.idx[0] > 0 ? [ti.idx[0] - 1] : []), ...ti.idx];
        let L = 0; for (let k = 1; k < idx.length; k++) L += this.dist(T[idx[k]], T[idx[k - 1]]);
        const vano = idx.length > 1 ? L / (idx.length - 1) : 150;
        return Math.round(Math.max(1, Math.min(4, vano / 150)) * 2) / 2;
    }
    dist(a, b) { const C = this.C; return C.Cartesian3.distance(C.Cartesian3.fromDegrees(a.lon, a.lat), C.Cartesian3.fromDegrees(b.lon, b.lat)); }

    matriz(t, s) {
        const C = this.C, base = C.Transforms.eastNorthUpToFixedFrame(C.Cartesian3.fromDegrees(t.lon, t.lat, t.h || 0));
        const rot = C.Matrix4.fromRotation(C.Matrix3.fromRotationZ(t.giro));
        return C.Matrix4.multiply(C.Matrix4.multiply(base, rot, new C.Matrix4()), C.Matrix4.fromUniformScale(s), new C.Matrix4());
    }
    // Punto del modelo (m, coordenadas de Línea 3D: x lateral, y arriba) en coordenadas del globo.
    mundo(i, x, y) { const C = this.C; return C.Matrix4.multiplyByPoint(this.torres[i].M, new C.Cartesian3(-x, 0, y), new C.Cartesian3()); }
    arriba(p) { return this.C.Ellipsoid.WGS84.geodeticSurfaceNormal(p, new this.C.Cartesian3()); }

    fraccion(t) {
        if (this.simulado) return 1;
        const f = this.obra[t.id]?.m ?? this.obra[String(t.id)]?.m;
        return f === undefined || f === null || f < 0 || isNaN(f) ? 1 : Math.min(1, +f);
    }
    colorDe(t) {
        const { modo, hex } = this.color;
        if (modo === 'plateado') return '#c7ccd2';
        if (modo === 'fijo') return hex;
        if (modo === 'tipo') return t.color || '#b7c0cc';
        const p = this.obra[t.id]?.p ?? this.obra[String(t.id)]?.p;
        return css(colorAvance(p ?? null));
    }
    // URL del GLB de un grupo completo o a un paso de montaje (caché).
    urlDe(g, paso) {
        const k = g.clave + '|' + paso;
        if (!this.urls.has(k)) {
            let geo;
            if (paso >= PASOS) geo = g.usaReal ? geometriaL(g.real.data) : geometria(g.M);
            else {
                g.orden ??= ordenMontaje(g);
                const lim = g.orden.total * paso / PASOS, sel = [...g.orden.base]; let acc = 0;
                for (const x of g.orden.lista) { if (acc + x.w > lim) break; acc += x.w; sel.push(x.m); }
                geo = sel.length ? (g.usaReal ? geometriaL({ ...g.real.data, miembros: sel }) : geometria(sel)) : null;
            }
            this.urls.set(k, geo ? glb(geo) : null);
            geo?.dispose();
        }
        return this.urls.get(k);
    }

    // Coloca, colorea y cambia el modelo de cada torre según escala, montaje, familias ocultas y selección.
    actualizarTorres() {
        const C = this.C, s = this.escalaActual();
        this.torres.forEach((t, i) => {
            t.M = this.matriz(t, s);
            const f = this.fraccion(t), oculta = this.ocultas.has(t.familia);
            const paso = oculta || f <= 0 ? 0 : (f >= 1 ? PASOS : Math.min(PASOS - 1, Math.max(1, Math.round(f * PASOS))));
            const url = paso ? this.urlDe(t.grupo, paso) : null;
            if (t.url !== url) {
                if (t.mod) { this.scene.primitives.remove(t.mod); t.mod = null; }
                t.url = url;
                if (url) {
                    const pedido = t._p = (t._p || 0) + 1;
                    C.Model.fromGltfAsync({ url, modelMatrix: t.M, upAxis: C.Axis.Z, forwardAxis: C.Axis.X, id: { torre: i },
                        shadows: C.ShadowMode.ENABLED, colorBlendMode: C.ColorBlendMode.MIX, colorBlendAmount: .6, backFaceCulling: false })
                        .then(m => { if (pedido !== t._p || !this.viewer) { m.destroy(); return; } t.mod = this.scene.primitives.add(m); this.pintarTorre(t, i); })
                        .catch(() => { });
                }
            }
            if (t.mod) { t.mod.modelMatrix = t.M; this.pintarTorre(t, i); }
        });
    }
    pintarTorre(t, i) {
        const C = this.C, m = t.mod; if (!m) return;
        m.color = C.Color.fromCssColorString(this.colorDe(t));
        const sel = i === this.selTorre;
        m.silhouetteColor = C.Color.fromCssColorString('#ffd400'); m.silhouetteSize = sel ? 1.5 : 0;
    }
    quitarModelos() {
        for (const t of this.torres) if (t.mod) { this.scene.primitives.remove(t.mod); t.mod = null; }
        this.torres = [];
    }

    // ---------- Cables, cadenas, poleas, cuadrillas y marcas (se rehacen con cada cambio) ----------
    redibujar() {
        if (!this.viewer || !this.torres.length) return;
        this.actualizarTorres();
        this.armar();
        this.construir();
        this.posicionarEtiquetas();
    }

    armar() {
        const C = this.C, s = this.escalaActual();
        this.estaticas.removeAll(); this.puntos.removeAll();
        const aisl = C.Material.fromType('Color', { color: C.Color.fromCssColorString('#6fb3a2') });
        this.A = this.torres.map((t, i) => {
            const am = t.amarre, fase = {}, montada = !this.ocultas.has(t.familia) && this.fraccion(t) >= 1;
            FASES.forEach((ph, j) => {
                const p = am.fases[j];
                fase[ph] = { grapa: this.mundo(i, p.x, p.y - CADENA), polea: this.mundo(i, p.x, p.y - CADENA - 1.2), brazo: this.mundo(i, p.x * .92, p.y + .25) };
                if (montada) this.estaticas.add({ positions: [this.mundo(i, p.x, p.y), fase[ph].grapa], width: 4, material: aisl });
            });
            return { fase, guarda: this.mundo(i, am.guarda[0].x, am.guarda[0].y), opgw: this.mundo(i, am.guarda[1].x, am.guarda[1].y), top: this.mundo(i, 0, (t.alto || 40) + 4) };
        });
        // Torres sin montar: cimentación iniciada = cuadro gris; sin nada = anillo rojo en el sitio.
        this.torres.forEach((t, i) => {
            if (this.ocultas.has(t.familia) || this.fraccion(t) > 0) return;
            const c = !!(this.obra[t.id]?.c ?? this.obra[String(t.id)]?.c);
            this.puntos.add({ position: this.mundo(i, 0, .5), pixelSize: c ? 9 : 11, color: c ? C.Color.fromCssColorString('#c9c5bd') : C.Color.TRANSPARENT,
                outlineColor: c ? C.Color.fromCssColorString('#5d5a54') : C.Color.fromCssColorString('#ec1c24'), outlineWidth: 2.5, id: { torre: i } });
        });
    }

    cable(a, b, flecha, ancho, color, hasta = 1, guion = false) {
        if (hasta <= .001) return;
        const C = this.C, up = this.arriba(C.Cartesian3.midpoint(a, b, new C.Cartesian3())), pts = [], n = 28;
        for (let k = 0; k <= n; k++) pts.push(this.punto(a, b, flecha, hasta * k / n, up));
        const col = C.Color.fromCssColorString(color);
        this.lineas.add({ positions: pts, width: ancho, material: guion ? C.Material.fromType('PolylineDash', { color: col, dashLength: 12 }) : C.Material.fromType('Color', { color: col }) });
    }
    punto(a, b, flecha, t, up) {
        const C = this.C, p = C.Cartesian3.lerp(a, b, t, new C.Cartesian3());
        up ??= this.arriba(p);
        return C.Cartesian3.add(p, C.Cartesian3.multiplyByScalar(up, -4 * flecha * t * (1 - t), new C.Cartesian3()), p);
    }
    sobreSuelo(p, dz = 0) {
        const C = this.C, c = C.Cartographic.fromCartesian(p), h = this.aspecto === 'plano' ? 0 : (this.scene.globe.getHeight(c) ?? c.height);
        return C.Cartesian3.fromRadians(c.longitude, c.latitude, h + dz);
    }
    obrero(p, color = '#ff7a1a') { this.iconos.add({ image: ICO_OBRERO(color), position: p, width: 26, height: 26, disableDepthTestDistance: 3000, verticalOrigin: this.C.VerticalOrigin.CENTER }); }
    equipo(p, color) { this.iconos.add({ image: ICO_EQUIPO(color), position: this.sobreSuelo(p, 2), width: 32, height: 32, disableDepthTestDistance: 3000, verticalOrigin: this.C.VerticalOrigin.BOTTOM }); }
    desplazar(i, adelante, lado) {
        const C = this.C, t = this.torres[i], enu = C.Transforms.eastNorthUpToFixedFrame(C.Cartesian3.fromDegrees(t.lon, t.lat, t.h || 0));
        const g = t.giro, dE = -Math.sin(g), dN = Math.cos(g);
        return C.Matrix4.multiplyByPoint(enu, new C.Cartesian3(dE * adelante + dN * lado, dN * adelante - dE * lado, 0), new C.Cartesian3());
    }

    construir() {
        if (!this.A) return;
        const C = this.C, A = this.A, cl = x => Math.max(0, Math.min(1, x));
        this.lineas.removeAll(); this.iconos.removeAll();
        const amarillo = C.Color.fromCssColorString('#ffc400'), gris = C.Color.fromCssColorString('#b8bfc6');
        for (const ti of this.tiros) {
            if (!ti.idx.length) continue;
            const q = this.cuadros[ti.id] || this.cuadros[String(ti.id)] || {};
            const v = k => +(q[k] ?? 0);
            const pPol = v('Poleas'), pGuia = v('CableGuia'), pGua = v('Guarda'), pOp = v('Opgw'), pCond = v('Conductor'), pFl = v('Flechado'), pGr = v('Engrapado'), pSep = v('Separadores');
            const elegido = this.sel == null || this.sel === ti.id, w = elegido && this.sel != null ? 1 : .7;
            const T = ti.idx[0] > 0 ? [ti.idx[0] - 1, ...ti.idx] : [...ti.idx], n = T.length - 1;
            if (n < 1) continue;
            const torresCon = x => Math.floor(x * (n + 1) + 1e-6);
            for (let k = 0; k < n; k++) {
                const i = T[k], j = T[k + 1], L = this.dist(this.torres[i], this.torres[j]);
                const engrapado = torresCon(pGr) >= k + 2, flechado = pFl * n >= k + 1;
                const fl = L * (flechado ? .038 : .058), flg = L * (flechado ? .03 : .045);
                if (this.capas.guarda) {
                    this.cable(A[i].guarda, A[j].guarda, flg, 1.6 * w, '#c3cad1', cl(pGua * n - k));
                    this.cable(A[i].opgw, A[j].opgw, flg, 1.8 * w, '#5b636c', cl(pOp * n - k));
                }
                if (!this.capas.conductor) continue;
                const fGuia = cl(pGuia * n - k), fCond = cl(pCond * n - k), fs = cl(pSep * n - k);
                if (fGuia > 0 && fCond < 1) this.cable(A[i].fase.B.polea, A[j].fase.B.polea, L * .07, 1.6 * w, '#f2c230', fGuia, true);
                if (fCond <= 0) continue;
                for (const ph of FASES) {
                    const a = engrapado ? A[i].fase[ph].grapa : A[i].fase[ph].polea, b = engrapado ? A[j].fase[ph].grapa : A[j].fase[ph].polea;
                    this.cable(a, b, fl, 2.6 * w, COLOR_FASE[ph], fCond);
                    if (fs > 0 && elegido && this.sel != null) for (let k2 = 1; k2 < 8 && k2 / 8 <= fs; k2++)
                        this.puntos.add({ position: this.punto(a, b, fl, k2 / 8), pixelSize: 4, color: gris });
                    if (fs > 0 && fs < 1 && pSep < 1 && this.capas.obra && elegido && this.sel != null && ph === 'B') this.obrero(this.punto(a, b, fl, fs));
                }
            }
            if (!elegido || this.sel == null) continue;
            // Poleas colgadas mientras la torre no esté engrapada
            T.forEach((t2, k) => {
                if (!(torresCon(pPol) > k) || torresCon(pGr) > k) return;
                for (const ph of FASES) this.puntos.add({ position: A[t2].fase[ph].polea, pixelSize: 7, color: amarillo, outlineColor: C.Color.BLACK.withAlpha(.6), outlineWidth: 1 });
            });
            if (!this.capas.obra) continue;
            // Cuadrillas en alturas (poleas, engrapado, flechado) y frente de tendido (tensionadora y winche)
            const enAltura = x => {
                if (x <= 0 || x >= 1) return;
                const t2 = T[Math.min(n, torresCon(x))];
                for (const ph of ['A', 'C']) this.obrero(A[t2].fase[ph].brazo);
                this.obrero(this.sobreSuelo(this.desplazar(t2, 9, 6), 1.5), '#2e9e5b');
            };
            enAltura(pPol); enAltura(pGr);
            if (pFl > 0 && pFl < 1) this.obrero(A[T[Math.min(n, Math.floor(pFl * n) + 1)]].fase.B.brazo, '#2f6fdb');
            if ([pGuia, pGua, pOp, pCond].some(x => x > 0 && x < 1)) {
                this.equipo(this.desplazar(T[0], -45, 0), '#e8b324');
                this.equipo(this.desplazar(T[n], 40, 0), '#c8402a');
                this.obrero(this.sobreSuelo(this.desplazar(T[0], -36, 5), 1.5));
                this.obrero(this.sobreSuelo(this.desplazar(T[n], 31, 5), 1.5));
            }
        }
    }

    // ---------- Rótulos ----------
    rotular() {
        const C = this.C; this.etq.removeAll(); this.etiquetas = [];
        const et = (txt, i, tiro, esTiro) => {
            const l = this.etq.add({
                text: txt, font: esTiro ? '600 14px system-ui, sans-serif' : '600 12px system-ui, sans-serif', fillColor: C.Color.WHITE,
                showBackground: true, backgroundColor: esTiro ? C.Color.fromCssColorString('#ec1c24').withAlpha(.9) : C.Color.fromCssColorString('#11151b').withAlpha(.78),
                backgroundPadding: new C.Cartesian2(8, 4), verticalOrigin: C.VerticalOrigin.BOTTOM, horizontalOrigin: C.HorizontalOrigin.CENTER,
                pixelOffset: new C.Cartesian2(0, -6), disableDepthTestDistance: Number.POSITIVE_INFINITY, position: C.Cartesian3.ZERO, show: false
            });
            this.etiquetas.push({ l, i, tiro, esTiro });
        };
        this.torres.forEach((t, i) => et(t.nombre, i, t.tiro, false));
        this.tiros.forEach(ti => { if (ti.idx.length) et(ti.nombre, ti.idx[Math.floor(ti.idx.length / 2)], ti.id, true); });
    }
    posicionarEtiquetas() {
        if (!this.A) return;
        for (const e of this.etiquetas || []) {
            const t = this.torres[e.i];
            e.l.position = e.esTiro ? this.mundo(e.i, 0, (t.alto || 40) * 1.6) : this.A[e.i].top;
            e.l.show = this.capas.etiquetas && !this.ocultas.has(t.familia) && (e.esTiro ? this.sel == null : this.sel === e.tiro);
        }
    }

    // ---------- Cámara ----------
    // Tiro: de lado (perpendicular a su eje), un poco desde arriba. Línea completa: vista oblicua de toda la línea.
    encuadrar(rapido) {
        const C = this.C; if (!this.viewer || !this.torres.length) return;
        const ti = this.tiros.find(t => t.id === this.sel);
        const idx = ti ? [...(ti.idx[0] > 0 ? [ti.idx[0] - 1] : []), ...ti.idx] : this.torres.map((_, i) => i);
        if (!idx.length) return;
        const pts = idx.flatMap(i => [this.mundo(i, 0, 0), this.mundo(i, 0, this.torres[i].alto || 40)]);
        const bs = C.BoundingSphere.fromPoints(pts);
        const a = this.torres[idx[0]], b = this.torres[idx[idx.length - 1]];
        const rumbo = Math.atan2((b.lon - a.lon) * Math.cos(a.lat * Math.PI / 180), b.lat - a.lat);
        this.centro = bs;
        this.viewer.camera.flyToBoundingSphere(bs, {
            duration: rapido ? 0 : 1.4,
            offset: new C.HeadingPitchRange(ti ? rumbo - Math.PI / 2 : rumbo - Math.PI / 3, ti ? -.18 : -.55, bs.radius * (ti ? 2.7 : 2.4))
        });
    }
    girarCuadro() {
        if (!this.giro || !this.centro || this.reduce) return;
        const C = this.C, cam = this.viewer.camera;
        cam.lookAtTransform(C.Transforms.eastNorthUpToFixedFrame(this.centro.center));
        cam.rotateRight(.0025);
        cam.lookAtTransform(C.Matrix4.IDENTITY);
    }

    elegir(i, desdeVisor) {
        this.selTorre = i;
        this.torres.forEach((t, k) => this.pintarTorre(t, k));
        if (desdeVisor && this.dotnet) {
            const t = i >= 0 ? this.torres[i] : null;
            this.dotnet.invokeMethodAsync('AlSeleccionarTorre', t ? t.id : null, t ? t.alto || 0 : 0, t ? t.ancho || 0 : 0, t ? t.modeloTxt : '');
        }
    }

    // ---------- Terreno ----------
    async aplicarAspecto() {
        const C = this.C, v = this.viewer; if (!v) return;
        const a = this.aspecto, gl = this.scene.globe, plano = a === 'plano';
        const antes = v.terrainProvider instanceof C.EllipsoidTerrainProvider;
        if (plano !== antes) v.terrainProvider = plano ? new C.EllipsoidTerrainProvider() : terrenoTerrarium(C);
        this.capaImagen.show = a === 'satelite';
        if (a === 'poligonos' || a === 'malla') {
            const hs = this.torres.map(t => t.h || 0), lo = hs.length ? Math.min(...hs) - 150 : 0, hi = hs.length ? Math.max(...hs) + 400 : 3000;
            if (a === 'poligonos') {
                const cv = document.createElement('canvas'); cv.width = 256; cv.height = 1;
                const g = cv.getContext('2d'), gr = g.createLinearGradient(0, 0, 256, 0);
                [[0, '#46663a'], [.35, '#7f8a52'], [.7, '#86704f'], [1, '#e6e4de']].forEach(([p, c]) => gr.addColorStop(p, c));
                g.fillStyle = gr; g.fillRect(0, 0, 256, 1);
                gl.material = C.Material.fromType('ElevationRamp', { image: cv.toDataURL(), minimumHeight: lo, maximumHeight: hi });
            } else gl.material = C.Material.fromType('ElevationContour', { color: C.Color.fromCssColorString('#8fd3ff').withAlpha(.85), spacing: 25, width: 1.5 });
        } else gl.material = undefined;
        if (plano !== antes && this.torres.length) { await this.alturas(); this.redibujar(); }
    }

    // ---------- Interfaz pública (igual que tendido3d.js) ----------
    // cuadros = avance de tendido por tiro; torres = estado de obra por torre a la misma fecha ({ m, c, p }).
    estado(cuadros, torres) { this.cuadros = cuadros || {}; if (torres) this.obra = torres; this.redibujar(); }
    simular(on) { this.simulado = !!on; this.redibujar(); }
    escala(e) { this.escalaUsr = e > 0 ? e : null; this.redibujar(); }
    colores(modo, hex) { this.color = { modo: modo || 'avance', hex: hex || this.color.hex }; this.torres.forEach((t, i) => this.pintarTorre(t, i)); }
    familias(ocultas) { this.ocultas = new Set(ocultas || []); this.redibujar(); }
    seleccionar(id) { this.sel = id ?? null; this.redibujar(); this.encuadrar(); }
    capa(k, on) { this.capas[k] = !!on; if (this.viewer) { this.construir(); this.posicionarEtiquetas(); } }
    girar(on) { this.giro = !!on; this.reduce = matchMedia('(prefers-reduced-motion: reduce)').matches; }
    recentrar() { this.encuadrar(); }
    enfocar(id) {
        const C = this.C, i = this.torres.findIndex(t => t.id === id); if (i < 0) return;
        const t = this.torres[i], h = (t.alto || 40) * this.escalaActual();
        this.elegir(i, true);
        this.centro = new C.BoundingSphere(this.mundo(i, 0, (t.alto || 40) / 2), h);
        this.viewer.camera.flyToBoundingSphere(this.centro, { duration: 1.2, offset: new C.HeadingPitchRange(-t.giro + .9, -.3, Math.max(h * 3, 120)) });
    }
    deseleccionar() { if (this.selTorre >= 0) this.elegir(-1, false); }
    terreno(a) { this.aspecto = a || 'satelite'; this.aplicarAspecto(); }

    liberar() {
        this._ro?.disconnect();
        if (this._fs) { document.removeEventListener('fullscreenchange', this._fs); window.removeEventListener('resize', this._fs); }
        this.navegacion?.liberar();
        this._pedido++;
        try { this.clic?.destroy(); } catch { }
        try { this.viewer?.destroy(); } catch { }
        this.viewer = null; this.torres = [];
        for (const u of this.urls.values()) if (u) URL.revokeObjectURL(u);
        this.urls.clear();
    }
}
