---
modulo: planes-accion
titulo: Planes de acción
menu: Evaluación y mejora › Planes de acción
rutas: ['/planes-accion', '/planes-accion/nuevo', '/planes-accion/{PlanId:int}']
palabras: [acción correctiva, hallazgo, causa raíz, Ishikawa, 5 porqués, eficacia, producto]
opciones:
  - Planes de acción: /planes-accion
  - Abrir plan: /planes-accion | Abrir plan
actualizado: 2026-11-19
---

# Planes de acción

## Para qué sirve

Organice las acciones que responden a un hallazgo, un riesgo u otro problema del proyecto. Documente la causa, asigne responsables, conserve los soportes y verifique si el plan fue eficaz.

ISO 9001, numeral 10.2.

## Cómo entrar

Menú › Evaluación y mejora › Planes de acción.

También puede iniciar un plan desde un riesgo, su seguimiento, una auditoría o sus hallazgos. Si el registro ya tiene un plan asociado, toque su código para consultarlo.

## Qué hay en la pantalla

- El botón de indicador (Plan por plan) abre el detalle del plan seleccionado.
- El botón de tablero (Tablero por estado) agrupa los planes por estado.
- El botón de lista (Lista) abre la tabla. El código de cada fila lleva al plan. Entre las columnas están **Auditoría**, **Acciones** y **Estado del plan**.
- El encabezado incluye el botón de Excel (Importar planes (Excel)), el botón de lápiz (Editar plan (formulario completo)) y el botón de más (Nuevo plan de acción).
- En el formulario, **Análisis de causa** admite texto y herramientas gráficas.
- **Riesgos** muestra las relaciones del plan. El selector **Asociar riesgo** permite agregar otras.
- Las tarjetas de acción muestran **Tipo de acción**, **Fecha**, **Estado**, **Descripción** y **Responsables**. Los productos de cada acción pueden tener anexos.
- **Verificación de eficacia** reúne el criterio, la fecha prevista y, cuando corresponde, el resultado y los soportes.

## Cómo hacer…

### Cómo localizar y consultar un plan

1. Toque el botón de lista (Lista).
2. Localice el plan y toque su código, con el aviso **Abrir plan**.
3. Revise el hallazgo, el análisis y las acciones.
4. Si quiere partir de una acción concreta, toque esa acción en la tabla.
5. Use el botón de tablero (Tablero por estado) para revisar el conjunto agrupado según su avance.

### Cómo crear un plan o editarlo

1. Toque el botón de más (Nuevo plan de acción). Para modificar uno existente, selecciónelo y toque el botón de lápiz (Editar plan (formulario completo)).
2. Describa el hallazgo que origina el plan.
3. Complete **Procesos** y **Lugares** según corresponda.
4. Para vincular riesgos, seleccione cada uno con **Asociar riesgo**.
5. Complete **Análisis de causa** y registre las acciones que correspondan.
6. Toque el botón de disquete (Guardar). El formulario vuelve a la pantalla anterior.

### Cómo usar una herramienta de análisis de causa

1. Abra el formulario y ubique **Análisis de causa**.
2. Toque el botón de más (Agregar herramienta de análisis de causa (5 porqués, Ishikawa, árbol, Pareto…)).
3. Elija la herramienta que va a usar. Puede trabajar con varias en el mismo plan.
4. Registre las causas y marque como **Causa raíz** las que haya identificado.
5. Escriba la conclusión y toque el botón de disquete (Guardar en el campo).
6. De vuelta en el formulario, toque el botón de disquete (Guardar) para conservar el plan.

Si había un diagrama, use el botón de lápiz (Editar análisis de causa). Toque la miniatura para ampliarla o use el botón de pantalla (Maximizar).

### Cómo registrar una acción y sus soportes

1. En el formulario del plan, toque el botón de más (Agregar acción).
2. Indique **Tipo de acción**, **Fecha** y **Estado**.
3. Escriba la **Descripción** y seleccione los **Responsables**.
4. Toque el botón de más (Agregar producto) y registre el producto de la acción.
5. Toque el botón de clip (Adjuntar anexos a este producto) y seleccione sus archivos.
6. Toque el botón de disquete (Guardar) del plan.

Si una acción tiene varios productos, repita los pasos de producto y anexos en la misma tarjeta.

### Cómo programar y verificar la eficacia

