---
modulo: general
titulo: Uso general de SGC Web
menu: (toda la app)
rutas: [/cuenta/ingresar, /cuenta/ingresar-2fa, /cuenta/olvide, /cuenta/invitacion, /cuenta/solicitar-acceso, /cuenta/sin-proyectos, /mi-cuenta, /papelera, /acceso-denegado]
palabras: [asistente, π, pir, ayuda, chat, bot de ayuda, pregunta, ingresar, iniciar sesión, login, contraseña, clave, olvidé, invitación, cuenta, dos pasos, 2FA, código, menú, buscar, buscador, Ctrl K, favoritos, proyecto, cambiar de proyecto, permisos, rol, acceso denegado, botones, íconos, papelera, recuperar, restaurar, borrado, editar, guardar, importar, exportar, Excel, columnas, congelar, tema, foto]
opciones:
  - Restaurar: /papelera | Restaurar
  - Eliminar definitivamente: /papelera | Eliminar definitivamente
  - ¿Olvidó su contraseña?: /cuenta/olvide
  - Enviar enlace: /cuenta/olvide | Enviar enlace
  - Enviar solicitud: /cuenta/solicitar-acceso | Enviar solicitud
  - No pedir en este equipo por 30 días: /cuenta/ingresar-2fa | No pedir en este equipo por 30 días
  - '?': /?vista=proyecto | Asistente de ayuda
  - Asistente de ayuda: /?vista=proyecto | Asistente de ayuda
  - Contraer menú: /?vista=proyecto | Contraer menú
  - Cambiar de proyecto: /?vista=proyecto | Cambiar de proyecto
  - Iniciar sesión: /cuenta/ingresar
  - Solicitar acceso: /cuenta/solicitar-acceso
  - Mi cuenta: /mi-cuenta
  - Papelera: /papelera
  - Cambiar contraseña: /mi-cuenta | Cambiar contraseña
  - Cambiar foto: /mi-cuenta | Cambiar foto (PNG, JPG o WebP, máx. 2 MB)
  - Quitar foto: /mi-cuenta | Quitar foto
  - Verificación en dos pasos: /mi-cuenta | Verificación en dos pasos
  - Generar códigos de recuperación nuevos: /mi-cuenta | Generar códigos de recuperación nuevos
  - Mis proyectos: /mi-cuenta | Mis proyectos
  - Avisos por correo: /mi-cuenta | Avisos por correo
  - Bot de Telegram: /mi-cuenta | Bot de Telegram
  - Generar código de vinculación: /mi-cuenta | Generar código de vinculación
  - Privacidad y sesiones: /mi-cuenta | Privacidad y sesiones
  - Cerrar la sesión en todos los demás equipos: /mi-cuenta | Cerrar la sesión en todos los demás equipos
  - Descargar mis datos (JSON): /mi-cuenta | Descargar mis datos (JSON)
  - Temas: /temas
actualizado: 2026-11-19
---

# Uso general de SGC Web

## Para qué sirve
SGC Web reúne la información del sistema de gestión de calidad y de la obra de cada proyecto (líneas de
transmisión, torres, subestaciones): documentos, planos, riesgos, auditorías, ensayos, avance de obra y más.
Este capítulo explica lo que funciona igual en todas las pantallas.

## Cómo entrar

### Ingresar por primera vez (invitación)
El acceso es **solo por invitación**. El administrador le envía un correo con un enlace.
1. Abra el enlace del correo. Verá **Crear su cuenta**.
2. Escriba su **Nombre completo** y una contraseña de **mínimo 12 caracteres**, y repítala.
3. Acepte la política de tratamiento de datos y toque **Crear cuenta y entrar**.

Si el enlace dice **Invitación no válida**, pudo haber vencido o ya se usó: pida una nueva al administrador.

### Ingresar normalmente
1. En **Iniciar sesión**, escriba el **Correo electrónico** con el que fue invitado y su **Contraseña**.
2. Marque **Mantener la sesión** si es su equipo personal.
3. Si tiene activada la verificación en dos pasos, escriba el **Código** de su aplicación autenticadora y toque
   **Verificar**. Puede marcar **No pedir en este equipo por 30 días**.

### Si no tiene cuenta
En la pantalla de ingreso, use **Solicitar acceso**: escriba nombre, correo, empresa, el proyecto y el motivo, y
toque **Enviar solicitud**. El administrador revisa cada solicitud y, si procede, le envía una invitación.

## Qué hay en la pantalla

