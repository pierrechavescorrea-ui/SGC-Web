---
modulo: planos
titulo: Planos
menu: Apoyo › Planos
rutas: [/planos]
palabras: [planos, listado de planos, plano, hoja, hojas, revisión, rev, vigente, obsoleto, grupo de planos, estructural, civil, eléctrico, cargar PDF, actualizar plano, versión]
opciones:
  - Planos: /planos
  - Base de datos: /planos | Base de datos
  - Cargas de archivos: /planos | Base de datos
  - Importar listado (Excel): /planos | Importar listado (Excel)
  - Cargar planos (PDF): /planos | Cargar planos (PDF)
  - Actualizar: /planos | Actualizar
  - Ver todos: /planos | Ver todos
  - Nuevo grupo de planos: /planos | Nuevo grupo de planos
  - Pantalla completa: /planos | Pantalla completa
  - Salir de pantalla completa: /planos | Salir de pantalla completa
  - Campos: /planos | Campos
  - Exportar: /planos | Exportar
  - Todos: /planos | Todos
  - Grupo: /planos | Grupo
  - Nueva categoría: /planos | Nueva categoría
  - Elegir ícono: /planos | Elegir ícono
  - Archivos de esta carga: /planos | Archivos de esta carga
  - Ver solo esta carga: /planos | Ver solo esta carga
  - Ver solo sus planos: /planos | Ver solo sus planos
  - Eliminar (va a la Papelera): /planos | Eliminar (va a la Papelera)
  - Ver plano: /planos | Ver plano
  - Ver: /planos | Ver
  - Descargar: /planos | Descargar
  - Reemplazar: /planos | Reemplazar
  - Reemplazar archivo: /planos | Reemplazar archivo
  - Eliminar archivo: /planos | Eliminar archivo
  - Cancelar: /planos | Cancelar
  - Editar grupo: /planos | Editar grupo
  - Eliminar grupo: /planos | Eliminar grupo
  - Bases de datos Excel del listado: /planos | Bases de datos Excel del listado
  - Grupo de los planos nuevos: /planos | Grupo de los planos nuevos
  - Eliminar cargas seleccionadas: /planos | Eliminar cargas seleccionadas
  - Eliminar cargas de archivos seleccionadas: /planos | Eliminar cargas de archivos seleccionadas
  - Quitar: /planos | Quitar
  - Agregar: /planos | Agregar
  - Guardar: /planos | Guardar
  - Obsoletos: /planos | Obsoletos
  - Buscar plano: /planos | Buscar plano...
actualizado: 2026-11-21
---

# Planos

## Para qué sirve
Es el **listado maestro de planos** del proyecto. Por cada plano guarda sus datos, su revisión vigente y anterior, y
sus archivos (PDF) **vigentes** y **obsoletos**, incluidas todas sus **hojas**. Funciona igual que
[Documentos](documentos.md), pero sin archivos editables. ISO 9001 · 7.5.

## Cómo entrar
- Menú › **Apoyo** › **Planos**.
- Buscador (**Ctrl + K**): código o título del plano.
- Desde la hoja de vida de una torre (Trazado), en sus archivos: los planos vinculados a la torre o a un elemento.
- Bot de Telegram: **/planos** (por grupo) y **/plano código**.

## Qué hay en la pantalla
- **Grupos de planos**: pestañas compactas sobre la tabla (por ejemplo estructural, civil, eléctrico), con el número de
  planos de cada uno. **Todos**, cada grupo y **Nuevo grupo de planos**. Se reordenan arrastrándolas.
  Junto al buscador hay botones para editar o eliminar el grupo seleccionado.
- **Tabla**: código del plano (en azul si tiene archivos), revisión vigente y anterior, y las columnas del proyecto.
  En cada fila, **Actualizar**.
- **Encabezado**: pantalla completa · **Base de datos** (cargas de listado y de archivos) · Campos ·
  **Importar listado (Excel)** · **Cargar planos (PDF)** · Exportar · **Nuevo grupo de planos**.
  El ＋ del encabezado crea un **grupo**, no un plano: los planos entran al listado importando el Excel.

### El menú del código
Al tocar un código en azul: **Ver todos** (pantalla con todos los archivos), **Vigente** (hasta 3 archivos de la
revisión más reciente) y **Obsoletos**. Por archivo: **Ver plano**, **Descargar**, **Reemplazar archivo**, **Eliminar archivo**.

## Cómo hacer…

### Cómo agregar planos al listado
Use **Importar listado (Excel)** para incorporar los planos. **Nuevo grupo de planos** agrega una categoría al listado.

### Cómo cargar una revisión nueva de un plano
1. Toque **Actualizar** en la fila del plano. Se abre la pantalla del plano sola.
2. Elija la **Clase** (**Vigente** u **Obsoleta**), escriba la **Versión** y la **Fecha**, y cargue los PDF en
   **Archivos (hojas)** (uno por hoja).
