// Acciones declaradas en el marcado con data-accion (reemplazan los onclick="…" en línea, que la política de
// seguridad CSP bloquea). Un solo listener por tipo de evento en el documento: sirve también para lo que Blazor
// pinte después y sobrevive a la navegación mejorada.
(function () {
    if (window.sigAcciones) return;
    var acciones = {
        // Selecciona todo el texto del campo (enlaces para copiar).
        seleccionar: function (el) { if (el.select) el.select(); },
        // Copia el valor del campo anterior (input + botón copiar).
        'copiar-anterior': function (el) {
            var i = el.previousElementSibling;
            if (i && navigator.clipboard) navigator.clipboard.writeText(i.value || i.textContent || '').catch(function () { });
        },
        // Muestra u oculta la contraseña del campo data-objetivo.
        'ver-clave': function (el) {
            var i = document.getElementById(el.dataset.objetivo);
            if (i) i.type = i.type === 'password' ? 'text' : 'password';
        },
        // Botón «Volver» del encabezado (js/volver.js); data-destino es la pantalla por defecto.
        volver: function (el) { if (window.sigVolver) window.sigVolver(el.dataset.destino || '/'); },
        // Botón «+» de los gráficos (js/graficoEnsayos.js).
        'elementos-grafico': function (el) { if (window.sigwebElementosGrafico) window.sigwebElementosGrafico(el); },
        // Organigrama: acercar/alejar (data-factor) y ajustar.
        'org-zoom': function (el) { if (window.sigOrganigrama) window.sigOrganigrama.zoom(el, Number(el.dataset.factor) || 1); },
        'org-ajustar': function (el) { if (window.sigOrganigrama) window.sigOrganigrama.ajustar(el); }
    };
    var alCambiar = {
        // Lista que envía su formulario al cambiar (selector de proyecto del menú).
        'enviar-form': function (el) { if (el.form) el.form.requestSubmit(); }
    };
    function despachar(mapa, e) {
        var el = e.target && e.target.closest ? e.target.closest('[data-accion]') : null;
        if (!el) return;
        var f = mapa[el.dataset.accion];
        if (f) f(el, e);
    }
    document.addEventListener('click', function (e) { despachar(acciones, e); });
    document.addEventListener('change', function (e) { despachar(alCambiar, e); });
    window.sigAcciones = { acciones: acciones, alCambiar: alCambiar };
})();