### Menú lateral (izquierda)
- Arriba: la marca **SGC Web** (lleva al inicio), el **buscador** y el **proyecto** en el que está trabajando.
- Los módulos están agrupados según los capítulos de la norma ISO 9001: **Contexto y liderazgo**, **Planificación**,
  **Apoyo**, **Operación**, **Evaluación y mejora**, **Configuración** y, para administradores, **Administración**.
  Al pasar el mouse sobre un módulo, el texto de ayuda muestra el numeral ISO.
- Al pie: su nombre y foto (lleva a **Mi cuenta**) y el botón **Contraer menú**.
- El menú tiene dos modos: **barra** (solo íconos; al pasar el mouse sobre un grupo se abre su lista) y
  **expandido** (con nombres y grupos desplegables). El botón del pie cambia entre los dos y la app recuerda su elección.
- Solo aparecen los módulos que su rol le permite ver. Los que dicen **vacío** aún no tienen contenido.

### Encabezado de cada página (arriba)
- El **título** del módulo y la **ruta** (grupo › módulo).
- La **estrella** (**Agregar a favoritos**): el módulo queda en sus favoritos del buscador y del panel de inicio.
- El botón **Volver**: regresa a la pantalla anterior real, con la misma torre o registro que tenía abierto.
- A la derecha, los botones de la página, siempre en este orden:
  1. Los propios del módulo (vistas, gráficos, filtros…).
  2. **Campos** → **Importar** → **Exportar**.
  3. **Eliminar** → **Editar** → **Agregar** o **Guardar** (el principal, siempre en la esquina derecha).

### Los botones son íconos
Casi todos los botones muestran **solo un ícono**. **Deje el mouse quieto sobre un botón** para ver su nombre.
Íconos más comunes:

| Ícono | Qué hace |
|---|---|
| ＋ (Agregar) | Crear un registro nuevo |
| Lápiz (Editar) | Habilita la edición de la tabla o del registro |
| Disquete (Guardar) | Guarda los cambios |
| ✕ (Cancelar) | Descarta y cierra |
| Papelera (Eliminar) | Envía a la Papelera (se puede recuperar) |
| Bandeja con flecha ↓ (Importar) | Cargar datos desde Excel |
| Bandeja con flecha ↑ (Exportar) | Descargar en Excel o PDF |
| Base de datos con número (Base de datos / Cargas) | Ver y eliminar las cargas de Excel hechas antes |
| Embudo (Filtro) | Mostrar u ocultar filtros |
| Pantalla completa | Ocupa toda la pantalla con la tabla |

## Cómo hacer…

### Cómo pedir ayuda al asistente
1. Toque el botón **?** (**Asistente de ayuda**) a la derecha del encabezado de cualquier pantalla.
2. Escriba su pregunta con sus palabras, por ejemplo «¿cómo subo una nueva versión de un documento?» o
   «¿qué puedo hacer aquí?».
3. El asistente, **π**, responde con los pasos del manual y muestra de qué capítulo salen. Si la respuesta es de otro módulo,
   ofrece **Ir a** ese módulo; debajo propone otras secciones que también pueden servir.
4. **Nueva conversación** empieza de cero; **Cerrar** (o Esc) oculta el panel. La conversación se conserva mientras
   cambia de pantalla.

El asistente funciona dentro de la app, sin conexión a servicios externos: solo sabe lo que dice este manual. Si no
encuentra la respuesta, lo dice y sugiere secciones parecidas. Mientras usted escribe, π mira la caja de texto; si no,
sigue el puntero. Puede tocar la cabeza o las manos de π varias veces para ver cómo cambia su reacción.
También puede agarrar la cabeza, arrastrarla y soltarla. Si la suelta con impulso, rebota y luego vuelve sola al panel.
Con «reducir movimiento» activado en su sistema, π muestra gestos estáticos y vuelve de inmediato al soltarlo.

### Cómo encontrar cualquier cosa (buscador)
1. Toque **Buscar…** en el menú o presione **Ctrl + K**.
2. Escriba parte de lo que busca: un código de documento, un número de torre, un nombre de equipo, un texto de un
   riesgo… No importan tildes ni mayúsculas.
3. Arriba salen los módulos, sus favoritos y recientes; debajo, los registros encontrados en toda la app.
4. Toque un resultado: la app abre el módulo **con ese registro ya seleccionado**.

### Cómo cambiar de proyecto
1. En el menú, debajo del buscador, está el nombre del proyecto actual.
2. Si usted pertenece a varios proyectos, toque el nombre y elija otro en la lista (**Cambiar de proyecto**).
3. La app recarga con los datos del proyecto elegido. Cada proyecto tiene su propia información.

