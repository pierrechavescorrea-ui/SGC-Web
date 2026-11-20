---
modulo: configuracion
titulo: Configuración (Actividades, Tiros, Catálogos, Editor, Temas)
menu: Configuración
rutas: [/actividades, /tiros, /catalogos, /editor, /temas]
palabras: [configuración, actividades, parámetros, sub actividades, planilla, tiros, tablero de tiros, catálogos, listas desplegables, opciones, ítems, íconos, galería, plantillas de proyecto, editor, diseñador de formularios, formularios, campos nuevos, vistas, tableros, temas, colores, oscuro, cristal, pastel]
opciones:
  - Eliminar parámetro: /actividades | Eliminar parámetro
  - Seleccionar las torres que coinciden: /tiros | Seleccionar las torres que coinciden
  - actividades: /actividades
  - tiros: /tiros
  - Catálogos: /catalogos
  - Nueva actividad: /actividades | Nueva actividad
  - Eliminar actividad: /actividades | Eliminar actividad
  - Tablero: /tiros | Tablero
  - Tabla: /tiros | Tabla
  - Control de cambios: /tiros | Control de cambios
  - 'Editar: mover torres entre tiros, renombrar y ordenar: /tiros | Editar: mover torres entre tiros, renombrar y ordenar'
  - Nuevo catálogo: /catalogos | Nuevo catálogo
  - Editar catálogo: /catalogos | Editar catálogo
  - Abrir ítems: /catalogos | Abrir ítems
  - Galería de íconos: /catalogos?pestana=iconos
  - Plantillas de proyecto: /catalogos?pestana=plantillas
  - Formularios: /editor?pestana=formularios
  - Vistas (tableros de los registros): /editor?pestana=vistas
  - Oscuro: /temas | Oscuro
  - Cristal: /temas | Cristal
actualizado: 2026-11-19
---

# Configuración

## Para qué sirve
Aquí se ajusta la app a cada proyecto: las **actividades** de obra que se controlan, los **tiros** de tendido,
las **listas desplegables** (catálogos), el **diseño de los formularios** y las **vistas**, y los **colores** (temas).
Normalmente lo usa el administrador del proyecto.

## Cómo entrar
Menú › **Configuración** (ícono de engrane) › Actividades, Tiros, Catálogos, Editor o Temas.

---

## Actividades
Lista de actividades de obra (por grupo) con sus **parámetros** (sub actividades) que se controlan por torre o pata
y que alimentan la **Matriz PIE** y las pestañas de actividad de la hoja de vida.
- **Nueva actividad**, **Editar**, **Mover a otro grupo**, **Eliminar actividad**, **Eliminar parámetro** (a la Papelera).
- La casilla **Planilla** en un parámetro hace que salga en las planillas del bot de Telegram.

## Tiros
Organiza las torres en **tiros** de tendido.
- Vistas: **Tablero** (una columna por tiro, con las torres como fichas de color según su estado), **Tabla** y
  **Control de cambios**.
- **Cómo mover torres entre tiros:**
  1. Toque **Editar: mover torres entre tiros, renombrar y ordenar**.
  2. Seleccione torres (clic; Mayús + clic = rango; o **Seleccionar las torres que coinciden** con la búsqueda).
  3. Arrástrelas a otra columna, o use **Pasar las torres seleccionadas a otro tiro**, **Crear un tiro nuevo con las
     seleccionadas** o **Dejar las seleccionadas sin tiro**.
  4. Renombre un tiro en su cabecera (**Nombre del tiro**) y ordénelo con las flechas.
  5. **Deshacer el último paso** si se equivoca. Toque **Guardar** (puede escribir un motivo).
- Cada cambio queda en **Control de cambios** (qué torre pasó de qué tiro a cuál, cuándo y por qué).

## Catálogos
Las **listas desplegables** de toda la app (estados, tipos, procesos, orígenes…).
- A la izquierda, el menú por módulo: elija un módulo para ver solo sus catálogos.
- **Nuevo catálogo** (con **Opciones iniciales**, una por línea), **Editar catálogo**, **Abrir ítems**, **Nuevo ítem**.
- Cada opción puede tener formato (color, fondo, forma, negrita): así se ve en las tablas.
- **Renombrar una opción** actualiza los registros que ya la usaban.
- Pestañas extra: **Galería de íconos** y **Plantillas de proyecto** (libro de Excel para cargar catálogos, tiros y torres de un proyecto nuevo).

## Editor (formularios y vistas)
- **Formularios**: el diseñador de formularios. Permite mover campos entre pasos, renombrarlos, cambiar su tamaño y
  **agregar campos nuevos** (texto, número, lista, fecha, sí/no, archivos adjuntos…). Un campo de lista puede tener
  **Campos según la opción** (solo aparecen si se elige esa opción). **Restablecer** vuelve al diseño original.
  Los campos marcados como fijos u obligatorios por la app no se pueden ocultar.
- **Vistas (tableros de los registros)**: diseñe cómo se ve el detalle de un registro (por ejemplo Plan por plan en
  Planes de acción) con widgets; marque una como predeterminada.

## Temas
Elija una tarjeta para aplicar sus colores a la app. **Pastel** es un tema claro con fondos crema, lavanda y menta, y texto gris pizarra. La elección se guarda en su navegador.

---

## Permisos
Actividades, Tiros, Catálogos y Editor son módulos de **configuración**: los maneja el
**Administrador del proyecto** (permisos ConfigurarCampos y EditarVistas para formularios y vistas). Temas lo puede usar cualquiera.

## Problemas frecuentes
- **«Falta una opción en una lista desplegable»** → agréguela en **Catálogos** (busque el catálogo por su nombre o por módulo).
- **«Agregué un campo al formulario y no sale en la tabla»** → revise en el diseñador a qué tabla pertenece; los campos
  nuevos de la tabla del módulo también salen como columna.
- **«Moví torres de tiro y no se guardó»** → en el tablero los cambios quedan pendientes hasta **Guardar**.
- **«No veo Configuración en el menú»** → su rol no es administrador del proyecto.

## Preguntas frecuentes

### ¿Dónde creo una lista desplegable nueva?
Configuración › **Catálogos**: cree el catálogo, escriba sus opciones (una por línea) y elíjalo en el campo que lo use. Cada
opción puede tener color, fondo y forma.

### Si renombro una opción de un catálogo, ¿se pierden los registros que la usaban?
No. La app actualiza los registros que tenían esa opción (campos de lista y los módulos que la usan).

### ¿Cómo agrego un campo nuevo a un formulario de la app?
Configuración › **Editor** › Formularios: elija el formulario, use «Campo nuevo», acomódelo en el paso y guarde. El campo
aparece también como columna de la lista del módulo.

### ¿Cómo dejo un formulario como estaba al principio?
En el Editor, **Restablecer** vuelve el formulario al diseño original de la app (las versiones anteriores quedan guardadas).

### ¿Cómo cambio el orden o el nombre de los tiros?
Configuración › **Tiros**: en el tablero arrastre torres entre tiros, renombre en la cabecera y guarde. Cada cambio queda en el
control de cambios.

### ¿Dónde se configuran las actividades y parámetros de obra?
Configuración › **Actividades**: actividades, sus parámetros y la casilla «Planilla» para que salgan en el bot.

### ¿Cómo cambio el aspecto de la app?
Configuración › **Temas** (por ejemplo, **Pastel**, **Oscuro** o **Cristal**). Se guarda en su navegador.

### ¿Dónde quedó el Bot Telegram?
Ahora está en Administración › Bot de Telegram (lo ve el administrador).

## Relacionado
- [Uso general](general.md) — formularios y tablas.
