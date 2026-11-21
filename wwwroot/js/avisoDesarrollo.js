// Aviso de entorno de desarrollo: en vez de la banda fija, una ventana emergente UNA vez por sesión del navegador.
// MainLayout deja la marca <div data-aviso-dev="proyecto" hidden> solo cuando el acceso es el libre de desarrollo.
(() => {
    const CLAVE = 'sigweb.avisoDesarrollo';

    function leido() { try { return sessionStorage.getItem(CLAVE) === '1'; } catch { return false; } }
    function marcar() { try { sessionStorage.setItem(CLAVE, '1'); } catch { /* sin almacenamiento: se vuelve a mostrar */ } }

    function mostrar() {
        const marca = document.querySelector('[data-aviso-dev]');
        if (!marca || leido() || document.getElementById('sig-aviso-dev')) return;
        marcar();
        const fondo = document.createElement('div');
        fondo.id = 'sig-aviso-dev';
        fondo.className = 'modal-backdrop-custom sig-aviso-dev';
        const caja = document.createElement('div');
        caja.className = 'modal-box-custom sig-aviso-dev-caja';
        caja.setAttribute('aria-label', 'Entorno de desarrollo');
        const titulo = document.createElement('h5');
        titulo.textContent = 'Entorno de desarrollo';
        const texto = document.createElement('p');
        const proyecto = marca.getAttribute('data-aviso-dev');
        texto.textContent = 'Acceso sin contraseña como administrador' + (proyecto ? ' · ' + proyecto : '') + '.';
        const pie = document.createElement('div');
        pie.className = 'sig-aviso-dev-pie';
        const ok = document.createElement('button');
        ok.type = 'button';
        ok.className = 'btn btn-primary';
        ok.title = 'Aceptar';
        ok.setAttribute('aria-label', 'Aceptar');
        ok.setAttribute('data-modal-cancelar', '');
        ok.innerHTML = '<svg viewBox="0 0 24 24" width="18" height="18" fill="none" stroke="currentColor" stroke-width="2" ' +
            'stroke-linecap="round" stroke-linejoin="round" aria-hidden="true"><path d="M5 12.5l4.5 4.5L19 7"/></svg>';
        const cerrar = () => fondo.remove();
        ok.addEventListener('click', cerrar);
        fondo.addEventListener('click', e => { if (e.target === fondo) cerrar(); });
        pie.appendChild(ok);
        caja.append(titulo, texto, pie);
        fondo.appendChild(caja);
        document.body.appendChild(fondo);
        ok.focus();
    }

    if (document.readyState === 'loading') document.addEventListener('DOMContentLoaded', mostrar); else mostrar();
    // Navegación mejorada de Blazor: la marca puede llegar después (p. ej. tras iniciar sesión).
    const esperar = () => { if (window.Blazor?.addEventListener) Blazor.addEventListener('enhancedload', mostrar); else setTimeout(esperar, 300); };
    esperar();
})();
