// Gráficos del análisis estadístico de Ensayos (Components/Estadistica/AnalisisEstadistico).
// Histograma con curva normal y líneas verticales (media, mediana, moda, ±kσ, LIE/LSE, banda del IC), campana de Gauss
// con el área de la probabilidad sombreada y gráfico Q-Q normal. Chart.js 4 (lib/chartjs). Todo el cálculo viene de C#;
// aquí solo se dibuja la curva normal punto a punto con la fórmula de la densidad.
(function () {
    const graficos = {};
    const columnas = 12, fila = 46, separacion = 10;
    // Instances belong to a rendered component, not to a route: release listeners on removal.
    const paneles = new Map();
    let desconectados;
    function soltarGraficos(ids) {
        for (const id of ids || []) {
            if (graficos[id]) { graficos[id].destroy(); delete graficos[id]; }
        }
    }
    function clavePanel() {
        const tipo = location.pathname.match(/\/ensayos\/(\d+)(?:\/|$)/)?.[1] || 'global';
        return 'sigweb.estadistica.' + tipo;
    }

    function leerOcultos() {
        try {
            const ids = JSON.parse(localStorage.getItem(clavePanel() + '.ocultos'));
            return Array.isArray(ids) ? ids.filter(id => typeof id === 'string') : [];
        } catch { return []; }
    }

    function guardarOcultos(ids) {
        try { localStorage.setItem(clavePanel() + '.ocultos', JSON.stringify(ids)); } catch { }
    }

    function panelSoltar(raiz) {
        const p = paneles.get(raiz);
        if (!p) return;
        p.api.destruir();
        soltarGraficos(p.canvases.map(c => c.id));
        p.resize.disconnect();
        p.movil.removeEventListener('change', p.adaptar);
        p.eventos.abort();
        cancelAnimationFrame(p.frame);
        paneles.delete(raiz);
        if (!paneles.size) { desconectados?.disconnect(); desconectados = null; }
    }

    function arrastrePaleta(raiz, p, dotnet) {
        const opciones = { signal: p.eventos.signal };
        raiz.addEventListener('dragstart', e => {
            const boton = e.target.closest('[data-est-agregar]');
            if (!boton || boton.disabled || !p.editar || p.movil.matches || !e.dataTransfer) return;
            p.arrastrado = boton.dataset.estAgregar;
            e.dataTransfer.setData('text/plain', p.arrastrado);
            e.dataTransfer.effectAllowed = 'copy';
        }, opciones);
        raiz.addEventListener('dragend', () => { p.arrastrado = null; }, opciones);
        p.el.addEventListener('dragover', e => {
            if (!p.arrastrado || !p.editar || p.movil.matches) return;
            e.preventDefault();
            e.dataTransfer.dropEffect = 'copy';
        }, opciones);
        p.el.addEventListener('drop', e => {
            if (!p.arrastrado || !p.editar || p.movil.matches) return;
            e.preventDefault();
            const caja = p.el.getBoundingClientRect();
            const x = Math.floor((e.clientX - caja.left) / ((p.el.clientWidth + separacion) / columnas));
            const y = Math.max(0, Math.floor((e.clientY - caja.top) / (fila + separacion)));
            const posicion = { id: p.arrastrado, x, y };
            p.pendiente = posicion;
            p.arrastrado = null;
            dotnet.invokeMethodAsync('SoltarWidget', posicion.id).catch(() => {
                if (p.pendiente === posicion) p.pendiente = null;
            });
        }, opciones);
    }

    function ubicarAgregado(p) {
        const posicion = p.pendiente;
        if (!posicion) return;
        const widget = p.el.querySelector(':scope > .pw-w[data-w="' + CSS.escape(posicion.id) + '"]');
        if (!widget) return;
        p.pendiente = null;
        let layout;
        try { layout = JSON.parse(p.api.leer()); } catch { }
        if (!Array.isArray(layout)) return;
        const w = Number(widget.dataset.ancho), h = Number(widget.dataset.alto);
        layout.push({ id: posicion.id, x: Math.max(0, Math.min(columnas - w, posicion.x)), y: posicion.y, w, h });
        p.api.cargar(JSON.stringify(layout));
    }

    function panelAplicar(raiz, editar, restaurar, dotnet) {
        if (!raiz || !window.sigPanel) return;
        const el = raiz.querySelector('.est-panel');
        let p = paneles.get(raiz);
        if (p && p.el !== el) { panelSoltar(raiz); p = null; }
        if (!el) return;
        if (!p) {
            p = { el, movil: matchMedia('(max-width: 900px)'), frame: 0, canvases: [], eventos: new AbortController() };
            p.api = sigPanel.crear(clavePanel(), { posicion: true, angosta: 900, fila, gap: separacion });
            p.adaptar = () => {
                const edicion = p.editar && !p.movil.matches;
                el.classList.toggle('pw-editando', edicion);
                p.api.aplicar(el, edicion);
            };
            p.resize = new ResizeObserver(() => {
                cancelAnimationFrame(p.frame);
                p.frame = requestAnimationFrame(() => p.canvases.forEach(c => window.Chart?.getChart(c)?.resize()));
            });
            arrastrePaleta(raiz, p, dotnet);
            p.movil.addEventListener('change', p.adaptar);
            paneles.set(raiz, p);
            if (!desconectados) {
                desconectados = new MutationObserver(cambios => {
                    if (cambios.some(c => c.type === 'attributes')) {
                        Object.values(graficos).forEach(c => { coloresGrafico(c.config, c.canvas); c.update('none'); });
                    }
                    for (const r of paneles.keys()) if (!r.isConnected) panelSoltar(r);
                });
                desconectados.observe(document.documentElement, { childList: true, subtree: true, attributes: true, attributeFilter: ['data-tema'] });
            }
        }
        // Blazor puede reemplazar un canvas al quitar un widget o al filtrar el campo.
        const canvases = Array.from(el.querySelectorAll('canvas'));
        soltarGraficos(p.canvases.filter(c => !canvases.includes(c)).map(c => c.id));
        p.canvases = canvases;
        p.resize.disconnect();
        el.querySelectorAll(':scope > .pw-w').forEach(w => p.resize.observe(w));
        if (restaurar) {
            p.pendiente = null;
            p.api.cargar('[]');
        }
        ubicarAgregado(p);
        p.editar = editar;
        p.adaptar();
    }
    const pdf = (x, m, s) => Math.exp(-0.5 * ((x - m) / s) ** 2) / (s * Math.sqrt(2 * Math.PI));
    const fmt = (v) => { if (typeof v !== 'number' || !isFinite(v)) return '—'; const a = Math.abs(v); return v.toLocaleString(undefined, { maximumFractionDigits: a >= 1000 ? 1 : a >= 100 ? 2 : a >= 1 ? 3 : 4 }); };

    function tema(el) {
        if (typeof sigwebColoresTema === 'function') sigwebColoresTema(el);
        const cs = getComputedStyle(el);
        return { texto: cs.color || '#222', fondo: cs.backgroundColor };
    }

    function coloresGrafico(config, el) {
        const cs = getComputedStyle(el.closest('.est-card') || el);
        const texto = cs.getPropertyValue('--t-texto').trim() || '#1f2a3a';
        const secundario = cs.getPropertyValue('--t-texto-2').trim() || '#6b7686';
        const borde = cs.getPropertyValue('--t-borde').trim() || '#dfe3ea';
        const op = config.options;
        for (const eje of Object.values(op.scales || {})) {
            eje.ticks = { ...eje.ticks, color: secundario };
            eje.title = { ...eje.title, color: secundario };
            eje.grid = { ...eje.grid, color: borde };
            eje.border = { ...eje.border, color: borde };
        }
        op.plugins ||= {};
        op.plugins.legend ||= {};
        op.plugins.legend.labels = { ...op.plugins.legend.labels, color: texto };
        for (const d of config.data.datasets) if (d.estNormal) d.borderColor = texto;
    }

    function dibujar(id, config) {
        const el = document.getElementById(id);
        if (!el || !window.Chart) return null;
        if (graficos[id]) { graficos[id].destroy(); delete graficos[id]; }
        const previo = Chart.getChart(el);
        if (previo) previo.destroy();
        tema(el);
        coloresGrafico(config, el);
        const opciones = config.options;
        const conLeyenda = opciones.plugins.legend.display !== false;
        const conTitulo = !!opciones.plugins.title?.display;
        const titulosEjes = Object.keys(opciones.scales || {}).filter(id => opciones.scales[id].title?.display);
        opciones.onResize = (grafico, { width, height }) => {
            const compacto = width < 230 || height < 155;
            const { plugins, scales } = grafico.options;
            plugins.legend.display = conLeyenda && !compacto;
            if (conTitulo) plugins.title.display = !compacto;
            for (const eje of titulosEjes) scales[eje].title.display = !compacto;
        };
        graficos[id] = new Chart(el, config);
        return graficos[id];
    }

    // Líneas verticales y bandas: [{x, color, texto, guion, ancho}], bandas: [{desde, hasta, color, texto}].
    const pluginLineas = {
        id: 'estLineas',
        beforeDatasetsDraw(chart, _a, op) {
            const { ctx, chartArea: ca, scales: { x } } = chart;
            (op.bandas || []).forEach(b => {
                const x1 = Math.max(ca.left, x.getPixelForValue(b.desde)), x2 = Math.min(ca.right, x.getPixelForValue(b.hasta));
                if (!(x2 > x1)) return;
                ctx.save(); ctx.fillStyle = b.color; ctx.fillRect(x1, ca.top, x2 - x1, ca.bottom - ca.top); ctx.restore();
            });
        },
        afterDatasetsDraw(chart, _a, op) {
            const { ctx, chartArea: ca, scales: { x } } = chart;
            const ocupados = [];
            (op.lineas || []).forEach(l => {
                const px = x.getPixelForValue(l.x);
                if (!isFinite(px) || px < ca.left - 1 || px > ca.right + 1) return;
                ctx.save();
                ctx.strokeStyle = l.color; ctx.lineWidth = l.ancho || 2; ctx.setLineDash(l.guion || []);
                ctx.beginPath(); ctx.moveTo(px, ca.top); ctx.lineTo(px, ca.bottom); ctx.stroke();
                if (l.texto && chart.width >= 230 && chart.height >= 155) {
                    ctx.setLineDash([]);
                    ctx.font = '600 11px ' + (Chart.defaults.font.family || 'sans-serif');
                    const w = ctx.measureText(l.texto).width + 8;
                    // Las etiquetas se escalonan si chocan.
                    let fila = 0;
                    while (ocupados.some(o => o.fila === fila && Math.abs(o.px - px) < (o.w + w) / 2)) fila++;
                    const y = ca.top + 4 + fila * 16;
                    if (w > ca.width || y + 15 > ca.bottom) { ctx.restore(); return; }
                    ocupados.push({ px, w, fila });
                    const xx = Math.min(Math.max(px - w / 2, ca.left), ca.right - w);
                    ctx.fillStyle = l.color; ctx.globalAlpha = .92;
                    ctx.beginPath(); ctx.roundRect ? ctx.roundRect(xx, y, w, 15, 4) : ctx.rect(xx, y, w, 15); ctx.fill();
                    ctx.globalAlpha = 1; ctx.fillStyle = '#fff'; ctx.textBaseline = 'middle';
                    ctx.fillText(l.texto, xx + 4, y + 8);
                }
                ctx.restore();
            });
        }
    };

    function escalaY(modo) {
        return modo === 'pct' ? 'Frecuencia relativa (%)' : modo === 'densidad' ? 'Densidad' : 'Frecuencia';
    }

    window.sigEstadistica = {
        // d: { campo, clases:[{d,h,f}], n, media, desv, mediana, moda, lie, lse, icInf, icSup, op:{...} }
        histograma(id, d, dotnet) {
            const op = d.op || {};
            const ancho = d.clases.length ? (d.clases[0].h - d.clases[0].d) || 1 : 1;
            const factor = op.ejeY === 'pct' ? 100 / d.n : op.ejeY === 'densidad' ? 1 / (d.n * ancho) : 1;
            const barras = d.clases.map(c => ({ x: (c.d + c.h) / 2, y: c.f * factor, d: c.d, h: c.h, f: c.f }));
            const datasets = [{
                type: 'bar', label: 'Frecuencia observada', data: barras, parsing: false,
                backgroundColor: (op.color || '#5b7fa6') + 'cc', borderColor: op.color || '#5b7fa6', borderWidth: 1,
                barPercentage: 1, categoryPercentage: 1, grouped: false, order: 2,
            }];
            const lo0 = d.clases.length ? d.clases[0].d : d.media - 1, hi0 = d.clases.length ? d.clases[d.clases.length - 1].h : d.media + 1;
            let lo = lo0, hi = hi0;
            if (op.curva && d.desv > 0) {
                lo = Math.min(lo0, d.media - 3.5 * d.desv); hi = Math.max(hi0, d.media + 3.5 * d.desv);
                const pts = [];
                const fCurva = op.ejeY === 'pct' ? 100 * ancho : op.ejeY === 'densidad' ? 1 : d.n * ancho;
                for (let i = 0; i <= 160; i++) { const x = lo + (hi - lo) * i / 160; pts.push({ x, y: pdf(x, d.media, d.desv) * fCurva }); }
                datasets.push({ type: 'line', label: 'Distribución normal (μ = x̄, σ = s)', data: pts, parsing: false, borderColor: op.colorCurva || '#1f2937', estNormal: !op.colorCurva,
                    borderWidth: 2.2, pointRadius: 0, tension: .3, fill: false, order: 1 });
            }
            if (op.espec) { if (d.lie != null) lo = Math.min(lo, d.lie); if (d.lse != null) hi = Math.max(hi, d.lse); }
            const pad = (hi - lo) * .03;
            const lineas = [], bandas = [];
            if (op.ic && d.icInf != null) bandas.push({ desde: d.icInf, hasta: d.icSup, color: 'rgba(236,28,36,.12)' });
            if (op.sigmas > 0 && d.desv > 0)
                for (let k = 1; k <= op.sigmas; k++) {
                    lineas.push({ x: d.media - k * d.desv, color: '#8a8f98', guion: [4, 4], ancho: 1.2, texto: `−${k}σ` });
                    lineas.push({ x: d.media + k * d.desv, color: '#8a8f98', guion: [4, 4], ancho: 1.2, texto: `+${k}σ` });
                }
            if (op.espec) {
                if (d.lie != null) lineas.push({ x: d.lie, color: '#d97706', guion: [8, 4], texto: 'LIE ' + fmt(d.lie) });
                if (d.lse != null) lineas.push({ x: d.lse, color: '#d97706', guion: [8, 4], texto: 'LSE ' + fmt(d.lse) });
            }
            if (op.moda && d.moda != null) lineas.push({ x: d.moda, color: '#15803d', texto: 'Moda ' + fmt(d.moda) });
            if (op.mediana) lineas.push({ x: d.mediana, color: '#2563eb', texto: 'Mediana ' + fmt(d.mediana) });
            if (op.media) lineas.push({ x: d.media, color: '#EC1C24', texto: 'Media ' + fmt(d.media) });

            dibujar(id, {
                data: { datasets },
                plugins: [pluginLineas],
                options: {
                    responsive: true, maintainAspectRatio: false, animation: { duration: 250 },
                    layout: { padding: { top: 6 } },
                    interaction: { mode: 'nearest', intersect: true },
                    scales: {
                        x: { type: 'linear', min: lo - pad, max: hi + pad, offset: false, grid: { display: op.cuadricula !== false },
                            title: { display: true, text: d.campo } },
                        y: { beginAtZero: true, grid: { display: op.cuadricula !== false }, title: { display: true, text: escalaY(op.ejeY) } },
                    },
                    plugins: {
                        estLineas: { lineas, bandas },
                        legend: { display: op.leyenda !== false, position: 'bottom' },
                        title: { display: !!op.titulo, text: op.titulo || '' },
                        tooltip: {
                            callbacks: {
                                title: (it) => {
                                    const r = it[0].raw;
                                    return r.d != null ? `${fmt(r.d)} – ${fmt(r.h)}` : `x = ${fmt(r.x)}`;
                                },
                                label: (it) => {
                                    const r = it.raw;
                                    if (r.f != null) return [`${r.f} dato(s)`, `${(100 * r.f / d.n).toFixed(1)} % del total`];
                                    return `Normal: ${fmt(r.y)}`;
                                },
                            }
                        },
                    },
                    onClick: (_e, els) => {
                        const b = els.find(e => e.datasetIndex === 0);
                        if (b && dotnet) { const r = barras[b.index]; dotnet.invokeMethodAsync('ElegirRango', r.d, r.h); }
                    },
                    onHover: (e, els) => { e.native.target.style.cursor = els.some(x => x.datasetIndex === 0) ? 'pointer' : 'default'; },
                }
            });
        },

        // d: { media, desv, a, b, campo, pNormal, observados:[x] }  a/b null = sin límite.
        campana(id, d) {
            if (!(d.desv > 0)) { dibujar(id, { type: 'line', data: { datasets: [] } }); return; }
            const lo = d.media - 4 * d.desv, hi = d.media + 4 * d.desv;
            const pts = [], area = [];
            const a = d.a == null ? -Infinity : d.a, b = d.b == null ? Infinity : d.b;
            for (let i = 0; i <= 200; i++) {
                const x = lo + (hi - lo) * i / 200, y = pdf(x, d.media, d.desv);
                pts.push({ x, y });
                area.push({ x, y: x >= a && x <= b ? y : null });
            }
            const lineas = [{ x: d.media, color: '#EC1C24', texto: 'μ' }];
            for (let k = 1; k <= 3; k++) {
                lineas.push({ x: d.media - k * d.desv, color: '#8a8f98', guion: [3, 4], ancho: 1, texto: `−${k}σ` });
                lineas.push({ x: d.media + k * d.desv, color: '#8a8f98', guion: [3, 4], ancho: 1, texto: `+${k}σ` });
            }
            if (d.a != null) lineas.push({ x: d.a, color: '#7c3aed', texto: 'a = ' + fmt(d.a) });
            if (d.b != null) lineas.push({ x: d.b, color: '#7c3aed', texto: 'b = ' + fmt(d.b) });
            dibujar(id, {
                type: 'line',
                data: { datasets: [
                    { label: `P = ${(d.pNormal * 100).toFixed(2)} %`, data: area, parsing: false, borderWidth: 0, pointRadius: 0, fill: 'origin',
                      backgroundColor: 'rgba(124,58,237,.35)', spanGaps: false },
                    { label: 'Normal', data: pts, parsing: false, borderColor: '#1f2937', estNormal: true, borderWidth: 2, pointRadius: 0, fill: false, tension: .3 },
                    { type: 'scatter', label: 'Datos', data: (d.observados || []).map(v => ({ x: v, y: 0 })), parsing: false, pointRadius: 3,
                      pointStyle: 'line', rotation: 90, borderColor: 'rgba(91,127,166,.7)', borderWidth: 1.5 },
                ] },
                plugins: [pluginLineas],
                options: {
                    responsive: true, maintainAspectRatio: false, animation: { duration: 200 },
                    scales: {
                        x: { type: 'linear', min: lo, max: hi, title: { display: true, text: d.campo },
                             ticks: { callback: (v) => fmt(v) } },
                        y: { beginAtZero: true, title: { display: true, text: 'Densidad f(x)' }, ticks: { display: false } },
                    },
                    plugins: {
                        estLineas: { lineas },
                        legend: { position: 'bottom', labels: { filter: (it) => it.datasetIndex !== 1 } },
                        tooltip: { callbacks: { label: (it) => it.datasetIndex === 2 ? `Dato: ${fmt(it.raw.x)}` :
                            `x = ${fmt(it.raw.x)} · z = ${((it.raw.x - d.media) / d.desv).toFixed(2)}` } },
                    },
                }
            });
        },

        // d: { puntos:[{t,o}], campo }
        qq(id, d) {
            const ts = d.puntos.map(p => p.t), os = d.puntos.map(p => p.o);
            const lo = Math.min(...ts, ...os), hi = Math.max(...ts, ...os);
            dibujar(id, {
                type: 'scatter',
                data: { datasets: [
                    { label: 'Datos', data: d.puntos.map(p => ({ x: p.t, y: p.o })), parsing: false, pointRadius: 3.5,
                      backgroundColor: 'rgba(91,127,166,.75)' },
                    { type: 'line', label: 'Normal teórica (y = x)', data: [{ x: lo, y: lo }, { x: hi, y: hi }], parsing: false,
                      borderColor: '#EC1C24', borderWidth: 1.5, pointRadius: 0, borderDash: [6, 4] },
                ] },
                options: {
                    responsive: true, maintainAspectRatio: false, animation: { duration: 200 },
                    scales: {
                        x: { type: 'linear', title: { display: true, text: 'Cuantil normal teórico' } },
                        y: { type: 'linear', title: { display: true, text: 'Valor observado' } },
                    },
                    plugins: {
                        legend: { position: 'bottom' },
                        tooltip: { callbacks: { label: (it) => `Teórico ${fmt(it.raw.x)} · observado ${fmt(it.raw.y)}` } },
                    },
                }
            });
        },

        png(id, nombre) {
            const el = document.getElementById(id);
            if (!el) return;
            // Fondo sólido (el lienzo es transparente).
            const c = document.createElement('canvas'); c.width = el.width; c.height = el.height;
            const x = c.getContext('2d');
            x.fillStyle = getComputedStyle(el.closest('.est-card') || document.body).backgroundColor || '#fff';
            if (x.fillStyle === 'rgba(0, 0, 0, 0)') x.fillStyle = '#fff';
            x.fillRect(0, 0, c.width, c.height); x.drawImage(el, 0, 0);
            const a = document.createElement('a'); a.href = c.toDataURL('image/png'); a.download = (nombre || 'grafico') + '.png'; a.click();
        },

        panelAplicar, panelSoltar, leerOcultos, guardarOcultos,

        soltar: soltarGraficos,
    };
})();

// Chart.js se carga la primera vez que se dibuja un gráfico (js/librerias.js).
['histograma', 'campana', 'qq'].forEach(k => sigLibs.envolver(window.sigEstadistica, k, sigLibs.chart, sigLibs.hayChart));
