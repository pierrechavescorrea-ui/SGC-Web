import { MOUSE, TOUCH } from '../lib/three/three.module.min.js';

const editable = el => el?.closest?.('input, textarea, select, [contenteditable]:not([contenteditable="false"]), [role="textbox"]');

// La tecla es temporal: nunca se guarda un modo de navegación en preferencias.
export function espacioPan(el, cambiar) {
    let pan = false;
    const poner = v => { if (pan !== v) { pan = v; cambiar(v); } };
    const abajo = ev => {
        if (ev.code !== 'Space' || editable(ev.target) || editable(document.activeElement)) return;
        if (!el.isConnected || !el.getClientRects().length) return;
        ev.preventDefault();
        poner(true);
    };
    const arriba = ev => {
        if (ev.code !== 'Space') return;
        if (pan && !editable(ev.target)) ev.preventDefault();
        poner(false);
    };
    const soltar = () => poner(false);
    const foco = ev => { if (editable(ev.target)) soltar(); };
    const pulsar = ev => { if (ev.button === 1) ev.preventDefault(); };
    window.addEventListener('keydown', abajo, true);
    window.addEventListener('keyup', arriba, true);
    window.addEventListener('blur', soltar);
    document.addEventListener('visibilitychange', soltar);
    document.addEventListener('focusin', foco);
    el.addEventListener('pointerdown', pulsar, true);
    el.addEventListener('auxclick', pulsar);
    return {
        get pan() { return pan; },
        liberar() {
            soltar();
            window.removeEventListener('keydown', abajo, true);
            window.removeEventListener('keyup', arriba, true);
            window.removeEventListener('blur', soltar);
            document.removeEventListener('visibilitychange', soltar);
            document.removeEventListener('focusin', foco);
            el.removeEventListener('pointerdown', pulsar, true);
            el.removeEventListener('auxclick', pulsar);
        }
    };
}

export function navegacion3d(controles, el) {
    controles.mouseButtons = { LEFT: MOUSE.ROTATE, MIDDLE: MOUSE.PAN, RIGHT: MOUSE.PAN };
    controles.touches = { ONE: TOUCH.ROTATE, TWO: TOUCH.DOLLY_PAN };
    controles.screenSpacePanning = true;
    controles.zoomToCursor = true;
    let cursor = '';
    return espacioPan(el, pan => {
        controles.mouseButtons.LEFT = pan ? MOUSE.PAN : MOUSE.ROTATE;
        if (pan) cursor = el.style.cursor;
        el.style.cursor = pan ? 'grab' : cursor;
    });
}

export function navegacionCesium(viewer, C) {
    const c = viewer.scene.screenSpaceCameraController, E = C.CameraEventType;
    const aplicar = pan => {
        // Cesium: tilt orbita sobre el punto mirado; rotate desplaza sobre el globo.
        c.tiltEventTypes = pan ? [] : [E.LEFT_DRAG];
        c.rotateEventTypes = pan ? [E.LEFT_DRAG, E.MIDDLE_DRAG, E.RIGHT_DRAG] : [E.MIDDLE_DRAG, E.RIGHT_DRAG];
        c.translateEventTypes = c.rotateEventTypes;
        viewer.canvas.style.cursor = pan ? 'grab' : '';
    };
    aplicar(false);
    c.zoomEventTypes = [E.WHEEL, E.PINCH];
    c.minimumZoomDistance = 5;
    return espacioPan(viewer.canvas, aplicar);
}
