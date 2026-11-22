---
modulo: matriz-pie
titulo: Matriz PIE
menu: Operación › Matriz PIE
rutas: ['/matriz-pie']
palabras: [plan de inspección, cumplimiento, actividades, parámetros, torre, estado, evidencia, plan de inspección y ensayo, avance de calidad, liberación, soporte, semáforo]
opciones:
  - Matriz PIE: /matriz-pie
  - Torre por torre: /matriz-pie?vista=torre
  - Gráficos: /matriz-pie?vista=graficos
  - Matriz (detalle y edición): /matriz-pie?vista=matriz
  - Elegir torres: /matriz-pie | Elegir torres
  - Elegir actividades y parámetros: /matriz-pie | Elegir actividades y parámetros
  - Mostrar solo torres con estos estados: /matriz-pie | Mostrar solo torres con estos estados
  - Quitar todos los filtros: /matriz-pie | Quitar todos los filtros
  - Mapa de calor (torres × actividades): /matriz-pie?vista=mapa | Mapa de calor (torres × actividades)
  - Estados y pesos (%): /matriz-pie | !Estados y pesos (%)
  - Exportar: /matriz-pie?vista=matriz | !Exportar
  - Editar: /matriz-pie?vista=matriz | Editar
  - Salir de edición: /matriz-pie?vista=matriz | Salir de edición
  - Guardar cambios: /matriz-pie?vista=matriz | Guardar cambios
  - Agregar estado: /matriz-pie | Agregar estado
  - Eliminar estado: /matriz-pie | Eliminar estado
  - Buscar torre: /matriz-pie | Buscar torre...
  - Quitar selección: /matriz-pie | Quitar selección
  - Quitar adjunto: /matriz-pie?vista=matriz | Quitar adjunto
  - Cerrar sin guardar: /matriz-pie?vista=matriz | Cerrar sin guardar
  - Anterior: /matriz-pie?vista=torre | Anterior
  - Siguiente: /matriz-pie?vista=torre | Siguiente
  - Abrir la hoja de vida en Trazado: /matriz-pie?vista=torre | Abrir la hoja de vida en Trazado
actualizado: 2026-11-21
---

# Matriz PIE

## Para qué sirve

Consulte el cumplimiento del plan de inspección y ensayos por torre y actividad. Registre estados, fechas y documentación de los parámetros que correspondan.

ISO 9001, numeral 8.1.

## Cómo entrar

Menú › Operación › Matriz PIE.

Abra una torre desde el mapa de calor. En el detalle, el vínculo **Hoja de vida ›** lleva a Trazado cuando está disponible.

## Qué hay en la pantalla

- El encabezado muestra **Cumplimiento total del PIE** y los íconos de las vistas.
- El botón de tablero (Mapa de calor (torres × actividades)) resume el cumplimiento en colores.
- El botón de torre (Torre por torre) abre las actividades y parámetros de una torre.
- El botón de lista (Matriz (detalle y edición)) muestra la tabla con actividades, parámetros y columnas **%**.
- El botón de gráfico (Gráficos) abre la visualización de los datos.
- El botón de catálogo (Estados y pesos (%)) abre el panel con **Color**, **Nombre** y **Peso %**.
- Los controles **Elegir torres**, **Elegir actividades y parámetros** y **Mostrar solo torres con estos estados** acotan la consulta.

## Cómo hacer…

### Cómo consultar el cumplimiento del proyecto

1. Toque el botón de tablero (Mapa de calor (torres × actividades)).
2. Lea la cifra **Cumplimiento total del PIE** en el encabezado.
3. En el mapa, compare las torres y las actividades por color y porcentaje.
4. Busque la columna **%** para consultar el cumplimiento de una torre.
5. Abra la torre cuya información quiere revisar.

### Cómo filtrar torres y actividades

1. Abra **Torres** con el control **Elegir torres**.
2. Marque las torres que necesita consultar.
3. Abra **Elegir actividades y parámetros** y seleccione los elementos de interés.
4. Use **Mostrar solo torres con estos estados** para acotar por estado.
5. Para volver a la vista general, use **Quitar todos los filtros**.

### Cómo consultar una torre y su documentación

1. Toque el botón de torre (Torre por torre).
2. Seleccione una torre en la lista.
3. Revise el porcentaje general y el estado de cada actividad.
4. Abra el detalle de los parámetros de interés.
5. Toque **Hoja de vida ›** si necesita consultar la ficha en Trazado.

### Cómo actualizar estados desde la matriz

