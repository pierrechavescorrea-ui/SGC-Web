---
modulo: documentos
titulo: Documentos
menu: Apoyo › Documentos
rutas: [/documentos]
palabras: [listado maestro, documentos, procedimiento, formato, instructivo, PR, F-, versión, revisión, vigente, editable, obsoleto, control de cambios, tipo documental, tipología, estado del documento, cargar PDF, Word, actualizar documento, nueva versión, subir, pdf, foto, adjunto, bajar, excel, duplicado, borrar, papelera, no aparece]
opciones:
  - control de cambios: /documentos | Actualizar
  - Documentos: /documentos
  - ＋: /documentos | Nueva categoría
  - Quitar filtros: /documentos | Quitar filtros
  - Campos: /documentos | Campos
  - Actualizar: /documentos | Actualizar
  - Cargas de archivos adjuntos: /documentos | Base de datos
  - Bases de datos Excel del listado: /documentos | Base de datos
  - Importar listado (Excel): /documentos | Importar listado (Excel)
  - Cargar documentos (PDF, Word, Excel): /documentos | Cargar documentos (PDF, Word, Excel)
  - Eliminar documento seleccionado: /documentos | Eliminar documento seleccionado
  - Nuevo documento: /documentos | Nuevo documento
  - ＋ (Nuevo documento): /documentos | Nuevo documento
  - Ver todos: /documentos | Ver todos
  - Agregar revisión: /documentos | Agregar revisión
  - Editar: /documentos | Editar
  - Guardar cambios: /documentos | Guardar cambios
  - Obsoletos: /documentos | Obsoletos
  - Ver solo esta carga: /documentos | Ver solo esta carga
  - Ver solo sus documentos: /documentos | Ver solo sus documentos
  - Eliminar cargas y sus documentos (van a la Papelera): /documentos | Eliminar cargas y sus documentos (van a la Papelera)
  - Eliminar cargas y sus archivos (van a la Papelera): /documentos | Eliminar cargas y sus archivos (van a la Papelera)
actualizado: 2026-11-19
---

# Documentos

## Para qué sirve
Es el **listado maestro de documentos** del sistema de gestión (procedimientos, formatos, instructivos, certificados…).
Por cada documento guarda sus datos, sus archivos (**vigente**, **editable** y **obsoletos**) y su **control de
cambios** (qué cambió en cada versión, cuándo y quién). ISO 9001 · 7.5 (información documentada).

## Cómo entrar
- Menú › **Apoyo** › **Documentos**.
- Buscador (**Ctrl + K**): escriba el código o parte del nombre; el resultado abre el documento.
- Desde **Certificaciones** y **Direccionamiento estratégico**, los botones de actualizar o ver versiones abren
  aquí el documento ligado.
- Bot de Telegram: **/documentos** (por tipología) y **/doc código**.

## Qué hay en la pantalla
- **Pestañas de categorías** sobre la tabla: **Todos**, **Sin categoría**, las categorías creadas y **＋** para crear
  una. Se reordenan arrastrándolas.
- **Filtros**: Tipo documental y Estado (salen de los catálogos «Tipo documental» y «Estado del documento»),
  buscador y **Quitar filtros**.
- **Tabla**: columnas **Código** (identifica el documento), **Tipo documental**, **Estado** y las columnas propias
  del proyecto (las que se importaron del Excel o se crearon en **Campos**).
  - Un **código en azul** tiene archivos: tóquelo para ver el menú de archivos.
  - En cada fila, el botón **Actualizar** abre el control de cambios del documento.
- **Encabezado**: pantalla completa · **Cargas de archivos adjuntos** / **Bases de datos Excel del listado** ·
  Campos · **Importar listado (Excel)** · **Cargar documentos (PDF, Word, Excel)** · Exportar ·
  **Eliminar documento seleccionado** · **Editar** · **Nuevo documento**.

### El menú del código (archivos)
Al tocar un código en azul se abre una ventanita con:
- **Ver todos**: pantalla aparte con todos los archivos del documento.
- **Vigente**: hasta 3 archivos de la versión más reciente. Es lo que se debe usar.
- **Editable**: hasta 3 archivos editables (Word, Excel) de la versión más reciente.
- **Obsoletos**: lleva a la pantalla de versiones anteriores.
Desde cada archivo: **Ver**, **Descargar**, **Reemplazar archivo** o **Eliminar archivo** (va a la Papelera).

## Cómo hacer…

### Cómo agregar un documento nuevo
1. Elija la pestaña de categoría donde debe quedar (o **Todos**).
2. Toque **＋ (Nuevo documento)**.
3. Escriba el **Código**, el nombre y los demás datos; elija **Tipo documental** y **Estado**.
4. Cargue el archivo vigente y, si existe, el editable.
5. Toque **Guardar**.

### Cómo subir una nueva versión de un documento (control de cambios)
1. Busque el documento y toque **Actualizar** en su fila. Se abre la pantalla del documento sola.
2. En el **control de cambios**, toque **Agregar revisión**.
3. Escriba la **versión**, la **fecha**, la **descripción del cambio** y el **responsable**.
4. En esa fila, cargue el archivo **vigente** y el **editable**.
5. Toque **Guardar**. Desde ese momento, el listado muestra los archivos de esta revisión (la de fecha más reciente)
   y los anteriores pasan a versiones anteriores.

### Cómo ver una versión anterior
1. Toque el código del documento (en azul).
2. Toque **Obsoletos** o **Ver todos**.

