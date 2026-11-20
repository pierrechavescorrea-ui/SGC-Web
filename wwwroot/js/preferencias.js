// Preferencias de interfaz POR USUARIO (tema, menú, paneles, columnas, vistas…). Se carga en <head>, síncrono, antes de
// temaInicial.js y solo cuando la página trae <meta name="sig-preferencias"> (Data/Seguridad/PreferenciasUsuario.cs).
// Los demás scripts siguen usando localStorage con sus claves de siempre (sigweb.* / sigwebCols: / sigwebPin: /
// sigwebDensidad); aquí:
//   · al cargar, lo guardado del usuario reemplaza lo del navegador (otro usuario en el mismo equipo no hereda nada);
//     lo de módulos u opciones sin permiso no llega, así que se ve lo predeterminado;
//   · la primera vez (el usuario aún no tiene nada guardado y este navegador nunca sincronizó) se sube lo que ya había;
//   · cada setItem/removeItem de esas claves se envía al servidor (agrupado, 700 ms; al salir, con keepalive).
(function () {
    var meta = document.querySelector('meta[name="sig-preferencias"]');
    if (!meta || !window.localStorage) return;
    var d;
    try { d = JSON.parse(meta.getAttribute('content') || 'null'); } catch (e) { return; }
    if (!d || !d.u) return;

    var ls = window.localStorage, proto = Storage.prototype;
    var setNativo = proto.setItem, quitarNativo = proto.removeItem;
    var K_USUARIO = 'sigPreferenciasUsuario';
    var esPref = function (k) { return typeof k === 'string' && k.indexOf('sigweb') === 0; };
    var clavesLocales = function () {
        var r = [];
        for (var i = 0; i < ls.length; i++) { var k = ls.key(i); if (esPref(k)) r.push(k); }
        return r;
    };
    var url = new URL('api/preferencias', document.baseURI).href;
    // Fallo pasajero (red, choque de escrituras) = se reintenta una vez sin pisar lo que haya cambiado después.
    // 409 = la sesión ya es de otra cuenta: no se reintenta.
    function enviar(cambios, alSalir, reintento) {
        if (d.lectura) return;
        try {
            fetch(url, {
                method: 'POST', credentials: 'same-origin', keepalive: !!alSalir,
                headers: { 'Content-Type': 'application/json', 'X-Sig-Preferencias': d.u },
                body: JSON.stringify(cambios)
            }).then(function (r) {
                if (!r.ok && r.status !== 409 && r.status !== 400 && r.status !== 413 && !reintento) volverAIntentar(cambios);
            }, function () { if (!reintento) volverAIntentar(cambios); });
        } catch (e) { }
    }
    function volverAIntentar(cambios) {
        setTimeout(function () {
            var c = {}, n = 0;
            Object.keys(cambios).forEach(function (k) { if (!(k in pendientes)) { c[k] = cambios[k]; n++; } });
            if (n > 0) enviar(c, false, true);
        }, 3000);
    }

    try {
        var previo = ls.getItem(K_USUARIO);
        if (d.inicial && previo === null) {
            // Navegador que nunca sincronizó y usuario sin nada guardado: se conserva lo local y se sube.
            var subir = {}, n = 0;
            clavesLocales().forEach(function (k) { subir[k] = ls.getItem(k); n++; });
            if (n > 0) enviar(subir);
        } else {
            clavesLocales().forEach(function (k) { quitarNativo.call(ls, k); });
            var p = d.p || {};
            Object.keys(p).forEach(function (k) { if (esPref(k)) setNativo.call(ls, k, p[k]); });
        }
        setNativo.call(ls, K_USUARIO, d.u);
    } catch (e) { return; }

    var pendientes = {}, hay = false, temporizador = null;
    function vaciar(alSalir) {
        clearTimeout(temporizador);
        if (!hay) return;
        var c = pendientes; pendientes = {}; hay = false;
        enviar(c, alSalir);
    }
    function anotar(k, v) {
        pendientes[k] = v; hay = true;
        clearTimeout(temporizador);
        temporizador = setTimeout(vaciar, 700);
    }
    proto.setItem = function (k, v) {
        setNativo.call(this, k, v);
        if (this === ls && esPref(String(k))) anotar(String(k), String(v));
    };
    proto.removeItem = function (k) {
        quitarNativo.call(this, k);
        if (this === ls && esPref(String(k))) anotar(String(k), null);
    };
    window.addEventListener('pagehide', function () { vaciar(true); });
    document.addEventListener('visibilitychange', function () { if (document.visibilityState === 'hidden') vaciar(true); });
})();
