// Estado de las torres (Costos y presupuestos): todo el tablero cabe en la pantalla, sin scroll.
// Se le da a .et el alto que queda bajo él; las fichas se reparten el alto de cada columna (CSS grid)
// y la letra se ajusta con unidades de contenedor. En pantallas angostas (≤ 1000 px) se deja el scroll.
(function () {
    function ajustar() {
        var t = document.querySelector('.et');
        if (!t) return;
        if (window.matchMedia('(max-width: 1000px)').matches) { t.style.height = ''; return; }
        var alto = Math.max(360, window.innerHeight - t.getBoundingClientRect().top - window.scrollY * 0 - 14);
        t.style.height = alto + 'px';
        var sobra = document.documentElement.scrollHeight - window.innerHeight;
        if (sobra > 0) t.style.height = Math.max(360, alto - sobra) + 'px';
    }
    window.sigEtAjustar = function () { requestAnimationFrame(ajustar); };
    window.addEventListener('resize', window.sigEtAjustar);
})();
