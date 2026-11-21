---
modulo: trazado
titulo: Trazado
menu: Operación › Trazado
rutas: [/trazado, /trazado/tabla-torres, /trazado/datos-adicionales]
palabras: [trazado, torre anterior, torre siguiente, siguiente torre, navegar entre torres, mapa, torre, torres, hoja de vida, ficha, pata, patas, elemento, elementos, KMZ, coordenadas, ubicación, este, norte, sistema de coordenadas, magna sirgas, DWG, DXF, capas, tabla de torres, cotas, vanos, alturas, revisión de la tabla, deshacer revisión, vincular planos, materiales de la torre, ensayos de la torre, actividades, parámetros, unificar torres, renombrar torre]
opciones:
  - Trazado (KMZ): /trazado | Base de datos
  - Elementos (DWG/DXF): /trazado | Base de datos
  - Ver, ocultar, renombrar o eliminar capas: /trazado | Base de datos
  - 'Corregir columnas: /trazado/tabla-torres?revisiones=1 | Corregir columnas: deshace esta revisión y vuelve a la pantalla de columnas con el mismo archivo'
  - Comparar A → B: /trazado/tabla-torres?revisiones=1 | Revisión anterior
  - Trazado: /trazado
  - Mapa: /trazado
  - Vista Mapa: /trazado
  - Tabla: /trazado?vista=tabla
  - Vista Tabla: /trazado?vista=tabla
  - Tabla de torres (cotas, vanos, alturas): /trazado/tabla-torres
  - Tabla de torres: /trazado/tabla-torres
  - Tabla de torres (Excel): /trazado/datos-adicionales
  - Unificar torres duplicadas: /trazado | Unificar torres duplicadas
  - Torre anterior: /trazado | Torre anterior
  - Torre siguiente: /trazado | Torre siguiente
  - Base de datos: /trazado | Base de datos
  - Campos: /trazado?vista=tabla | Campos
  - Exportar: /trazado?vista=tabla | Exportar
  - Ver revisiones y cambios por torre: /trazado/tabla-torres?revisiones=1
  - Revisiones de la tabla (cambios por torre, deshacer): /trazado/tabla-torres?revisiones=1
  - Ver sus cambios (contra la revisión anterior): /trazado/tabla-torres?revisiones=1 | Ver sus cambios (contra la revisión anterior)
  - Deshacer esta revisión: /trazado/tabla-torres?revisiones=1 | Deshacer esta revisión (devuelve los valores anteriores)
  - Descargar el archivo de esta revisión: /trazado/tabla-torres?revisiones=1 | Descargar el archivo de esta revisión
actualizado: 2026-11-21
---

# Trazado

## Para qué sirve
Muestra la línea en un **mapa** con todas sus torres y reúne en la **hoja de vida** de cada torre todo lo que tiene
que ver con ella: archivos, planos, ensayos, materiales, información técnica, riesgos, planes de acción, actividades
de obra y los cambios que ha tenido. También es donde se carga la **tabla de torres** (coordenadas de cada torre
y de cada pata, cotas, vanos, alturas), con control de revisiones, y el trazado (KMZ). ISO 9001 · 8.5.

## Cómo entrar
- Menú › **Operación** › **Trazado**.
- Buscador (**Ctrl + K**): escriba el número o nombre de la torre; abre la torre seleccionada.
- Desde **Avance de obra** y **Matriz PIE**, el botón de salto de torre lleva a la hoja de vida de esa misma torre.
- Bot de Telegram: **/torre 45** muestra la hoja de vida en el teléfono.

## Qué hay en la pantalla
**Encabezado** (de izquierda a derecha):
- Vistas: **Mapa** · **Tabla** · **Tabla de torres (cotas, vanos, alturas)**.
- Con una torre seleccionada: botones para saltar a la misma torre en **Avance de obra** o **Matriz PIE**.
- **Base de datos** (Bases de datos): cargar la tabla de torres (con coordenadas), el trazado (KMZ) y DWG/DXF, y ver lo cargado.
- **Unificar torres duplicadas**.
- En la vista Tabla: **Campos** y **Exportar**.

**Vista Mapa**: el mapa con las torres y las capas cargadas, y un panel con la lista de torres (**Buscar por nombre...**).
Al tocar una torre (en el mapa o en la lista) se abre su panel con sus **elementos**.

**Vista Tabla**: todas las torres del trazado en una tabla, con las columnas del proyecto.

### Elementos de una torre
Cada torre tiene una lista de elementos:
- **Hoja de vida** (siempre la primera; no se renombra ni se elimina): reúne todo lo de la torre.
- Las **patas** (Pata A, B, C, D…) y otros elementos (trincho, cuneta, gavión…).

