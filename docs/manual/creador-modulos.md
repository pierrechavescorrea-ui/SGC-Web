---
modulo: general
titulo: Creador de módulos
menu: Administración › Proyectos › Creador de módulos
rutas: [/admin/modulos, /admin/modulos/{id}, /mod/{clave}]
palabras: [crear módulo, módulo nuevo, inspecciones, formulario nuevo, pasos, campos, vínculo, enlazar, publicar módulo, plantilla, registro, lista de chequeo]
opciones:
  - Creador de módulos: /admin/modulos
  - Módulo nuevo: /admin/modulos | !Módulo nuevo
actualizado: 2026-11-19
---

# Creador de módulos

## Para qué sirve
Permite que el administrador del SGC arme módulos nuevos sin programar, por ejemplo **Inspecciones**, **Permisos de
trabajo** o **Visitas de obra**. Un módulo se arma **por pasos**. Cada paso tiene campos: texto, número, fecha, listas,
sí/no, archivos y **vínculos** a la información que ya existe en la app (torres, ensayos, equipos, planos, documentos,
materiales, planes de acción, riesgos y otros módulos creados). Al **publicarlo** aparece en el menú de todos los
proyectos, con permisos por rol, Papelera, buscador, Campos (columnas, fórmulas y formatos) y Dossier.

## Cómo entrar
Menú › Administración › Proyectos › **Creador de módulos**. Solo lo ve el administrador del SGC.
Los módulos publicados se abren desde su capítulo del menú (el que se eligió al diseñarlos).

## Qué hay en la pantalla
- **Lista de módulos**: una tarjeta por módulo con su ícono, capítulo del menú y estado: *Borrador* (nunca publicado),
  *Publicado vN*, *cambios sin publicar* o *Archivado*. El botón **Módulo nuevo** (＋) pide el nombre.
- **Diseñador** (al abrir una tarjeta), como un formulario de Windows:
  - **Herramientas** (barra oscura de íconos a la izquierda; al pasar el mouse dice qué es cada una). Grupos: entrada de
    datos (texto, párrafo, número, fecha, lista o catálogo, sí/no, lista de chequeo, firma, foto, adjuntos), enlace a la app
    (selector de registro: torres, equipos, planos…), herramientas de la app (tabla de datos, gráfico, indicador, estadística,
    mapa, visor PDF, modelo 3D, carpetas y archivos, línea de tiempo) y diseño (etiqueta, separador, grupo). El punto azul
    marca las que se enlazan a datos de la app. El botón ▮▮ cambia entre 1, 2 o 3 columnas.
  - **Lienzo** (centro): una pestaña por paso (＋ agrega otro) y una rejilla de 12 columnas. Se arrastra una herramienta al
    lienzo (o doble clic para ponerla al final), se mueve arrastrando el control y se cambia de tamaño con la esquina azul.
  - **Esquema**: el árbol pasos › controles; clic = seleccionar.
  - **Propiedades** del control elegido: etiqueta, ayuda, ancho × alto, paso; **Modo** (*Editable*, *Solo vista* o
    *Calculado*), obligatorio, columna en la lista y **Visible si** (otro campo = un valor); y en **Datos**: opciones o
    catálogo de una lista, ítems de una lista de chequeo, unidad y decimales; para herramientas de la app la **Fuente** (de
    qué módulo salen los datos), **Filtrar por** (un selector de registro del mismo módulo), la columna que enlaza, columnas,
    cálculo (conteo, suma, promedio, mínimo, máximo, porcentaje), agrupación, fecha, latitud/longitud y tipo de gráfico. Sin
    control elegido muestra el **paso** (título, indicación, N/A, mover, quitar) y el **módulo** (nombre, ícono, menú, código,
    estados).
  - Cada panel (herramientas, Esquema, Propiedades) tiene **pin**: suelto queda flotando y se arrastra por su título a donde
    quiera; **Fijar todos los paneles** los devuelve a los costados. El navegador recuerda cómo los dejó.
