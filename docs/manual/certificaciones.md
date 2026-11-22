---
modulo: certificaciones
titulo: Certificaciones
menu: Apoyo › Certificaciones
rutas: [/certificaciones]
palabras: [certificados, certificación, ISO 9001, ISO 14001, ISO 45001, vigencia, vencido, por vencer, vitrina, ente certificador, subir, cargar, pdf, renovación, descargar, historial, borrar, papelera, no aparece]
opciones:
  - Certificaciones: /certificaciones
  - Pantalla completa: /certificaciones?vista=vitrina | Pantalla completa
  - Agregar certificado: /certificaciones | Agregar certificado
  - Ver actual: /certificaciones?vista=vitrina | Ver actual
  - Actualizar: /certificaciones?vista=vitrina | Actualizar
  - Versiones obsoletas: /certificaciones?vista=vitrina | Versiones obsoletas
  - Ver todos: /certificaciones?vista=vitrina | Ver todos
  - Por vencer: /certificaciones | Por vencer
  - Vencidas: /certificaciones | Vencidas
  - Editar: /certificaciones?vista=vitrina | Editar
  - Descargar: /certificaciones?vista=vitrina | Descargar
  - Elegir archivo: /certificaciones | Elegir archivo
  - Guardar: /certificaciones | Guardar
  - Eliminar: /certificaciones?vista=vitrina | Eliminar
  - Enviar a la Papelera: /certificaciones?vista=vitrina | Enviar a la Papelera
  - Vitrina: /certificaciones?vista=vitrina | Vitrina
  - Lista: /certificaciones?vista=lista | Lista
  - Cancelar: /certificaciones | Cancelar
  - Certificado: /certificaciones | Certificado
  - Documento: /certificaciones | Documento
  - Sin archivo vigente: /certificaciones | Sin archivo vigente
  - Norma: /certificaciones | Norma
  - Sistema: /certificaciones | Sistema
  - N.º de certificado: /certificaciones | N.º de certificado
  - Organismo certificador: /certificaciones | Organismo certificador
  - Emisión: /certificaciones | Emisión
  - Vencimiento: /certificaciones | Vencimiento
  - Alcance: /certificaciones | Alcance
  - Documento del listado: /certificaciones | Documento del listado
  - Código del documento: /certificaciones | Código del documento
  - Certificado vigente (PDF o imagen): /certificaciones | Certificado vigente (PDF o imagen)
actualizado: 2026-11-21
---

# Certificaciones

## Para qué sirve
Muestra los **certificados de la empresa** (por ejemplo ISO 9001) en una vitrina, con su vigencia: cuáles están
vigentes, cuáles **por vencer** (90 días o menos) y cuáles vencidos. Cada certificado guarda su PDF vigente y
sus versiones anteriores. Es un módulo **de la empresa**: se ve igual en todos los proyectos.

## Cómo entrar
Menú › **Apoyo** › **Certificaciones**.

## Qué hay en la pantalla
- Vistas: **Vitrina** (marcos con la primera página del certificado, sello y barra de vigencia) y **Lista**.
- Filtros: **Todas**, **Vigentes**, **Por vencer**, **Vencidas**.
- **Pantalla completa** (modo exhibición) y **Agregar certificado**.
- En cada certificado: **Ver actual**, **Descargar**, **Actualizar**, **Versiones obsoletas**, **Ver todos**,
  **Editar** y **Eliminar**.

## Cómo hacer…

### Cómo agregar un certificado
1. Toque **Agregar certificado**.
2. Llene los datos (norma, ente, fechas de vigencia…) y elija el PDF (**Elegir archivo**).
3. Toque **Guardar**.

### Cómo cargar la renovación de un certificado
1. En el certificado, toque **Actualizar**. Se abre su documento en **Documentos**, en la pantalla de actualizar.
2. Agregue la revisión nueva con el PDF vigente y guarde. Al guardar vuelve a Certificaciones.
3. Revise que las fechas de vigencia del certificado estén al día (**Editar**).

### Cómo saber qué certificados están por vencer
Toque el filtro **Por vencer** (90 días o menos) o **Vencidas**.

## Permisos
**Ver** permite consultar y **Descargar** obtener archivos. **Crear** y **Editar** corresponden a altas, fichas y versiones. **Eliminar** envía el certificado a la Papelera. **Consulta** de fábrica ve y descarga. En proyectos con Certificaciones de Empresa, la fuente compartida es de solo lectura.
Eliminar un certificado **no** borra su documento del listado maestro.

## Problemas frecuentes
- **«El marco sale vacío»** → el certificado no tiene PDF vigente (**Sin archivo vigente**): use **Actualizar**.
- **«No puedo editar»** → en los proyectos este módulo es de solo lectura; se administra en la Empresa.
- **«Sigue saliendo vencido después de cargar la renovación»** → actualice las fechas de vigencia con **Editar**.
  La vigencia se calcula con **Vencimiento** en la ficha del certificado.

## Preguntas frecuentes

### ¿Cómo subo el PDF de un certificado nuevo?

En Certificaciones, toque **Agregar certificado**, diligencie la norma y las fechas de vigencia, use **Elegir archivo** para seleccionar el PDF y toque **Guardar**.

### ¿Dónde veo cuáles certificados están por vencer?

En Certificaciones, use **Por vencer** para revisar los que vencen en 90 días o menos. Use **Vencidas** para revisar los que ya vencieron.

### ¿Qué hago si la vitrina dice «Sin archivo vigente»?

En Certificaciones, localice la tarjeta y toque **Actualizar**. En [Documentos](documentos.md), agregue una revisión con el PDF vigente y guárdela. Al volver, compruebe que esté disponible **Ver actual**.

### ¿Por qué sigue vencido si ya cargué el PDF renovado?

En Certificaciones, toque **Editar** en la tarjeta del certificado, actualice **Vencimiento** y toque **Guardar**. La vigencia se calcula con esa fecha de la ficha del certificado.

### ¿Dónde descargo el PDF vigente o busco uno viejo?

En Certificaciones, use **Descargar** en la tarjeta para obtener el PDF vigente. Use **Versiones obsoletas** o **Ver todos** para consultar las revisiones en [Documentos](documentos.md).

### ¿Qué hago si ya existe un documento con ese código?

Si Certificaciones muestra «Ya existe un documento con el código …», compruebe el código en [Documentos](documentos.md). Si está renovando un certificado de la vitrina, abra su tarjeta y use **Actualizar**.

### ¿Qué pasa con el PDF si borro el certificado de la vitrina?

En Certificaciones, **Eliminar** y **Enviar a la Papelera** retiran el certificado de la vitrina. El documento y sus revisiones siguen en [Documentos](documentos.md).

### ¿Dónde veo todos los certificados en una tabla?

En Certificaciones, toque **Lista** para verlos en tabla. Use **Vitrina** para volver a los marcos con la vista previa.

### ¿Por qué no aparecen las opciones para renovar o borrar un certificado?

Si el proyecto toma Certificaciones de la Empresa, la vista del proyecto permite consultar los certificados compartidos. Cambie a la Empresa para actualizarlos.

### ¿Cómo vinculo un certificado con algo que ya está en Documentos?

Use **Agregar certificado** y seleccione el registro en **Documento**. Puede abrir su archivo vigente desde la ficha.

## Relacionado
- [Documentos](documentos.md) — cada certificado es un documento del listado (tipo «Certificados»).
- [Direccionamiento estratégico](direccionamiento.md) — misma forma de trabajo.
