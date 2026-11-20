// Vitrina de Certificaciones y mosaicos del Direccionamiento: miniatura de la primera página del PDF vigente
// (o de la página data-pag: tira de páginas al dividir un PDF)
// (pdf.js se carga solo la primera vez que hace falta) y modo exhibición en pantalla completa.
(function () {
    const PDFJS = new URL('lib/pdfjs/', document.baseURI).href;   // pdfjs-dist 4.10.38 vendorizado
    let cargaPdf = null;
    const hechas = new Map();   // url → dataURL de la miniatura (no se vuelve a dibujar al re-renderizar)

    function pdfjs() {
        if (!cargaPdf) {
            cargaPdf = import(PDFJS + 'pdf.min.mjs').then(m => {
                m.GlobalWorkerOptions.workerSrc = PDFJS + 'pdf.worker.min.mjs';
                return m;
            });
        }
        return cargaPdf;
    }

    // Documentos abiertos por url: la tira de páginas del Direccionamiento dibuja varias páginas del mismo PDF.
    const abiertos = new Map();
    function abrir(url) {
        if (!abiertos.has(url)) {
            const p = pdfjs().then(lib => lib.getDocument(url).promise);
            p.catch(() => abiertos.delete(url));
            abiertos.set(url, p);
        }
        return abiertos.get(url);
    }

    async function dibujar(url, ancho, numero) {
        const doc = await abrir(url);
        const pag = await doc.getPage(Math.min(Math.max(numero || 1, 1), doc.numPages));
        const base = pag.getViewport({ scale: 1 });
        const escala = (ancho * (window.devicePixelRatio || 1) * 1.5) / base.width;
        const vp = pag.getViewport({ scale: escala });
        const lienzo = document.createElement('canvas');
        lienzo.width = Math.ceil(vp.width);
        lienzo.height = Math.ceil(vp.height);
        await pag.render({ canvasContext: lienzo.getContext('2d'), viewport: vp }).promise;
        pag.cleanup();
        return lienzo.toDataURL('image/jpeg', 0.85);
    }

    function poner(el, src) {
        if (el.dataset.pdfListo === src) return;
        el.dataset.pdfListo = src;
        el.replaceChildren();
        const img = document.createElement('img');
        img.alt = '';
        img.src = src;
        el.appendChild(img);
    }

    // Mosaicos del Direccionamiento: alto de la sala = lo que queda de la ventana; se elige el número de
    // columnas que deja las hojas más grandes cabiendo todas, y si aun así sobra contenido se reduce un poco.
    const HOJA = 1.414, MARCO = 48, PLACA = 92, GX = 24, GY = 22, MIN = 110, MAX = 460;
    function ajustar(sala) {
        const n = sala.querySelectorAll('.cv-obra').length;
        if (!n) { sala.style.height = ''; return; }
        const cs = getComputedStyle(sala);
        const padX = parseFloat(cs.paddingLeft) + parseFloat(cs.paddingRight);
        const padY = parseFloat(cs.paddingTop) + parseFloat(cs.paddingBottom);
        const lleno = document.fullscreenElement === sala;
        let alto = lleno ? window.innerHeight : window.innerHeight - sala.getBoundingClientRect().top - 16;
        alto = Math.max(alto, 260);
        sala.style.height = lleno ? '' : alto + 'px';
        const W = sala.clientWidth - padX, H = alto - padY;
        let mejor = { w: 0, c: 1 };
        for (let c = 1; c <= n; c++) {
            const filas = Math.ceil(n / c);
            const porAncho = (W - (c - 1) * GX) / c;
            const porAlto = ((H - (filas - 1) * GY) / filas - MARCO - PLACA) * HOJA + MARCO;
            const w = Math.min(porAncho, porAlto);
            if (w > mejor.w) mejor = { w, c };
        }
        let w = Math.max(MIN, Math.min(MAX, Math.floor(mejor.w)));
        sala.style.setProperty('--de-c', mejor.c);
        sala.style.setProperty('--de-w', w + 'px');
        for (let i = 0; i < 12 && sala.scrollHeight > sala.clientHeight + 1 && w > MIN; i++) {
            w = Math.max(MIN, Math.floor(w * 0.94));
            sala.style.setProperty('--de-w', w + 'px');
        }
    }

    window.sigVitrina = {
        ajustarMosaico: function (sala) {
            if (!sala || !sala.querySelectorAll) return;
            if (!sala._deAjuste) {
                const f = () => {
                    if (!sala.isConnected) { window.removeEventListener('resize', f); document.removeEventListener('fullscreenchange', f); return; }
                    ajustar(sala);
                };
                sala._deAjuste = f;
                window.addEventListener('resize', f);
                document.addEventListener('fullscreenchange', f);
            }
            ajustar(sala);
        },
        miniaturas: function (contenedor) {
            if (!contenedor || !contenedor.querySelectorAll) return;
            contenedor.querySelectorAll('[data-pdf]').forEach(el => {
                const url = el.getAttribute('data-pdf');
                if (!url) return;
                const numero = parseInt(el.getAttribute('data-pag') || '1', 10) || 1;   // data-pag: página a dibujar (1 por defecto)
                const clave = numero === 1 ? url : url + '#' + numero;
                const lista = hechas.get(clave);
                if (typeof lista === 'string') { if (lista) poner(el, lista); return; }
                if (lista) { lista.then(src => src && poner(el, src)); return; }
                const p = dibujar(url, el.clientWidth || 240, numero)
                    .then(src => { hechas.set(clave, src); return src; })
                    .catch(() => { hechas.set(clave, ''); return ''; });   // sin miniatura: queda la lámina
                hechas.set(clave, p);
                p.then(src => src && poner(el, src));
            });
        },
        pantallaCompleta: function (el) {
            if (!el) return;
            if (document.fullscreenElement) { document.exitFullscreen(); return; }
            if (el.requestFullscreen) el.requestFullscreen();
        }
    };
})();