Junto a cada elemento: **Abrir ficha**, casilla **Ver en el mapa**, **Ícono en el mapa**, **Color en el mapa** y
**Más opciones**. El asa ⠿ (**Arrastrar para reordenar**) cambia el orden.

### Pestañas de la ficha (hoja de vida o elemento)
Las pestañas son íconos; deje el mouse encima para ver su nombre:
- **Archivos y ensayos**: carpetas de archivos de la torre, **planos vinculados**, ensayos con sus informes y
  **materiales** relacionados.
- **Información**: datos técnicos de la torre (tabla de torres y campos adicionales). Tiene buscador
  (**Buscar campo o valor...**).
- **Riesgos**: riesgos materializados en la torre, en tarjetas desplegables (**Ver riesgo materializado**).
  La etiqueta **Por plan de acción** indica que el riesgo aparece porque un plan de acción está ubicado en la torre.
- **Planes de acción**: planes ubicados en la torre (**Ver plan de acción**, **Editar plan**).
- **Cambios**: lo que cambió en esta torre en cada revisión de la tabla de torres, KMZ o coordenadas.
- **Módulos** (solo si algún módulo creado tiene un vínculo a torres): los registros de esos módulos que enlazan esta
  torre (por ejemplo, sus inspecciones), con su estado; ＋ crea uno nuevo ya enlazado a la torre.
- Una pestaña por cada **actividad** de obra (con su nombre): parámetros, estado (Matriz PIE), fechas y evidencias.

Las pestañas se reordenan arrastrándolas; el orden es el mismo para todas las torres.
En la ficha de un elemento solo se ve lo de ese elemento; en la hoja de vida, lo de toda la torre.

## Cómo hacer…

### Cómo abrir la hoja de vida de una torre
1. En la vista **Mapa**, busque la torre en la lista (**Buscar por nombre...**) o tóquela en el mapa.
2. En su lista de elementos, toque **Hoja de vida** (**Abrir ficha**).
3. Use las pestañas para ver archivos, información, riesgos, planes, cambios o actividades.
4. Para pasar a otra torre sin cerrar la ficha, use las flechas **Torre anterior** y **Torre siguiente** del encabezado
   (orden por número de torre). Se queda en la misma pestaña y en el mismo elemento (hoja de vida, Pata A…); si la otra
   torre no tiene ese elemento, abre su hoja de vida.

Atajo: escriba el número de la torre en el buscador (**Ctrl + K**).

### Cómo cargar el trazado o la tabla de torres
1. Toque **Base de datos** en el encabezado.
2. Arrastre el archivo a la zona de carga o use la tarjeta que corresponda:
   - **Tabla de torres (Excel)**: trae las **coordenadas de cada torre y de cada pata** y los demás datos. Ver el
     siguiente apartado. Todo Excel que suba aquí se trata como tabla de torres.
   - **Trazado (KMZ)** (opcional): solo **crea las torres que aún no existen**; las existentes conservan la ubicación de la tabla de torres (si una no tenía ubicación, recibe la del KMZ). Elija qué torres importar (**Filtrar por nombre**, **Seleccionar filtrados**, **Quitar
     filtrados**). Puede escribir una **Referencia de esta carga (opcional)**, por ejemplo A29-9.
   - **Elementos (DWG/DXF)**: importar o inspeccionar el dibujo.
3. Si la app encuentra torres que podrían ser la misma, pregunta qué hacer. **Cancelar todo** sale sin cargar nada.

Ya **no existe** una carga aparte de coordenadas en Excel: la ubicación de las torres sale de la tabla de torres.

### Cómo cargar una revisión nueva de la tabla de torres
La tabla de torres es la fuente de la **ubicación** de las torres: el mapa, Avance de obra, Línea 3D y el tendido
las toman de aquí.
1. **Base de datos** › **Tabla de torres (Excel)**, o desde la vista **Tabla de torres**.
2. Escriba la **Revisión** (la app la sugiere del nombre del archivo, por ejemplo «A29-10»), la fecha y, si quiere,
   el comunicado.
3. Elija la hoja del libro y la fila de encabezados. En **Migrar como**, revise cada columna:
   - **Identificador de torre**: el número o código de la torre (obligatorio).
   - **Coordenada de la torre**: la columna Este y la columna Norte de la torre (en **Detalle** elija Este o Norte).
   - **Coordenada de pata**: Este y Norte de cada pata (A, B, C, D).
   - **Campo de la torre** / **Campo de la pata**: los demás datos. Cada columna se liga sola al campo con el mismo
     nombre; en **Detalle** elija un campo existente o **➕ Campo nuevo…**.
   La app reconoce sola las columnas de coordenadas por su encabezado (por ejemplo «COORDENADAS … NORTE» y «ESTE»).