### Cómo cargar muchos archivos de una vez
1. Toque **Cargar documentos (PDF, Word, Excel)**.
2. Elija los archivos. La app pregunta **¿Qué estás cargando?**: vigentes, editables u obsoletos, y la **Fecha de las
   versiones**.
3. Para cada archivo, la app **sugiere el documento** del listado según el código, números o palabras del nombre.
   Revise y corrija la sugerencia.
4. Confirme. Toda la carga queda registrada en **Cargas de archivos adjuntos**.

### Cómo importar o actualizar el listado desde Excel
1. Toque **Importar listado (Excel)**.
2. Elija **Hoja** y **Fila de encabezados** (**Usar como fila de encabezados**).
3. Para cada columna del Excel, elija su destino: una columna existente, **Código**, **Tipo documental**, **Estado**,
   una columna nueva, o no incluirla (**Incluir columna**).
4. Confirme. Las filas con un **código que ya existe** actualizan ese documento; no se duplican.

### Cómo editar datos en la tabla
1. Toque **Editar**. La tabla, que normalmente es solo lectura, se vuelve editable.
2. Cambie los datos y toque **Guardar cambios**. **Descartar cambios y salir de edición** deshace todo.

### Cómo deshacer una carga equivocada
1. Toque **Bases de datos Excel del listado** (para importaciones de Excel) o **Cargas de archivos adjuntos**
   (para cargas de archivos).
2. Use **Ver solo esta carga** / **Ver solo sus documentos** para revisar.
3. Toque **Eliminar cargas y sus documentos** (o **…y sus archivos**). Todo va a la Papelera y se puede restaurar.

### Cómo crear una categoría
1. Toque **＋** en las pestañas de categorías.
2. Escriba el nombre (no se puede repetir) y, si quiere, elija un ícono.
3. Los documentos nuevos e importados van a la pestaña que esté seleccionada.

## Permisos
- **Ver**: consultar el listado. **Descargar**: abrir y bajar archivos y exportar.
- **Crear**: documentos nuevos y archivos. **Editar**: modificar datos y agregar revisiones.
- **Importar**: Excel del listado y cargas masivas de archivos. **Eliminar**: enviar a la Papelera.
- **ConfigurarCampos**: agregar o cambiar columnas (**Campos**).

## Problemas frecuentes
- **«El código no está en azul»** → el documento no tiene archivos cargados. Use **Actualizar** › **Agregar revisión**
  para cargarlos.
- **«Cargué una versión nueva pero sigue saliendo la anterior»** → el listado muestra la revisión con **fecha más
  reciente**. Revise la fecha de la revisión nueva en **Actualizar**.
- **«El Tipo documental o el Estado no aparece en la lista»** → esas listas salen de **Configuración › Catálogos**
  («Tipo documental» y «Estado del documento»). Pida que se agregue la opción allá.
- **«Importé el Excel dos veces»** → no pasa nada: se actualizan los mismos documentos por su código.
- **«No veo el botón Editar o Nuevo documento»** → su rol es de consulta en este módulo.
- **«No encuentro un documento»** → revise que esté en la pestaña **Todos** y sin filtros (**Quitar filtros**).

## Preguntas frecuentes

### ¿Cómo subo una versión nueva de un procedimiento?

En Documentos, ubique la fila y toque **Actualizar**. Use **Agregar revisión**, diligencie la versión y la fecha, adjunte el archivo vigente y guarde.

### ¿Por qué sigue saliendo el PDF viejo si ya subí el nuevo?

En Documentos, toque **Actualizar** y revise la fecha de la revisión nueva. El listado muestra los archivos de la revisión con fecha más reciente.

### ¿Dónde quedan los archivos obsoletos y los editables?

En Documentos, toque el código en azul. El menú muestra la sección **Editable** de la revisión vigente cuando los hay. Use **Obsoletos** para las versiones anteriores o **Ver todos** para abrir la ficha.

### ¿Qué hago si el código del documento no me deja abrir archivos?

En Documentos, si el código no aparece en azul, abra **Actualizar**, toque **Agregar revisión** y cargue los archivos que correspondan.

### ¿Qué hago si dice que el código quedaría repetido?

En la edición de Documentos, busque el código citado en «El código … quedaría repetido.». Corrija el valor duplicado y vuelva a usar **Guardar cambios**.

### ¿Qué pasa si importo un Excel con un código que ya existe?

En **Importar listado (Excel)** de Documentos, asigne la columna al campo **Código**. Una fila con un código existente actualiza ese documento.

### ¿Qué hago si la carga masiva dice «No se cargaron»?

En Documentos, lea los nombres y motivos detallados en el aviso. Use **Cargar documentos (PDF, Word, Excel)** para volver a intentar los archivos afectados y revise el documento sugerido para cada uno.

### ¿Cómo deshago una carga de archivos sin quitar todo el listado?

En Documentos, abra **Cargas de archivos adjuntos**, use **Ver solo sus documentos** para revisar y confirme con **Eliminar cargas y sus archivos (van a la Papelera)** en la carga seleccionada.

### ¿Por qué no veo el botón para cargar documentos en lote?

Para usar **Cargar documentos (PDF, Word, Excel)**, revise el permiso **Importar** de Documentos con el administrador del proyecto.

## Relacionado
- [Planos](planos.md) — funciona igual, para planos.
- [Uso general](general.md) — importar, editar tablas y Papelera.
- Certificaciones y Direccionamiento estratégico — cada certificado o documento estratégico es un documento de este listado.
