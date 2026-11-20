---
modulo: costos
titulo: Costos y presupuestos
menu: Planificación › Costos y presupuestos
rutas: ['/costos']
palabras: [presupuesto, cantidades, valoración, PDT, APUs, actas, metas, personal, indirectos, subir, cargar, bajar plantilla, Excel, XML, corte, reprogramación, sin casar, borrar, no aparece]
opciones:
  - Costos y presupuestos: /costos
  - Fecha de corte: /costos | Fecha de corte
  - Avance diario (cantidades por torre e ítem): /costos | Avance diario (cantidades por torre e ítem)
  - Avance diario: /costos | Avance diario (cantidades por torre e ítem)
  - Detalle de torres: /costos | Detalle de torres
  - Detalle de montaje: /costos | Detalle de montaje
  - PDT (programa de trabajo): /costos | PDT (programa de trabajo)
  - Control por tiro: /costos | Control por tiro
  - Formulario de precios: /costos | Formulario de precios
  - Actas de facturación: /costos | Actas de facturación
  - Presupuesto: /costos | Presupuesto
  - Costos (órdenes y gastos): /costos | Costos (órdenes y gastos)
  - Personal: /costos | Personal
  - Costo por actividad: /costos | Costo por actividad
  - Contrato, indirectos y parámetros: /costos | Contrato, indirectos y parámetros
  - Metas de producción y facturación: /costos | Metas de producción y facturación
  - Historial de cargas: /costos | Base de datos
  - Tablero (informe): /costos | !Tablero (informe)
  - Importar el libro de costos (Excel): /costos | Importar el libro de costos (Excel)
  - Descargar plantilla: /costos | Descargar plantilla
  - Cargar plantilla: /costos | Cargar plantilla
  - Aplicar: /costos | Aplicar
  - Deshacer la carga: /costos | Deshacer la carga
  - Deshacer: /costos | Deshacer
  - Cargar una versión desde MS Project (XML): /costos | Cargar una versión desde MS Project (XML)
  - Importar (lo importado antes del libro va a la Papelera): /costos | Importar (lo importado antes del libro va a la Papelera)
actualizado: 2026-11-19
---

# Costos y presupuestos

## Para qué sirve

Controle las cantidades ejecutadas, el presupuesto y los costos del proyecto. Consulte las metas, el programa de trabajo y las actas con la fecha de corte que corresponda.

## Cómo entrar

Menú › Planificación › Costos y presupuestos.

Las vistas se eligen con los botones del encabezado. Pase sobre cada ícono para leer su aviso.

## Qué hay en la pantalla

- **Fecha de corte** determina el corte de la consulta. **$ actual** y **$ año** alternan el año de precio usado para valorizar.
- El botón de tablero (Tablero (informe)) abre el resumen. El estado de las torres ahora está en [Avance de obra](avance-obra.md); el enlace anterior lleva allá.
- El botón de curva (Seguimiento de meta) y el botón de meta (Metas de producción y facturación) permiten consultar el seguimiento y las metas registradas.
- El grupo de vistas incluye **Avance diario (cantidades por torre e ítem)**, **Detalle de torres**, **Detalle de montaje**, **PDT (programa de trabajo)** y **Control por tiro**.
- El grupo también incluye **Formulario de precios**, **Actas de facturación**, **Presupuesto**, **Costos (órdenes y gastos)**, **Personal**, **Costo por actividad** y **Contrato, indirectos y parámetros**.
- El botón de base de datos abre las cargas del libro. El botón de Excel (Importar el libro de costos (Excel)) abre el análisis del archivo.
- Según la vista, el encabezado muestra los botones de lápiz (Editar), de más (Agregar) o de disquete (Guardar).

## Cómo hacer…

### Cómo consultar el resumen a una fecha de corte

1. Seleccione la **Fecha de corte**.
2. Abra el botón de tablero (Tablero (informe)).
3. Revise si el encabezado muestra **$ actual** o **$ año**.
4. Alterne el modo para valorizar al precio del año de corte o al del año de ejecución.
5. Compare las cifras con el corte y el modo de valoración que eligió.

### Cómo revisar las cantidades por torre e ítem

1. Abra el botón de seguimiento (Avance diario (cantidades por torre e ítem)).
2. Revise los registros de cantidades del día y su ítem asociado.
3. Abra **Detalle de torres** para consultar las cantidades por torre.
4. Abra **Detalle de montaje** para revisar el montaje.
5. Seleccione **Control por tiro** si necesita revisar el conjunto de torres de un tiro.

### Cómo comparar metas y ejecución