1. Toque el botón de lista (Matriz (detalle y edición)).
2. Use el botón de lápiz (Editar).
3. Localice la torre y el parámetro.
4. Seleccione el estado que corresponda. Use **N/A** cuando el parámetro no aplique.
5. Revise los cambios pendientes y toque el botón de disquete (Guardar cambios).
6. Para salir sin guardar, use el botón de equis (Salir de edición). Si aparece **¿Descartar los cambios sin guardar?**, toque el botón de equis (Descartar).

### Cómo cargar documentación para una actividad

1. Seleccione una torre en **Torre por torre**.
2. Abra la actividad con **Editar valores y cargar documentación**.
3. Ubique el parámetro correspondiente.
4. Toque el botón de clip (Cargar documentación) y seleccione archivos PDF o imágenes.
5. Revise los nombres de los archivos y toque el botón de disquete (Guardar).

### Cómo revisar o agregar estados y sus pesos

1. Salga de **Gráficos** si está abierto.
2. Toque el botón de catálogo (Estados y pesos (%)).
3. Revise **Color**, **Nombre** y **Peso %** de los estados existentes.
4. Para crear uno, toque el botón de más (Agregar estado).
5. Diligencie las columnas del nuevo estado y revise los cambios en la matriz.

### Cómo volver al listado sin aplicar cambios

1. Cuando edite la matriz, toque **Salir de edición**.
2. Si hay cambios pendientes, toque **Seguir editando** para volver o **Descartar** para salir.
3. Si quiere conservarlos, toque **Guardar cambios** en el encabezado.

## Permisos

- **Ver** permite consultar mapa, torre, matriz y gráficos.
- **Editar** corresponde a la modificación de registros. **Descargar** corresponde a descargas y exportaciones.
- El rol de fábrica **Consulta** permite ver y descargar.

En el rol de fábrica, **Colaborador** puede consultar, descargar y agregar registros o evidencias. **Editor** añade edición y eliminación. Modificar un estado existente requiere permiso de edición, aprobación o administración del módulo en la validación de escritura. Quitar documentos puede requerir **Eliminar**, según la operación.

## Problemas frecuentes

- **«El mapa o la matriz muestra pocas torres»**. Revise las tres selecciones y use **Quitar todos los filtros**.
- **«Guardar cambios está deshabilitado»**. Cambie al menos una celda antes de intentar guardar.
- **«No aparece el botón Editar»**. Abra **Matriz (detalle y edición)** y revise su permiso **Editar**.
- **«La casilla muestra un guion»**. Consulte el aviso de la celda para conocer el motivo.

## Preguntas frecuentes

### ¿Cómo veo el cumplimiento de una torre?
En la vista **Mapa** toque la fila de la torre, o abra la vista **Torre** y elíjala: muestra el % y una tarjeta por actividad
con cada parámetro y su estado.

### ¿Cómo cambio el estado de un parámetro?
Vista **Torre**: toque la cabecera de la actividad para abrir su editor (aplica, estado, fecha, facturado, observación) y
guarde. También se edita en la vista **Matriz** con **Editar**.

### ¿Dónde subo la documentación de un parámetro?
En el editor de la actividad (vista Torre), en la fila del elemento y parámetro: el archivo se carga al momento.

### ¿Qué diferencia hay entre Fecha y Fecha de facturado?
Fecha es cuando se registró el parámetro; Fecha de facturado es cuando se facturó. El gráfico de la Matriz PIE puede agrupar
por cualquiera de las dos.

### ¿Cómo se calcula el % de cumplimiento?
Con los estados y sus pesos (botón **Estados y pesos**). El mismo cálculo se usa en la hoja de vida de la torre y en los
indicadores.

### ¿Por qué un parámetro sale apagado?
No aplica a ese elemento (sin la palomita de aplica).

### ¿Cómo exporto la matriz?
Con **Exportar** del encabezado; en la vista Mapa exporta el mapa de calor.

### ¿Cómo voy de la Matriz PIE a la hoja de vida o al avance de obra de la misma torre?
En la vista Torre use los saltos de torre del encabezado: Hoja de vida y Avance de obra abren esa misma torre.

### ¿Dónde miro el mapa de calor del PIE?
Abra **Mapa de calor (torres × actividades)**. Use los filtros de torres, actividades o estados para acotar la consulta.

### ¿Cómo vuelvo a ver todas las torres y actividades?
Toque **Quitar todos los filtros**. Dentro de un filtro puede usar **Quitar selección** para limpiar solo ese selector.

### ¿Dónde se cambian los pesos de los estados?
Abra **Estados y pesos (%)**. Ahí puede revisar pesos y colores, o usar **Agregar estado**.

## Relacionado

- [Indicadores](indicadores.md) — puede calcular el cumplimiento del PIE.
- [Trazado](trazado.md) — abre la hoja de vida de la torre.
- [Avance de obra](avance-obra.md) — muestra el avance físico por fecha de corte.
