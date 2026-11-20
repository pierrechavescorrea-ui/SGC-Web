---
modulo: modelos-3d
titulo: Modelos 3D
menu: Operación › Modelos 3D
rutas: ['/ifc', '/modelos3d/detalle']
palabras: [IFC, visor, torre, piezas, perfiles, versiones, JSON, Línea 3D]
opciones:
  - BP6: /ifc?modelo=torre-bp6.ifc
  - Datos de la torre, validación y supuestos: /ifc?modelo=torre-bp6.ifc | Datos de la torre, validación y supuestos
  - Descargar .ifc: /ifc?modelo=torre-bp6.ifc | Descargar .ifc
  - 'Detalle por pieza: torre completa (piloto)': /modelos3d/detalle
  - BP6 · torre completa (piloto): /modelos3d/detalle
  - Detalle por pieza: /modelos3d/detalle
  - Modelos en Línea 3D: /ifc | Modelos en Línea 3D
  - Lista de materiales: /ifc?modelo=torre-bp6.ifc | Lista de materiales
  - Versiones: /ifc?modelo=torre-bp6.ifc | Versiones
  - Nueva versión: /ifc?modelo=torre-bp6.ifc | Nueva versión
actualizado: 2026-11-19
---

# Modelos 3D

## Para qué sirve

Consulte los modelos de torres, su lista de materiales y los documentos de validación. Cargue modelos IFC y administre sus versiones y vínculos con Línea 3D.

ISO 9001, numeral 8.5.

## Cómo entrar

Menú › Operación › Modelos 3D.

Seleccione una familia y después el modelo. Desde el modelo **BP6** puede abrir **Detalle por pieza: torre completa (piloto)**.

## Qué hay en la pantalla

- La galería agrupa los modelos por familia. Cada tarjeta puede mostrar el código, versión, cantidad de piezas y datos de validación.
- En la galería, el botón de importación (Cargar modelo IFC) abre el formulario de carga.
- En el detalle están **Lista de materiales**, **Datos de la torre, validación y supuestos**, **Descargar .ifc**, **Versiones** y **Nueva versión**.
- El botón de documento (Informe de validación) aparece cuando hay un informe asociado.
- La tabla **Versiones** incluye **Versión**, **Fecha**, **Archivo**, **Validación**, **Supuestos**, **JSON**, **Informe** y **Cambios**.
- **Modelos en Línea 3D** abre la tabla de vínculos. En ella puede usar **Buscar modelo...**.
- El visor de detalle por pieza identifica el piloto como **BP6 · torre completa (piloto)**.

## Cómo hacer…

### Cómo abrir un modelo y revisar sus datos

1. Seleccione una familia en la galería.
2. Toque la tarjeta del modelo.
3. Revise la geometría en el visor.
4. Use el botón de información (Datos de la torre, validación y supuestos).
5. Si aparece un informe asociado, use el botón de documento (Informe de validación).

### Cómo consultar una pieza de la lista de materiales

1. Abra un modelo con lista de materiales disponible.
2. Toque el botón de lista (Lista de materiales).
3. Localice la fila del perfil o marca de interés.
4. Use el botón de ubicación (Ver en el modelo) donde esté disponible.
5. Para volver al visor, toque el botón de equis (Cerrar lista).

### Cómo cargar un modelo IFC

1. En la galería, toque el botón de importación (Cargar modelo IFC).
2. Diligencie **Código**, **Tipo en tabla de torres**, **Familia** y **Descripción** según corresponda.
3. Seleccione el archivo en **IFC**.
4. Adjunte **JSON del modelo**, **Miniatura** e **Informe de validación** si dispone de ellos. Sin JSON, aparece **⚙ JSON desde el IFC**: al guardar, la app lo genera desde el IFC (barras, perfil, marca, acero y lista de materiales con peso = área del perfil × longitud × 7850 kg/m³). Lo que el IFC no trae queda marcado como supuesto (por ejemplo, marcas «S/M-n»). Así la torre se ve también en la lista de materiales, Línea 3D, el montaje de Avance de obra y Tendido.
5. Revise los avisos de lectura y toque el botón de disquete (Guardar).

### Cómo cargar y consultar versiones

1. Abra el modelo y toque el botón de nueva versión (Nueva versión).
2. Seleccione el IFC de la revisión y diligencie **Cambios de esta versión**.
3. Toque el botón de disquete (Guardar).
4. Abra el botón de versiones (Versiones) para consultar las filas del historial.
5. Use **Ver esta versión** para abrir una versión concreta.

### Cómo elegir las columnas de tipo y cuerpo

En **Trazado › Tabla de torres › Campos**, use el selector **Torre 3D** de las columnas importadas a nivel de torre.

