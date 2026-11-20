// Mi cuenta › foto de perfil: al elegir la imagen se recorta al centro en cuadrado, se reduce a 512 px y se vuelve a
// codificar en el navegador (JPEG 0,88). Así sube liviana y sin metadatos (EXIF con GPS del teléfono). Luego se envía el formulario.
(function () {
    document.addEventListener('change', function (e) {
        var inp = e.target;
        if (!inp.matches || !inp.matches('.mc-subir input[type=file]') || !inp.files || !inp.files[0]) return;
        var f = inp.files[0], form = inp.form;
        if (!/^image\/(png|jpeg|webp)$/.test(f.type)) { form.submit(); return; }   // el servidor dirá qué falla
        var img = new Image(), url = URL.createObjectURL(f);
        img.onload = function () {
            try {
                var lado = Math.min(img.naturalWidth, img.naturalHeight), t = Math.min(512, lado);
                var c = document.createElement('canvas'); c.width = t; c.height = t;
                c.getContext('2d').drawImage(img, (img.naturalWidth - lado) / 2, (img.naturalHeight - lado) / 2, lado, lado, 0, 0, t, t);
                c.toBlob(function (b) {
                    URL.revokeObjectURL(url);
                    if (b && window.DataTransfer) {
                        var dt = new DataTransfer();
                        dt.items.add(new File([b], 'foto.jpg', { type: 'image/jpeg' }));
                        inp.files = dt.files;
                    }
                    form.submit();
                }, 'image/jpeg', 0.88);
            } catch (_) { form.submit(); }
        };
        img.onerror = function () { URL.revokeObjectURL(url); form.submit(); };
        img.src = url;
    });
})();