1. Abra **Metas de producción y facturación** con el botón de meta.
2. Revise el periodo de la meta y los valores registrados.
3. Cambie al botón de curva (Seguimiento de meta).
4. Consulte el seguimiento por semana y cuadrilla.
5. Compruebe la fecha de corte antes de comparar con otros informes.

### Cómo importar el libro de costos

1. Toque el botón de Excel (Importar el libro de costos (Excel)).
2. Use **Elegir el libro (INFORME DIARIO…xlsx)** y seleccione el archivo.
3. Revise las hojas reconocidas, los recuentos y los avisos.
4. Si aparecen diferencias, compare **DETALLE (por torre)** con **Avance diario**. Desde el inicio de DETALLE se toma esa fuente.
5. Si el resultado corresponde al libro esperado, toque el botón de disquete (Importar (lo importado antes del libro va a la Papelera)).
6. Abra el botón de base de datos para comprobar que se registró la carga.

#### Cómo descargar y cargar una plantilla de Costos

| Vista | Hoja de la plantilla | Qué identifica la fila |
|---|---|---|
| **Formulario de precios** | **ItemsPago** | **Código** del ítem o capítulo. Use **Capítulo** para referenciar el código padre, incluso si su fila aparece más abajo en el mismo libro. |
| **Presupuesto** | **CodigosPresupuesto** | **Código** presupuestal. Incluye el grupo y el importe inicial. |
| **Detalle de torres** | **CantidadesPrevistas** | **Torre**, **Ítem** y **Pata** opcional. Use la pata de la torre indicada. Esta hoja también admite los ítems de montaje. |
| **Avance diario (cantidades por torre e ítem)** | **Produccion** | **Fecha**, **Ítem**, ubicación (**Torre**, **Pata** o **Tiro** cuando aplique) y **Cuadrilla**. |

1. Entre a la vista de la tabla y toque el botón de Excel (Descargar plantilla).
2. Marque **Con los datos actuales del proyecto** para incluir registros existentes. Deje la casilla desmarcada para obtener la plantilla vacía. Toque el botón de Excel (Descargar).
3. Diligencie la hoja indicada y conserve el nombre y los encabezados. En **CantidadesPrevistas** y **Produccion**, cree primero los ítems y los lugares de Trazado que vaya a usar.
4. En la misma vista, toque el botón de Excel (Cargar plantilla) y elija el archivo .xlsx.
5. Revise **Vista previa**. Si aparece una tabla de errores, corrija la **Hoja**, **Fila** y **Columna** indicadas y cargue el archivo de nuevo.
6. Diligencie **Motivo** y toque el botón de disquete (Aplicar). Para revertir la carga, abra **Historial de cargas**, toque el botón de papelera (Deshacer la carga) y confirme con el botón de papelera (Deshacer).

En **Produccion**, una sola fila puede representar cada combinación de fecha, ítem, ubicación y cuadrilla. Si existen varios registros con esa combinación, la plantilla avisa de la ambigüedad. Para capturar movimientos separados con la misma combinación, utilice la edición manual de Avance diario.

### Cómo consultar el programa de trabajo

1. Abra el botón de programa (PDT (programa de trabajo)).
2. Elija una versión del programa.
3. Distinga la versión **vigente** de la **línea base** en el selector.
4. Revise las tareas y las asociaciones a torres e ítems.
5. Use la vista del programa para consultar el avance programado y la línea base.

### Cómo cambiar datos de una vista editable

1. Abra la vista que contiene los datos.
2. Toque el botón de lápiz (Editar) cuando esté disponible.
3. Actualice los valores correspondientes a esa vista.
4. Revise los cambios y toque el botón de disquete (Guardar).
5. Para abandonar la edición, use el botón de equis (Cancelar edición).

### Cómo incorporar una versión del PDT desde MS Project

1. Abra el botón de programa (PDT (programa de trabajo)).
2. Toque el botón de importación (Cargar una versión desde MS Project (XML)).
3. Seleccione el archivo XML y diligencie **Motivo (reprogramación, otrosí…)** y **Aprobado por** según corresponda.
4. Revise el recuento de tareas, la asociación con ítems y torres, y los avisos.
5. Toque el botón de disquete (Guardar como versión nueva).
6. Seleccione la versión y abra el botón de versiones (Bitácora de cambios) para consultar las diferencias registradas.

### Cómo registrar las cantidades previstas por torre

