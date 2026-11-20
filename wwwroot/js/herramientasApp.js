
const mapas = new Map();

const observarSalidas = new MutationObserver(() => {
    for (const [elemento, limpiar] of mapas) {
        if (!elemento.isConnected) {
            limpiar();
            mapas.delete(elemento);
        }
    }
    if (mapas.size === 0) observarSalidas.disconnect();
});

export async function mostrarMapa(elemento, puntos) {
    const L = await window.sigLibs.leaflet();
    if (!elemento?.isConnected || !puntos.length) return;
    mapas.get(elemento)?.();

    const { calle, satelite, hibrido, terreno } = window.crearCapasBase();
    const mapa = L.map(elemento, { maxZoom: 22, layers: [calle], preferCanvas: true, scrollWheelZoom: false });
    L.control.layers({ Calle: calle, 'Satélite': satelite, 'Híbrido': hibrido, Terreno: terreno }).addTo(mapa);
    const color = getComputedStyle(elemento).getPropertyValue('--t-acento').trim() || '#a9272c';
    const posiciones = [];
    for (const punto of puntos) {
        const posicion = [punto.lat, punto.lon];
        posiciones.push(posicion);
        const etiqueta = document.createElement('span');
        etiqueta.textContent = punto.etiqueta;
        L.circleMarker(posicion, { radius: 5, color, fillOpacity: .75 }).bindPopup(etiqueta).addTo(mapa);
    }

    let ajustado = false;
    const ajustar = () => {
        if (!elemento.clientWidth || !elemento.clientHeight) return;
        mapa.invalidateSize({ pan: false });
        if (ajustado) return;
        ajustado = true;
        if (posiciones.length === 1) mapa.setView(posiciones[0], 16);
        else mapa.fitBounds(posiciones, { padding: [18, 18], maxZoom: 16 });
    };
    const tamano = new ResizeObserver(ajustar);
    tamano.observe(elemento);
    mapas.set(elemento, () => {
        tamano.disconnect();
        mapa.remove();
    });
    observarSalidas.observe(document.body, { childList: true, subtree: true });
    ajustar();
}
