---
modulo: organigrama
titulo: Organigrama
menu: Contexto y liderazgo › Organigrama
rutas: [/organigrama]
palabras: [organigrama, cargos, cargo, jefe, subordinado, staff, apoyo, ocupante, plazas, versión, borrador, vigente, obsoleta, aprobar, publicar, importar organigrama]
opciones:
  - Versión anterior: /organigrama?vista=cambios | Versión anterior
  - Versión posterior: /organigrama?vista=cambios | Versión posterior
  - Organigrama: /organigrama
  - Pantalla completa: /organigrama | Pantalla completa
  - Importar organigrama (Excel) a un borrador: /organigrama | Importar organigrama (Excel) a un borrador
  - Acercar: /organigrama | Acercar
  - Alejar: /organigrama | Alejar
  - Ajustar a la pantalla: /organigrama | Ajustar a la pantalla
  - Desplegar todo: /organigrama | Desplegar todo
  - Plegar todo: /organigrama | Plegar todo
  - Lista de cargos: /organigrama?vista=lista | Lista de cargos
  - Versiones y control de cambios: /organigrama?vista=cambios | Versiones y control de cambios
  - Organigrama (diagrama): /organigrama | Organigrama
  - Buscar cargo o persona: /organigrama | Buscar cargo o persona
  - Crear organigrama (borrador): /organigrama | Crear organigrama (borrador)
  - Editar (crea un borrador): /organigrama | Editar (crea un borrador)
  - Editar borrador: /organigrama | Editar borrador
  - Agregar cargo (subordinado del seleccionado): /organigrama | Agregar cargo (subordinado del seleccionado)
  - Agregar subordinado: /organigrama | Agregar subordinado
  - Agregar cargo de apoyo: /organigrama | Agregar cargo de apoyo
  - Mover antes: /organigrama | Mover antes
  - Mover después: /organigrama | Mover después
  - Quitar cargo: /organigrama | Quitar cargo
  - Quitar también toda su rama: /organigrama | Quitar también toda su rama
  - Cancelar edición: /organigrama | Cancelar edición
  - Guardar: /organigrama | Guardar
  - Exportar: /organigrama | Exportar
  - Crear borrador con lo importado: /organigrama | Crear borrador con lo importado
  - Eliminar versión: /organigrama | Eliminar versión
  - Aprobar y publicar: /organigrama | Aprobar y publicar
  - Publicar como vigente: /organigrama | Publicar como vigente
  - Enviar a la Papelera: /organigrama | Enviar a la Papelera
  - Elegir archivo: /organigrama | Elegir archivo
  - Nuevo cargo: /organigrama | Nuevo cargo: arrástralo sobre un jefe o una línea de mando
  - Nuevo cargo de apoyo: /organigrama | Nuevo cargo de apoyo: arrástralo sobre un jefe
actualizado: 2026-11-21
---

# Organigrama

## Para qué sirve
Muestra la estructura de cargos del proyecto (quién depende de quién, ocupantes y plazas) con **control de cambios
por versiones**: cada versión es una foto completa del organigrama. ISO 9001 · 5.3.

## Cómo entrar
Menú › **Contexto y liderazgo** › **Organigrama**.

## Qué hay en la pantalla
- Vistas: **Organigrama** (el diagrama), **Lista de cargos** y **Versiones y control de cambios**.
- Selector de versión: la **vigente**, el **borrador** (si existe) y las anteriores (**Versión anterior** / **Versión posterior**).
- Cifras: **Cargos**, **Niveles jerárquicos**, **Personas nombradas**, **Cargos sin ocupante**, **Cargos con supuestos por confirmar**.
- En el diagrama: **Acercar**, **Alejar**, **Ajustar a la pantalla**, **Desplegar todo**, **Plegar todo**,
  **Buscar cargo o persona** y **Pantalla completa**. También se hace zoom con Ctrl + rueda y se mueve arrastrando el fondo.
- **Importar organigrama (Excel) a un borrador** y Exportar (Excel o PDF).

### Estados de una versión
**Borrador** (se puede editar) → **Vigente** (publicada) → **Obsoleta** (cuando otra la reemplaza).
Solo el borrador se edita.

## Cómo hacer…