1. Abra el formulario y busque **Verificación de eficacia**.
2. Diligencie **Criterio: qué se revisará para saber si la causa se eliminó**.
3. Indique la **Fecha programada de verificación** y guarde.
4. Una vez cerrado el plan, vuelva al bloque y elija **Eficaz** o **No eficaz** en **Resultado**.
5. Complete **Fecha de verificación**, **Verificó** y **Evidencia de la verificación (qué se revisó y qué se encontró)**.
6. Si corresponde, toque el botón de adjuntar (Adjuntar soportes (fotos, registros, actas)).
7. Toque el botón de disquete (Guardar).

Si el plan guardado quedó como **No eficaz**, puede usar el botón de plan (No eficaz: abrir un plan nuevo para el mismo origen).

### Cómo importar planes y acciones desde Excel

1. Toque el botón de Excel (Importar planes (Excel)).
2. Seleccione el libro, la **Hoja** y la fila de encabezados.
3. Asigne el destino de las columnas.
4. Revise el código de cada plan. Las filas con el mismo código se agrupan en un plan.
5. Toque el botón de importar (Importar N fila(s)). Cada fila representa una acción.
6. Revise el resultado y abra un plan importado desde su código.

Si el código ya existe, la importación puede añadirle acciones. En el panel de cargas puede usar el botón de ubicación (Ver solo esta carga).

## Permisos

- **Ver** corresponde a la consulta de planes y acciones. **Descargar** corresponde a los archivos y las exportaciones.
- **Crear**, **Editar** y **Eliminar** corresponden al alta, la modificación y el envío a la Papelera.
- **Importar** corresponde a la carga desde Excel. **ConfigurarCampos** y **EditarVistas** corresponden a sus ajustes respectivos.
- El rol de fábrica **Coordinador / aprobador** incluye **Aprobar** y se describe como el rol que cierra planes y verifica eficacia.
- El rol de fábrica **Consulta** permite ver y descargar. Los permisos efectivos dependen de la configuración del proyecto.

> Por confirmar: qué permiso efectivo controla guardar el resultado de eficacia cuando se combinan permisos individuales.

## Problemas frecuentes

- **«No aparece Resultado en Verificación de eficacia»**. Revise el estado del plan. El resultado se habilita al quedar cerrado o si ya existe una verificación.
- **«Editar plan (formulario completo) está deshabilitado»**. Seleccione un plan y compruebe el permiso **Editar**.
- **«Mi plan no aparece en la lista»**. Quite el filtro de carga con el botón de equis (Quitar) si ve una etiqueta con el nombre del archivo.
- **«Las acciones importadas quedaron en un plan existente»**. Compare los códigos del archivo y del listado. Un mismo código agrupa las acciones.

## Preguntas frecuentes

### ¿Cómo creo un plan de acción?
Botón **Agregar** del encabezado (o desde un riesgo, un seguimiento o un hallazgo de auditoría, que llenan el origen solos).
Llene los pasos, agregue las acciones y **Guardar**.

### ¿Cómo agrego los soportes de una acción?
En la tarjeta de la acción, en cada producto, adjunte los archivos. También quedan en Adjuntos del plan, en una carpeta
automática por acción.

### ¿Cómo hago el análisis de causa?
En el campo Análisis de causa toque editar: puede usar 5 porqués, Ishikawa, árbol de causas, lluvia de ideas, Pareto, AMEF o
Es / No es, marcar la causa raíz y escribir la conclusión.

### ¿Cómo verifico la eficacia?
Al crear el plan defina el criterio y la fecha programada. Con el plan cerrado registre el resultado (eficaz o no), la fecha,
quién verificó y los soportes. Si no fue eficaz, el hallazgo se reabre.

### ¿Cómo asocio el plan a uno o varios riesgos?
En el editor del plan, use el selector de riesgos: puede elegir varios.

### ¿Cómo importo planes desde Excel?
Importar del encabezado: una fila = una acción; las filas con el mismo código forman el mismo plan. Recargar el mismo archivo no
duplica.

### ¿Cómo veo los planes como tablero o lista?
Con las vistas del encabezado: Plan (uno a uno), Tablero (Abierta / En ejecución / Cerrada) y Lista (tabla).

### ¿Por qué no me deja guardar el plan?
Falta algún campo obligatorio: el mensaje al pie dice cuál («Falta diligenciar: …»).

## Relacionado

- [Auditoría interna](auditoria-interna.md) — permite crear y vincular planes para uno o varios hallazgos.
- [Riesgos](riesgos.md) — permite iniciar un plan desde un riesgo o desde su seguimiento.
- [Indicadores](indicadores.md) — puede calcular el porcentaje de planes cerrados.
