---
modulo: ensayos
titulo: Análisis estadístico de ensayos
menu: Operación › Ensayos › Gráficos › Análisis estadístico
rutas: []
palabras: [análisis estadístico, estadística, histograma, campana, normalidad, Q-Q, cuartiles, diagrama de caja, media, promedio, capacidad del proceso, Cpk, LIE, LSE, probabilidad, intervalo de confianza]
opciones:
  - Ensayos: /ensayos
  - Gráficos: /ensayos | Gráficos
  - Análisis estadístico: /ensayos | Análisis estadístico
  - Límite inferior de especificación (opcional): /ensayos | Límite inferior de especificación (opcional)
  - Límite superior de especificación (opcional): /ensayos | Límite superior de especificación (opcional)
  - Editar disposición: /ensayos | Editar disposición
  - Terminar edición: /ensayos | Terminar edición
  - Restaurar disposición de fábrica: /ensayos | Restaurar disposición de fábrica
  - Descargar imagen: /ensayos | Descargar imagen
  - Líneas de desviación estándar: /ensayos | Líneas de desviación estándar
  - Eje vertical: /ensayos | Eje vertical
  - Regla para el número de clases: /ensayos | Regla para el número de clases
  - Número de clases: /ensayos | Número de clases
  - Más opciones del gráfico: /ensayos | Más opciones del gráfico
  - Quitar filtro: /ensayos | Quitar filtro
actualizado: 2026-11-21
---

# Análisis estadístico de ensayos

## Para qué sirve

Explore la distribución de un campo numérico de los resultados de laboratorio, compare grupos y revise cómo se comportan los datos frente a límites de especificación.

## Cómo entrar

Menú › Operación › **Ensayos**. Elija el grupo y el tipo de ensayo. Abra **Gráficos** en el encabezado y después la pestaña **Análisis estadístico**. Los controles se encuentran dentro del tipo de ensayo seleccionado.

## Qué hay en la pantalla

- La barra incluye **Campo a analizar**, **Comparar por**, los filtros, el **Nivel de confianza** (90 %, 95 % o 99 %) y los límites **LIE** y **LSE**. El contador distingue valores utilizables de filas filtradas.
- El panel muestra recuento, media, mediana, moda, desviación estándar, CV, asimetría, curtosis, distribución y Cpk.
- Puede abrir **Histograma y distribución normal**, **Lectura de los datos**, **Probabilidad de ocurrencia**, **Gráfico Q-Q normal**, diagrama de caja y tablas descriptivas, de frecuencias o de grupos. Hay controles de disposición para mover, redimensionar, quitar y volver a agregar tarjetas.

## Cómo hacer…

### Cómo comparar resultados por grupo

1. En **Análisis estadístico**, elija el **Campo a analizar**.
2. Seleccione un campo en **Comparar por**.
3. Aplique filtros si quiere revisar solo parte de los resultados. Los comparte con la pestaña **Gráficos**.
4. Revise el diagrama de caja y la tabla de grupos.

### Cómo ajustar y descargar el histograma

1. En el histograma, elija **Eje vertical** para mostrar frecuencia, porcentaje o densidad.
2. Cambie la **Regla para el número de clases**. Si elige el número manualmente, diligencie **Número de clases**.
3. Use **Líneas de desviación estándar** o **Más opciones del gráfico** para ajustar la presentación.
4. Toque **Descargar imagen** en la tarjeta.

### Cómo revisar la capacidad y la probabilidad

1. Diligencie el **Límite inferior de especificación (opcional)** y el **Límite superior de especificación (opcional)** para consultar Cpk.
2. En **Probabilidad de ocurrencia**, decida si busca la probabilidad de quedar por debajo, por encima o entre dos valores, o un valor para una probabilidad dada.
3. Escriba los valores que pida la opción escogida y revise el resultado.

### Cómo ordenar las tarjetas del análisis

1. Toque **Editar disposición**.
2. Arrastre las tarjetas y utilice el tirador para cambiar su tamaño. Puede quitar tarjetas y volver a agregarlas.
3. Toque **Terminar edición**. Para volver al diseño inicial, toque **Restaurar disposición de fábrica**.

## Permisos

Se requiere **Ver** en Ensayos para consultar un tipo y abrir sus gráficos. En los roles de fábrica, **Consulta** puede ver y descargar y **Consulta sin descarga** tiene solo **Ver**. Los permisos para registrar resultados o configurar las columnas se administran en Ensayos.

## Problemas frecuentes

- **«Este ensayo no tiene campos numéricos para analizar.»** Revise los campos del tipo de ensayo.
- **«No hay valores… con los filtros elegidos.»** Quite filtros o seleccione otro campo numérico.
- **«Cpk queda vacío.»** Compruebe el límite inferior, el superior y los resultados del campo.

## Preguntas frecuentes

### ¿Dónde saco el histograma de resistencia de los ensayos?
Abra el tipo de ensayo, toque **Gráficos** y luego **Análisis estadístico**. Seleccione el campo de resistencia y revise **Histograma y distribución normal**.

### ¿Cómo miro solo una torre en el análisis estadístico?
Agregue un filtro de torre si el campo está disponible y elija su valor. El análisis usa las filas filtradas.

### ¿Puedo comparar el mismo resultado por grupo?
Use **Comparar por**. Revise el diagrama de caja y la tabla de grupos.

### ¿Dónde meto mis límites para que salga el Cpk?
Indique los valores en **LIE** y **LSE** en la barra del análisis.

### ¿Cómo veo si los resultados siguen una distribución normal?
Revise **Distribución**, el histograma y el **Gráfico Q-Q normal**. Consulte la lectura de los datos para interpretar lo que muestra el panel.

### ¿Cómo cambio el número de barras del histograma?
Use **Regla para el número de clases** y elija **Clases: elegir número** para indicar la cantidad.

### ¿Puedo sacar el gráfico como imagen?
Toque **Descargar imagen** en el histograma, la tarjeta de probabilidad o el gráfico Q-Q normal.

### ¿Por qué salen menos datos que filas filtradas?
El primer número cuenta solo filas que tienen valor en **Campo a analizar**.

### ¿Se mantienen mis filtros si vuelvo a Gráficos?
Sí. **Gráficos** y **Análisis estadístico** comparten sus filtros.

### Quité una tarjeta del análisis, ¿cómo la recupero?
Entre en **Editar disposición** y agréguela de nuevo o use **Restaurar disposición de fábrica**.

## Relacionado

- [Ensayos](ensayos.md) — resultados, gráficos y resumen por ensayo o torre.