### Cómo hacer un cambio en el organigrama
1. Use **Editar (crea un borrador)** desde la versión vigente o **Editar borrador** si ya lo tiene.
2. Haga los cambios (ver abajo). Todo queda en pantalla hasta guardar.
3. Toque **Guardar**. **Cancelar edición** descarta.
4. Use **Aprobar y publicar** para abrir el formulario. Diligencie la vigencia, el motivo y las firmas. Toque **Publicar como vigente**.
   La versión anterior pasa a obsoleta.

### Cómo agregar un cargo
- Con un cargo seleccionado: **Agregar subordinado** (o **Agregar cargo (subordinado del seleccionado)**).
- O arrastre desde la paleta **Nuevo cargo** sobre un jefe o sobre una línea de mando (queda antes o después de
  otro cargo). Para un cargo de apoyo (staff), arrastre **Nuevo cargo de apoyo** sobre un jefe.
- Llene en la ficha: cargo, ocupante(s) (**Varios: uno por línea o separados por coma**), área, **Plazas** y, si un dato
  no es seguro, marque **Supuesto por confirmar**.

### Cómo mover o quitar un cargo
- Mover: en edición, arrastre la tarjeta sobre su nuevo jefe, o use **Mover antes** / **Mover después**.
- Quitar: **Quitar cargo**. Si tiene subordinados, puede elegir **Quitar también toda su rama**.

### Cómo comparar dos versiones
Vista **Versiones y control de cambios**: línea de tiempo de versiones y comparación A → B (cargos nuevos, quitados y cambiados).

### Cómo importar el organigrama desde Excel
1. **Importar organigrama (Excel) a un borrador** › **Elegir archivo**.
2. Revise lo leído y toque **Crear borrador con lo importado**. La app reconoce los cargos que ya existían.
3. Revise el borrador y publíquelo.

## Permisos
**Ver** permite consultar, **Descargar** permite exportar, **Editar** permite cambiar el borrador e **Importar** corresponde a Excel. **Eliminar** corresponde a enviar una versión a la Papelera y **Aprobar** a publicar. **Consulta** de fábrica puede ver y descargar.

## Problemas frecuentes
- **«No puedo cambiar nada»** → está viendo una versión publicada. Toque **Editar** para crear el borrador.
- **«Hice cambios y no aparecen en la vigente»** → están en el borrador: falta **Publicar como vigente**.
- **«El diagrama no cabe»** → **Ajustar a la pantalla** o **Plegar todo**.

## Preguntas frecuentes

### ¿Cómo hago un cambio en el organigrama vigente?
Toque **Editar**: la app crea un borrador copiando la versión vigente. Haga los cambios, **Guardar** y luego **Aprobar** para que
el borrador quede vigente.

### ¿Por qué no puedo editar el organigrama?
Solo el borrador se edita. Si está viendo la versión vigente u obsoleta, use **Editar** para abrir el borrador.

### ¿Cómo agrego un cargo debajo de un jefe?
En edición, arrastre «cargo nuevo» de la paleta sobre la tarjeta del jefe (o sobre una línea de mando). La ficha se llena con el
jefe y el área.

### ¿Qué es un cargo de apoyo (staff)?
Un cargo que asesora a un jefe sin estar en su línea de mando; se dibuja al lado.

### ¿Cómo comparo dos versiones?
Vista **Versiones**: elija A y B para ver qué cargos se agregaron, quitaron o cambiaron.

### ¿Cómo importo el organigrama desde Excel?
Con el botón de importar del encabezado: el Excel debe tener las columnas del formato que exporta la app. Lo importado queda
como borrador.

### ¿Qué pasa con un cargo que quito?
Va a la Papelera al guardar el borrador, con la misma fecha; las versiones anteriores lo conservan.

### ¿Cómo exporto el organigrama?
Exportar Excel (hoja Organigrama y Control de cambios) o PDF en tamaño A3.

### ¿Cómo veo si hay cargos sin persona asignada?

Revise **Cargos sin ocupante** y **Lista de cargos**.

### ¿Cómo apruebo el cambio para que lo vean todos?

Abra el borrador, toque **Aprobar y publicar** en el encabezado, diligencie vigencia, motivo y firmas y use **Publicar como vigente**.

## Relacionado
- [Partes interesadas](partes-interesadas.md) — mismo manejo de versiones.
- [Manuales de funciones](manuales-funciones.md)
