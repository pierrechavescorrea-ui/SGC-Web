---
modulo: costos-no-calidad
titulo: Costos de no calidad
menu: Evaluación y mejora › Costos de no calidad
rutas: ['/costos-no-calidad']
palabras: [costo de no calidad, rubro, hallazgo, Pareto, costo asociado, plan de acción, retrabajos, pérdida, concepto, pesos, exportar, excel, plan borrado, papelera, no aparece, reproceso, sobrecosto, costos de fallas, desperdicio]
opciones:
  - Ver el plan en Planes de acción: /costos-no-calidad?vista=plan | Ver el plan en Planes de acción
  - Ver el plan: /costos-no-calidad?vista=tabla | Ver el plan
  - Costos de no calidad: /costos-no-calidad
  - Año del hallazgo: /costos-no-calidad | Año del hallazgo
  - Origen del hallazgo: /costos-no-calidad | Origen del hallazgo
  - Tipo de hallazgo: /costos-no-calidad | Tipo de hallazgo
  - Proceso: /costos-no-calidad | Proceso
  - Estado del plan: /costos-no-calidad | Estado del plan
  - Solo con costo: /costos-no-calidad | Solo con costo
  - Plan por plan: /costos-no-calidad?vista=plan
  - Tabla: /costos-no-calidad?vista=tabla
  - Gráficos: /costos-no-calidad?vista=graficos
  - Filtros: /costos-no-calidad | !Filtros
  - Quitar filtros: /costos-no-calidad | Quitar filtros
  - Ordenar por costo (mayor primero): /costos-no-calidad?vista=plan | Ordenar por costo (mayor primero)
  - Editar los costos (formulario del plan): /costos-no-calidad?vista=plan | Editar los costos (formulario del plan)
  - Exportar a Excel (lo filtrado): /costos-no-calidad?vista=tabla | Exportar a Excel (lo filtrado)
  - Pantalla completa: /costos-no-calidad | Pantalla completa
  - Salir de pantalla completa: /costos-no-calidad | Salir de pantalla completa
  - Abrir el diseñador del formulario: /editor?pestana=formularios&form=planes-accion
  - Buscar código, hallazgo, origen…: /costos-no-calidad | Buscar código, hallazgo, origen…
  - Ordenar por fecha (reciente primero): /costos-no-calidad?vista=plan | Ordenar por fecha (reciente primero)
  - Elementos del gráfico: /costos-no-calidad?vista=graficos | Elementos del gráfico
  - Anterior: /costos-no-calidad?vista=plan | Anterior
  - Siguiente: /costos-no-calidad?vista=plan | Siguiente
actualizado: 2026-11-21
---

# Costos de no calidad

## Para qué sirve

Consulte los costos registrados en los planes de acción y compare su distribución por rubro. Use los filtros para revisar los costos de un conjunto de hallazgos o de un plan concreto.

## Cómo entrar

Menú › Evaluación y mejora › Costos de no calidad.

También puede abrir el formulario de un plan desde el detalle de costos.

## Qué hay en la pantalla

- El encabezado incluye el botón de indicador (Plan por plan), el botón de tabla (Tabla) y el botón de gráfico (Gráficos).
- El botón de embudo (Filtros) abre **Año del hallazgo**, **Origen del hallazgo**, **Tipo de hallazgo**, **Proceso** y **Estado del plan**.
- El panel de filtros también incluye **Buscar código, hallazgo, origen…** y **Solo con costo**.
- El tablero resume el total, la tendencia y la composición por rubro.
- **Plan por plan** muestra una lista lateral y el detalle del plan. Puede ordenar por costo o fecha.
- **Tabla** incluye **Código**, **Fecha**, **Hallazgo**, **Origen**, **Tipo**, **Proceso**, **Estado** y las columnas que se obtienen del paso de costos.
- **Gráficos** muestra la distribución por rubro, el comportamiento por mes y otras comparaciones del conjunto filtrado.

## Cómo hacer…

### Cómo consultar los costos de un año o proceso

1. Toque el botón de embudo (Filtros).
2. Elija **Año del hallazgo** y **Proceso**.
3. Si necesita acotar más, use **Origen del hallazgo**, **Tipo de hallazgo** o **Estado del plan**.
4. Marque **Solo con costo** para ocultar los planes sin valores en ese paso.
5. Revise el total y la composición por rubro.

### Cómo localizar el plan de mayor costo

1. Toque el botón de indicador (Plan por plan).
2. Toque el botón de orden (Ordenar por costo (mayor primero)).
3. Seleccione el primer plan de la lista filtrada.
4. Revise el costo y su distribución por rubro.
5. Use **Ver el plan en Planes de acción** para consultar el hallazgo y sus acciones.

### Cómo modificar los costos de un plan

1. Seleccione el plan en **Plan por plan**.
2. Toque el botón de lápiz (Editar los costos (formulario del plan)).
3. Busque el paso de costos en el formulario.
4. Corrija los rubros que correspondan y toque el botón de disquete (Guardar) del plan.
5. Vuelva a **Costos de no calidad** y revise el importe actualizado.

### Cómo comparar los rubros en gráficos