3. Toque **Guardar**. Un vigente de una revisión nueva actualiza solo la **Rev. vigente** y la **Rev. anterior** del plano.
   Abajo se ve la tabla con todos los archivos del plano (Versión, Clase, Fecha, Archivo).

### Cómo cargar muchos PDF de una vez
1. Toque **Cargar planos (PDF)**.
2. Elija los archivos. La app sugiere a qué plano corresponde cada uno (por código o palabras del nombre).
3. Revise las sugerencias y confirme. La carga queda en **Cargas de archivos**.

### Cómo importar el listado desde Excel
1. Seleccione el grupo destino (las filas nuevas quedan en ese grupo; **Grupo de los planos nuevos**).
2. Toque **Importar listado (Excel)**, elija hoja y fila de encabezados, y el destino de cada columna.
3. Confirme. Los códigos que ya existen se actualizan; no se duplican.

### Cómo ver todas las hojas de un plano
Toque el código y luego **Ver todos**. La sección **Vigente** muestra las hojas de la revisión más reciente.

### Cómo crear o cambiar un grupo
- Nuevo: **Nuevo grupo de planos**, nombre y, si quiere, **Elegir ícono**.
- Editar o eliminar: seleccione la pestaña del grupo y use los botones junto al buscador. Eliminar envía a la Papelera.

### Cómo vincular un plano a una torre
Se hace desde la torre, no desde aquí: ver [Trazado › Cómo vincular planos](trazado.md#cómo-vincular-planos-a-una-torre-o-elemento).

### Cómo consultar o retirar un archivo de una carga

1. Abra **Base de datos** y localice **Cargas de archivos**.
2. Use **Archivos de esta carga** para revisar los adjuntos o **Ver solo sus planos** para filtrar el listado.
3. Marque la carga. Use **Eliminar cargas de archivos seleccionadas** y revise el alcance. Confirme con **Eliminar (va a la Papelera)**.

## Permisos
Ver (consultar), Descargar (abrir y bajar archivos, exportar), Crear y Editar (grupos, revisiones y archivos),
Importar (Excel y carga masiva de PDF), Eliminar (a la Papelera) y Configurar campos (columnas). **Consulta** de fábrica ve y descarga.

## Problemas frecuentes
- **«El Excel o PDF exportado solo trae algunos planos»** → se exporta el **grupo seleccionado**. Elija **Todos** antes de exportar.
- **«El plano sigue mostrando la revisión anterior»** → el archivo nuevo se cargó como obsoleto o con una versión menor.
  Revíselo en **Actualizar**.
- **«Importé y los planos quedaron en otro grupo»** → van al grupo seleccionado al importar. Muévalos editando su grupo.
- **«No veo los planos en la torre»** → hay que vincularlos desde la hoja de vida de la torre en Trazado.

## Preguntas frecuentes

### ¿Cómo cargo la revisión nueva de un plano?
Use **Actualizar** en la fila del plano: elija la clase (vigente u obsoleta), la versión y los archivos. El vigente de una
versión nueva actualiza la revisión vigente y la anterior.

### ¿Cómo cargo muchos PDF de una vez?
Con **cargar PDF** del encabezado: la app sugiere a qué plano va cada archivo (por código o palabras) y usted confirma.

### ¿Cómo importo el listado de planos desde Excel?
Con **cargar Excel**: elija hoja, fila de encabezados y destino de cada columna. Recargar el mismo archivo no duplica.

### ¿Dónde veo todas las hojas de un plano?
Toque el código del plano (en azul) y elija Ver todos.

### ¿Cómo organizo los planos por grupos?
Con las pestañas de grupo sobre la tabla: ＋ crea uno nuevo; arrástrelas para cambiar el orden.

### ¿Cómo vinculo un plano a una torre?
Desde la hoja de vida de la torre (Trazado) use Vincular planos: puede vincularlo solo a esa torre o a todas las de la misma
familia o tipo.

### ¿Qué pasa con las revisiones viejas?
Quedan como obsoletas en Ver todos; el listado muestra solo los vigentes de la revisión más reciente.

### ¿Cómo elimino una carga equivocada?
En el panel de cargas (botón de cargas), elimine la carga: sus filas o archivos van a la Papelera con la misma marca.

### ¿Puedo descargar solo los planos de una categoría?

Seleccione la categoría o el grupo antes de abrir **Exportar**. El grupo **Todos** vuelve al conjunto de planos.

### ¿Dónde cambio el PDF de una revisión sin crear otra?

En **Actualizar**, use **Reemplazar archivo** en la revisión correspondiente.

## Relacionado
- [Documentos](documentos.md) — misma forma de trabajo.
- [Trazado](trazado.md) — vincular planos a torres y elementos.
- [Uso general](general.md) — importar, Papelera, buscador.
