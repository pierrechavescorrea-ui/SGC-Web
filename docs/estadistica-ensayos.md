# Análisis estadístico de Ensayos — fórmulas y referencias

Ruta: **Ensayos › (tipo) › Gráficos › pestaña «Análisis estadístico»**. Usa las mismas filas que el gráfico, después de los filtros.
Código: `Data/Estadistica/Distribuciones.cs` (funciones de distribución), `Descriptiva.cs` (cálculos), `Interpretacion.cs` (lectura en
español), `Components/Estadistica/AnalisisEstadistico.razor(.cs)` (pantalla), `wwwroot/js/estadistica.js` (gráficos, Chart.js).

Todo se calcula en el servidor sin librerías externas. Se verificó contra SciPy 1.17 con una muestra de 16 valores: media, desviación,
asimetría, curtosis, cuartiles, Anderson-Darling (A² sin ajustar 0,7150) y Jarque-Bera (3,4122; p = 0,1816) coinciden. Los valores críticos
t(0,975; 15) = 2,1314 y χ²(0,025; 15) = 6,2621 coinciden con las tablas.

## Notación
n = número de datos con valor en el campo elegido; xᵢ = datos; x₍ᵢ₎ = datos ordenados de menor a mayor; α = 1 − nivel de confianza.

## Tendencia central y dispersión
| Medida | Fórmula | Equivalente en Excel |
|---|---|---|
| Media | x̄ = Σxᵢ / n | PROMEDIO |
| Mediana, cuartiles, percentiles | h = (n − 1)·p; Q(p) = x₍⌊h⌋₎ + (h − ⌊h⌋)(x₍⌊h⌋+1₎ − x₍⌊h⌋₎) | MEDIANA, CUARTIL.INC, PERCENTIL.INC |
| Moda | valor(es) con mayor frecuencia; si ninguno se repite, no hay moda | MODA.VARIOS (#N/A si no hay) |
| Moda de la clase modal (Czuber) | L + d₁/(d₁ + d₂)·h; L = límite inferior de la clase modal, d₁ y d₂ = diferencias de frecuencia con la clase anterior y la siguiente, h = ancho de clase | — |
| Varianza muestral | s² = Σ(xᵢ − x̄)² / (n − 1) | VAR.S |
| Desviación estándar muestral | s = √s² | DESVEST.M |
| Desviación poblacional | σₙ = √(Σ(xᵢ − x̄)² / n) | DESVEST.P |
| Desviación media absoluta | Σ\|xᵢ − x̄\| / n | DESVPROM |
| Error estándar de la media | s / √n | — |
| Coeficiente de variación | s / \|x̄\| × 100 % | — |
| Rango, RIC | máx − mín; Q3 − Q1 | — |

## Forma
| Medida | Fórmula | Excel |
|---|---|---|
| Asimetría | G1 = n / ((n − 1)(n − 2)) · Σ((xᵢ − x̄)/s)³ (n ≥ 3) | COEFICIENTE.ASIMETRIA |
| Error de la asimetría | SES = √(6n(n − 1) / ((n − 2)(n + 1)(n + 3))) | — |
| Curtosis (exceso) | G2 = n(n + 1) / ((n − 1)(n − 2)(n − 3)) · Σ((xᵢ − x̄)/s)⁴ − 3(n − 1)² / ((n − 2)(n − 3)) (n ≥ 4) | CURTOSIS |
| Error de la curtosis | SEK = 2·SES·√((n² − 1) / ((n − 3)(n + 5))) | — |

Lectura (criterios de uso común, se citan en el texto que da la app):
- |G1| < 0,5: la distribución es aproximadamente simétrica. Entre 0,5 y 1 es moderadamente asimétrica; > 1 es muy asimétrica (Bulmer, *Principles of Statistics*, 1979).
- G2 > 0: leptocúrtica. G2 < 0: platicúrtica. |G2| < 0,5 se lee como mesocúrtica.
- La forma es significativa si |G1/SES| > 2 o |G2/SEK| > 2.
- Media > mediana indica cola hacia la derecha; media < mediana, cola hacia la izquierda.
- CV < 15 %: variabilidad baja; 15–30 %: moderada; > 30 %: alta. Es un criterio orientativo, no viene de una norma.

## Intervalos de confianza (90, 95 o 99 %)
- **Media:** x̄ ± t(1 − α/2; n − 1)·s/√n, igual que INTERVALO.CONFIANZA.T.
- **Desviación estándar:** de √((n − 1)s² / χ²(1 − α/2; n − 1)) a √((n − 1)s² / χ²(α/2; n − 1)) (Montgomery, *Introduction to Statistical Quality Control*, cap. 4).
- **Cálculo de t y χ²:** se invierten por bisección las funciones de distribución:
  - t de Student: Fₜ(t) = 1 − ½·I_{ν/(ν+t²)}(ν/2, ½);
  - χ²: F(x) = P(ν/2, x/2);
  - I = beta incompleta regularizada y P = gamma incompleta regularizada, calculadas con *Numerical Recipes* 3.ª ed., §6.1–6.4 (Lanczos, fracción continua de Lentz y serie).

## Distribución normal y probabilidad de ocurrencia
- **Densidad:** f(x) = e^(−(x − μ)²/(2σ²)) / (σ√(2π)), con μ = x̄ y σ = s.
- **Probabilidad:** Φ(z) = ½·erfc(−z/√2). erfc es la aproximación de Chebyshev de *Numerical Recipes* §6.2.2 (error relativo < 1,2·10⁻⁷). Φ⁻¹ usa el algoritmo de P. J. Acklam con un paso de Halley.
- **Probabilidad según la normal:** P(a ≤ X ≤ b) = Φ((b − x̄)/s) − Φ((a − x̄)/s). También se puede pedir P(X ≤ a), P(X ≥ a) y el valor con probabilidad p (x̄ + s·Φ⁻¹(p)).
- **Probabilidad observada:** número de datos en [a, b] / n. Si los datos no son normales, esta es la que vale.
- **Curva sobre el histograma:** f(x)·n·h en frecuencia, f(x)·h·100 en porcentaje, f(x) en densidad (h = ancho de clase).
- **Regla empírica:** % de datos dentro de x̄ ± kσ, comparado con lo esperado en una normal: 68,27 %, 95,45 % y 99,73 %.

## Normalidad
- **Anderson-Darling:**
  - A² = −n − (1/n)·Σᵢ(2i − 1)[ln Φ(z₍ᵢ₎) + ln(1 − Φ(z₍ₙ₊₁₋ᵢ₎))], con zᵢ = (xᵢ − x̄)/s;
  - ajuste por parámetros estimados: A* = A²(1 + 0,75/n + 2,25/n²);
  - valor p por tramos (D'Agostino y Stephens, *Goodness-of-Fit Techniques*, 1986, tabla 4.9):
    - A* ≥ 0,6: p = e^(1,2937 − 5,709A* + 0,0186A*²);
    - 0,34 ≤ A* < 0,6: p = e^(0,9177 − 4,279A* − 1,38A*²);
    - 0,2 ≤ A* < 0,34: p = 1 − e^(−8,318 + 42,796A* − 59,938A*²);
    - A* < 0,2: p = 1 − e^(−13,436 + 101,14A* − 223,73A*²).
  - Requiere n ≥ 8. Con α = 0,05, p < 0,05 rechaza la normalidad.
- **Jarque-Bera:** JB = n/6·(b₁² + b₂²/4). Aquí b₁ = m₃/m₂^1,5 y b₂ = m₄/m₂² − 3, con momentos poblacionales; el valor p sale de χ² con 2 gl. Requiere n ≥ 4.
- **Gráfico Q-Q:** el valor observado x₍ᵢ₎ contra el cuantil teórico x̄ + s·Φ⁻¹((i − 0,375)/(n + 0,25)) (posición de Blom).

## Atípicos y diagrama de caja (Tukey)
- Leve: fuera de [Q1 − 1,5·RIC, Q3 + 1,5·RIC].
- Extremo: fuera de [Q1 − 3·RIC, Q3 + 3·RIC].
- Los bigotes llegan hasta el último dato dentro de 1,5·RIC. El rombo marca la media y la línea la mediana.

## Histograma: número de clases
- Sturges: k = ⌈log₂ n⌉ + 1 (por defecto).
- Scott: h = 3,49·s·n^(−1/3).
- Freedman-Diaconis: h = 2·RIC·n^(−1/3).
- Raíz: k = ⌈√n⌉.
- Manual: 1 a 200 clases.

Las clases tienen el mismo ancho, de mín a máx. La última incluye el máximo.

## Capacidad (opcional, con LIE y/o LSE)
- Cp = (LSE − LIE) / 6s.
- Cpk = mín((LSE − x̄)/3s, (x̄ − LIE)/3s).
- Fuera de especificación: el esperado es Φ((LIE − x̄)/s) + 1 − Φ((LSE − x̄)/s); también se muestra el observado.
- Lectura: Cpk ≥ 1,33 = capaz; 1–1,33 = justo; < 1 = no capaz (criterio de uso común en la industria, p. ej. el manual SPC de AIAG). Si los datos no son normales, la app avisa que Cp y Cpk son solo indicativos.

## Comparación por grupos
El mismo análisis descriptivo se aplica por cada valor del campo elegido en «Comparar por», hasta 60 grupos. Se muestra la tabla y un diagrama de caja por grupo. No se aplica ninguna prueba de hipótesis entre grupos (ANOVA): queda pendiente si se pide.

## Supuestos y límites
- s es la desviación muestral (n − 1) en todo el análisis, también en la curva normal y en Cp/Cpk. No se usa la desviación de corto plazo (R̄/d₂), porque los ensayos no vienen en subgrupos.
- Los valores vacíos o no numéricos del campo no entran en el cálculo, y la app dice cuántos son.
- Los campos tipo Fórmula se analizan con el valor que ya calculó la tabla del ensayo.