- Si una columna contiene el código completo, como `BP6`, márquela como **Tipo de torre**.
- Si el tipo y el cuerpo vienen separados, marque la columna de la familia como **Tipo de torre** y la del número como **Cuerpo**. Por ejemplo, `BP` y `6` se buscan como `BP6`.

En **Campos**, las marcas se guardan al cambiar el selector. En **Avance de obra**, abra el botón de catálogo (Actividades, pesos y cotas). En **Cotas del visor 3D**, puede elegir **Tipo de torre** y **Cuerpo** en columnas distintas. Use el botón de disquete (Guardar ajustes).

Con **Automático**, la app busca encabezados como **Tipo de Estructura** y **Cantidad de Cuerpos**. Un cuerpo separado debe ser un entero positivo. También se admite un valor como `3 cuerpos`. Si falta un cuerpo válido, la vista puede dibujar una silueta y no supone que el código termina en 1. En **Modelos en Línea 3D**, vincule el IFC con el nombre del código correspondiente.

### Cómo vincular un modelo con Línea 3D

1. Vuelva a la galería y abra el botón de línea (Modelos en Línea 3D).
2. Use **Buscar modelo...** para localizarlo.
3. Revise el nombre que se usa para el vínculo.
4. Marque **Mostrar este modelo en Línea 3D**.
5. Si aparece un aviso de nombre repetido, revise cuál de los modelos debe quedar vinculado.

### Cómo abrir el piloto de detalle por pieza

1. Abra el modelo **BP6**.
2. Toque el botón de piezas (Detalle por pieza: torre completa (piloto)).
3. Compruebe que el encabezado indique **Detalle por pieza**.
4. Revise el subtítulo **BP6 · torre completa (piloto)**.
5. Use la flecha de regreso del encabezado para volver a la galería.

## Permisos

- **Ver** da acceso a la galería y a los visores. **Descargar** corresponde a la descarga del IFC y otros archivos.
- **Importar**, **Crear**, **Editar** y **Eliminar** corresponden a las operaciones sobre cargas, registros, cambios y Papelera.
- El rol de fábrica **Consulta** permite ver y descargar.

> Por confirmar: qué acción individual controla la vinculación con Línea 3D y la asignación manual de perfil o plano a una pieza.

## Problemas frecuentes

- **«Lista de materiales está deshabilitada»**. Revise si el modelo tiene un JSON de miembros disponible.
- **«La carga dice No es un IFC»**. Seleccione un archivo IFC válido.
- **«La carga dice JSON no válido»**. Revise que el archivo incluya la lista de miembros del modelo.
- **«Varios modelos tienen el mismo nombre»**. Revise los nombres en **Modelos en Línea 3D**. El aviso indica que solo uno queda vinculado con ese nombre.

## Preguntas frecuentes

### ¿Cómo giro, muevo o acerco el modelo?
Botón izquierdo = girar; rueda pulsada o botón derecho (o barra espaciadora + izquierdo) = desplazar; rueda = acercar. Es igual
en todos los visores 3D de la app.

### ¿Cómo cargo un modelo IFC nuevo?
En los mosaicos use el botón **importar**: código, tipo de la tabla de torres, familia y el archivo IFC (obligatorio); opcionales
el JSON del modelo, la miniatura y el informe de validación.

### ¿Cómo subo una versión nueva de un modelo?
En el visor, **nueva versión**. La vigente es la versión mayor que no esté en la Papelera; las anteriores quedan en versiones.

### ¿Dónde veo la lista de materiales de la torre?
En el visor, el botón de lista: al elegir una pieza se resalta y muestra su ficha.

### ¿Por qué el modelo no aparece en Avance de obra o Línea 3D?
Revise que el tipo de la torre en la tabla de torres coincida con el modelo (columnas de tipo y cuerpo) y que el modelo tenga su
JSON cargado.

### ¿Qué significan los supuestos de la ficha del modelo?
Son las decisiones que se tomaron al modelar porque el plano no las daba. Cada modelo trae su lista numerada y su validación.

### ¿Cómo vuelvo a una versión anterior del modelo?
Botón **versiones**: puede ver una versión anterior o eliminar la actual (va a la Papelera con su impacto).

### ¿Qué atajos de teclado tiene el visor?
F (encuadrar), H / Alt+H (ocultar / mostrar), 1 3 7 0 (vistas; Ctrl = la opuesta), M (medir), Esc. El botón de atajos los
lista todos.

## Relacionado

- [Avance de obra](avance-obra.md) — utiliza los modelos vinculados en el visor de torres y de línea.
- [Planos](planos.md) — reúne los documentos que pueden asociarse a las piezas.
