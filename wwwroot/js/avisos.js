// Avisos elegantes (2026-11-16), para TODAS las pantallas sin tocar cada página:
//  · Guardado: toda franja verde (.alert-success) corta y sin controles se oculta (atributo, no clase: Blazor maneja
//    ese nodo) y en su lugar aparece una ventanita con un check que se dibuja; se cierra sola o con clic.
//  · Guardado lento: al pulsar un botón de guardar, si en 300 ms no hubo respuesta aparece la ventanita con un cargador
//    circular, que luego se vuelve check (éxito o formulario cerrado) o desaparece (error/aviso).
//  · Advertencias y errores (.alert-warning / .alert-danger) que aparecen justo después de una acción del usuario: la misma
//    ventanita con ícono ámbar o rojo (se cierra con clic; la advertencia sola a los ~4 s, el error a los ~8 s). Los avisos
//    que ya están al abrir la pantalla (estado, migración pendiente…) se quedan en su lugar.
//  · Impacto al eliminar: el recuadro de confirmación que contiene el panel de impacto (.imp-panel) se eleva a ventana
//    emergente con fondo atenuado (data-imp-emergente).
(function () {
    if (window.sigAvisos) return;
    const reducido = matchMedia('(prefers-reduced-motion: reduce)');
    let caja = null, tCerrar = 0, ultimoExito = 0;

    // La navegación mejorada de Blazor sincroniza el <body> y quita lo que no conoce: si la ventanita debe verse y ya
    // no está en la página, se vuelve a poner (mismo nodo, mismo estado).
    function asegurar() { if (caja && caja.classList.contains('visible') && !caja.isConnected) document.body.appendChild(caja); }
    function crearCaja() {
        if (caja) { if (!caja.isConnected) document.body.appendChild(caja); return caja; }
        caja = document.createElement('div');
        caja.className = 'sig-aviso';
        caja.setAttribute('aria-hidden', 'true');
        caja.innerHTML =
            '<div class="sig-aviso-tarjeta">' +
            '<span class="sig-aviso-giro" aria-hidden="true"></span>' +
            '<svg class="sig-aviso-check" viewBox="0 0 52 52" aria-hidden="true"><circle cx="26" cy="26" r="24"/><path d="M15 27l7 7 15-16"/></svg>' +
            '<svg class="sig-aviso-alerta" viewBox="0 0 24 24" aria-hidden="true"><path d="M10.3 3.9 1.8 18a2 2 0 0 0 1.7 3h17a2 2 0 0 0 1.7-3L13.7 3.9a2 2 0 0 0-3.4 0z"/><path d="M12 9v4"/><path d="M12 17h.01"/></svg>' +
            '<svg class="sig-aviso-error" viewBox="0 0 24 24" aria-hidden="true"><circle cx="12" cy="12" r="10"/><path d="m15 9-6 6"/><path d="m9 9 6 6"/></svg>' +
            '<span class="sig-aviso-txt"></span></div>';
        caja.addEventListener('click', ocultar);
        document.body.appendChild(caja);
        return caja;
    }

    function anunciar(texto, urgente = false) {
        const estado = document.getElementById('sig-aviso-estado');
        const error = document.getElementById('sig-aviso-error');
        if (estado) estado.textContent = urgente ? '' : texto;
        if (error) error.textContent = urgente ? texto : '';
    }

    function ocultar() {
        clearTimeout(tCerrar);
        if (caja) caja.classList.remove('visible', 'cargando', 'exito', 'adv', 'err');
        anunciar('');
    }

    function exito(texto) {
        const c = crearCaja();
        ultimoExito = Date.now();
        clearTimeout(tCerrar);
        c.querySelector('.sig-aviso-txt').textContent = texto || 'Guardado con éxito';
        c.classList.remove('cargando', 'exito', 'adv', 'err');
        void c.offsetWidth;                          // reinicia la animación del check
        c.classList.add('visible', 'exito');
        anunciar(c.querySelector('.sig-aviso-txt').textContent);
        tCerrar = setTimeout(ocultar, reducido.matches ? 2400 : 1900);
    }

    function alerta(texto, esError) {
        const c = crearCaja();
        clearTimeout(tCerrar);
        c.querySelector('.sig-aviso-txt').textContent = texto;
        c.classList.remove('cargando', 'exito', 'adv', 'err');
        void c.offsetWidth;
        c.classList.add('visible', esError ? 'err' : 'adv');
        anunciar(texto, esError);
        tCerrar = setTimeout(ocultar, esError ? 8000 : 4200);
    }

    function cargando(texto) {
        const c = crearCaja();
        clearTimeout(tCerrar);
        c.querySelector('.sig-aviso-txt').textContent = texto || 'Guardando…';
        c.classList.remove('exito', 'adv', 'err');
        c.classList.add('visible', 'cargando');
        anunciar(c.querySelector('.sig-aviso-txt').textContent);
    }

    // ---------- Franjas verdes → ventanita ----------
    const vistos = new WeakMap();
    function texto(el) { return (el.textContent || '').replace(/\s+/g, ' ').trim(); }
    function convertible(el, max = 200) {
        const t = texto(el);
        if (!t || t.length > max) return false;
        if (el.querySelector('button:not(.btn-close), a, ul, ol, table, input, select, textarea, details')) return false;
        if (el.closest('[data-sin-aviso], .modal-box-custom .imp-panel')) return false;
        return true;
    }
    function revisarExitos() {
        document.querySelectorAll('.alert-success').forEach(el => {
            const t = texto(el);
            if (vistos.get(el) === t) return;
            vistos.set(el, t);
            if (!convertible(el)) { el.removeAttribute('data-aviso-oculto'); return; }
            el.setAttribute('data-aviso-oculto', '');
            exito(t.replace(/^[✓✔]\s*/, ''));
            terminarEspera(true);
        });
    }

    // ---------- Advertencias y errores tras una acción → ventanita ----------
    let ultimaAccion = 0;
    for (const ev of ['pointerdown', 'keydown', 'change']) document.addEventListener(ev, () => { ultimaAccion = Date.now(); }, true);
    function revisarAlertas() {
        document.querySelectorAll('.alert-warning, .alert-danger').forEach(el => {
            const t = texto(el);
            if (vistos.get(el) === t) return;
            const primeraVez = !vistos.has(el);
            vistos.set(el, t);
            if (!t) return;
            // Solo lo que aparece o cambia por algo que hizo el usuario (no los avisos fijos de la pantalla).
            if (Date.now() - ultimaAccion > 5000 && primeraVez) return;
            if (!convertible(el, 240)) { el.removeAttribute('data-aviso-oculto'); return; }
            el.setAttribute('data-aviso-oculto', '');
            alerta(t, el.classList.contains('alert-danger'));
        });
    }

    // ---------- Espera del guardado ----------
    const SEL_GUARDAR = '.frm-pie .btn-primary, .frm-pie .btn-danger, .pg-btn-primario, button[title^="Guardar"], button[data-texto^="Guardar"]';
    let espera = null;   // { boton, t0, errores, tCargador, tTope }
    function firmaErrores() {
        return [...document.querySelectorAll('.alert-danger, .alert-warning, .frm-pie-msg, .validation-message')]
            .map(texto).join('|');
    }
    function esGuardar(b) {
        if (!b || b.disabled) return false;
        const t = (b.getAttribute('title') || b.getAttribute('data-texto') || b.textContent || '').trim().toLowerCase();
        if (/^(cancelar|cerrar|descartar|volver)/.test(t)) return false;
        if (/eliminar|papelera|vaciar|borrar/.test(t)) return false;      // los borrados no se anuncian como guardado
        if (/^(nuevo|nueva|agregar|añadir|actualizar|siguiente|continuar|anterior|importar|cargar|elegir|ver )/.test(t)) return false;   // abren otra pantalla o paso
        return /guardar|crear|aplicar|confirmar|registrar|aprobar|publicar/.test(t) || !!b.closest('.frm-pie');
    }
    function terminarEspera(ok) {
        if (!espera) return;
        clearTimeout(espera.tCargador); clearTimeout(espera.tTope);
        const mostrado = caja && caja.classList.contains('cargando');
        espera = null;
        if (ok === true) { if (Date.now() - ultimoExito > 300) exito(); }
        else if (mostrado) ocultar();
    }
    document.addEventListener('click', e => {
        const b = e.target.closest && e.target.closest('button');
        if (!b || !b.matches(SEL_GUARDAR) || !esGuardar(b)) return;
        terminarEspera(false);
        espera = { boton: b, t0: Date.now(), errores: firmaErrores() };
        espera.tCargador = setTimeout(() => { if (espera) cargando(); }, 300);
        espera.tTope = setTimeout(() => terminarEspera(false), 15000);
    }, true);

    function revisarEspera() {
        if (!espera) return;
        if (firmaErrores() !== espera.errores) { terminarEspera(false); return; }
        // El formulario se cerró (Guardar cierra y vuelve) o el botón salió de la pantalla sin error = guardado.
        if (!document.body.contains(espera.boton)) {
            const e0 = espera;
            setTimeout(() => { if (espera === e0) terminarEspera(firmaErrores() === e0.errores); }, 120);
        }
    }

    // ---------- Confirmación de borrado con impacto → ventana emergente ----------
    const SEL_CONTENEDOR = '.modal-box-custom, .panel-formulario, .card, .border-danger, .cv-elim, .org-quitar';
    function revisarImpacto() {
        document.querySelectorAll('.imp-panel').forEach(p => {
            const c = p.parentElement && p.parentElement.closest(SEL_CONTENEDOR);
            if (!c || c.closest('.modal-box-custom') || c.hasAttribute('data-imp-emergente')) return;
            // Un recuadro que ocupa toda la página (p. ej. la tarjeta del listado) no se eleva.
            if (c.querySelector('table.tabla-pin, .pg-cab')) return;
            c.setAttribute('data-imp-emergente', '');
        });
        document.querySelectorAll('[data-imp-emergente]').forEach(c => {
            if (!c.querySelector('.imp-panel')) c.removeAttribute('data-imp-emergente');
        });
        document.documentElement.toggleAttribute('data-imp-abierto', !!document.querySelector('[data-imp-emergente]'));
    }
    document.addEventListener('keydown', e => {
        if (e.key !== 'Escape') return;
        const c = document.querySelector('[data-imp-emergente]');
        const b = c && [...c.querySelectorAll('button')].find(x => /^(cancelar|cerrar)/i.test((x.getAttribute('title') || '').trim()));
        if (b) b.click();
    });

    let pendiente = false;
    function revisar() {
        pendiente = false;
        asegurar(); revisarExitos(); revisarEspera(); revisarAlertas(); revisarImpacto();
    }
    function programar() { if (!pendiente) { pendiente = true; requestAnimationFrame(revisar); } }
    function iniciar() {
        // Sobre <html>: la navegación mejorada de Blazor puede reemplazar el <body>.
        new MutationObserver(programar).observe(document.documentElement, { childList: true, subtree: true, characterData: true });
        revisar();
    }
    if (document.body) iniciar(); else document.addEventListener('DOMContentLoaded', iniciar);
    (function enganchar() {
        if (window.Blazor && Blazor.addEventListener) Blazor.addEventListener('enhancedload', () => { asegurar(); programar(); });
        else setTimeout(enganchar, 300);
    })();

    window.sigAvisos = { exito, alerta, cargando, ocultar };
})();
