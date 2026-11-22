---
modulo: ensayos
titulo: Ensayos
menu: Operación › Ensayos
rutas: ['/ensayos', '/ensayos/{TipoEnsayoId:int}', '/ensayos/resumen']
palabras: [laboratorio, muestra, resultado, concreto, informe, histograma, normalidad, resumen, subir, cargar, pdf, muestra, No. Informe, pata, planilla, Excel, borrar, papelera, no aparece, rotura de cilindros, resistencia, muestra de obra, ensayo de laboratorio, resultados por torre]
opciones:
  - Resumen de ensayos: /ensayos/resumen
  - Por ensayo (una tabla por tipo): /ensayos/resumen?vista=ensayo
  - Por torre (todos sus ensayos): /ensayos/resumen?vista=torre
  - Exportar a Excel (una hoja por ensayo, lo filtrado): /ensayos/resumen | Exportar a Excel (una hoja por ensayo, lo filtrado)
  - Abrir el ensayo para marcar sus columnas: /ensayos/resumen?vista=ensayo | Abrir el ensayo para marcar sus columnas
  - Agregar resultado: /ensayos | Agregar resultado
  - Importar desde Excel: /ensayos | Importar desde Excel
  - Confirmar e importar: /ensayos | Confirmar e importar
  - Base de datos: /ensayos | Base de datos
  - Eliminar esta carga (va a la Papelera): /ensayos | Eliminar esta carga (va a la Papelera)
  - Gráficos: /ensayos | !Gráficos
  - Resumen de ensayos desde Ensayos: /ensayos | Resumen de ensayos: todas las torres con las columnas marcadas «Resumen» en cada ensayo
  - Nuevo grupo de ensayos: /ensayos | Nuevo grupo de ensayos
  - Buscar grupo: /ensayos | Buscar grupo...
  - Editar grupo: /ensayos | Editar grupo
  - Eliminar grupo: /ensayos | Eliminar grupo
  - Buscar tipo de ensayo: /ensayos | Buscar tipo de ensayo...
  - Nuevo tipo de ensayo: /ensayos | Nuevo tipo de ensayo
  - Editar tipo de ensayo: /ensayos | Editar tipo de ensayo
  - Eliminar tipo de ensayo: /ensayos | Eliminar tipo de ensayo
  - Vista de hoja de cálculo: /ensayos | Vista de hoja de cálculo
  - Pantalla completa del ensayo: /ensayos | Pantalla completa
  - Salir de pantalla completa del ensayo: /ensayos | Salir de pantalla completa
  - Pantalla completa del resumen: /ensayos/resumen | Pantalla completa
  - Salir de pantalla completa del resumen: /ensayos/resumen | Salir de pantalla completa
  - Campos: /ensayos | Campos
  - Exportar: /ensayos | !Exportar
  - Editar muestra (fila seleccionada): /ensayos | Editar muestra (fila seleccionada)
  - Campos de la plantilla del ensayo (tipo, unidad, fórmula, adjunto, importación): /ensayos | Campos de la plantilla del ensayo (tipo, unidad, fórmula, adjunto, importación)
  - Gestionar documentos del informe: /ensayos | Gestionar documentos del informe
  - Subir documentos del informe: /ensayos | Subir documentos del informe
  - Leer archivo: /ensayos | Leer archivo
  - Es esta torre: /ensayos | Es esta torre
  - Ninguna, dejar sin torre: /ensayos | Ninguna, dejar sin torre
  - Sí, eliminar carga: /ensayos | Sí, eliminar carga
  - Buscar en lo mostrado (torre, valor…): /ensayos/resumen | Buscar en lo mostrado (torre, valor…)
  - Grupo de ensayos: /ensayos/resumen | Grupo de ensayos
  - Torre o localización: /ensayos/resumen | Torre o localización
  - Hoja de vida de la torre: /ensayos/resumen?vista=torre | Hoja de vida de la torre
  - Análisis estadístico: /ensayos | Análisis estadístico
actualizado: 2026-11-21
---

# Ensayos

## Para qué sirve

Organice los tipos de ensayo y consulte resultados por torre o localización. Registre muestras, adjunte informes y analice las mediciones numéricas.

ISO 9001, numeral 8.6.

## Cómo entrar

Menú › Operación › Ensayos.

Abra un grupo y luego el tipo de ensayo. Para cruzar resultados entre tipos, use el botón de lista (Resumen de ensayos: todas las torres con las columnas marcadas «Resumen» en cada ensayo).

## Qué hay en la pantalla

