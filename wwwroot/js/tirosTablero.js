(function () {
    'use strict';

    var estados = new WeakMap();

    function crearEstado(raiz, referencia) {
        var estado = { referencia: referencia };
        var gesto = null;
        var visual = null;
        var frame = 0;
        var suprimirHasta = 0;

        function editando() {
            return raiz.getAttribute('data-editando') === '1';
        }

        function cercano(elemento, selector) {
            return elemento && elemento.closest ? elemento.closest(selector) : null;
        }

        function fichas(accion) {
            Array.prototype.forEach.call(raiz.querySelectorAll('.tt-t'), accion);
        }

        function idFicha(ficha) {
            return parseInt(ficha.getAttribute('data-id'), 10);
        }

        function invocar(metodo, ids, argumento) {
            try {
                Promise.resolve(estado.referencia.invokeMethodAsync(metodo, ids, argumento))
                    .catch(function (error) { console.warn('sigTirosTablero: ' + metodo, error); });
            } catch (error) {
                console.warn('sigTirosTablero: ' + metodo, error);
            }
        }

        // Se consultan las fichas actuales; Blazor puede sustituirlas durante el gesto.
        function destino() {
            var columna = cercano(document.elementFromPoint(gesto.x, gesto.y), '.tt-col');
            if (!columna || !raiz.contains(columna)) return null;
            var tiro = columna.getAttribute('data-tiro');
            if (tiro === null) return null;
            var diferentes = false;
            fichas(function (ficha) {
                if (gesto.ids.indexOf(idFicha(ficha)) < 0) return;
                var origen = cercano(ficha, '.tt-col');
                if (!origen || origen.getAttribute('data-tiro') !== tiro) diferentes = true;
            });
            return diferentes ? columna : null;
        }

        function quitarClases() {
            Array.prototype.forEach.call(raiz.querySelectorAll('.tt-arrastrada, .tt-sobre, .tt-lazo-toca'), function (elemento) {
                elemento.classList.remove('tt-arrastrada', 'tt-sobre', 'tt-lazo-toca');
            });
            raiz.classList.remove('tt-arrastrando');
        }

        function limpiar() {
            var anterior = gesto;
            gesto = null;
            if (frame) cancelAnimationFrame(frame);
            frame = 0;
            quitarClases();
            if (visual && visual.parentNode) visual.parentNode.removeChild(visual);
            visual = null;
            if (anterior) {
                try {
                    if (raiz.hasPointerCapture(anterior.pointerId)) raiz.releasePointerCapture(anterior.pointerId);
                } catch (error) { /* La captura puede haberse perdido al retirar la raíz. */ }
            }
        }

        function rectangulo() {
            return {
                left: Math.min(gesto.inicioX, gesto.x), top: Math.min(gesto.inicioY, gesto.y),
                right: Math.max(gesto.inicioX, gesto.x), bottom: Math.max(gesto.inicioY, gesto.y)
            };
        }

        function actualizar() {
            if (gesto.tipo === 'arrastre') {
                visual.style.left = (gesto.x + 14) + 'px';
                visual.style.top = (gesto.y + 14) + 'px';
                raiz.classList.add('tt-arrastrando');
                fichas(function (ficha) {
                    ficha.classList.toggle('tt-arrastrada', gesto.ids.indexOf(idFicha(ficha)) >= 0);
                });
                var columna = destino();
                Array.prototype.forEach.call(raiz.querySelectorAll('.tt-col'), function (elemento) {
                    elemento.classList.toggle('tt-sobre', elemento === columna);
                });
            } else {
                var rect = rectangulo();
                visual.style.left = rect.left + 'px';
                visual.style.top = rect.top + 'px';
                visual.style.width = (rect.right - rect.left) + 'px';
                visual.style.height = (rect.bottom - rect.top) + 'px';
                gesto.ids = [];
                fichas(function (ficha) {
                    var caja = ficha.getBoundingClientRect();
                    var toca = caja.width > 0 && caja.height > 0 && caja.left <= rect.right &&
                        caja.right >= rect.left && caja.top <= rect.bottom && caja.bottom >= rect.top;
                    ficha.classList.toggle('tt-lazo-toca', toca);
                    var id = idFicha(ficha);
                    if (toca && !isNaN(id) && gesto.ids.indexOf(id) < 0) gesto.ids.push(id);
                });
            }
        }

        function velocidad(posicion, minimo, maximo) {
            if (posicion < minimo || posicion > maximo) return 0;
            if (posicion - minimo < 40 && posicion <= (minimo + maximo) / 2) return -12;
            if (maximo - posicion < 40) return 12;
            return 0;
        }

        function animar() {
            frame = 0;
            if (!gesto || !gesto.activo) return;
            if (!editando() || !raiz.isConnected) { cancelar(); return; }
            if (gesto.tipo === 'arrastre') {
                var tablero = raiz.querySelector('.tt-tablero');
                if (tablero) {
                    var caja = tablero.getBoundingClientRect();
                    if (gesto.y >= caja.top && gesto.y <= caja.bottom) {
                        tablero.scrollLeft += velocidad(gesto.x, caja.left, caja.right);
                    }
                }
                Array.prototype.forEach.call(raiz.querySelectorAll('.tt-col-torres'), function (contenedor) {
                    var caja = contenedor.getBoundingClientRect();
                    if (gesto.x >= caja.left && gesto.x <= caja.right) {
                        contenedor.scrollTop += velocidad(gesto.y, caja.top, caja.bottom);
                    }
                });
            }
            actualizar();
            frame = requestAnimationFrame(animar);
        }

        function activar() {
            gesto.activo = true;
            visual = document.createElement('div');
            if (gesto.tipo === 'arrastre') {
                visual.className = 'tt-fantasma';
                visual.style.cssText = 'position:fixed;pointer-events:none;background:#111827;color:white;border-radius:8px;padding:4px 10px;box-shadow:0 4px 12px rgba(0,0,0,.25);z-index:2000;font:12px sans-serif;';
                visual.textContent = gesto.nombre;
                if (gesto.ids.length > 1) {
                    var insignia = document.createElement('span');
                    insignia.className = 'tt-fantasma-n';
                    insignia.style.cssText = 'display:inline-block;margin-left:8px;background:#2563eb;border-radius:999px;padding:2px 6px;text-align:center;';
                    insignia.textContent = String(gesto.ids.length);
                    visual.appendChild(insignia);
                }
            } else {
                visual.className = 'tt-lazo';
                visual.style.cssText = 'position:fixed;pointer-events:none;border:1px solid #2563eb;background:rgba(37,99,235,.12);z-index:1999;box-sizing:border-box;';
            }
            document.body.appendChild(visual);
            // Capturar tras superar el umbral conserva intacto el clic simple en la ficha.
            try { raiz.setPointerCapture(gesto.pointerId); } catch (error) { /* Se mantienen los listeners globales de respaldo. */ }
            actualizar();
            frame = requestAnimationFrame(animar);
        }

        function abajo(evento) {
            if (gesto || evento.button !== 0 || !editando()) return;
            var ficha = cercano(evento.target, '.tt-t');
            var tipo;
            if (ficha && raiz.contains(ficha)) tipo = 'arrastre';
            else {
                if (cercano(evento.target, '.tt-t, input, select, button, .tt-col-cab')) return;
                var fondo = cercano(evento.target, '.tt-col-torres, .tt-tablero');
                if (!fondo || !raiz.contains(fondo)) return;
                tipo = 'lazo';
            }
            gesto = {
                pointerId: evento.pointerId, inicioX: evento.clientX, inicioY: evento.clientY,
                x: evento.clientX, y: evento.clientY, tipo: tipo, activo: false,
                sumar: !!(evento.ctrlKey || evento.metaKey || evento.shiftKey), ids: []
            };
            if (tipo === 'arrastre') {
                gesto.nombre = ficha.textContent.trim();
                var seleccionada = ficha.classList.contains('sel');
                var idInicial = idFicha(ficha);
                fichas(function (actual) {
                    var id = idFicha(actual);
                    if ((seleccionada ? actual.classList.contains('sel') : id === idInicial) &&
                        !isNaN(id) && gesto.ids.indexOf(id) < 0) gesto.ids.push(id);
                });
                if (!gesto.ids.length) limpiar();
            }
        }

        function mover(evento) {
            if (!gesto || evento.pointerId !== gesto.pointerId) return;
            if (!editando()) { cancelar(); return; }
            gesto.x = evento.clientX;
            gesto.y = evento.clientY;
            if (!gesto.activo) {
                var dx = gesto.x - gesto.inicioX;
                var dy = gesto.y - gesto.inicioY;
                if (dx * dx + dy * dy <= 25) return;
                activar();
            }
            if (evento.cancelable) evento.preventDefault();
            actualizar();
        }

        function terminar(evento) {
            if (!gesto || evento.pointerId !== gesto.pointerId) return;
            var llamada = null;
            if (gesto.activo) {
                suprimirHasta = Date.now() + 50;
                gesto.x = evento.clientX;
                gesto.y = evento.clientY;
                if (editando()) {
                    actualizar();
                    if (gesto.tipo === 'arrastre') {
                        var columna = destino();
                        if (columna) llamada = ['Soltar', gesto.ids.slice(), columna.getAttribute('data-tiro')];
                    } else if (gesto.ids.length) {
                        llamada = ['Lazo', gesto.ids.slice(), !!(gesto.sumar || evento.ctrlKey || evento.metaKey || evento.shiftKey)];
                    }
                }
            }
            limpiar();
            if (llamada) invocar(llamada[0], llamada[1], llamada[2]);
        }

        function cancelar(evento) {
            // La captura implícita táctil de una ficha se pierde al capturar en la raíz.
            if (evento && evento.type === 'lostpointercapture' && evento.target !== raiz) return;
            if (evento && gesto && evento.pointerId !== undefined && evento.pointerId !== gesto.pointerId) return;
            if (gesto && gesto.activo) suprimirHasta = Date.now() + 50;
            limpiar();
        }

        function tecla(evento) {
            if (evento.key === 'Escape') cancelar();
        }

        function clic(evento) {
            if (suprimirHasta && Date.now() <= suprimirHasta) {
                suprimirHasta = 0;
                evento.preventDefault();
                evento.stopPropagation();
                evento.stopImmediatePropagation();
            } else suprimirHasta = 0;
        }

        function arrastreNativo(evento) {
            if (gesto && editando()) evento.preventDefault();
        }

        raiz.addEventListener('pointerdown', abajo);
        raiz.addEventListener('click', clic, true);
        raiz.addEventListener('dragstart', arrastreNativo);
        raiz.addEventListener('lostpointercapture', cancelar);
        window.addEventListener('pointermove', mover, { passive: false });
        window.addEventListener('pointerup', terminar);
        window.addEventListener('pointercancel', cancelar);
        window.addEventListener('keydown', tecla);
        window.addEventListener('blur', cancelar);

        estado.destruir = function () {
            limpiar();
            suprimirHasta = 0;
            raiz.removeEventListener('pointerdown', abajo);
            raiz.removeEventListener('click', clic, true);
            raiz.removeEventListener('dragstart', arrastreNativo);
            raiz.removeEventListener('lostpointercapture', cancelar);
            window.removeEventListener('pointermove', mover);
            window.removeEventListener('pointerup', terminar);
            window.removeEventListener('pointercancel', cancelar);
            window.removeEventListener('keydown', tecla);
            window.removeEventListener('blur', cancelar);
            estado.referencia = null;
        };
        return estado;
    }

    window.sigTirosTablero = {
        iniciar: function (raiz, dotnetRef) {
            if (!raiz || !raiz.addEventListener) return;
            var estado = estados.get(raiz);
            if (estado) estado.referencia = dotnetRef;
            else estados.set(raiz, crearEstado(raiz, dotnetRef));
        },
        destruir: function (raiz) {
            var estado = raiz && estados.get(raiz);
            if (!estado) return;
            estado.destruir();
            estados.delete(raiz);
        }
    };
}());
