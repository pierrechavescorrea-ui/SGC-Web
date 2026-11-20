// Librerías pesadas bajo demanda: Chart.js y Leaflet ya no se cargan en cada página (antes iban en <head> de App.razor
// y bloqueaban el primer pintado). La primera pantalla que dibuja un gráfico o un mapa las pide; las siguientes
// reutilizan la misma promesa. Todo local (wwwroot/lib), sin CDN, compatible con la política de seguridad (CSP).
//   sigLibs.chart()   → Promise<Chart>
//   sigLibs.leaflet() → Promise<L>   (también inserta lib/leaflet/leaflet.css)
//   sigLibs.envolver(obj, nombre, cargar) → la función obj[nombre] espera a la librería antes de ejecutarse.
(function () {
    if (window.sigLibs) return;
    const pendientes = {};
    function script(src) {
        return new Promise((ok, mal) => {
            const s = document.createElement('script');
            s.src = src;
            s.onload = () => ok();
            s.onerror = () => { s.remove(); mal(new Error('No se pudo cargar ' + src)); };
            document.head.appendChild(s);
        });
    }
    function css(href) {
        if (document.querySelector('link[data-sig-lib="' + href + '"]')) return Promise.resolve();
        return new Promise(ok => {
            const l = document.createElement('link');
            l.rel = 'stylesheet'; l.href = href; l.dataset.sigLib = href;
            l.onload = l.onerror = () => ok();
            document.head.appendChild(l);
        });
    }
    // Una sola carga por librería; si falla (sin red, archivo ausente) se puede reintentar en la próxima llamada.
    function una(clave, f) {
        return pendientes[clave] || (pendientes[clave] = f().catch(e => { delete pendientes[clave]; console.error(e); throw e; }));
    }
    const sigLibs = {
        chart() {
            if (window.Chart) return Promise.resolve(window.Chart);
            return una('chart', () => script('lib/chartjs/chart.umd.js').then(() => {
                // Colores del tema para los gráficos (js/temas.js) ahora que Chart existe.
                if (window.sigTema && window.sigTema.reaplicar) window.sigTema.reaplicar();
                return window.Chart;
            }));
        },
        leaflet() {
            if (window.L && window.L.map) return css('lib/leaflet/leaflet.css').then(() => window.L);
            return una('leaflet', () => Promise.all([css('lib/leaflet/leaflet.css'), script('lib/leaflet/leaflet.js')]).then(() => window.L));
        },
        // Envuelve obj[nombre]: si la librería ya está, llama directo (síncrono, como antes); si no, la carga y luego
        // llama. Blazor espera la promesa devuelta, así que las llamadas siguientes (etiquetas, tamaño) van después.
        envolver(obj, nombre, cargar, yaEsta) {
            const f = obj && obj[nombre];
            if (typeof f !== 'function' || f.__sigLib) return;
            const g = function (...a) {
                if (yaEsta()) return f.apply(this, a);
                return cargar().then(() => f.apply(this, a));
            };
            g.__sigLib = true;
            obj[nombre] = g;
        }
    };
    sigLibs.hayChart = () => !!window.Chart;
    sigLibs.hayLeaflet = () => !!(window.L && window.L.map) && !!document.querySelector('link[data-sig-lib="lib/leaflet/leaflet.css"]');
    window.sigLibs = sigLibs;
})();
