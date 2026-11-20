import { navegacion3d } from './navegacion3d.js';
// Visor IFC (Three.js + web-ifc, ambos vendorizados en lib/). Módulo ES.
// Lee el archivo .ifc directamente: geometría, estructura espacial y propiedades
// salen del IFC, no del JSON del visor 3D. Admite varias torres a la vez (comparar):
// cada una es un modelo IFC abierto aparte y se coloca a la derecha de la anterior.
// Presentación estilo visor de Blender: modos de sombreado (Sólido, Material, Matcap, Alambre,
// Rayos X, Plano técnico), oclusión ambiental (GTAO), sombra en el suelo, contorno de selección,
// gizmo de ejes y atajos (F, H, Alt+H, /, 1 3 7 0, M, Alt+Z, Esc). Se dibuja solo cuando algo cambia.
import * as THREE from '../lib/three/three.module.min.js';
import { OrbitControls } from '../lib/three/OrbitControls.js';
import { RoomEnvironment } from '../lib/three/addons/environments/RoomEnvironment.js';
import { EffectComposer } from '../lib/three/addons/postprocessing/EffectComposer.js';
import { RenderPass } from '../lib/three/addons/postprocessing/RenderPass.js';
import { GTAOPass } from '../lib/three/addons/postprocessing/GTAOPass.js';
import { OutlinePass } from '../lib/three/addons/postprocessing/OutlinePass.js';
import { OutputPass } from '../lib/three/addons/postprocessing/OutputPass.js';
import * as WebIFC from '../lib/web-ifc/web-ifc-api.js';
import { geometriaL } from './perfilesL.js';

const COLOR_SEL = '#ffa733';
const PALETA = ['#3b82c4', '#d9822b', '#8e5bd0', '#2ca58d', '#c44b4b', '#b8a038', '#5a8f29', '#c25b9c', '#4bb3c4', '#7a6a58'];
const ACERO = { material: '#8f99a5', solido: '#b8bec6', matcap: '#ffffff', rayosx: '#9fb4cc', tecnico: '#ffffff', alambre: '#8f99a5' };
// Piezas con perfil supuesto (sin marca leída): misma forma de ángulo L, solo un tono más cálido.
const TONO_SUPUESTO = '#c79a5b', MEZCLA_SUPUESTO = .32;
const PREFS = 'sigweb.visorifc';

// Íconos (trazo 24×24, mismo estilo del resto de la app).
const ICO = {
    iso: '<path d="M12 3 4 7.5v9L12 21l8-4.5v-9z"/><path d="M4 7.5 12 12l8-4.5M12 12v9"/>',
    frente: '<rect x="5" y="4" width="14" height="16" rx="1"/><path d="M5 20 12 4l7 16"/>',
    lateral: '<rect x="5" y="4" width="14" height="16" rx="1"/><path d="M12 4v16M5 12h14"/>',
    planta: '<rect x="4" y="4" width="16" height="16" rx="1"/><path d="M4 4l16 16M20 4 4 20"/>',
    encuadrar: '<path d="M4 9V4h5M20 9V4h-5M4 15v5h5M20 15v5h-5"/><circle cx="12" cy="12" r="2.5"/>',
    medir: '<path d="M3 17 17 3l4 4L7 21z"/><path d="M7 13l2 2M10 10l2 2M13 7l2 2"/>',
    corte: '<circle cx="6" cy="6" r="3"/><circle cx="6" cy="18" r="3"/><path d="M20 4 8.1 15.9M14.5 14.5 20 20M8.1 8.1 12 12"/>',
    solido: '<circle cx="12" cy="12" r="8" fill="currentColor" fill-opacity=".25"/>',
    material: '<circle cx="12" cy="12" r="8"/><path d="M8 9.5a4.5 4.5 0 0 1 4-2.5"/>',
    matcap: '<circle cx="12" cy="12" r="8"/><circle cx="9.5" cy="9.5" r="2.2" fill="currentColor"/>',
    alambre: '<circle cx="12" cy="12" r="8"/><path d="M4 12h16M12 4c-3 3-3 13 0 16M12 4c3 3 3 13 0 16"/>',
    rayosx: '<rect x="4" y="4" width="11" height="11" rx="1"/><rect x="9" y="9" width="11" height="11" rx="1" stroke-dasharray="2 2"/>',
    tecnico: '<path d="M4 20h16M6 20V9l6-5 6 5v11"/><path d="M9 20v-6h6v6"/>',
    capas: '<path d="m12 3 9 5-9 5-9-5z"/><path d="m3 13 9 5 9-5"/>',
    comparar: '<rect x="3" y="5" width="7" height="14" rx="1"/><path d="M17 8v8M13 12h8"/>',
    lista: '<path d="M8 6h12M8 12h12M8 18h12M4 6h.01M4 12h.01M4 18h.01"/>',
    pantalla: '<path d="M4 9V4h5M20 9V4h-5M4 15v5h5M20 15v5h-5"/>',
    atajos: '<rect x="3" y="6" width="18" height="12" rx="2"/><path d="M7 10h.01M11 10h.01M15 10h.01M7 14h10"/>',
    cerrar: '<path d="M6 6l12 12M18 6 6 18"/>',
};
const svg = (k, cls = '') => `<svg class="vifc-i ${cls}" viewBox="0 0 24 24">${ICO[k]}</svg>`;

// Lleva una lista con scroll a una posición (fila resaltada desde el modelo).
export function desplazar(el, top) {
    if (!el) return;
    const cab = el.querySelector('thead')?.getBoundingClientRect().height || 0;
    if (top < el.scrollTop || top > el.scrollTop + el.clientHeight - cab - 40) el.scrollTop = Math.max(0, top - el.clientHeight / 3);
}

// marco: elemento que pasa a pantalla completa (visor + lista de materiales de la página).
export async function crear(contenedor, urlIfc, dotnet, catalogo, nombre, marco) {
    const v = new VisorIfc(contenedor, dotnet, marco);
    v.catalogo = catalogo || [];
    await v.agregar(urlIfc, nombre);
    return v;
}

