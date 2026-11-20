// Perfil de las piezas de una torre (JSON de wwwroot/modelos3d) y perfil SUPUESTO de las que no tienen marca
// vinculada (o cuya marca no da un ángulo L en la lista de materiales). Sin dependencias: lo usan el visor
// (perfilesL.js) y tools/planos-dwg/esqueleto/perfiles-supuestos.mjs, que lo deja escrito en el JSON para los IFC.
// Regla del perfil supuesto, en orden:
//   1) la pieza con perfil leído más parecida de la misma cara, grupo y función (tipo: montante, diagonal, horizontal…);
//   2) la más parecida del mismo grupo y función;  3) la más parecida de la misma función en toda la torre;
//   4) el ángulo L más chico de la hoja de la pieza;  5) el ángulo L más chico de la lista.
// "Más parecida" = menor distancia entre puntos medios + 2 × diferencia de longitud (mm).

export const RE_L = /^\s*L\s*(\d+(?:[.,]\d+)?)\s*[Xx]\s*(\d+(?:[.,]\d+)?)(?:\s*[Xx]\s*(\d+(?:[.,]\d+)?))?/;

export function parsePerfil(txt) {
    const m = RE_L.exec(txt || '');
    if (!m) return null;
    const f = s => parseFloat(s.replace(',', '.'));
    return m[3] ? { a: f(m[1]), b: f(m[2]), t: f(m[3]) } : { a: f(m[1]), b: f(m[1]), t: f(m[2]) };
}

const area = p => (p.a + p.b - p.t) * p.t;

export function listaPorMarca(data) {
    const lista = new Map();
    for (const l of data.lista_materiales || [])
        if ((l.clase || 'PERFIL') === 'PERFIL' && l.marca && !lista.has(String(l.marca).trim().toUpperCase()))
            lista.set(String(l.marca).trim().toUpperCase(), l);
    return lista;
}

// Fila de la lista de materiales con ángulo L para la marca del miembro (o null).
export function filaLeida(m, lista) {
    const marcas = String(m.marca || '').split(/[\/,]/).map(s => s.trim().toUpperCase()).filter(Boolean);
    return marcas.map(k => lista.get(k)).find(f => f && parsePerfil(f.perfil)) || null;
}

const hojaDe = m => {
    const h = m.plano?.hoja ?? (/^H(\d+)/i.exec(m.tramo || '') || [])[1];
    return h == null || h === '' ? null : Number(h);
};
const medio = m => [(m.a[0] + m.b[0]) / 2, (m.a[1] + m.b[1]) / 2, (m.a[2] + m.b[2]) / 2];
const largo = m => Math.hypot(m.b[0] - m.a[0], m.b[1] - m.a[1], m.b[2] - m.a[2]);

// Map miembro → { perfil, origen } para las piezas sin perfil leído. Respeta perfil_supuesto ya escrito en el JSON.
export function perfilesSupuestos(data) {
    const lista = listaPorMarca(data);
    const leidas = [], faltan = [];
    for (const m of data.miembros || []) {
        const f = filaLeida(m, lista);
        if (f) leidas.push({ m, perfil: f.perfil, marca: f.marca, c: medio(m), L: largo(m) });
        else faltan.push(m);
    }
    const Ls = [...lista.values()].filter(l => parsePerfil(l.perfil));
    const menor = filas => filas.reduce((x, l) => !x || area(parsePerfil(l.perfil)) < area(parsePerfil(x.perfil)) ? l : x, null);
    const menorTodo = menor(Ls);
    const out = new Map();
    for (const m of faltan) {
        if (m.perfil_supuesto && parsePerfil(m.perfil_supuesto)) { out.set(m, { perfil: m.perfil_supuesto, origen: m.perfil_supuesto_origen || 'Supuesto' }); continue; }
        const c = medio(m), L = largo(m);
        const niveles = [
            [x => x.m.grupo === m.grupo && x.m.cara === m.cara && x.m.tipo === m.tipo, 'misma cara, grupo y función'],
            [x => x.m.grupo === m.grupo && x.m.tipo === m.tipo, 'mismo grupo y función'],
            [x => x.m.tipo === m.tipo, 'misma función'],
        ];
        let r = null;
        for (const [ok, txt] of niveles) {
            let mejor = null, sc = Infinity;
            for (const x of leidas) {
                if (!ok(x)) continue;
                const s = Math.hypot(x.c[0] - c[0], x.c[1] - c[1], x.c[2] - c[2]) + 2 * Math.abs(x.L - L);
                if (s < sc) { sc = s; mejor = x; }
            }
            if (mejor) { r = { perfil: mejor.perfil, origen: `Supuesto: perfil de la pieza ${mejor.marca} (${txt})` }; break; }
        }
        if (!r) {
            const h = hojaDe(m), deHoja = h == null ? null : menor(Ls.filter(l => Number(l.hoja) === h));
            if (deHoja) r = { perfil: deHoja.perfil, origen: `Supuesto: ángulo L más chico de la hoja H${h}` };
            else if (menorTodo) r = { perfil: menorTodo.perfil, origen: 'Supuesto: ángulo L más chico de la lista de materiales' };
        }
        if (r) out.set(m, r);
    }
    return out;
}
