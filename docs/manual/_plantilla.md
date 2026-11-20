---
modulo: clave-del-modulo        # clave de CatalogoModulos (Data/Seguridad/Permisos.cs); "general" si no es un módulo
titulo: Nombre visible en el menú
menu: Grupo › Opción            # ruta en el menú lateral, tal como la ve el usuario
rutas: [/ruta, /ruta/{id}]      # @page de las páginas que cubre este capítulo
palabras: [sinónimos, términos que el usuario podría usar]
opciones:                       # enlaces directos del asistente: lo que el cuerpo nombra en **negrita** se vuelve enlace
  # - Nombre exacto en negrita: /ruta?vista=x | Texto exacto del control (title/aria-label/texto)
  # - Otro: /ruta | !Texto del botón    (con ! el asistente además lo abre: solo vistas y paneles)
actualizado: AAAA-MM-DD
---

# Nombre del módulo

## Para qué sirve
Dos o tres frases, en lenguaje de usuario (no de programador): qué problema resuelve y quién lo usa.
Numeral ISO 9001 si aplica.

## Cómo entrar
Menú › Grupo › Opción. Otros accesos (botones en otras páginas, búsqueda, bot).

## Qué hay en la pantalla
Las partes de la página de arriba a abajo, con el **nombre exacto** de cada pestaña, botón y columna
tal como aparece en pantalla.

## Cómo hacer…
### Cómo <tarea frecuente 1>
1. Paso concreto con el **nombre exacto del botón**.
2. …

### Cómo <tarea frecuente 2>
…

## Permisos
Qué acción hace falta para cada cosa (Ver, Descargar, Comentar, Crear, Editar, Eliminar, Importar, Aprobar,
ConfigurarCampos, EditarVistas). Qué ve alguien con rol Consulta.

## Problemas frecuentes
- **«No veo el botón X»** → causa y solución.
- …

## Relacionado
- [Otro módulo](otro-modulo.md) — por qué se relaciona.

<!--
Reglas de redacción (para quien escriba o actualice un capítulo):
- Todo sale del código: textos de botones, pestañas y mensajes copiados del .razor. Nada inventado.
- Si algo no está claro en el código, escribir «> Por confirmar: …» en vez de suponer.
- Español, segunda persona de usted («Toque», «Escriba»), frases cortas.
- Sin nombres de clases, tablas ni rutas de archivos en el cuerpo (solo en el encabezado).
-->