4. Revise el **Sistema de coordenadas (torre y patas)**. La app lo sugiere por el encabezado (por ejemplo
   «Magna-Sirgas Bogotá») o por la magnitud de los números. Si se elige mal, las torres quedarían fuera de Colombia:
   la app las avisa en rojo y no las ubica.
5. Las filas en **rojo** son columnas repetidas (dos columnas al mismo campo): corríjalas para poder continuar.
6. Si un código se parece a una torre existente, elija en **Coincidencias**: **Omitir**, **Migrar a** esa torre, o
   **Son torres distintas: crear** la torre nueva.
7. Revise la comparación:
   - **Torres nuevas**: códigos que no existían y traen coordenadas. Se crean con su hoja de vida y sus 4 patas.
   - **Por torre**: valores que cambian y torres o patas que cambian de **Ubicación**. Puede excluir una torre o
     escribirle una observación.
   - **Se omiten**: filas sin coordenadas de torre (no se pueden crear) o marcadas Omitir.
8. Confirme. La revisión queda **Vigente** y la anterior pasa a **Anterior**. Se guarda una copia del Excel.

**Deshacer esta revisión** también devuelve las torres a su ubicación anterior y envía a la Papelera las torres que
esa revisión creó.

### Cómo ver qué cambió entre revisiones, o deshacer una
1. Vista **Tabla de torres** › **Revisiones de la tabla (cambios por torre, deshacer)**
   (o en el panel de Base de datos: **Ver revisiones y cambios por torre**).
2. La tabla muestra cada revisión: origen (Tabla de torres, KMZ o Coordenadas), estado y conteos de cambios.
3. **Ver sus cambios (contra la revisión anterior)** muestra el detalle por torre.
   **Comparar A → B** compara dos revisiones cualesquiera (nuevos, modificados, cambios de ubicación).
4. **Descargar el archivo de esta revisión** baja el Excel con el que se cargó.
5. **Deshacer esta revisión** devuelve los valores anteriores (solo la vigente). Lo que se cambió después no se toca.
6. **Corregir columnas**: si se equivocó al asignar columnas, deshace la revisión y reabre el mismo archivo con la
   configuración que usó, para corregirla.

Los cambios de una sola torre también se ven en su hoja de vida › pestaña **Cambios**.

### Cómo vincular planos a una torre o elemento
1. Abra la ficha (hoja de vida o el elemento, por ejemplo Pata A) › **Archivos y ensayos**.
2. Toque **Vincular planos del listado maestro**.
3. Busque por código, título u hoja (por ejemplo «6052» o «6052 BP3»).
4. Si el plano tiene varias hojas, elija **Vincular las N** o **Elegir manualmente** (marque las hojas y toque
   **Vincular las hojas marcadas**).
5. Elija el alcance: **solo esta ficha**, o el mismo elemento en **todas las torres de la misma familia** (BP) o del
   **mismo tipo** exacto (BP3).

Para quitar un vínculo, toque ✕ (**Quitar vínculo**) junto al plano.

### Cómo relacionar materiales con la torre
1. Hoja de vida › **Archivos y ensayos** › bloque **Materiales**.
2. Toque **Relacionar material del listado**, busque el material (**Buscar material...**) y elija **Dónde**
   (la torre, un elemento o el tiro).
3. ✕ (**Quitar relación**) lo desliga (no borra el material).

### Cómo registrar una actividad de obra en una torre o pata
1. Abra la ficha del elemento y toque la pestaña de la actividad.
2. En el parámetro, toque el **lápiz (Editar)**.
3. Marque **Aplica / completado**, elija el **Estado (Matriz PIE)**, las fechas y cargue las evidencias.
4. Toque **Guardar**.

Con **Elegir qué actividades ver en este elemento** decide qué pestañas de actividad salen en ese elemento.

### Cómo agregar un elemento a una torre
1. Seleccione la torre.
2. Toque **Agregar elemento (trincho, cuneta, gavión...)**, escriba el **Nombre del elemento** y toque **Agregar**.

### Cómo cambiar el nombre, color o ícono de una torre en el mapa
- Nombre: con la torre seleccionada, **Cambiar nombre de la torre** › **Guardar**.
- Color e ícono: **Color en el mapa** e **Ícono en el mapa** junto a la torre o al elemento.

### Cómo manejar las capas del mapa
En el panel de Base de datos, **Ver, ocultar, renombrar o eliminar capas**: casilla **Mostrar/ocultar en el mapa**,
**Renombrar** y **Eliminar del mapa**.