Algunos módulos son **de la empresa** y se ven iguales en todos los proyectos (por ejemplo Direccionamiento
estratégico y Certificaciones).

### Cómo editar una tabla
1. Toque el **lápiz (Editar)** en el encabezado. La tabla pasa a modo edición.
2. Cambie los valores. Junto a **Guardar** aparece cuántos cambios lleva sin guardar.
3. Toque **Guardar**. Nada se guarda hasta ese momento. Para descartar, toque **Cancelar**.
4. Si intenta salir de la pantalla con cambios pendientes, la app le pide confirmar.

### Cómo llenar un formulario
- El título de cada casilla aparece dentro de ella y sube cuando escribe.
- El asterisco (*) marca los campos obligatorios. Si falta alguno, la app dice «Falta diligenciar: …».
- Algunos pasos del formulario tienen el botón **N/A**: márquelo si ese paso no aplica; sus campos dejan de exigirse.
- Los botones **Cancelar** y **Guardar** están **al pie** del formulario. Al guardar, vuelve a donde estaba.

### Cómo acomodar las columnas de una tabla
- **Mover una columna:** arrastre su encabezado a otra posición.
- **Congelar una columna** (que no se mueva al desplazarse a la derecha): toque la **chincheta** del encabezado.
- **Densidad** (Amplia, Normal, Compacta) y **Restablecer columnas**: botón de configuración de tabla en el encabezado.
- La app recuerda el orden en su navegador.

### Cómo importar desde Excel
1. Toque **Importar** (bandeja con flecha ↓, o el ícono de Excel).
2. Elija el archivo, la **Hoja** y la **Fila de encabezados**.
3. Indique a qué columna de la app va cada columna del Excel (o «No importar»).
4. Confirme. Volver a cargar el mismo archivo **no duplica** registros: los actualiza. Una celda vacía en el Excel no
   borra el dato que ya existía.
5. Cada importación queda como una **carga** (botón **Base de datos**). Si se equivocó, elimine esa carga y sus
   registros van a la Papelera.

### Cómo recuperar algo que eliminó (Papelera)
Nada se borra de inmediato: **Eliminar** envía a la Papelera.
1. Menú › **Papelera**.
2. Busque el registro (por texto, módulo o fecha).
3. Selecciónelo y toque **Restaurar**.
4. **Eliminar definitivamente** / **Borrar para siempre** no se puede deshacer.

Antes de eliminar, la app muestra el **impacto**: qué otros registros dependen de ese (por ejemplo, los archivos de
un documento o los planes de un hallazgo).

### Cómo cambiar su contraseña, foto o seguridad (Mi cuenta)
Toque su nombre al pie del menú. En **Mi cuenta** puede:
- **Cambiar contraseña** y **Cambiar foto** (PNG, JPG o WebP, máx. 2 MB) o **Quitar foto**.
- Activar la **Verificación en dos pasos** con una aplicación autenticadora y **Generar códigos de recuperación nuevos**.
- Ver **Mis proyectos**.
- Elegir sus **Avisos por correo**.
- Vincular el **Bot de Telegram** (**Generar código de vinculación**).
- En **Privacidad y sesiones**: **Cerrar la sesión en todos los demás equipos** y **Descargar mis datos (JSON)**.

Los datos que dicen **Lo cambia el administrador** (por ejemplo, su rol) no los puede cambiar usted.

### Cómo recuperar su contraseña
1. En **Iniciar sesión**, toque **¿Olvidó su contraseña?**
2. Escriba su correo y toque **Enviar enlace**.
3. Abra el enlace del correo y escriba la contraseña nueva.

Si perdió el teléfono con la verificación en dos pasos, en la pantalla del código toque
**¿Perdió el teléfono? Use un código de recuperación**.

### Cómo cambiar los colores de la app
Menú › Configuración › **Temas**: elija un tema (por ejemplo **Oscuro** o **Cristal**).

## Permisos
Cada usuario tiene un **rol** en cada proyecto. El rol define qué puede hacer en cada módulo:

| Rol | Qué puede hacer |
|---|---|
| Consulta sin descarga | Ve la información pero no descarga ni exporta archivos (cliente o interventoría) |
| Consulta | Ve y descarga. No modifica nada |
| Colaborador | Consulta, registra seguimientos, observaciones y evidencias, y crea registros nuevos |
| Editor | Crea, edita, importa y envía a la Papelera en los módulos operativos |
| Coordinador / aprobador | Editor que además aprueba versiones, cierra planes, verifica eficacia y ve costos |
| Administrador del proyecto | Todo dentro del proyecto: configuración, campos, vistas, formularios, catálogos y Papelera |