1. Abra el botón de torre (Detalle de torres) o el botón de montaje (Detalle de montaje).
2. Para crear una fila, toque el botón de más (Agregar). Para cambiar una cantidad existente, toque el botón de lápiz (Editar).
3. En una fila nueva, seleccione **Torre** e **Ítem**. Si el ítem tiene ámbito **Pata**, puede seleccionar una **Pata** o dejar **Sin pata** para registrar la cantidad de la torre.
4. Diligencie **Cantidad prevista**. Si la combinación ya existe, el guardado actualiza su cantidad.
5. Para retirar una fila guardada, toque el botón de papelera (Eliminar cantidad prevista) y el botón de visto bueno (Confirmar eliminación). La eliminación definitiva se aplica al guardar.
6. Toque el botón de disquete (Guardar) del encabezado. Para salir sin aplicar sus cambios, toque el botón de equis (Cancelar edición).

## Permisos

- **Ver** corresponde a la consulta y **Descargar** a las exportaciones disponibles.
- **Editar**, **Crear** e **Importar** corresponden a modificar datos, agregar registros e importar el libro.
- El módulo es de acceso restringido. En los roles de fábrica, **Consulta** carece de acceso y **Coordinador / aprobador** puede ver y descargar. El **Administrador del proyecto** tiene todas las acciones.

## Problemas frecuentes

- **«No veo Costos y presupuestos en el menú»**. Compruebe el proyecto activo y su permiso **Ver**.
- **«La importación avisa de torres sin casar»**. Compare los nombres del libro con Trazado. El aviso indica que esas filas quedan fuera.
- **«El importe cambia al tocar $ actual o $ año»**. Revise el año cuyo precio se aplica en cada modo.
- **«No puedo corregir la línea base»**. Seleccione una versión que se pueda corregir o guarde una versión nueva.

## Preguntas frecuentes

### ¿Dónde veo el total de costos al cierre del mes?

En Costos y presupuestos, elija **Fecha de corte** y abra **Tablero (informe)**. Revise si está usando **$ actual** o **$ año** antes de comparar cifras.

### ¿Dónde consulto lo ejecutado por torre e ítem de pago?

En Costos y presupuestos, abra **Avance diario (cantidades por torre e ítem)**. Consulte **Detalle de torres** o **Detalle de montaje** para separar cantidades por vista. Para consultar porcentajes de obra, abra [Avance de obra](avance-obra.md).

### ¿Qué hago si aparece «No se pudo leer el libro»?

En Costos y presupuestos, revise el detalle del aviso y vuelva a elegir el libro .xlsx en **Importar el libro de costos (Excel)**. Confirme las hojas detectadas antes de importar.

### ¿Qué hago con las torres sin casar al importar el libro de costos?

En **Importar el libro de costos (Excel)**, revise los nombres indicados en el aviso. Compárelos con [Trazado](trazado.md) y corrija las diferencias antes de cargar el libro otra vez.

### ¿Qué pasa con los registros anteriores si vuelvo a importar el libro?

En Costos y presupuestos, **Importar (lo importado antes del libro va a la Papelera)** sustituye los datos de la carga anterior del libro. Revise los avisos y compruebe la carga nueva en **Historial de cargas**.

### ¿Cómo bajo el Excel para cargar mi lista de precios?

En Costos y presupuestos, abra **Formulario de precios** y toque **Descargar plantilla**. Marque **Con los datos actuales del proyecto** si quiere incluir los ítems existentes. Llene la hoja **ItemsPago** y vuelva con **Cargar plantilla**.

### ¿Qué hago si la vista previa de la plantilla trae errores?

En Costos y presupuestos, corrija en Excel la **Hoja**, **Fila** y **Columna** que muestra **Vista previa**. Use **Cargar plantilla** otra vez, diligencie **Motivo** y toque **Aplicar**.

### ¿Cómo revierto una carga de plantilla que hice por error?

En Costos y presupuestos, abra **Historial de cargas**, localice la aplicación de la plantilla y toque **Deshacer la carga**. Revise el alcance y confirme con **Deshacer**.

### ¿Dónde cargo una reprogramación del PDT desde Project?

En Costos y presupuestos, abra **PDT (programa de trabajo)**. Use **Cargar una versión desde MS Project (XML)**, seleccione el XML y revise los avisos antes de guardar la versión nueva.

### ¿Por qué no veo Costos y presupuestos en el menú?

Compruebe el proyecto activo y solicite que revisen su permiso **Ver** en Costos y presupuestos. El rol de fábrica **Consulta** carece de acceso a este módulo.

## Relacionado

- [Avance de obra](avance-obra.md) — muestra el avance físico por torre, pata y fecha.
- [Trazado](trazado.md) — aporta las torres con las que se relacionan las cantidades.
- [Costos de no calidad](costos-no-calidad.md) — consolida rubros de los planes de acción.
