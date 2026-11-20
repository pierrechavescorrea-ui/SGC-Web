// Diseñador de formularios (Configuración › Catálogos › Formularios) y apoyo a los formularios diseñados.
// · sigDisenador.aplicar(el, paso, json, dotnet): el boceto de un paso usa el mismo motor del panel de Inicio
//   (js/inicio.js, sigPanel.crear) en modo edición permanente: arrastrar el campo, tirador de tamaño, los demás se
//   apartan. Cada cambio se avisa a Blazor (Movido(paso, json)); Blazor guarda el diseño en la base, no aquí.
// · En un formulario con diseño (.frm-diseno-activo) los pasos se ordenan con CSS order; lo que no es un paso
//   (avisos, botones sueltos) toma el orden del paso que lo precede en el código, para no saltar arriba.
(function () {
    'use strict';
    var instancias = new Map();

    window.sigDisenador = {
        aplicar: function (el, paso, json, dotnet) {
            if (!el || !window.sigPanel || !window.sigPanel.crear) return;
            var ent = instancias.get(el);
            if (ent && ent.paso !== paso) { ent.api.destruir(); instancias.delete(el); ent = null; }
            if (!ent) {
                ent = {
                    paso: paso,
                    api: window.sigPanel.crear('sigweb.disenador.' + paso, {
                        posicion: true, fila: 58, gap: 12, angosta: 0, extra: 1, minAlto: 58,
                        guardar: function (layout) {
                            try { dotnet.invokeMethodAsync('Movido', paso, JSON.stringify(layout)); } catch (e) { }
                        },
                        // Soltar un campo sobre la rejilla de otro paso: Blazor decide si se puede (la lógica manda).
                        alSoltarFuera: function (id, pasoDestino, x, y) {
                            try { dotnet.invokeMethodAsync('MovidoAPaso', id, pasoDestino, x, y); } catch (e) { }
                        }
                    })
                };
                instancias.set(el, ent);
            }
            ent.api.cargar(json || '[]');
            ent.api.aplicar(el, true);
        },
        destruir: function () {
            instancias.forEach(function (ent, el) { if (!el.isConnected) { ent.api.destruir(); instancias.delete(el); } });
        },
        limpiar: function () {
            instancias.forEach(function (ent) { ent.api.destruir(); });
            instancias.clear();
        }
    };

    // Vistas configurables de un registro (Plan por plan): el mismo motor; en diseño avisa cada cambio a Blazor (MovidaVista).
    var vistas = new Map();
    window.sigVistaReg = {
        aplicar: function (el, json, editando, dotnet) {
            if (!el || !window.sigPanel || !window.sigPanel.crear) return;
            var ent = vistas.get(el);
            if (!ent) {
                ent = { editando: !!editando };
                ent.api = window.sigPanel.crear('sigweb.vistareg', {
                    posicion: true, fila: 46, gap: 12, angosta: 900, extra: 2, minAlto: 0,
                    guardar: function (layout) {
                        if (!ent.editando) return;
                        try { dotnet.invokeMethodAsync('MovidaVista', JSON.stringify(layout)); } catch (e) { }
                    }
                });
                vistas.set(el, ent);
            }
            ent.editando = !!editando;
            ent.api.cargar(json || '[]');
            ent.api.aplicar(el, !!editando);
        }
    };

    function ordenarSueltos() {
        var cont = document.querySelectorAll('.frm-diseno-activo');
        for (var i = 0; i < cont.length; i++) {
            var orden = 0, hijos = cont[i].children;
            for (var j = 0; j < hijos.length; j++) {
                var h = hijos[j];
                if (h.hasAttribute('data-paso')) { orden = parseInt(h.style.order || '0', 10) || orden; continue; }
                var o = String(orden + 1);
                if (h.style.order !== o) h.style.order = o;
            }
        }
        // Paneles que ya no están en la página: se sueltan (si destruir falla, igual salen de la lista: antes el error se repetía cada 600 ms).
        instancias.forEach(function (ent, el) { if (!el.isConnected) { try { ent.api.destruir(); } catch (e) { } instancias.delete(el); } });
        vistas.forEach(function (ent, el) { if (!el.isConnected) { try { ent.api.destruir(); } catch (e) { } vistas.delete(el); } });
    }
    setInterval(function () { if (!document.hidden) ordenarSueltos(); }, 600);
})();
