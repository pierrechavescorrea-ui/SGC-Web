# Librerías web vendorizadas (`wwwroot/lib`)

Inventario de las librerías del navegador con su versión, origen y huella SHA-256 (auditoría 2026-11-22, S23).
La comprobación automática está en `docs/librerias-web.sha256` y la ejecuta el CI (`sha256sum -c docs/librerias-web.sha256`).
Al actualizar una librería: reemplazar sus archivos, regenerar las huellas con el comando del final y actualizar esta tabla.

| Librería | Archivo principal | Versión | Origen | Notas |
| --- | --- | --- | --- | --- |
| Bootstrap | `bootstrap/dist/` | 5.3.3 | [npm bootstrap](https://www.npmjs.com/package/bootstrap) | |
| CesiumJS | `cesium/` | 1.147.0 | [npm cesium](https://www.npmjs.com/package/cesium), carpeta `Build/Cesium` | Trae DOMPurify 3.4.17. **Parcheado**: el Knockout embebido usa `(0,eval)("this")`, reemplazado por `globalThis` (la CSP no permite `eval`). Por eso su huella no coincide con la del paquete. Repetir el parche al actualizar. Sin `index.js`/`index.cjs` del paquete. |
| Chart.js | `chartjs/chart.umd.js` | 4.6.1 | [npm chart.js](https://www.npmjs.com/package/chart.js) | Se carga bajo demanda (`js/librerias.js`). |
| Leaflet | `leaflet/` | 1.9.4 | [npm leaflet](https://www.npmjs.com/package/leaflet) | Bajo demanda. |
| PDF.js | `pdfjs/` | 4.10.38 | [npm pdfjs-dist](https://www.npmjs.com/package/pdfjs-dist) | `pdf.min.mjs` + `pdf.worker.min.mjs`. |
| three.js | `three/` | r170 (0.170.0) | [npm three](https://www.npmjs.com/package/three) | Addons de `examples/jsm` en `three/addons` con imports relativos. |
| Univer | `univer/` | No declarada en el archivo | Paquete armado a mano | Pendiente: reconstruir desde npm con versión fija para poder cotejar avisos. |
| web-ifc | `web-ifc/` | No declarada en el archivo | [npm web-ifc](https://www.npmjs.com/package/web-ifc) | MPL 2.0, sin modificar. Pendiente: fijar versión al próximo reemplazo. |

Regenerar las huellas (desde la raíz del repositorio):

```bash
sha256sum $(cut -d' ' -f3 docs/librerias-web.sha256) > docs/librerias-web.sha256
```