El administrador puede dar o quitar permisos puntuales a una persona en un módulo. Los módulos de costos
(**Costos y presupuestos**, **Costos de no calidad**) son sensibles y no los ve cualquier rol.

## Problemas frecuentes
- **«No veo un módulo en el menú»** → su rol no tiene permiso para verlo en este proyecto, o está en otro proyecto.
  Revise el proyecto en el menú; si es el correcto, pida acceso al administrador.
- **«Acceso denegado» al abrir un enlace** → el enlace es de un módulo que su rol no puede ver. Pida acceso al administrador.
- **«No veo el botón Editar, Agregar o Eliminar»** → su rol es de consulta en ese módulo. Los botones que no puede usar
  no aparecen.
- **«No puedo descargar»** → su rol es **Consulta sin descarga**.
- **«No encuentro qué hace un botón»** → deje el mouse quieto encima: aparece su nombre.
- **«Cambié algo y no se guardó»** → en modo edición hay que tocar **Guardar**; al salir sin guardar, los cambios se pierden.
- **«Aún no tiene proyectos»** al ingresar → su cuenta existe pero no está asignada a ningún proyecto. Pida al
  administrador que lo agregue.
- **«Borré algo por error»** → búsquelo en la **Papelera** y toque **Restaurar**.
- **«El menú solo muestra íconos»** → está en modo barra. Toque el botón del pie del menú para expandirlo.

## Preguntas frecuentes

### ¿Cómo vuelvo a la pantalla donde estaba?
Use el botón de **Volver** (flecha) del encabezado: lleva a la pantalla anterior de esa pestaña con la misma torre, filtro o
elemento que tenía abierto. No use el Atrás del navegador si está editando: la app le avisa si hay cambios sin guardar.

### ¿Por qué los botones no tienen texto?
En toda la app los botones son solo íconos. Deje el mouse encima para ver qué hace cada uno (aparece su nombre). El botón
principal (Guardar, Agregar) siempre es el último, en la esquina derecha.

### ¿Qué hago si guardé algo por error o lo borré?
Lo eliminado no se pierde: va a la **Papelera** (menú › Papelera), desde donde se restaura. Antes de eliminar, la app le
muestra qué otros registros dependen de lo que va a borrar.

### ¿Por qué no me deja editar, crear o eliminar?
Su rol tiene permiso de ver pero no de editar, crear o eliminar en ese módulo (los botones no aparecen o salen apagados).
También pasa en registros cerrados o versiones aprobadas, que solo se cambian desde su borrador. Pida el permiso al
administrador (Administración › Roles).

### ¿Por qué no veo un módulo en el menú?
Su rol no tiene permiso **Ver** en ese módulo o el módulo está deshabilitado en el proyecto. Pídalo al administrador
(Administración › Usuarios y acceso). Si cambió de proyecto, cada proyecto tiene sus propios permisos.

### ¿Cómo encuentro una torre, un plano o un documento sin saber en qué módulo está?
Pulse **Ctrl + K** (o la lupa del menú) y escriba parte del código o del nombre. El buscador recorre toda la app y abre el
registro exacto.

### ¿Cómo cambio el tema o el color de la app?
Menú › Configuración › **Temas**. El tema se guarda en su navegador.

### ¿Cómo cambio de proyecto?
Toque el nombre o la sigla del proyecto en la parte superior del menú y elija otro. La app abre el mismo módulo en el
proyecto nuevo si usted tiene acceso.

### ¿Qué significa «Cambios sin guardar»?
Está en modo edición y modificó algo que todavía no guarda. Toque **Guardar** o **Cancelar**; si sale de la pantalla, la app
le pide confirmar para no perder lo escrito.

### ¿Cómo exporto una tabla a Excel?
En las listas, el botón **Exportar** del encabezado descarga lo que está viendo (con los filtros aplicados).

### ¿El asistente de ayuda usa internet o inteligencia artificial externa?
No. El asistente busca solo en el manual de la app, dentro del servidor. Nada de lo que escriba sale de la app.

## Relacionado
- [Trazado](trazado.md) — mapa y hoja de vida de cada torre.
- [Documentos](documentos.md) y [Planos](planos.md) — listados maestros.
- [Planes de acción](planes-accion.md) — tratamiento de hallazgos.