### Cómo unir torres repetidas
Toque **Unificar torres duplicadas** en el encabezado y siga los pasos.
> Por confirmar: pasos exactos de la ventana de unificación.

## Permisos
- **Ver**: mapa, tabla y hojas de vida. **Descargar**: archivos y exportar.
- **Importar**: cargar KMZ, coordenadas, tabla de torres y DWG/DXF.
- **Crear / Editar**: elementos, archivos, vínculos de planos, materiales, actividades y nombre de torres.
- **Comentar**: registrar evidencias y observaciones de actividades.
- **Eliminar**: elementos, archivos y capas (a la Papelera).
- **ConfigurarCampos**: columnas de la vista Tabla.

## Problemas frecuentes
- **«El mapa no carga»** → la tabla, las hojas de vida y las cargas siguen funcionando. Recargue la página; si sigue,
  avise al administrador.
- **«Cargué la tabla de torres con columnas equivocadas»** → Revisiones › **Corregir columnas**, o **Deshacer
  esta revisión**.
- **«No puedo continuar en la carga de la tabla»** → hay columnas repetidas (filas en rojo). Deje una sola columna por campo.
- **«¿Dónde cargo las coordenadas?»** → en la **tabla de torres**: marque sus columnas como **Coordenada de la torre**
  (Este y Norte) y **Coordenada de pata**. Ya no hay un Excel de coordenadas aparte.
- **«Las torres quedaron en otro lugar o fuera de Colombia»** → el sistema de coordenadas está mal elegido. Deshaga la
  revisión o use **Corregir columnas** y elija el sistema correcto (por ejemplo MAGNA-SIRGAS Origen Bogotá).
- **«Unas torres de la tabla no se crearon»** → esas filas no traen coordenadas de torre, o se marcaron Omitir.
- **«No veo los planos en la ficha de la pata»** → los planos se vinculan por ficha. Vincúlelos en esa pata o use el
  alcance «todas las torres de la familia».
- **«Aparecen dos torres iguales»** → use **Unificar torres duplicadas**.
- **«En la ficha de una pata no veo los ensayos de toda la torre»** → es normal: la ficha de un elemento muestra solo lo
  suyo; la **Hoja de vida** muestra todo.
- **«No veo una actividad en un elemento»** → revise **Elegir qué actividades ver en este elemento**.

## Preguntas frecuentes

### ¿Cómo abro la hoja de vida de una torre?
En el mapa o la tabla toque la torre y luego Hoja de vida. También desde Ctrl + K escribiendo el código de la torre.

### ¿Cómo cargo el trazado o la tabla de torres?
Con **Bases de datos** del encabezado: arrastre el KMZ, el Excel de la tabla de torres o el DWG/DXF; cada archivo va a su flujo.

### ¿Cómo cargo las coordenadas de las torres?
Vienen en la misma tabla de torres (columnas Este/Norte con su sistema); ya no hay un Excel de coordenadas aparte.

### ¿Cómo deshago una carga de la tabla de torres?
Tabla de torres › Revisiones: **Deshacer** la vigente devuelve los valores anteriores sin tocar lo cambiado después.

### ¿Dónde veo qué cambió en una torre?
En su hoja de vida, pestaña **Cambios**: una tarjeta por revisión con los valores anteriores y nuevos.

### ¿Cómo vinculo planos a una torre o a todas las de su tipo?
En la ficha, Vincular planos: busque por palabras y elija el alcance (esta ficha, la familia o el tipo exacto).

### ¿Dónde veo las inspecciones u otros registros de una torre?
En la pestaña **Módulos** de la hoja de vida (aparece si un módulo creado enlaza torres); ＋ crea uno ya enlazado.

### ¿Cómo paso a la torre siguiente sin cerrar la hoja de vida?
Con las flechas **Torre anterior** y **Torre siguiente** del encabezado: conservan la pestaña y el elemento abiertos.

### ¿Cómo reordeno las pestañas de la hoja de vida?
Arrástrelas; el orden queda igual para todas las torres.

### ¿Por qué una torre no aparece en el mapa?
No tiene coordenadas o están fuera de Colombia; revise la tabla de torres (columnas de coordenadas y su sistema).

## Relacionado
- [Planos](planos.md) — listado maestro de donde salen los planos vinculados.
- [Riesgos](riesgos.md) y [Planes de acción](planes-accion.md) — lo que aparece en esas pestañas.
- Avance de obra y Matriz PIE — la misma torre vista desde el avance y el cumplimiento.
- [Uso general](general.md) — buscador, Papelera, permisos.