const esc = s => String(s ?? '').replace(/[&<>"]/g, c => ({ '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;' }[c]));
const val = p => (p && typeof p === 'object' && 'value' in p) ? p.value : p;
const clave = (mid, id) => mid + ':' + id;
const num = (n, d = 0) => (n == null || n === '' || isNaN(+n)) ? '—' : (+n).toLocaleString('es-CO', { minimumFractionDigits: d, maximumFractionDigits: d });

function fondo(tipo) {
    const c = document.createElement('canvas'); c.width = 4; c.height = 256;
    const x = c.getContext('2d'), gr = x.createLinearGradient(0, 0, 0, 256);
    const p = { estudio: ['#f7f8fa', '#d6dbe2'], cielo: ['#cfe3f6', '#f3efe6'], oscuro: ['#2b313b', '#12151a'], tecnico: ['#ffffff', '#ffffff'] }[tipo] || ['#f7f8fa', '#d6dbe2'];
    gr.addColorStop(0, p[0]); gr.addColorStop(1, p[1]); x.fillStyle = gr; x.fillRect(0, 0, 4, 256);
    const t = new THREE.CanvasTexture(c); t.colorSpace = THREE.SRGBColorSpace; return t;
}
function texMatcap() {
    const c = document.createElement('canvas'); c.width = c.height = 256; const x = c.getContext('2d');
    const g = x.createRadialGradient(95, 80, 10, 128, 128, 128);
    g.addColorStop(0, '#ffffff'); g.addColorStop(.35, '#c9d1da'); g.addColorStop(.8, '#6c7784'); g.addColorStop(1, '#2d343d');
    x.fillStyle = g; x.beginPath(); x.arc(128, 128, 128, 0, 7); x.fill();
    const t = new THREE.CanvasTexture(c); t.colorSpace = THREE.SRGBColorSpace; return t;
}

class VisorIfc {
    constructor(el, dotnet, marco) {
        this.el = el; this.dotnet = dotnet; this.marco = marco || el;
        this.extra = [];              // piezas resaltadas además de la seleccionada (fila de la lista o Mayús+clic)
        this.modelos = [];            // {mid, url, nombre, grupo, niveles, idTorre, etiqueta}
        this.mallas = new Map();      // 'mid:id' -> Mesh
        this.info = new Map();        // 'mid:id' -> {mid, id, nivel, perfil, leido, tipo, marca}
        this.sel = null;
        this.medir = false; this.puntos = []; this.marcasMedida = [];
        this.ocultas = new Set(); this.aislado = null; this.nivelOn = new Map();
        const pref = (() => { try { return JSON.parse(localStorage.getItem(PREFS)) || {}; } catch { return {}; } })();
        const oscuro = ['oscuro', 'cristal'].includes(document.documentElement.dataset.tema);
        this.sombreado = pref.sombreado || 'material';
        this.color = pref.color || 'acero';
        this.opc = { ao: pref.ao ?? true, sombras: pref.sombras ?? true, rejilla: pref.rejilla ?? true, fondo: pref.fondo || (oscuro ? 'oscuro' : 'estudio') };

        el.style.position = 'relative';
        el.classList.add('vifc');
        const r = this.renderer = new THREE.WebGLRenderer({ antialias: true });
        r.setPixelRatio(Math.min(window.devicePixelRatio, 2));
        r.localClippingEnabled = true;
        r.shadowMap.enabled = true; r.shadowMap.type = THREE.PCFSoftShadowMap;
        r.toneMapping = THREE.ACESFilmicToneMapping;
        el.appendChild(r.domElement);

        this.scene = new THREE.Scene();
        this.env = new THREE.PMREMGenerator(r).fromScene(new RoomEnvironment(), .04).texture;
        this.hemi = new THREE.HemisphereLight(0xffffff, 0x9aa3ad, .55); this.scene.add(this.hemi);
        this.sol = new THREE.DirectionalLight(0xffffff, 2.4);
        this.sol.shadow.mapSize.set(2048, 2048); this.sol.shadow.bias = -.0004;
        this.scene.add(this.sol, this.sol.target);
        this.texMatcap = texMatcap();

        this.camara = new THREE.PerspectiveCamera(32, 1, 0.05, 5000);
        this.controles = new OrbitControls(this.camara, r.domElement);
        this.navegacion = navegacion3d(this.controles, this.renderer.domElement);
        this.controles.enableDamping = true; this.controles.dampingFactor = .12;
        this.controles.addEventListener('change', () => { this.sucio = true; this.planos(); });

        this.corte = new THREE.Plane(new THREE.Vector3(0, -1, 0), 1e6);
        this.raycaster = new THREE.Raycaster();
        this.matArista = new THREE.LineBasicMaterial({ color: 0x1b2027, transparent: true, opacity: .3, clippingPlanes: [this.corte] });

        // Posproceso: render → oclusión ambiental → contorno de selección → tono/sRGB.
        this.composer = new EffectComposer(r);
        this.composer.addPass(new RenderPass(this.scene, this.camara));
        this.ao = new GTAOPass(this.scene, this.camara, 1, 1);
        this.ao.blendIntensity = 1;
        this.ao.updateGtaoMaterial({ radius: .55, distanceExponent: 1.4, thickness: 1.2, scale: 1.1, samples: 16 });
        // El fondo degradado es un plano que el pase de normales pintaría en el origen: se quita solo durante ese pase.
        const aoRender = this.ao.render.bind(this.ao);
        this.ao.render = (...a) => { const bg = this.scene.background; this.scene.background = null; aoRender(...a); this.scene.background = bg; };
        this.composer.addPass(this.ao);
        this.outline = new OutlinePass(new THREE.Vector2(1, 1), this.scene, this.camara);
        Object.assign(this.outline, { edgeStrength: 8, edgeThickness: 2, edgeGlow: 0, pulsePeriod: 0 });
        this.outline.visibleEdgeColor.set(COLOR_SEL); this.outline.hiddenEdgeColor.set('#8a4b00');
        this.composer.addPass(this.outline);
        this.composer.addPass(new OutputPass());

        this.crearUi();
        this.ro = new ResizeObserver(() => this.redimensionar());
        this.ro.observe(el);
        document.addEventListener('fullscreenchange', this._fs = () => { this.redimensionar(); this.bFull?.classList.toggle('on', document.fullscreenElement === this.marco); });
        const cv = r.domElement;
        cv.addEventListener('pointerdown', e => { this.down = [e.clientX, e.clientY]; });
        cv.addEventListener('pointerup', e => {
            if (e.button === 0 && this.down && Math.hypot(e.clientX - this.down[0], e.clientY - this.down[1]) < 5) this.clic(e);
        });
        cv.addEventListener('dblclick', e => { const h = this.tocar(e); if (h) this.enfocarVarios([h.object.userData.k]); });
        window.addEventListener('keydown', this._tecla = e => this.tecla(e));
        const loop = () => {
            this.raf = requestAnimationFrame(loop);
            this.anim?.();
            this.controles.update();
            if (this.sucio) { this.sucio = false; this.composer.render(); this.dibujarGizmo(); this.etiquetaMedida(); }
        };
        loop();
    }

    guardarPrefs() {
        try { localStorage.setItem(PREFS, JSON.stringify({ sombreado: this.sombreado, color: this.color, ...this.opc })); } catch { }
    }

    // ---------- Carga de modelos ----------
    async api_() {
        if (this.api) return this.api;
        this.api = new WebIFC.IfcAPI();
        this.api.SetWasmPath(new URL('../lib/web-ifc/', import.meta.url).href, true);
        await this.api.Init();
        return this.api;
    }

    async agregar(url, nombre) {
        const api = await this.api_();
        this.estado(`Cargando ${nombre || url.split('/').pop()}…`);
        const datos = new Uint8Array(await (await fetch(url)).arrayBuffer());
        const mid = api.OpenModel(datos, { COORDINATE_TO_ORIGIN: false });
        const mod = { mid, url, nombre: nombre || '', niveles: [], idTorre: null, porPieza: new Map(), planos: new Map(), planoPieza: {}, asignadas: new Set() };
        await this.leerEstructura(mod);
        if (!mod.nombre && mod.idTorre) mod.nombre = val(api.GetLine(mid, mod.idTorre).Name) || 'Torre';

        // web-ifc entrega la geometría en metros (aplica la unidad del IFC) y con Y arriba.
        mod.grupo = new THREE.Group();
        api.StreamAllMeshes(mid, fm => {
            const id = fm.expressID, geos = [];
            let color = null;
            for (let i = 0; i < fm.geometries.size(); i++) {
                const pg = fm.geometries.get(i);
                const g = api.GetGeometry(mid, pg.geometryExpressID);
                const v = api.GetVertexArray(g.GetVertexData(), g.GetVertexDataSize());
                const ix = api.GetIndexArray(g.GetIndexData(), g.GetIndexDataSize());
                const pos = new Float32Array(v.length / 2), nor = new Float32Array(v.length / 2);
                for (let k = 0, j = 0; k < v.length; k += 6, j += 3) {
                    pos[j] = v[k]; pos[j + 1] = v[k + 1]; pos[j + 2] = v[k + 2];
                    nor[j] = v[k + 3]; nor[j + 1] = v[k + 4]; nor[j + 2] = v[k + 5];
                }
                const bg = new THREE.BufferGeometry();
                bg.setAttribute('position', new THREE.BufferAttribute(pos, 3));
                bg.setAttribute('normal', new THREE.BufferAttribute(nor, 3));
                bg.setIndex(new THREE.BufferAttribute(ix, 1));
                bg.applyMatrix4(new THREE.Matrix4().fromArray(pg.flatTransformation));
                geos.push(bg);
                color = pg.color;
                g.delete();
            }
            if (!geos.length) return;
            const geo = geos.length === 1 ? geos[0] : unir(geos);
            const k = clave(mid, id);
            const mesh = new THREE.Mesh(geo, new THREE.MeshStandardMaterial());
            mesh.castShadow = true; mesh.receiveShadow = true;
            mesh.userData.k = k;
            mesh.userData.colorIfc = new THREE.Color(color.x, color.y, color.z);
            mesh.userData.opacidadIfc = color.w;
            const aristas = new THREE.LineSegments(new THREE.EdgesGeometry(geo, 30), this.matArista);
            mesh.add(aristas);
            mod.grupo.add(mesh);
            this.mallas.set(k, mesh);
            const inf = this.info.get(k) || { mid, id };
            this.info.set(k, inf);
        });

        // A la derecha del último modelo, con una separación de medio ancho.
        const caja = new THREE.Box3().setFromObject(mod.grupo);
        if (this.modelos.length) {
            const prev = new THREE.Box3();
            this.modelos.forEach(m => prev.expandByObject(m.grupo));
            const sep = Math.max(4, (caja.max.x - caja.min.x) * .5);
            mod.grupo.position.x = prev.max.x + sep - caja.min.x;
        }
        this.scene.add(mod.grupo);
        mod.etiqueta = etiqueta(mod.nombre);
        const c2 = new THREE.Box3().setFromObject(mod.grupo);
        mod.etiqueta.position.set((c2.min.x + c2.max.x) / 2, c2.max.y + Math.max(2, (c2.max.y - c2.min.y) * .05), (c2.min.z + c2.max.z) / 2);
        mod.etiqueta.scale.multiplyScalar(Math.max(1, (c2.max.y - c2.min.y) / 40));
        mod.etiqueta.visible = this.modelos.length > 0;
        this.scene.add(mod.etiqueta);
        this.modelos.push(mod);
        if (this.modelos.length > 1) this.modelos.forEach(m => m.etiqueta.visible = true);

        this.recalcularCaja();
        this.pintar();
        this.vista(this.modelos.length > 1 ? 'frente' : 'iso', false);
        this.panelCapas();
        await this.panelTorre();
        this.listaComparar();
        this.estado('');
    }

    quitar(mid) {
        if (this.modelos.length <= 1) return;
        const i = this.modelos.findIndex(m => m.mid === mid); if (i < 0) return;
        const mod = this.modelos[i];
        if (this.sel && this.sel.startsWith(mid + ':')) this.seleccionar(null);
        this.extra = this.extra.filter(k => !k.startsWith(mid + ':'));
        this.scene.remove(mod.grupo); this.scene.remove(mod.etiqueta);
        mod.grupo.traverse(o => { if (o.geometry) o.geometry.dispose(); if (o.material && o.material !== this.matArista) o.material.dispose(); });
        for (const k of [...this.mallas.keys()]) if (k.startsWith(mid + ':')) { this.mallas.delete(k); this.info.delete(k); this.ocultas.delete(k); }
        try { this.api.CloseModel(mid); } catch { }
        this.modelos.splice(i, 1);
        // Reacomodar los que quedan, de izquierda a derecha.
        let x = null;
        for (const m of this.modelos) {
            m.grupo.position.x = 0;
            const c = new THREE.Box3().setFromObject(m.grupo);
            if (x !== null) m.grupo.position.x = x - c.min.x;
            const c2 = new THREE.Box3().setFromObject(m.grupo);
            m.etiqueta.position.x = (c2.min.x + c2.max.x) / 2;
            x = c2.max.x + Math.max(4, (c2.max.x - c2.min.x) * .5);
            m.etiqueta.visible = this.modelos.length > 1;
        }
        this.recalcularCaja();
        this.vista(this.modelos.length > 1 ? 'frente' : 'iso');
        this.pintar(); this.panelCapas(); this.panelTorre(); this.listaComparar();
    }

    recalcularCaja() {
        this.caja = new THREE.Box3();
        this.modelos.forEach(m => this.caja.expandByObject(m.grupo));
        const s = this.caja.getSize(new THREE.Vector3()), c = this.caja.getCenter(new THREE.Vector3());
        for (const o of [this.rejillaObj, this.suelo]) if (o) { this.scene.remove(o); o.geometry.dispose(); o.material.dispose(); }
        const n = Math.ceil(Math.max(s.x, s.z) * 2.2 / 5) * 5 + 10;
        this.rejillaObj = new THREE.GridHelper(n, n / 2.5, 0x9aa3ae, 0xc5cbd2);
        this.rejillaObj.material.transparent = true; this.rejillaObj.material.opacity = .45;
        this.rejillaObj.position.set(c.x, this.caja.min.y, c.z);
        this.suelo = new THREE.Mesh(new THREE.PlaneGeometry(n * 3, n * 3), new THREE.ShadowMaterial({ opacity: .2 }));
        this.suelo.rotation.x = -Math.PI / 2; this.suelo.position.set(c.x, this.caja.min.y - .01, c.z); this.suelo.receiveShadow = true;
        this.scene.add(this.rejillaObj, this.suelo);
        const h = Math.max(s.y, s.x, 10);
        this.sol.position.set(c.x + h * .45, this.caja.max.y + h * .5, c.z + h * .65); this.sol.target.position.copy(c);
        Object.assign(this.sol.shadow.camera, { left: -h * .9, right: h * .9, top: h * .9, bottom: -h * .9, near: .5, far: h * 4 });
        this.sol.shadow.camera.updateProjectionMatrix();
        this.corteSlider.max = String(Math.ceil(this.caja.max.y * 10) / 10);
        this.corteSlider.min = String(Math.floor(this.caja.min.y * 10) / 10);
        this.corteSlider.value = this.corteSlider.max;
        this.corte.constant = 1e6; this.txtCorte.textContent = '';
    }

    async leerEstructura(mod) {
        const api = this.api, mid = mod.mid;
        const arbol = await api.properties.getSpatialStructure(mid, false);
        const recorrer = (n, nivel) => {
            const tipo = typeof n.type === 'number' ? api.GetNameFromTypeCode(n.type) : String(n.type).toUpperCase();
            if (tipo === 'IFCBUILDINGSTOREY') { nivel = { id: n.expressID, nombre: '', ids: [] }; mod.niveles.push(nivel); }
            if (tipo === 'IFCBUILDING') mod.idTorre = n.expressID;
            if (tipo === 'IFCMEMBER' && nivel) { nivel.ids.push(n.expressID); this.info.set(clave(mid, n.expressID), { mid, id: n.expressID, nivel: nivel.id, nivelNombre: '' }); }
            (n.children || []).forEach(c => recorrer(c, nivel));
        };
        recorrer(arbol, null);
        for (const nv of mod.niveles) {
            nv.nombre = val(api.GetLine(mid, nv.id).Name) || ('Nivel ' + nv.id);
            nv.ids.forEach(id => { const inf = this.info.get(clave(mid, id)); if (inf) inf.nivelNombre = nv.nombre; });
        }
        // Perfil y estado por miembro (Pset SigWeb_Pieza)
        const rels = api.GetLineIDsWithType(mid, WebIFC.IFCRELDEFINESBYPROPERTIES);
        for (let i = 0; i < rels.size(); i++) {
            const r = api.GetLine(mid, rels.get(i));
            const ps = api.GetLine(mid, r.RelatingPropertyDefinition.value);
            if (val(ps.Name) !== 'SigWeb_Pieza') continue;
            const datos = {};
            for (const h of ps.HasProperties || []) { const p = api.GetLine(mid, h.value); datos[val(p.Name)] = val(p.NominalValue); }
            for (const o of r.RelatedObjects) {
                const k = clave(mid, o.value);
                const inf = this.info.get(k) || { mid, id: o.value };
                inf.perfil = datos.Perfil; inf.leido = datos.PerfilLeido === true || datos.PerfilLeido === 'T' || datos.PerfilLeido === '.T.';
                inf.supuesto = datos.PerfilSupuesto === true || datos.PerfilSupuesto === 'T' || datos.PerfilSupuesto === '.T.';
                inf.tipo = datos.Tipo; inf.marca = datos.Marca; inf.pieza = datos.Id != null ? String(datos.Id) : null;
                if (inf.pieza) mod.porPieza.set(inf.pieza, k);
                this.info.set(k, inf);
            }
        }
    }

    // ---------- Sombreado y colores ----------
    crearMaterial(c, opacidad, seleccionada) {
        const s = this.sombreado, base = { side: THREE.DoubleSide, clippingPlanes: [this.corte] };
        let m;
        if (s === 'matcap') m = new THREE.MeshMatcapMaterial({ ...base, color: c, matcap: this.texMatcap });
        else if (s === 'tecnico') m = new THREE.MeshBasicMaterial({ ...base, color: c, polygonOffset: true, polygonOffsetFactor: 1, polygonOffsetUnits: 1 });
        else {
            const metal = s === 'material' && this.color === 'acero';
            m = new THREE.MeshStandardMaterial({ ...base, color: c, metalness: s === 'solido' ? .05 : metal ? .78 : .3, roughness: s === 'solido' ? .75 : metal ? .36 : .5, envMapIntensity: .95 });
            if (seleccionada) m.emissive = new THREE.Color('#7a3d00');
        }
        const transp = s === 'rayosx' && !seleccionada ? .16 : opacidad;
        m.transparent = transp < 1; m.opacity = transp; m.depthWrite = transp >= 1;
        if (s === 'alambre' && !seleccionada) m.visible = false;
        return m;
    }

    pintar() {
        const s = this.sombreado;
        const nombresNivel = [...new Set(this.modelos.flatMap(m => m.niveles.map(n => n.nombre)))];
        const colorNivel = new Map(nombresNivel.map((n, i) => [n, new THREE.Color(PALETA[i % PALETA.length])]));
        const perfiles = [...new Set([...this.info.values()].filter(x => x.leido || x.supuesto).map(x => x.perfil))].sort(ordenPerfil);
        const colorPerfil = new Map(perfiles.map((p, i) => [p, new THREE.Color().setHSL((i * 0.618) % 1, .55, .55)]));
        const marcadas = new Set([this.sel, ...this.extra].filter(Boolean));
        const acero = new THREE.Color(ACERO[s] || ACERO.material);
        for (const [k, m] of this.mallas) {
            const inf = this.info.get(k) || {};
            let c = acero;
            if (this.color === 'nivel') c = colorNivel.get(inf.nivelNombre) || acero;
            else if (this.color === 'estado') c = new THREE.Color(inf.leido ? '#2ca58d' : inf.supuesto ? '#d9b44a' : '#d9822b');
            else if (this.color === 'perfil') c = inf.leido || inf.supuesto ? colorPerfil.get(inf.perfil) || new THREE.Color('#555b66') : new THREE.Color('#555b66');
            if (inf.supuesto && !inf.manual && (this.color === 'acero' || this.color === 'nivel' || this.color === 'perfil'))
                c = c.clone().lerp(new THREE.Color(TONO_SUPUESTO), MEZCLA_SUPUESTO);
            else if (this.color === 'ifc') c = m.userData.colorIfc;
            const sel = marcadas.has(k);
            const op = this.color === 'ifc' && m.userData.opacidadIfc < 1 ? m.userData.opacidadIfc : 1;
            m.material.dispose();
            m.material = this.crearMaterial(sel ? new THREE.Color(COLOR_SEL) : c, sel ? 1 : op, sel);
        }
        // Aristas: protagonistas en Alambre y Plano técnico, tenues en Sólido y Rayos X.
        const ar = { alambre: [0x1b2027, 1], tecnico: [0x111111, 1], rayosx: [0x51657c, .5], solido: [0x1b2027, .15] }[s];
        this.matArista.visible = !!ar;
        if (ar) { this.matArista.color.set(ar[0]); this.matArista.opacity = ar[1]; this.matArista.transparent = ar[1] < 1; }
        const tec = s === 'tecnico';
        this.scene.background?.dispose?.();
        this.scene.background = fondo(tec ? 'tecnico' : this.opc.fondo);
        this.scene.environment = ['material', 'solido', 'rayosx'].includes(s) ? this.env : null;
        this.ao.enabled = this.opc.ao && ['material', 'solido', 'matcap'].includes(s);
        const sombras = this.opc.sombras && !tec && s !== 'rayosx' && s !== 'alambre';
        this.renderer.shadowMap.enabled = sombras; this.sol.castShadow = sombras;
        if (this.suelo) { this.suelo.visible = sombras; this.suelo.material.opacity = this.opc.fondo === 'oscuro' ? .35 : .2; }
        if (this.rejillaObj) this.rejillaObj.visible = this.opc.rejilla && !tec;
        this.renderer.toneMapping = tec || s === 'matcap' ? THREE.NoToneMapping : THREE.ACESFilmicToneMapping;
        this.outline.selectedObjects = [...marcadas].map(k => this.mallas.get(k)).filter(Boolean);
        this.barra.querySelectorAll('[data-s]').forEach(b => b.classList.toggle('on', b.dataset.s === s));

        this.leyenda = [];
        if (this.color === 'nivel') this.leyenda = nombresNivel.map((n, i) => [PALETA[i % PALETA.length], n]);
        if (this.color === 'estado') this.leyenda = [['#2ca58d', 'Perfil leído (L)'], ['#d9b44a', 'Perfil supuesto (L)'], ['#d9822b', 'Sin perfil']];
        if (this.color === 'acero' && [...this.info.values()].some(x => x.supuesto && !x.manual))
            this.leyenda = [['#' + acero.getHexString(), 'Perfil leído'], ['#' + acero.clone().lerp(new THREE.Color(TONO_SUPUESTO), MEZCLA_SUPUESTO).getHexString(), 'Perfil supuesto']];
        if (this.color === 'perfil') this.leyenda = [...perfiles.map(p => ['#' + colorPerfil.get(p).getHexString(), p]), ['#555b66', 'No leído']];
        this.dibujarLeyenda();
        this.sucio = true;
    }

    ponerSombreado(s) {
        this.sombreado = s; this.guardarPrefs(); this.pintar();
    }

    // Grupo encendido (Capas), no oculto con H y dentro de la vista aislada (/).
    aplicarVisibilidad() {
        for (const [k, m] of this.mallas) {
            const inf = this.info.get(k) || {};
            m.visible = this.nivelOn.get(clave(inf.mid, inf.nivel)) !== false && !this.ocultas.has(k) && (!this.aislado || this.aislado.has(k));
        }
        this.sucio = true;
    }

    // ---------- Interacción ----------
    tocar(e) {
        const r = this.renderer.domElement.getBoundingClientRect();
        this.raycaster.setFromCamera(new THREE.Vector2(((e.clientX - r.left) / r.width) * 2 - 1, -((e.clientY - r.top) / r.height) * 2 + 1), this.camara);
        const visibles = [...this.mallas.values()].filter(m => m.visible);
        return this.raycaster.intersectObjects(visibles, false).filter(h => this.corte.distanceToPoint(h.point) >= 0)[0] || null;
    }

    clic(e) {
        if (e.button !== 0 || this.navegacion.pan) return;
        const h = this.tocar(e);
        if (this.medir) { if (h) this.agregarPuntoMedida(h.point); return; }
        const k = h ? h.object.userData.k : null;
        if (e.shiftKey && k && this.sel) {
            if (k === this.sel) return;
            const i = this.extra.indexOf(k);
            if (i >= 0) this.extra.splice(i, 1); else this.extra.push(k);
            this.pintar(); this.fichaVarias();
            return;
        }
        this.seleccionar(k);
    }

    async seleccionar(k, desdeLista) {
        this.sel = k;
        if (!desdeLista) this.extra = [];
        this.pintar();
        if (!desdeLista && this.dotnet) {
            const i = k ? this.info.get(k) : null, m = i ? this.modelos.find(x => x.mid === i.mid) : null;
            this.dotnet.invokeMethodAsync('AlSeleccionarPieza', m?.url ?? null, i?.pieza ?? null).catch(() => { });
        }
        if (!k) { this.ficha.style.display = 'none'; return; }
        const { mid, id } = this.info.get(k) || {};
        const mod = this.modelos.find(m => m.mid === mid);
        const e = this.api.GetLine(mid, id);
        const psets = await this.api.properties.getPropertySets(mid, id, true);
        let html = `<div class="vifc-pz-cab"><span class="vifc-pz-id">${esc(val(e.Name))}</span><button type="button" class="vifc-x" data-x title="Cerrar">${svg('cerrar')}</button></div><dl class="vifc-dl">`;
        if (this.modelos.length > 1) html += fila('Torre', mod?.nombre);
        html += fila('Clase IFC', this.api.GetNameFromTypeCode(e.type));
        html += fila('Tipo predefinido', val(e.PredefinedType));
        html += fila('Tipo de objeto', val(e.ObjectType));
        html += fila('Tag (marca)', val(e.Tag));
        const inf = this.info.get(k), pl = mod && inf?.pieza ? mod.planos.get(mod.planoPieza[inf.pieza]) : null;
        if (inf?.manual) html += fila('Perfil asignado', `${inf.perfil} · marca ${inf.marca}`);
        if (pl) html += fila('Plano', pl.url
            ? `<a href="${esc(pl.url)}" target="_blank" rel="noopener" title="Abrir el plano">${esc(pl.texto)}</a>`
            : esc(pl.texto), true);
        html += fila('GlobalId', val(e.GlobalId));
        html += '</dl>';
        for (const ps of psets) {
            html += `<div class="vifc-sec">${esc(val(ps.Name))}</div><dl class="vifc-dl">`;
            for (const h of (ps.HasProperties || ps.Quantities || [])) {
                let v = val(h.NominalValue);
                if (v === undefined) v = val(h.LengthValue) ?? val(h.WeightValue);
                if (typeof v === 'number') v = (Math.round(v * 10) / 10).toLocaleString('es-CO');
                if (v === true || v === 'T') v = 'Sí'; if (v === false || v === 'F') v = 'No';
                html += fila(val(h.Name), v);
            }
            html += '</dl>';
        }
        if (this.sel !== k) return;   // otra selección mientras se leían las propiedades
        this.ficha.innerHTML = html;
        this.ficha.querySelector('[data-x]').onclick = () => this.seleccionar(null);
        this.ficha.style.display = 'block';
    }

    fichaVarias() {
        const ks = [this.sel, ...this.extra].filter(Boolean);
        if (ks.length < 2) { if (this.sel) this.seleccionar(this.sel, true); return; }
        this.ficha.innerHTML = `<div class="vifc-pz-cab"><span class="vifc-pz-id">${ks.length} piezas</span><button type="button" class="vifc-x" data-x title="Cerrar">${svg('cerrar')}</button></div>`;
        this.ficha.querySelector('[data-x]').onclick = () => this.seleccionar(null);
        this.ficha.style.display = 'block';
    }

    // ---------- Cámara ----------
    // Con la ficha de la torre abierta, el modelo se centra en el espacio libre a su izquierda.
    anchoUtil() {
        if (this.torreEl.style.display === 'none' || !this.el.clientWidth) return 1;
        return Math.max(.4, 1 - (this.torreEl.offsetWidth + 24) / this.el.clientWidth);
    }
    desplazado(c, dir, d) {
        const u = this.anchoUtil(); if (u >= 1) return c.clone();
        const der = new THREE.Vector3().crossVectors(new THREE.Vector3(0, 1, 0), dir); if (der.lengthSq() < 1e-6) der.set(1, 0, 0); der.normalize();
        const ancho = 2 * d * Math.tan(THREE.MathUtils.degToRad(this.camara.fov / 2)) * this.camara.aspect;
        return c.clone().addScaledVector(der, ancho * (1 - u) / 2);
    }
    vista(tipo, animar = true) {
        if (!this.caja || this.caja.isEmpty()) return;
        const c = this.caja.getCenter(new THREE.Vector3()), t = this.caja.getSize(new THREE.Vector3());
        const dirs = { iso: [.62, .38, .78], frente: [0, .02, 1], lateral: [1, .02, 0], planta: [0, 1, .0001], atras: [0, .02, -1], izq: [-1, .02, 0], abajo: [0, -1, .0001] };
        const v = new THREE.Vector3(...(dirs[tipo] || dirs.iso)).normalize();
        const tanV = Math.tan(THREE.MathUtils.degToRad(this.camara.fov / 2)), u = this.anchoUtil();
        const ancho = Math.abs(v.x) > .9 ? t.z : Math.abs(v.z) > .9 ? t.x : Math.max(t.x, t.z);
        const alto = Math.abs(v.y) > .9 ? t.z : t.y;
        const d = Math.max((alto * .5) / tanV, (ancho * .5) / (tanV * this.camara.aspect * u)) * 1.14 + Math.max(t.x, t.z) * .5;
        const obj = this.desplazado(c, v, d);
        this.radioModelo = t.length() / 2; this.controles.maxDistance = this.radioModelo * 12; this.planos();
        this.ir(obj.clone().addScaledVector(v, d), obj, animar);
    }
    // Planos de recorte según la distancia actual (antes far = 12× la distancia del encuadre: al alejarse, la torre desaparecía).
    planos() {
        const r = this.radioModelo || 50, d = this.camara.position.distanceTo(this.controles.target);
        this.camara.near = Math.max(.01, (d - r * 3) > 0 ? (d - r * 3) / 2 : d / 500); this.camara.far = d + r * 10; this.camara.updateProjectionMatrix();
    }
    ir(pos, obj, animar = true) {
        if (!animar) { this.anim = null; this.camara.position.copy(pos); this.controles.target.copy(obj); this.controles.update(); this.sucio = true; return; }
        const p0 = this.camara.position.clone(), o0 = this.controles.target.clone(), t0 = performance.now();
        this.anim = () => {
            const k = Math.min(1, (performance.now() - t0) / 450), e = 1 - Math.pow(1 - k, 3);
            this.camara.position.lerpVectors(p0, pos, e); this.controles.target.lerpVectors(o0, obj, e); this.sucio = true;
            if (k >= 1) this.anim = null;
        };
    }
    // Encuadra la selección (o todo lo visible).
    encuadrar() {
        const ks = [this.sel, ...this.extra].filter(Boolean);
        if (ks.length) return this.enfocarVarios(ks);
        const b = new THREE.Box3();
        for (const m of this.mallas.values()) if (m.visible) b.expandByObject(m);
        this.enfocarCaja(b.isEmpty() ? this.caja : b);
    }
    enfocarVarios(ks) {
        const b = new THREE.Box3();
        ks.forEach(k => { const m = this.mallas.get(k); if (m) b.expandByObject(m); });
        if (!b.isEmpty()) this.enfocarCaja(b);
    }
    enfocarCaja(b) {
        const c = b.getCenter(new THREE.Vector3()), r = Math.max(b.getSize(new THREE.Vector3()).length() * .5, 1.2);
        const dir = this.camara.position.clone().sub(this.controles.target).normalize();
        const d = r / Math.sin(THREE.MathUtils.degToRad(this.camara.fov / 2)) * 1.5;
        this.camara.near = Math.max(.01, d / 500); this.camara.updateProjectionMatrix();
        const o = this.desplazado(c, dir, d);
        this.ir(o.clone().addScaledVector(dir, d), o);
    }

    // ---------- Medir ----------
    agregarPuntoMedida(p) {
        if (this.puntos.length >= 2) this.limpiarMedida();
        const s = new THREE.Mesh(new THREE.SphereGeometry(.12, 16, 12), new THREE.MeshBasicMaterial({ color: 0xec1c24, depthTest: false }));
        s.position.copy(p); s.renderOrder = 10; this.scene.add(s); this.marcasMedida.push(s);
        this.puntos.push(p.clone());
        if (this.puntos.length === 2) {
            const ln = new THREE.Line(new THREE.BufferGeometry().setFromPoints(this.puntos), new THREE.LineBasicMaterial({ color: 0xec1c24, depthTest: false }));
            ln.renderOrder = 10; this.scene.add(ln); this.marcasMedida.push(ln);
            this.estado('');
        } else this.estado('Elige el segundo punto', true);
        this.sucio = true;
    }
    limpiarMedida() {
        for (const o of this.marcasMedida) { this.scene.remove(o); o.geometry.dispose(); o.material.dispose(); }
        this.marcasMedida = []; this.puntos = [];
        this.estado(this.medir ? 'Elige el primer punto' : '', true);
        this.sucio = true;
    }
    etiquetaMedida() {
        const e = this.txtMedida;
        if (this.puntos.length !== 2) { e.style.display = 'none'; return; }
        const [a, b] = this.puntos, m = a.clone().add(b).multiplyScalar(.5).project(this.camara);
        e.style.display = 'block';
        e.style.left = ((m.x + 1) / 2 * this.el.clientWidth + 8) + 'px';
        e.style.top = ((1 - m.y) / 2 * this.el.clientHeight - 12) + 'px';
        e.textContent = `${num(a.distanceTo(b) * 1000)} mm · ΔX ${num((b.x - a.x) * 1000)} · ΔY ${num((b.z - a.z) * 1000)} · ΔZ ${num((b.y - a.y) * 1000)}`;
    }

    // ---------- Atajos de teclado (como en Blender) ----------
    tecla(e) {
        if (!this.el.isConnected || this.el.offsetParent === null && document.fullscreenElement !== this.marco) return;
        const t = e.target;
        if (t && (t.isContentEditable || /^(INPUT|TEXTAREA|SELECT)$/.test(t.tagName))) return;
        const k = e.key.toLowerCase();
        if (k === 'f' && !e.ctrlKey) this.encuadrar();
        else if (k === 'h' && e.altKey) { this.ocultas.clear(); this.aplicarVisibilidad(); }
        else if (k === 'h' && !e.ctrlKey) { [this.sel, ...this.extra].filter(Boolean).forEach(x => this.ocultas.add(x)); this.seleccionar(null); this.aplicarVisibilidad(); }
        else if (k === '/') {
            const ks = [this.sel, ...this.extra].filter(Boolean);
            this.aislado = this.aislado || !ks.length ? null : new Set(ks);
            this.aplicarVisibilidad(); this.estado(this.aislado ? 'Vista aislada · / para salir' : '', true);
            if (this.aislado) this.encuadrar(); else this.vista('iso');
        }
        else if (k === 'z' && e.altKey) this.ponerSombreado(this.sombreado === 'rayosx' ? 'material' : 'rayosx');
        else if (k === 'm' && !e.ctrlKey) this.bMedir.click();
        else if (k === 'escape') { if (this.medir) this.bMedir.click(); else this.seleccionar(null); }
        else if (e.key === '1') this.vista(e.ctrlKey ? 'atras' : 'frente');
        else if (e.key === '3') this.vista(e.ctrlKey ? 'izq' : 'lateral');
        else if (e.key === '7') this.vista(e.ctrlKey ? 'abajo' : 'planta');
        else if (e.key === '0') this.vista('iso');
        else return;
        e.preventDefault();
    }


    pantallaCompleta() {
        if (document.fullscreenElement) document.exitFullscreen();
        else this.marco.requestFullscreen?.();
    }

    // ---------- Lista de materiales (página) ----------
    habilitarLista(si) { if (this.bLista) this.bLista.style.display = si ? '' : 'none'; }

    setPlanos(url, enlaces, porPieza) {
        const mod = this.modelos.find(m => m.url === url) || this.modelos[0];
        if (!mod) return;
        mod.planos = new Map((enlaces || []).map(p => [p.clave, p]));
        mod.planoPieza = porPieza || {};
    }

    // Asignaciones manuales de la lista de materiales (Modelos 3D): cada pieza [{pieza, a, b, tipo, cara, marca, perfil}]
    // se redibuja como el ángulo L real de su perfil (misma regla de orientación que el generador del IFC).
    // Se reemplaza la geometría de la malla del IFC, así la pieza conserva selección, colores y grupos.
    aplicarAsignaciones(url, lista) {
        const mod = this.modelos.find(m => m.url === url) || this.modelos[0];
        if (!mod) return 0;
        for (const k of mod.asignadas) {
            const m = this.mallas.get(k), inf = this.info.get(k);
            if (!m || !m.userData.geoOriginal) continue;
            m.geometry.dispose(); m.geometry = m.userData.geoOriginal; delete m.userData.geoOriginal;
            this.aristas(m);
            if (inf && inf.original) { Object.assign(inf, inf.original); delete inf.original; delete inf.manual; }
        }
        mod.asignadas.clear();
        let n = 0;
        for (const it of lista || []) {
            const k = mod.porPieza.get(String(it.pieza)), m = k ? this.mallas.get(k) : null;
            if (!m || !it.a || !it.b) continue;
            const g = geometriaL({ miembros: [{ a: it.a, b: it.b, tipo: it.tipo, cara: it.cara, marca: it.marca }],
                lista_materiales: [{ marca: it.marca, perfil: it.perfil, clase: 'PERFIL' }] });
            if (!g.userData.conPerfil) { g.dispose(); continue; }   // perfil que no es un ángulo L: se deja como está
            m.userData.geoOriginal = m.geometry;
            m.geometry = g;
            this.aristas(m);
            const inf = this.info.get(k);
            if (inf) {
                inf.original = { perfil: inf.perfil, leido: inf.leido, supuesto: inf.supuesto, marca: inf.marca };
                inf.perfil = it.perfil; inf.leido = true; inf.supuesto = false; inf.marca = it.marca; inf.manual = true;
            }
            mod.asignadas.add(k); n++;
        }
        this.pintar();
        if (this.sel) this.seleccionar(this.sel, true);
        return n;
    }

    aristas(m) {
        const ln = m.children.find(c => c.isLineSegments);
        if (ln) { ln.geometry.dispose(); ln.geometry = new THREE.EdgesGeometry(m.geometry, 30); }
        this.sucio = true;
    }

    // Selecciona y encuadra todas las piezas de una fila de la lista (Ids de pieza del Pset SigWeb_Pieza).
    async verPiezas(url, ids) {
        const mod = this.modelos.find(m => m.url === url) || this.modelos[0];
        if (!mod) return false;
        const ks = (ids || []).map(id => mod.porPieza.get(String(id))).filter(k => k && this.mallas.has(k));
        if (!ks.length) return false;
        // Si la pieza está en un grupo apagado, oculta o fuera de la vista aislada, se vuelve a mostrar.
        let cambio = false;
        for (const k of ks) {
            const inf = this.info.get(k), g = clave(inf.mid, inf.nivel);
            if (this.nivelOn.get(g) === false) { this.nivelOn.set(g, true); cambio = true; }
            if (this.ocultas.delete(k)) cambio = true;
        }
        if (this.aislado && ks.some(k => !this.aislado.has(k))) { this.aislado = null; this.estado('', true); cambio = true; }
        if (cambio) { this.aplicarVisibilidad(); this.panelCapas(); }
        if (this.corte.constant < 1e5) { this.corte.constant = 1e6; this.corteSlider.value = this.corteSlider.max; this.txtCorte.textContent = ''; }
        this.extra = ks.slice(1);
        await this.seleccionar(ks[0], true);
        this.enfocarVarios(ks);
        return true;
    }

    // ---------- Miniatura para el catálogo (mismo motor, acero sobre estudio, vista isométrica) ----------
    miniatura() {
        const antes = { sel: this.sel, extra: this.extra, sombreado: this.sombreado, color: this.color, fondo: this.opc.fondo, corte: this.corte.constant,
            pos: this.camara.position.clone(), obj: this.controles.target.clone(), ficha: this.torreEl.style.display, etiquetas: this.modelos.map(m => m.etiqueta.visible), rejilla: this.opc.rejilla };
        this.sel = null; this.extra = []; this.sombreado = 'material'; this.color = 'acero'; this.opc.fondo = 'estudio'; this.opc.rejilla = false; this.corte.constant = 1e6;
        this.torreEl.style.display = 'none'; this.modelos.forEach(m => m.etiqueta.visible = false);
        this.pintar(); this.vista('iso', false);
        this.composer.render();
        // Recorte alrededor del modelo con proporción 23:25 (tarjeta del catálogo).
        const cv = this.renderer.domElement, W = cv.width, H = cv.height;
        let x0 = 1, y0 = 1, x1 = -1, y1 = -1;
        const b = this.caja;
        for (const x of [b.min.x, b.max.x]) for (const y of [b.min.y, b.max.y]) for (const z of [b.min.z, b.max.z]) {
            const p = new THREE.Vector3(x, y, z).project(this.camara);
            x0 = Math.min(x0, p.x); x1 = Math.max(x1, p.x); y0 = Math.min(y0, p.y); y1 = Math.max(y1, p.y);
        }
        let cx = (x0 + x1) / 4 * W + W / 2, cy = H / 2 - (y0 + y1) / 4 * H;
        let h = (y1 - y0) / 2 * H * 1.08, w = h * 460 / 500;
        if ((x1 - x0) / 2 * W * 1.08 > w) { w = (x1 - x0) / 2 * W * 1.08; h = w * 500 / 460; }
        const out = document.createElement('canvas'); out.width = 460; out.height = 500;
        const g = out.getContext('2d');
        g.fillStyle = '#e9ecf0'; g.fillRect(0, 0, 460, 500);
        g.drawImage(cv, cx - w / 2, cy - h / 2, w, h, 0, 0, 460, 500);
        const url = out.toDataURL('image/png');
        Object.assign(this, { sel: antes.sel, extra: antes.extra, sombreado: antes.sombreado, color: antes.color });
        this.opc.fondo = antes.fondo; this.opc.rejilla = antes.rejilla; this.corte.constant = antes.corte;
        this.torreEl.style.display = antes.ficha; this.modelos.forEach((m, i) => m.etiqueta.visible = antes.etiquetas[i]);
        this.camara.position.copy(antes.pos); this.controles.target.copy(antes.obj);
        this.pintar();
        return url.substring(url.indexOf(',') + 1);
    }

    redimensionar() {
        const w = this.el.clientWidth, h = this.el.clientHeight;
        if (!w || !h) return;
        this.renderer.setSize(w, h); this.composer.setSize(w, h);
        this.camara.aspect = w / h; this.camara.updateProjectionMatrix();
        this.sucio = true;
        this.ubicarGizmo();
    }

    estado(t, aviso) {
        if (!this.txtEstado) return;
        this.txtEstado.textContent = t;
        this.txtEstado.className = 'vifc-estado' + (aviso ? ' vifc-aviso' : '');
        this.txtEstado.style.display = t ? 'block' : 'none';
    }

    // ---------- Interfaz sobre el lienzo ----------
    crearUi() {
        const div = (cls, padre = this.el) => { const d = document.createElement('div'); d.className = cls; padre.appendChild(d); return d; };
        this.barra = div('vifc-barra');
        const grupo = () => div('vifc-grp', this.barra);
        const b = (g, ico, title, fn) => {
            const x = document.createElement('button'); x.type = 'button'; x.className = 'vifc-tb'; x.innerHTML = svg(ico); x.title = title;
            x.onclick = fn; g.appendChild(x); return x;
        };
        const gv = grupo();
        b(gv, 'iso', 'Isométrica (0)', () => this.vista('iso'));
        b(gv, 'frente', 'Frente · cara transversal (1)', () => this.vista('frente'));
        b(gv, 'lateral', 'Lateral · cara longitudinal (3)', () => this.vista('lateral'));
        b(gv, 'planta', 'Planta (7)', () => this.vista('planta'));
        const gh = grupo();
        b(gh, 'encuadrar', 'Encuadrar (F)', () => this.encuadrar());
        this.bMedir = b(gh, 'medir', 'Medir (M)', () => {
            this.medir = !this.medir; this.bMedir.classList.toggle('on', this.medir);
            this.limpiarMedida();
        });
        const bCorte = b(gh, 'corte', 'Corte horizontal', () => {
            const on = bCorte.classList.toggle('on'); corte.style.display = on ? 'flex' : 'none';
            if (!on) { this.corte.constant = 1e6; this.corteSlider.value = this.corteSlider.max; this.txtCorte.textContent = ''; this.sucio = true; }
        });
        const corte = document.createElement('label');
        corte.className = 'vifc-corte'; corte.title = 'Altura del corte';
        corte.innerHTML = '<input type="range" step="0.1"><span></span>';
        this.corteSlider = corte.querySelector('input'); this.txtCorte = corte.querySelector('span');
        this.corteSlider.oninput = () => { this.corte.constant = parseFloat(this.corteSlider.value); this.txtCorte.textContent = `${num(+this.corteSlider.value, 1)} m`; this.sucio = true; };
        gh.appendChild(corte);
        const gs = grupo();
        [['solido', 'Sólido'], ['material', 'Material · acero galvanizado'], ['matcap', 'Matcap · estudio'], ['alambre', 'Alambre'], ['rayosx', 'Rayos X (Alt+Z)'], ['tecnico', 'Plano técnico']]
            .forEach(([s, t]) => { const x = b(gs, s, t, () => this.ponerSombreado(s)); x.dataset.s = s; });
        const gc = grupo();
        this.bCapas = b(gc, 'capas', 'Capas y apariencia', () => { const on = this.bCapas.classList.toggle('on'); this.capasEl.style.display = on ? 'block' : 'none'; });
        this.bComparar = b(gc, 'comparar', 'Agregar otra torre para comparar', () => this.alternarMenuComparar());
        this.bLista = b(gc, 'lista', 'Lista de materiales', () => this.dotnet?.invokeMethodAsync('AlternarLista').catch(() => { }));
        this.bLista.style.display = 'none';
        this.bFull = b(gc, 'pantalla', 'Pantalla completa', () => this.pantallaCompleta());
        b(gc, 'atajos', 'F encuadrar · H ocultar · Alt+H mostrar todo · / aislar · 1 3 7 0 vistas (Ctrl = opuesta) · M medir · Alt+Z rayos X · Mayús+clic varias · Esc', () => { });

        this.menuComparar = div('vifc-panel vifc-menu');
        this._fueraMenu = e => { if (this.menuComparar.style.display === 'block' && !this.menuComparar.contains(e.target) && !this.bComparar.contains(e.target)) this.menuComparar.style.display = 'none'; };
        document.addEventListener('pointerdown', this._fueraMenu, true);

        this.capasEl = div('vifc-panel vifc-capas'); this.capasEl.style.display = 'none';
        this.torreEl = div('vifc-panel vifc-torre'); this.torreEl.style.display = 'none';
        this.ficha = div('vifc-panel vifc-pieza'); this.ficha.style.display = 'none';
        this.leyendaEl = div('vifc-leyenda');
        this.gizmoEl = div('vifc-gizmo');
        this.gizmoCv = document.createElement('canvas'); this.gizmoCv.width = this.gizmoCv.height = 232; this.gizmoEl.appendChild(this.gizmoCv);
        this.gizmoCv.addEventListener('click', e => {
            const rc = this.gizmoCv.getBoundingClientRect(), x = (e.clientX - rc.left) * 232 / rc.width, y = (e.clientY - rc.top) * 232 / rc.height;
            const hit = (this.ejesGz || []).filter(a => Math.hypot(a.x - x, a.y - y) < 22).sort((a, b) => b.z - a.z)[0];
            if (hit) this.vista(hit.vista);
        });
        this.txtMedida = div('vifc-medida');
        this.txtEstado = div('vifc-estado');
    }

    // Gizmo y barra dejan libre la ficha de la torre; el panel de capas va debajo de la barra.
    ubicarGizmo() {
        const abierta = this.torreEl.style.display !== 'none', ancho = abierta ? this.torreEl.offsetWidth + 22 : 12;
        this.gizmoEl.style.right = ancho + 'px';
        this.barra.style.maxWidth = `calc(100% - ${ancho + 20}px)`;
        this.capasEl.style.top = (this.barra.offsetTop + this.barra.offsetHeight + 8) + 'px';
        this.leyendaEl.style.left = `calc((100% - ${ancho - 12}px) / 2)`;
        this.leyendaEl.style.maxWidth = `calc(100% - ${ancho + 360}px)`;
    }

    dibujarGizmo() {
        const g = this.gizmoCv.getContext('2d'), C = 116, R = 78;
        g.clearRect(0, 0, 232, 232);
        const inv = this.camara.quaternion.clone().invert();
        // Convención de planos: Z vertical (Y de three), X transversal, Y longitudinal.
        const ejes = [
            { n: 'X', v: [1, 0, 0], c: '#e5484d', vista: 'lateral' }, { n: 'Y', v: [0, 0, -1], c: '#46a758', vista: 'atras' }, { n: 'Z', v: [0, 1, 0], c: '#3e7bfa', vista: 'planta' },
            { n: '', v: [-1, 0, 0], c: '#e5484d', vista: 'izq' }, { n: '', v: [0, 0, 1], c: '#46a758', vista: 'frente' }, { n: '', v: [0, -1, 0], c: '#3e7bfa', vista: 'abajo' }];
        this.ejesGz = ejes.map(a => { const p = new THREE.Vector3(...a.v).applyQuaternion(inv); return { ...a, x: C + p.x * R, y: C - p.y * R, z: p.z }; }).sort((a, b) => a.z - b.z);
        for (const a of this.ejesGz) {
            if (a.n) { g.strokeStyle = a.c; g.lineWidth = 4; g.beginPath(); g.moveTo(C, C); g.lineTo(a.x, a.y); g.stroke(); }
            g.beginPath(); g.arc(a.x, a.y, a.n ? 17 : 13, 0, 7);
            if (a.n) { g.fillStyle = a.c; g.fill(); g.fillStyle = '#fff'; g.font = '600 20px system-ui, sans-serif'; g.textAlign = 'center'; g.textBaseline = 'middle'; g.fillText(a.n, a.x, a.y + 1); }
            else { g.fillStyle = a.c + '33'; g.fill(); g.strokeStyle = a.c; g.lineWidth = 2.5; g.stroke(); }
        }
    }

    listaComparar() {
        this.bComparar.style.display = this.catalogo?.length ? '' : 'none';
        if (this.menuComparar.style.display === 'block') this.dibujarMenuComparar();
    }

    alternarMenuComparar() {
        if (this.menuComparar.style.display === 'block') { this.menuComparar.style.display = 'none'; return; }
        this.dibujarMenuComparar();
        const rb = this.bComparar.getBoundingClientRect(), re = this.el.getBoundingClientRect();
        this.menuComparar.style.left = (rb.left - re.left) + 'px';
        this.menuComparar.style.top = (rb.bottom - re.top + 6) + 'px';
        this.menuComparar.style.display = 'block';
    }

    dibujarMenuComparar() {
        const ya = new Set(this.modelos.map(m => m.url));
        const fams = [...new Set(this.catalogo.map(c => c.familia || ''))];
        let html = '';
        for (const f of fams) {
            if (f) html += `<h4>${esc(f)}</h4>`;
            this.catalogo.forEach((c, i) => {
                if ((c.familia || '') !== f) return;
                html += `<button type="button" class="vifc-op" data-i="${i}" ${ya.has(c.url) ? 'disabled' : ''}>${esc(c.nombre)}</button>`;
            });
        }
        this.menuComparar.innerHTML = html;
        this.menuComparar.querySelectorAll('[data-i]').forEach(d => {
            const it = this.catalogo[+d.dataset.i];
            d.onclick = async () => {
                this.menuComparar.style.display = 'none';
                this.bComparar.disabled = true;
                try { await this.agregar(it.url, it.nombre); } catch (e) { this.estado('No se pudo cargar: ' + (e?.message || e), true); }
                this.bComparar.disabled = false;
            };
        });
    }

    panelCapas() {
        const sw = (on, attr) => `<button type="button" class="vifc-sw ${on ? 'on' : ''}" ${attr}></button>`;
        const sel = (id, ops, v) => `<select class="vifc-sel" data-c="${id}">${ops.map(([k, t]) => `<option value="${k}" ${k === v ? 'selected' : ''}>${t}</option>`).join('')}</select>`;
        let html = `<h4>Apariencia</h4>
            <div class="vifc-fila">Oclusión ambiental${sw(this.opc.ao, 'data-o="ao"')}</div>
            <div class="vifc-fila">Sombras${sw(this.opc.sombras, 'data-o="sombras"')}</div>
            <div class="vifc-fila">Rejilla${sw(this.opc.rejilla, 'data-o="rejilla"')}</div>
            <div class="vifc-fila">Fondo${sel('fondo', [['estudio', 'Estudio'], ['cielo', 'Cielo'], ['oscuro', 'Oscuro']], this.opc.fondo)}</div>
            <div class="vifc-fila">Color${sel('color', [['acero', 'Acero'], ['nivel', 'Por grupo'], ['perfil', 'Por perfil'], ['estado', 'Perfil leído'], ['ifc', 'Del IFC']], this.color)}</div>
            <h4>Grupos</h4>`;
        for (const m of this.modelos) {
            const total = m.niveles.reduce((s, n) => s + n.ids.length, 0);
            if (this.modelos.length > 1)
                html += `<div class="vifc-fila vifc-mod"><b>${esc(m.nombre)}</b><span class="vifc-sub">${total}</span><button type="button" class="vifc-x" data-q="${m.mid}" title="Quitar de la comparación">${svg('cerrar')}</button></div>`;
            for (const n of m.niveles)
                html += `<div class="vifc-fila"><span>${esc(n.nombre)} <span class="vifc-sub">${n.ids.length}</span></span>${sw(this.nivelOn.get(clave(m.mid, n.id)) !== false, `data-m="${m.mid}" data-n="${n.id}"`)}</div>`;
        }
        this.capasEl.innerHTML = html;
        this.capasEl.querySelectorAll('[data-o]').forEach(x => x.onclick = () => {
            this.opc[x.dataset.o] = !this.opc[x.dataset.o]; x.classList.toggle('on', this.opc[x.dataset.o]); this.guardarPrefs(); this.pintar();
        });
        this.capasEl.querySelector('[data-c="fondo"]').onchange = e => { this.opc.fondo = e.target.value; this.guardarPrefs(); this.pintar(); };
        this.capasEl.querySelector('[data-c="color"]').onchange = e => {
            this.color = e.target.value;
            if (this.color !== 'acero' && ['alambre', 'tecnico', 'rayosx'].includes(this.sombreado)) this.sombreado = 'solido';
            this.guardarPrefs(); this.pintar();
        };
        this.capasEl.querySelectorAll('[data-n]').forEach(x => x.onclick = () => {
            const g = clave(+x.dataset.m, +x.dataset.n), on = this.nivelOn.get(g) === false;
            this.nivelOn.set(g, on); x.classList.toggle('on', on); this.aplicarVisibilidad();
        });
        this.capasEl.querySelectorAll('[data-q]').forEach(x => x.onclick = () => this.quitar(+x.dataset.q));
    }

    // Ficha de la torre: indicadores, validación plano / modelo y supuestos (Pset SigWeb_Torre del IFC).
    async panelTorre() {
        let html = '', hay = false;
        const uno = this.modelos.length === 1;
        for (const m of this.modelos) {
            if (!m.idTorre) continue;
            const psets = await this.api.properties.getPropertySets(m.mid, m.idTorre, true);
            const ps = psets.find(p => val(p.Name) === 'SigWeb_Torre');
            if (!ps) continue;
            hay = true;
            const d = {};
            (ps.HasProperties || []).forEach(h => d[val(h.Name)] = val(h.NominalValue));
            const e = this.api.GetLine(m.mid, m.idTorre);
            const total = +d.Miembros || 0, conL = +d.MiembrosConPerfil || 0, pct = total ? Math.round(conL / total * 100) : 0;
            html += `<div class="vifc-tt"><b>${esc(m.nombre || val(e.Name))}</b><span>${esc(val(e.Name))}</span></div>
                <div class="vifc-kpis">
                  <div class="vifc-kpi"><div class="k">Alto total</div><div class="v">${num(+d.AltoTotal_mm / 1000, 2)} <small>m</small></div></div>
                  <div class="vifc-kpi"><div class="k">Ancho de base</div><div class="v">${num(+d.AnchoBase_mm / 1000, 2)} <small>m</small></div></div>
                  <div class="vifc-kpi"><div class="k">Peso perfiles leídos</div><div class="v">${num(d.PesoPerfilesLeidos_kg)} <small>kg</small></div></div>
                  <div class="vifc-kpi"><div class="k">Perfil leído</div><div class="v">${num(conL)} <small>/ ${num(total)}${+d.MiembrosPerfilSupuesto ? ` · ${num(+d.MiembrosPerfilSupuesto)} supuestos` : ""}</small></div><div class="vifc-prog"><i style="width:${pct}%"></i></div></div>
                </div>`;
            if (!uno) continue;
            const vali = Object.keys(d).filter(k => k.startsWith('Validacion')).sort().map(k => String(d[k]));
            const sup = Object.keys(d).filter(k => k.startsWith('Supuesto')).sort().map(k => String(d[k]));
            if (vali.length) {
                html += `<h4>Validación plano / modelo</h4><table class="vifc-val"><tr><th>Hoja</th><th>Cota</th><th class="n">Plano</th><th class="n">Modelo</th><th class="n">Δ</th></tr>`;
                for (const v of vali) {
                    const r = /^(.*?) · (.*): plano ([-\d.,]+) mm \/ modelo ([-\d.,]+) mm$/.exec(v);
                    if (!r) { html += `<tr><td colspan="5">${esc(v)}</td></tr>`; continue; }
                    const p = parseFloat(r[3]), mo = parseFloat(r[4]), dd = mo - p, ok = Math.abs(dd) < 1;
                    html += `<tr><td>${esc(r[1])}</td><td>${esc(r[2])}</td><td class="n">${num(p)}</td><td class="n">${num(mo)}</td><td class="n ${ok ? 'ok' : 'av'}">${ok ? '✓ 0' : num(dd)}</td></tr>`;
                }
                html += '</table>';
            } else html += `<div class="vifc-falta">Sin validación en el IFC</div>`;
            if (sup.length) html += `<details class="vifc-sup-d"><summary>Supuestos <span>${sup.length}</span></summary><ol class="vifc-sup">${sup.map(s => `<li>${esc(s)}</li>`).join('')}</ol></details>`;
            else html += `<div class="vifc-falta">Sin lista de supuestos en el IFC</div>`;
            if (d.Fuente) html += `<div class="vifc-fuente">${esc(d.Fuente)}</div>`;
        }
        if (!hay) html = `<div class="vifc-falta">El IFC no trae el Pset SigWeb_Torre (validación y supuestos)</div>`;
        this.torreEl.innerHTML = `<button type="button" class="vifc-x vifc-x-abs" data-x title="Cerrar">${svg('cerrar')}</button>` + html;
        this.torreEl.querySelector('[data-x]').onclick = () => this.alternarTorre(false);
        if (this.torreInicial === undefined) { this.torreInicial = true; if (this.el.clientWidth >= 900) this.alternarTorre(true, false); }
    }

    alternarTorre(abrir, encuadrar = true) {
        const on = typeof abrir === 'boolean' ? abrir : this.torreEl.style.display === 'none';
        this.torreEl.style.display = on ? 'block' : 'none';
        this.ubicarGizmo();
        if (encuadrar) this.vista(this.modelos.length > 1 ? 'frente' : 'iso');
        else this.vista(this.modelos.length > 1 ? 'frente' : 'iso', false);
    }

    dibujarLeyenda() {
        if (!this.leyenda.length) { this.leyendaEl.style.display = 'none'; return; }
        this.leyendaEl.style.display = 'flex';
        this.leyendaEl.innerHTML = this.leyenda.map(([c, t]) => `<span><i style="background:${c}"></i>${esc(t)}</span>`).join('');
    }

    dispose() {
        this.navegacion.liberar(); this.controles.dispose();
        cancelAnimationFrame(this.raf);
        this.ro.disconnect();
        window.removeEventListener('keydown', this._tecla);
        document.removeEventListener('fullscreenchange', this._fs);
        if (document.fullscreenElement === this.marco) document.exitFullscreen();
        document.removeEventListener('pointerdown', this._fueraMenu, true);
        this.scene.traverse(o => { if (o.geometry) o.geometry.dispose(); if (o.material) { o.material.map?.dispose(); o.material.dispose(); } });
        this.scene.background?.dispose?.(); this.env.dispose(); this.texMatcap.dispose();
        this.composer.dispose?.(); this.ao.dispose?.(); this.outline.dispose?.();
        this.renderer.dispose();
        for (const m of this.modelos) { try { this.api.CloseModel(m.mid); } catch { } }
        this.el.classList.remove('vifc');
        this.el.replaceChildren();
    }
}

// Rótulo con el nombre de la torre (sprite que siempre mira a la cámara).
function etiqueta(texto) {
    const c = document.createElement('canvas'), ctx = c.getContext('2d');
    const f = 48; ctx.font = `600 ${f}px sans-serif`;
    const w = Math.ceil(ctx.measureText(texto).width) + 40;
    c.width = w; c.height = f + 28;
    ctx.font = `600 ${f}px sans-serif`;
    ctx.fillStyle = 'rgba(31,35,43,.88)'; ctx.fillRect(0, 0, c.width, c.height);
    ctx.fillStyle = '#ffffff'; ctx.textBaseline = 'middle'; ctx.fillText(texto, 20, c.height / 2);
    const t = new THREE.CanvasTexture(c); t.colorSpace = THREE.SRGBColorSpace;
    const s = new THREE.Sprite(new THREE.SpriteMaterial({ map: t, depthTest: false }));
    s.scale.set(w / c.height * 2, 2, 1);
    s.renderOrder = 20;
    return s;
}

function fila(k, v, html) {
    if (v === undefined || v === null || v === '') return '';
    return `<dt>${esc(k)}</dt><dd>${html ? v : esc(v)}</dd>`;
}

function ordenPerfil(a, b) {
    const n = s => (String(s).match(/\d+/g) || []).map(Number);
    const x = n(a), y = n(b);
    return (x[0] - y[0]) || (x[1] - y[1]) || String(a).localeCompare(String(b));
}

function unir(geos) {
    let nv = 0, ni = 0;
    geos.forEach(g => { nv += g.attributes.position.count; ni += g.index.count; });
    const pos = new Float32Array(nv * 3), nor = new Float32Array(nv * 3), ix = new Uint32Array(ni);
    let ov = 0, oi = 0;
    for (const g of geos) {
        pos.set(g.attributes.position.array, ov * 3); nor.set(g.attributes.normal.array, ov * 3);
        const gi = g.index.array; for (let k = 0; k < gi.length; k++) ix[oi + k] = gi[k] + ov;
        ov += g.attributes.position.count; oi += gi.length; g.dispose();
    }
    const r = new THREE.BufferGeometry();
    r.setAttribute('position', new THREE.BufferAttribute(pos, 3));
    r.setAttribute('normal', new THREE.BufferAttribute(nor, 3));
    r.setIndex(new THREE.BufferAttribute(ix, 1));
    return r;
}