1. Aplique los filtros del conjunto que necesita revisar.
2. Toque el botón de gráfico (Gráficos).
3. Revise el Pareto para ordenar los rubros por costo y consultar el porcentaje acumulado.
4. Compare los meses en el gráfico de composición por rubro.
5. Use el botón de más (Elementos del gráfico) para ajustar los elementos disponibles.

### Cómo exportar los resultados filtrados

1. Aplique los filtros de la consulta.
2. Abra el botón de tabla (Tabla).
3. Revise las filas y los totales que necesita exportar.
4. Toque el botón de Excel (Exportar a Excel (lo filtrado)).
5. Para exportar otro conjunto, use el botón de equis (Quitar filtros) y aplique la nueva selección.

### Cómo revisar qué campos entran en el resumen

1. Abra el diseñador desde el botón de formulario del encabezado. Su aviso empieza por **Campos del paso**.
2. Localice el paso de costos y compruebe que **Módulo que lo lee** indique **Costos de no calidad**.
3. Revise qué campos están marcados **Resumen**.
4. Use campos numéricos para los rubros y los demás campos admitidos para clasificar los resultados.
5. Vuelva al módulo y revise las columnas de **Tabla**.

### Cómo navegar los costos plan por plan

1. Abra **Plan por plan**. Ordene por costo o use **Ordenar por fecha (reciente primero)**.
2. Use **Anterior** y **Siguiente** o elija un plan de la lista.
3. Toque **Ver el plan en Planes de acción** para abrir su registro.

## Permisos

- **Ver** da acceso a la consulta. **Descargar** corresponde a exportar los datos.
- El rol de fábrica **Consulta** carece de acceso a este módulo. **Coordinador / aprobador** puede consultar y descargar.
- Para cambiar los valores del plan necesita **Editar** en **Planes de acción**. **Editar formularios** corresponde al diseño del formulario.

## Problemas frecuentes

- **«No encuentro un plan»**. Revise **Solo con costo**, el año, los demás filtros y si el plan está en la Papelera.
- **«El rubro que diligencié no aparece»**. Revise que el campo esté en el paso vinculado y esté marcado **Resumen**.
- **«El botón de Excel está deshabilitado»**. Compruebe que los filtros dejan resultados y que tiene **Descargar**.
- **«El tablero no muestra costos»**. Revise que exista un paso vinculado a **Costos de no calidad** y que tenga campos numéricos marcados **Resumen**.
- **«No veo Costos de no calidad en el menú»**. Consulte el permiso **Ver** con el administrador del proyecto.

## Preguntas frecuentes

### ¿Cómo veo cuánto costaron los hallazgos de un proceso este año?

En Costos de no calidad, abra **Filtros** y seleccione **Año del hallazgo** y **Proceso**. Use **Solo con costo** si quiere contar únicamente los planes con costo.

### ¿Dónde veo qué plan de acción salió más caro?

En Costos de no calidad, abra **Plan por plan** y toque **Ordenar por costo (mayor primero)**. Abra el primer plan de la lista filtrada para ver sus rubros.

### ¿Dónde corrijo un valor de costo mal digitado?

En Costos de no calidad, seleccione el plan en **Plan por plan** y toque **Editar los costos (formulario del plan)**. Corrija el importe en [Planes de acción](planes-accion.md) y guarde. Para modificarlo necesita **Editar** en Planes de acción.

### ¿Por qué no aparece el rubro que diligencié en el plan?

En el editor del formulario de Planes de acción, revise que el rubro sea un campo numérico marcado **Resumen** dentro del paso cuyo **Módulo que lo lee** es **Costos de no calidad**.

### ¿Qué me muestra el Pareto de no calidad?

En **Gráficos** de Costos de no calidad, el Pareto ordena los rubros por importe y muestra el porcentaje acumulado del conjunto filtrado.

### ¿Cómo saco a Excel solo los costos que estoy viendo?

En Costos de no calidad, aplique los filtros, abra **Tabla** y toque **Exportar a Excel (lo filtrado)**. Si sigue deshabilitado, compruebe que haya filas y que tenga **Descargar**.

### ¿Por qué dejó de aparecer un plan que tenía costos?

En Costos de no calidad, abra **Filtros**, revise **Solo con costo** y pruebe **Quitar filtros** si está disponible. Si el plan sigue ausente, compruebe si fue enviado a la [Papelera](general.md).

### ¿Por qué no veo Costos de no calidad en el menú?

Solicite al administrador del proyecto que revise **Ver** en Costos de no calidad. El rol de fábrica **Consulta** carece de acceso a este módulo.

### ¿Dónde veo el hallazgo que originó un costo?

En Costos de no calidad, ubique el registro en **Tabla** y toque su código, con el aviso **Ver el plan**. En el plan consulte el hallazgo y sus acciones.

### ¿Cómo veo primero los hallazgos más recientes?
En **Plan por plan**, use **Ordenar por fecha (reciente primero)**.

### ¿Dónde ajusto cómo se ve el gráfico de costos?
Entre en **Gráficos** y toque **Elementos del gráfico** en el gráfico que va a revisar.

## Relacionado

- [Planes de acción](planes-accion.md) — es el origen de los costos y permite corregirlos.
- [Auditoría interna](auditoria-interna.md) — sus hallazgos pueden dar origen a planes de acción.
- [Dossier](dossier.md) — permite incluir los datos del módulo si tiene acceso.