- Al entrar se muestran los grupos de ensayos. Dentro de un grupo se muestran los tipos que contiene.
- Cada tipo abre su listado de resultados. Las columnas dependen del ensayo y de los campos configurados.
- En el encabezado del tipo están el botón de gráfico de barras (Vista de hoja de cálculo), el botón de gráfico (Gráficos), el botón de importación (Importar desde Excel) y el botón de más (Agregar resultado).
- En el listado puede elegir una categoría y usar **Buscar por torre...** o **Buscar por localización...**.
- La pantalla de gráficos ofrece **Análisis estadístico**. Comparte sus filtros con los gráficos.
- El resumen permite cambiar entre **Por ensayo (una tabla por tipo)** y **Por torre (todos sus ensayos)**.

## Cómo hacer…

### Cómo encontrar los resultados de un ensayo

1. Seleccione el grupo y el tipo de ensayo.
2. Elija **Todos** o una categoría.
3. Use **Buscar por torre...** o **Buscar por localización...**, según el ensayo.
4. Revise el **No. Informe** cuando aparezca en la tabla.
5. Si necesita trabajar con la cuadrícula, toque el botón de gráfico de barras (Vista de hoja de cálculo).

### Cómo crear un grupo y un tipo de ensayo

1. Toque el botón de más (Nuevo grupo de ensayos).
2. Diligencie el nombre del grupo y toque el botón de más (Agregar).
3. Abra el grupo y toque el botón de más (Nuevo tipo de ensayo).
4. Complete los datos del tipo y toque el botón de más (Agregar).
5. Abra el nuevo tipo para consultar o registrar resultados.

### Cómo registrar un resultado y su informe

1. Abra el tipo de ensayo y toque el botón de más (Agregar resultado).
2. Elija **Torre** y, cuando corresponda, **Elemento (pata)**. Si el ensayo usa localización, revise **Localización**.
3. Diligencie los valores de la muestra en los campos del ensayo.
4. Cuando corresponda, indique **No. Informe** y cargue los PDF en **Informe — adjuntar PDF(s)**.
5. Toque el botón de disquete (Guardar) y compruebe que el resultado aparece en la tabla.

### Cómo importar resultados desde Excel

1. Abra el tipo de ensayo que recibirá los datos.
2. Toque el botón de importación (Importar desde Excel).
3. Seleccione el archivo y toque el botón de importación (Leer archivo).
4. Revise la hoja y las columnas que corresponden al ensayo.
5. Toque el botón de visto bueno (Revisar). El aviso indica la cantidad de filas.
6. Revise las filas y toque **Confirmar e importar**. El botón indica cuántas filas se importarán.

### Cómo analizar un campo numérico

1. Abra el tipo de ensayo y toque el botón de gráfico (Gráficos).
2. Abra **Análisis estadístico**. En la barra elija **Campo a analizar** y, si necesita comparar categorías, use **Comparar por**.
3. Toque **Agregar filtro** para elegir un campo. Ajuste los valores o las fechas en las tarjetas debajo de la barra. El conteo a la derecha indica cuántas filas filtradas tienen valor en el campo analizado.
4. Elija el nivel de confianza y diligencie **LIE** o **LSE** si necesita límites de especificación.
5. Revise el histograma y los widgets de métricas, lectura y tablas. Puede desplazarse dentro de las tablas para ver todas las filas y columnas.
6. En una pantalla de más de 900 px, toque el lápiz para ver la cuadrícula y la paleta **Widgets**. Arrastre los bloques para moverlos y use el tirador de la esquina inferior derecha para cambiar su tamaño. Quite un bloque con **×** y vuelva a agregarlo con un clic en la paleta o arrastrándolo a la cuadrícula.
7. Toque el lápiz para volver al análisis. El navegador guarda la disposición para cada tipo de ensayo. El botón **Restaurar disposición de fábrica** recupera la posición, el tamaño y los widgets originales. En pantallas de hasta 900 px, los widgets se apilan.

### Cómo exportar el resumen de ensayos

1. Abra **Resumen de ensayos** desde el encabezado de la página principal.
2. Use el botón de tabla (Por ensayo (una tabla por tipo)) o el botón de torre (Por torre (todos sus ensayos)).
3. Aplique los filtros del resumen antes de descargar.
4. Revise las columnas marcadas **Resumen** en cada tipo.
5. Toque el botón de Excel (Exportar a Excel (una hoja por ensayo, lo filtrado)).

### Cómo editar una muestra y su informe

1. Abra el tipo de ensayo y seleccione una fila. Toque **Editar muestra (fila seleccionada)** y, al terminar, **Guardar**.
2. En la fila use **Subir documentos del informe** o **Gestionar documentos del informe**, según haya archivos cargados.
3. En **Base de datos**, use **Eliminar esta carga (va a la Papelera)** si necesita retirar una carga completa.