- Encabezado del diseñador: **Diseño**, **Vista previa**, **Versiones publicadas**, deshacer y rehacer, abrir el módulo,
  **Duplicar**, **Archivar**/**Reactivar** (o **Eliminar** si nunca se publicó), **Descartar** cambios sin publicar,
  **Guardar borrador** y **Publicar**.
- Atajos: Supr quita el control elegido, Ctrl+Z / Ctrl+Y deshacen y rehacen, Ctrl+D duplica, Ctrl+S guarda, flechas mueven
  el control (Mayús + flechas cambian su tamaño), Esc quita la selección.

## Cómo hacer…
### Cómo crear un módulo (ejemplo: Inspecciones)
1. Toque **Módulo nuevo**, escriba «Inspecciones» y confirme.
2. Sin control elegido, en Propiedades › Módulo: capítulo del menú (por ejemplo *Operación*), ícono, prefijo *INS* y
   estados (Programada, Ejecutada, Cerrada con *fin*).
3. Arrastre un **Selector de registro** y en Datos › Fuente elija *Trazado* (torres). Agregue Fecha, una Lista «Inspector».
4. Arrastre una **Tabla de datos**: Fuente *Resultados de ensayo*, Filtrar por *Torre* y como columna que enlaza la columna
   de la torre. Así cada inspección muestra los ensayos de la torre elegida. Lo mismo con un **Gráfico**, un **Indicador**,
   el **Mapa**, el **Visor PDF** (planos) o el **Modelo 3D**.
5. Agregue pasos (pestañas) con lista de chequeo, fotos y firma.
6. Revise la **Vista previa**, toque **Guardar borrador** y luego **Publicar**.
7. En Administración › **Roles** dé permisos al nuevo módulo a cada rol. Hasta entonces solo lo ve el administrador.

### Cómo cambiar un módulo que ya se usa
Cambie lo que necesite y toque **Publicar**: los registros guardados se conservan. Renombrar campos o pasos, moverlos,
agregar campos nuevos u opciones es seguro. Quitar un campo no borra sus datos: la columna queda oculta en la tabla.
Cambiar el tipo de un campo publicado a otro incompatible (por ejemplo, de Texto a Número) no se permite: cree un
campo nuevo y quite el anterior.

### Cómo eliminar un módulo
1. Abra el módulo y toque **Eliminar** en el encabezado. Si nunca se publicó, se borra el diseño. Si ya se publicó, sale del
   menú y de los permisos en todos los proyectos, pero sus registros se conservan: queda en la sección **Eliminados** al pie
   de la lista de módulos.
2. Desde **Eliminados**: **Restaurar** lo devuelve al menú con sus registros; **Eliminar definitivamente** muestra cuántos
   registros y adjuntos tiene en cada proyecto y, después de escribir el nombre exacto, borra el diseño, su historia y todos
   sus registros, valores, vínculos y adjuntos. Esto no se puede deshacer.
Si solo quiere ocultarlo por un tiempo, use **Archivar**.

### Cómo volver a una versión anterior
**Versiones publicadas** › botón de restaurar en la versión: queda en el borrador para revisarla y publicarla otra vez.

### Cómo usar un módulo creado
En su capítulo del menú: la lista muestra los registros con buscador y filtro por estado; **Campos** permite columnas
calculadas y formatos condicionales como en las demás listas, y **Exportar** baja la tabla. **Nuevo registro** (＋) abre
el formulario por pasos. Los vínculos se eligen con ＋ (ventana con buscador y las columnas del módulo de destino) y cada
ficha elegida abre el registro enlazado. Los estados se cambian con los botones de color del encabezado del formulario.

### Cómo ver cifras y gráficos de un módulo
En la página del módulo, el botón **Tablero (cifras y gráficos)** muestra el total, los creados este mes, cuántos hay en
cada estado (clic = la lista filtrada por ese estado), los registros por mes de los últimos 12 meses, el reparto de cada
lista, sí/no y vínculo, y mínimo, promedio, máximo y total de cada número. Respeta la búsqueda y el filtro de estado.

### Cómo ver los registros de una torre
Si un módulo tiene un vínculo a **Trazado** (torres), la hoja de vida de cada torre muestra la pestaña **Módulos** con
los registros que la enlazan; desde allí ＋ crea un registro nuevo ya enlazado a esa torre.

## Permisos
- Diseñar, publicar y archivar: solo el administrador del SGC.
- En cada módulo publicado, por rol (Administración › Roles): **Ver** (lista y registros), **Crear**, **Editar**,
  **Eliminar** (a la Papelera), **Aprobar** (pasar a un estado *fin* o editar un registro cerrado), **Descargar**
  (exportar y Dossier) y **Configurar campos**.
- Los vínculos solo muestran registros de módulos que el usuario puede ver.

## Problemas frecuentes
- **«Publiqué y nadie lo ve»** → falta dar el permiso **Ver** a los roles en Administración › Roles, o el módulo está
  deshabilitado en el proyecto (Administración › Proyectos).
- **«No me deja publicar»** → el aviso amarillo dice qué falta: pasos sin campos, listas sin opciones, vínculos sin
  módulo de destino, nombres de campo repetidos o un cambio de tipo incompatible.
- **«No puedo editar un registro cerrado»** → está en un estado *fin*: lo cambia quien tiene **Aprobar**.

## Preguntas frecuentes

### ¿Puedo crear un módulo de Inspecciones sin programar?
Sí. Administración › **Creador de módulos** › Módulo nuevo. Arrastre herramientas al lienzo (texto, listas, fechas, firma,
fotos, selector de torre, tablas y gráficos de otros módulos), ajuste sus propiedades y **Publicar**.

### ¿Cómo hago que una tabla muestre solo los ensayos de la torre elegida?
Agregue un **Selector de registro** con Fuente = torres. Luego en la **Tabla de datos**: Fuente = resultados de ensayo,
**Filtrar por** = ese selector y «Columna enlace» = la columna de la torre en los ensayos.

### ¿Qué significan Editable, Solo vista y Calculado?
Editable: el usuario lo llena. Solo vista: se muestra pero nadie lo cambia desde el formulario. Calculado: la cifra la calcula
la app (por ejemplo un indicador).

### ¿Si cambio el módulo pierdo los registros que ya existen?
No. Renombrar, mover, agregar campos u opciones es seguro. Quitar un campo deja sus datos guardados como columna oculta. Lo
único que no se permite es cambiar un campo a un tipo incompatible.

### ¿Por qué no me deja publicar?
El aviso amarillo dice qué falta: pasos sin campos, listas sin opciones, vínculos o herramientas sin fuente, filtros que apuntan
a un selector que ya no existe, listas de chequeo sin ítems o nombres de campo repetidos.

### ¿Cómo muevo o agrando un control?
Arrástrelo por el lienzo; para cambiar el tamaño, tire de la esquina azul. También: flechas para mover y Mayús + flechas para
agrandar. Ctrl+Z deshace.

### ¿Cómo oculto los paneles para tener más espacio?
Cada panel (herramientas, Esquema, Propiedades) tiene un pin: suelto queda flotando y se arrastra por su título. **Fijar todos
los paneles** los devuelve a los costados.

### ¿Cómo hago que un campo aparezca solo en ciertos casos?
En Propiedades › **Visible si**, elija una lista o un Sí/No del módulo y el valor que lo muestra.

### ¿Cómo recupero un módulo que eliminé?
Al pie de la lista de módulos, en **Eliminados**, toque Restaurar. «Eliminar definitivamente» borra también sus registros y no se
puede deshacer.

### Publiqué y los usuarios no ven el módulo, ¿qué falta?
Darle permiso Ver a los roles en Administración › Roles, y que el módulo esté habilitado en el proyecto.

## Relacionado
- [Administración](administracion.md) — roles y módulos por proyecto.
- [Dossier](dossier.md) — los módulos creados salen como una carpeta más.
- [Configuración](configuracion.md) — catálogos que alimentan las listas.
