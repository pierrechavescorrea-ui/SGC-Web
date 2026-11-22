---
modulo: papelera
titulo: Papelera de reciclaje
menu: Papelera
rutas: [/papelera]
palabras: [papelera, reciclaje, recuperar, restaurar, borré, borrado, recuperar eliminado, basurero, vaciar, depurar, borrar para siempre, retención]
opciones:
  - Papelera: /papelera
  - Campos: /papelera | Campos
  - Exportar: /papelera | !Exportar
  - Buscar en Papelera: /papelera | Buscar por módulo, registro o detalle...
  - Mostrar 500 filas más: /papelera | Mostrar 500 filas más
  - Mostrar todas las filas: /papelera | Mostrar todas las filas
  - Seleccionar todos los que coinciden con el filtro: /papelera | Seleccionar todos los que coinciden con el filtro
  - Restaurar: /papelera | Restaurar
  - Restaurar de todos modos: /papelera | Restaurar de todos modos
  - Eliminar definitivamente: /papelera | Eliminar definitivamente
  - Vaciar seleccionados: /papelera | Vaciar seleccionados
  - Vaciar todo: /papelera | Vaciar todo
  - Borrar para siempre: /papelera | Borrar para siempre
  - Cancelar: /papelera | Cancelar
actualizado: 2026-11-21
---

# Papelera de reciclaje

## Para qué sirve

Recupere registros eliminados o borre definitivamente los que ya no necesita. La tabla reúne los registros de la Papelera del proyecto activo.

## Cómo entrar

Toque **Papelera** en el menú lateral.

## Qué hay en la pantalla

- El encabezado muestra el total y ofrece **Campos**, **Exportar**, **Vaciar seleccionados** y **Vaciar todo**. Los últimos dos botones muestran un conteo en su etiqueta al pasar el puntero.
- Filtre por **Módulo**, o escriba en **Buscar por módulo, registro o detalle...**. Puede ordenar por columna y ver **Días restantes**.
- Se muestran hasta 200 filas inicialmente. Use **Mostrar 500 filas más** o **Mostrar todas las filas**. Cada fila tiene una casilla, **Restaurar** y **Eliminar definitivamente**.

## Cómo hacer…

### Cómo recuperar un registro borrado

1. Busque el registro por módulo, nombre o detalle.
2. Toque **Restaurar** en su fila.
3. Si aparece un aviso, revise los menús afectados y la solución sugerida. Use **Restaurar de todos modos** para continuar o **Cancelar** para salir.
4. Compruebe el resultado y abra el módulo original.

### Cómo borrar definitivamente uno o varios registros

1. Para una fila use **Eliminar definitivamente**. Para varias, márquelas y toque **Vaciar seleccionados**.
2. Compruebe el registro o la cantidad en la confirmación. **Vaciar todo** incluye los registros de todos los módulos de la Papelera, también los que el filtro oculta.
3. Toque **Borrar para siempre** para confirmar. Se eliminan los archivos asociados. Espere el resultado.

### Cómo exportar el listado visible

1. Aplique los filtros y muestre todas las filas que quiere incluir.
2. Toque **Exportar** y elija **Excel** o **PDF**.

## Permisos

En la configuración de fábrica, el **Administrador del proyecto** tiene acceso a la Papelera y puede restaurar y eliminar definitivamente. **Consulta**, **Consulta sin descarga**, **Colaborador**, **Editor** y **Coordinador / aprobador** carecen de acceso de fábrica. Los permisos efectivos se pueden ajustar en Administración › Roles y permisos.

## Problemas frecuentes

- **«No hay resultados para ese filtro.»** Borre la búsqueda y elija **Todos** en **Módulo**.
- **«El botón está deshabilitado.»** Espere si hay una operación en curso. Para **Vaciar seleccionados** primero marque filas.
- **«El registro sigue ahí después de vaciar.»** Lea el resultado. Si tiene dependencias, la app vuelve a intentar el borrado durante el vaciado y muestra los fallos que queden.

## Preguntas frecuentes

### ¿Cómo recupero un registro borrado en la Papelera?
Busque su fila en **Papelera** y toque **Restaurar**. Si sale una advertencia, revísela antes de confirmar.

### ¿Puedo buscar por lo que decía el registro?
Sí. Busque por el nombre o el detalle que aparece en la Papelera, o elija un módulo.

### ¿Por qué no sale la lista de módulos?
El selector aparece cuando la Papelera reconoce más de un módulo. Espere a que cargue la tabla para usarlo.

### ¿Para qué salen días al lado del registro?
**Días restantes** indica cuánto queda de la retención de 30 días, cuando se conoce la fecha de eliminación.

### ¿Cómo veo los otros registros si solo salen 200?
Toque **Mostrar 500 filas más** o **Mostrar todas las filas**.

### ¿El Excel incluye también las filas que todavía no se ven?
Use **Mostrar todas las filas** antes de **Exportar** para incluir todas las coincidencias del filtro.

### ¿La casilla del encabezado marca solo las filas que veo?
**Seleccionar todos los que coinciden con el filtro** marca todas las coincidencias, incluso las filas que aún no se muestran.

### ¿Vaciar todo respeta el filtro de módulo?
**Vaciar todo** incluye toda la Papelera del proyecto. Para limitar el borrado, marque las filas deseadas y use **Vaciar seleccionados**.

### ¿Cómo borro para siempre un solo registro?
En su fila, toque **Eliminar definitivamente** y confirme con **Borrar para siempre**. Se eliminan también sus archivos asociados.

### ¿Por qué me avisa algo antes de recuperar el registro?
Algunas restauraciones afectan otros datos. Revise el aviso y decida entre **Cancelar** y **Restaurar de todos modos**.

### ¿Por qué no encuentro la Papelera en el menú?
El rol de fábrica **Administrador del proyecto** tiene acceso. Pida que revisen sus permisos para el proyecto activo.

## Relacionado

- [Administración](administracion.md) — roles y permisos por proyecto.