### Cómo distinguir la plantilla de la tabla del ensayo

1. Abra el tipo de ensayo. Use **Campos de la plantilla del ensayo (tipo, unidad, fórmula, adjunto, importación)** para editar la definición de sus resultados.
2. Use **Campos** para configurar columnas de la tabla. Marque **Resumen** para incluir una columna en **Resumen de ensayos**. Marque **Información Bot** para controlar los datos mostrados fuera de la tabla, como en el bot y la hoja de vida.

Para interpretar el histograma, los límites de especificación y el Cpk, consulte [Análisis estadístico de ensayos](analisis-estadistico-ensayos.md).

## Permisos

- **Ver** permite consultar grupos, resultados, gráficos y resumen.
- **Crear**, **Editar**, **Eliminar** e **Importar** corresponden a las operaciones sobre los ensayos y resultados.
- **Descargar** permite exportar. **ConfigurarCampos** corresponde a la configuración de campos.
- De fábrica, **Consulta sin descarga** puede ver. **Consulta** también descarga. **Colaborador** agrega resultados o evidencias. **Editor** y **Coordinador / aprobador** también editan, importan y eliminan. **Administrador del proyecto** incluye la configuración de campos.

## Problemas frecuentes

- **«El resumen dice Sin columnas marcadas «Resumen»»**. Use el botón de lápiz (Abrir el ensayo para marcar sus columnas) y revise la configuración de campos.
- **«Este ensayo no tiene campos numéricos para analizar.»**. Revise el tipo de los campos con quien configura el ensayo.
- **«El análisis no muestra valores para el campo»**. Revise los filtros y el campo seleccionado.
- **«No encuentro el resultado»**. Vuelva a **Todos** y borre la búsqueda.

## Preguntas frecuentes

### ¿Dónde subo el PDF del laboratorio para una muestra?

En Ensayos, abra el grupo y el tipo de ensayo, y toque **Agregar resultado**. Diligencie **No. Informe** y use **Informe — adjuntar PDF(s)**. Guarde y consulte la muestra en la tabla de ese tipo.

### ¿Qué hago si dice «Debes indicar el No. de Torre antes de guardar»?

En el formulario de Ensayos, seleccione la **Torre** de la muestra. Seleccione también **Elemento (pata)** si corresponde. Si el ensayo usa localización y el aviso la solicita, diligencie **Localización**.

### ¿Cómo subo los resultados de laboratorio que tengo en Excel?

En Ensayos, abra el tipo que recibirá los datos y toque **Importar desde Excel**. Lea el archivo, revise la hoja y las columnas asignadas. Tras revisar las filas, toque **Confirmar e importar**.

### ¿Cómo borro una carga de ensayos que subí por error?

En Ensayos, abra el tipo correspondiente y toque **Base de datos**. Ubique el lote, use **Eliminar esta carga (va a la Papelera)** y revise los resultados afectados antes de confirmar.

### ¿Dónde veo todos los ensayos de una torre?

Abra **Resumen de ensayos** y cambie a **Por torre (todos sus ensayos)**. También puede consultar la hoja de vida de la torre en [Trazado](trazado.md).

### ¿Qué hago si el resumen dice «Sin columnas marcadas «Resumen»»?

En **Resumen de ensayos**, use **Abrir el ensayo para marcar sus columnas** y revise los campos que deben quedar marcados **Resumen**. La configuración requiere **ConfigurarCampos**.

### ¿Cómo saco en Excel el resumen filtrado de ensayos?

Abra **Resumen de ensayos**, aplique los filtros y toque **Exportar a Excel (una hoja por ensayo, lo filtrado)**. Revise el permiso **Descargar** si el control no está disponible.

### ¿Qué hago si el análisis dice «Este ensayo no tiene campos numéricos para analizar.»?

En Ensayos, abra el tipo y compruebe en **Gráficos** si tiene campos numéricos disponibles. Solicite a quien configura el ensayo que revise el tipo de los campos que espera analizar.

### ¿Cuál es el botón para editar una muestra que ya existe?
Dentro del tipo de ensayo seleccione su fila y toque **Editar muestra (fila seleccionada)**.

### ¿La casilla Información Bot también saca la columna en el Resumen de ensayos?
Para el resumen marque **Resumen** en **Campos**. **Información Bot** controla los datos para el bot y las fichas.

### ¿Dónde miro la resistencia con histograma y límites?
Dentro del tipo de ensayo toque **Gráficos** y **Análisis estadístico**. Elija el campo numérico y revise el histograma.

## Relacionado

- [Trazado](trazado.md) — la hoja de vida de una torre reúne sus ensayos.
- [Dossier](dossier.md) — permite incluir tablas y adjuntos de los módulos seleccionados.
