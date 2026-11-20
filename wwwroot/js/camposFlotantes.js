// Etiquetas flotantes en todos los formularios (css/formularios.css, bloque "Etiquetas flotantes").
// Busca cada <label> seguida de su caja (input/select/textarea, o un .input-group que empieza por una)
// y la marca con data-* (no con clases: Blazor reescribe class al renderizar y borraría las nuestras).
//   label[data-flot="abajo"]  -> el título se ve dentro de la caja vacía, en bajo contraste.
//   label[data-flot="arriba"] -> la caja tiene foco o valor: el título sube y queda pequeño.
// Además enlaza label ↔ caja (for/id) para lectores de pantalla y clic en el título.
// Excluye tablas, el encabezado oscuro (.pg-cab) y todo lo que esté dentro de [data-sin-flotante].
(function () {
    'use strict';

    var TIPOS_NO = { checkbox: 1, radio: 1, range: 1, color: 1, file: 1, hidden: 1, button: 1, submit: 1, reset: 1, image: 1 };
    // Opción vacía de una lista: "", "-- Seleccione --", "—", "–", "Elegir…".
    var SIN_VALOR = /^\s*([-–—].*|selecci.*|elegir.*|escoger.*|ninguno|\(ninguno\))?\s*$/i;
    var pares = [];
    var n = 0;

    function esCaja(el) {
        if (!el || !el.matches) return false;
        if (!el.matches('input.form-control, select.form-select, textarea.form-control')) return false;
        if (el.tagName === 'INPUT' && TIPOS_NO[(el.type || '').toLowerCase()]) return false;
        if (el.multiple || (el.tagName === 'SELECT' && el.size > 1)) return false;
        return true;
    }

    function cajaDe(label) {
        var sig = label.nextElementSibling;
        if (esCaja(sig)) return sig;
        if (sig && sig.classList && sig.classList.contains('input-group') && esCaja(sig.firstElementChild)) return sig.firstElementChild;
        return null;
    }

    function enlazar(label) {
        if (label.hasAttribute('data-flot')) return;
        if (label.classList.contains('form-check-label') || label.classList.contains('visually-hidden')) return;
        if (label.querySelector('input, select, textarea')) return;
        var fuera = label.closest('td, th, .pg-cab, [data-sin-flotante]');
        // Una ventana emergente abierta desde un botón del encabezado (p. ej. Avisar por correo) sí lleva etiquetas flotantes.
        if (fuera && !(fuera.classList.contains('pg-cab') && label.closest('.modal-box-custom'))) return;
        var caja = cajaDe(label);
        if (!caja) return;
        var padre = label.parentElement;
        if (padre) {
            var cs = getComputedStyle(padre);
            // Título y caja lado a lado (fila flex o grid): se deja como está.
            if ((cs.display.indexOf('flex') >= 0 && cs.flexDirection.indexOf('row') === 0) || cs.display.indexOf('grid') >= 0) return;
        }
        if (!label.htmlFor && !caja.id) caja.id = 'flot-' + (++n);
        if (!label.htmlFor) label.htmlFor = caja.id;
        label.setAttribute('data-flot', 'abajo');
        caja.setAttribute('data-flot-caja', '');
        if (caja.tagName === 'TEXTAREA') label.setAttribute('data-flot-area', '');
        if (caja.required) label.setAttribute('data-flot-req', '');
        pares.push({ label: label, caja: caja });
        estado(label, caja);
    }

    function tieneValor(caja) {
        if (caja.tagName === 'SELECT') {
            if (caja.selectedIndex < 0) return false;
            var op = caja.options[caja.selectedIndex];
            return !(caja.value === '' && SIN_VALOR.test(op.text));
        }
        if (caja.value !== '') return true;
        // Número/fecha con texto inválido a medio escribir: el navegador da value "" pero hay algo escrito.
        return !!(caja.validity && caja.validity.badInput);
    }

    function estado(label, caja) {
        var foco = document.activeElement === caja;
        var lleno = tieneValor(caja);
        var arriba = foco || lleno;
        var v = arriba ? 'arriba' : 'abajo';
        if (label.getAttribute('data-flot') !== v) label.setAttribute('data-flot', v);
        if (foco) label.setAttribute('data-flot-foco', ''); else label.removeAttribute('data-flot-foco');
        if (lleno) caja.removeAttribute('data-flot-vacia'); else caja.setAttribute('data-flot-vacia', '');
        var invalida = caja.classList.contains('invalid') || caja.getAttribute('aria-invalid') === 'true';
        if (invalida) label.setAttribute('data-flot-error', ''); else label.removeAttribute('data-flot-error');
        if (caja.offsetWidth) label.style.setProperty('--flot-w', caja.offsetWidth + 'px');
        // Título largo cortado con …: el texto completo va de tooltip en la caja (sin pisar un title propio).
        var cortado = label.scrollWidth > label.clientWidth + 1;
        var propio = caja.hasAttribute('title') && !caja.hasAttribute('data-flot-title');
        if (cortado && !propio) {
            var txt = label.textContent.replace(/\s+/g, ' ').trim();
            if (caja.getAttribute('title') !== txt) { caja.setAttribute('title', txt); caja.setAttribute('data-flot-title', ''); }
        } else if (!cortado && caja.hasAttribute('data-flot-title')) {
            caja.removeAttribute('title'); caja.removeAttribute('data-flot-title');
        }
    }

    function revisar() {
        var vivos = [];
        for (var i = 0; i < pares.length; i++) {
            var p = pares[i];
            if (!p.label.isConnected || !p.caja.isConnected) continue;
            // Blazor puede cambiar la caja que sigue a la etiqueta: se vuelve a enlazar.
            if (cajaDe(p.label) !== p.caja) { p.label.removeAttribute('data-flot'); p.caja.removeAttribute('data-flot-caja'); p.caja.removeAttribute('data-flot-vacia'); continue; }
            estado(p.label, p.caja);
            vivos.push(p);
        }
        pares = vivos;
        var ls = document.querySelectorAll('label:not([data-flot])');
        for (var j = 0; j < ls.length; j++) enlazar(ls[j]);
    }

    var pendiente = false;
    function pronto() {
        if (pendiente) return;
        pendiente = true;
        requestAnimationFrame(function () { pendiente = false; revisar(); });
    }

    function alEvento(e) {
        var t = e.target;
        if (!t || !t.hasAttribute || !t.hasAttribute('data-flot-caja')) return;
        for (var i = 0; i < pares.length; i++) if (pares[i].caja === t) estado(pares[i].label, t);
    }

    function iniciar() {
        revisar();
        ['input', 'change', 'focusin', 'focusout'].forEach(function (ev) { document.addEventListener(ev, alEvento, true); });
        new MutationObserver(pronto).observe(document.body, { childList: true, subtree: true, attributes: true, attributeFilter: ['class', 'value', 'aria-invalid'] });
        // Blazor asigna value por propiedad (sin evento ni mutación): repaso liviano periódico.
        setInterval(function () { if (!document.hidden) revisar(); }, 700);
    }

    if (document.readyState === 'loading') document.addEventListener('DOMContentLoaded', iniciar);
    else iniciar();
})();
