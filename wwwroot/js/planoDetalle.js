// La hoja de procedencia prevalece sobre la hoja de montaje de la pieza.
export function referenciaPlano(p, meta) {
  if (p.sin_plano || p.marca === '(sin marca)') return null;
  const origen = p.origen_listado;
  const hoja = origen?.atributo?.hoja ?? origen?.lista?.hoja ?? p.plano?.hoja ?? p.hoja;
  const m = /^(?:H)?(\d+)$/i.exec(String(hoja ?? '').trim());
  const codigo = p.plano?.codigo ?? meta.plano?.codigo;
  if (!codigo || !m || Number(m[1]) < 1) return null;
  return { codigo, hoja: Number(m[1]), titulo: null, clave: `${codigo}|${Number(m[1])}` };
}

const esc = s => String(s ?? '').replace(/[&<>"]/g, c => ({ '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;' }[c]));
export function fichaPlano(ref, enlace, estado = 'cargando') {
  if (!ref) return 'Sin plano identificado';
  const nombre = `${ref.codigo}-H${ref.hoja}`;
  // Los adjuntos se sirven como rutas de la app; nunca insertar esquemas ejecutables.
  const url = enlace?.url;
  const segura = typeof url === 'string' && url.trim() === url && url.length > 0
    && !/[\u0000-\u0020\\]/.test(url) && !/^[a-z][a-z\d+.-]*:/i.test(url) && !url.startsWith('//');
  return `${esc(nombre)}${segura
    ? `<br><a href="${esc(url)}" target="_blank" rel="noopener noreferrer" title="${esc(enlace.texto || nombre)}">Abrir plano ↗</a>`
    : `<small> · ${estado === 'error' ? 'No se pudo consultar el plano' : estado === 'cargando' ? 'Buscando plano…' : 'Plano no cargado'}</small>`}`;
}
