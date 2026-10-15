# Análisis del OCR — 14 de octubre de 2026

## Funcionamiento observado

`OcrViewModel` crea una copia de trabajo, consulta el texto existente con Poppler y, cuando corresponde, rasteriza cada página. Solicita aproximadamente 300 dpi con un máximo de 4000 píxeles en el lado mayor. `TesseractEngine` valida el PNG, consulta los idiomas disponibles y ejecuta Tesseract con el modo de segmentación seleccionado. Lee TSV: texto, confianza, caja de cada palabra y bloque/párrafo/línea. La revisión permite corregir palabras y señala confianza inferior al 70%.

Hay dos salidas: una capa invisible buscable que conserva el escaneo y un PDF independiente con objetos de texto editables. La primera adapta horizontalmente cada palabra a su caja. La segunda agrupa palabras y usa posiciones escaladas a puntos PDF.

## Hallazgos

| Área | Evidencia en el código | Consecuencia |
|---|---|---|
| Fuente | TSV no se amplía con información tipográfica; `OcrWord` no contiene familia, estilo ni línea base. Ambos escritores usan Liberation Sans. | La fuente original no se reconoce. El modelo de objetos tampoco permite elegir una familia. |
| Tamaño | Reconstrucción: `FontSize = altura * .85`; capa buscable: tamaño igual a altura y línea base en `altura * .8`. | La caja de tinta no equivale al tamaño tipográfico. Mayúsculas, ascendentes y descendentes producen estimaciones inconsistentes. |
| Posición | Conversión proporcional de píxeles a puntos; reconstrucción añade 4 pt de ancho y usa altura por 1.6. El escritor editable emplea un formateador con ajuste de líneas y recorte. | Se conserva una posición aproximada, pero el texto puede redistribuirse o recortarse. Los espacios originales se sustituyen por un espacio por palabra. |
| Orientación | Se rechazan páginas con rotación o recorte; no hay corrección de inclinación del escaneo. | No se procesan ciertos PDF; una imagen inclinada dentro de una página sin rotación sigue llegando inclinada al motor. |
| Resolución | El lado mayor se limita a 4000 px. | En A3, el límite equivale aproximadamente a 242 dpi, aunque se solicitan 300. Ampliar una imagen tampoco recupera detalle ausente. |
| Rendimiento | Bucle secuencial y `--list-langs` por página, además de la consulta al abrir OCR. | Sobrecoste de procesos y ausencia de solapamiento de renderizado/reconocimiento. Su impacto real requiere medición. |
| Documentos mixtos | Cualquier texto extraíble hace que se omita toda la página si está activada la opción. | Un encabezado seleccionable puede ocultar un cuerpo escaneado pendiente de OCR. |
| Verificación | Pruebas sintéticas de texto, límites, cancelación, estructura y exportación. | No se mide error de caracteres/palabras, precisión de fuente/tamaño/posición ni tiempo por etapa sobre escaneos diversos. |

## Mejoras propuestas, por orden

1. **Medir antes de optimizar.** Registrar tiempos de extracción, rasterización, motor, parseo y exportación; memoria máxima, dimensiones y palabras por página. Usar un conjunto con transcripciones y cajas de referencia: español/inglés, columnas, tablas, letra pequeña, ruido e inclinación. Medir CER/WER, error geométrico y tiempo total; confianza OCR no sustituye exactitud.
2. **Reducir trabajo repetido.** Consultar idiomas una vez por lote o mantener una caché invalidable por configuración. Probar concurrencia acotada de dos páginas con resultados ordenados, progreso y cancelación; contrastarla con el límite actual de dos hilos de Tesseract para evitar sobrecarga. Limitar también los píxeles simultáneos. Contabilizar palabras incrementalmente.
3. **Mejorar entrada de forma selectiva.** Detectar orientación/inclinación y fondo irregular; corregir o binarizar cuando la imagen lo requiera. Comparar 300 dpi con mayor resolución para letra pequeña, respetando los límites. Un segundo intento debe concentrarse en regiones deficientes, no repetir todo indiscriminadamente. Guardar la transformación inversa para devolver cajas y líneas base al espacio original.
4. **Conservar geometría de líneas.** Evaluar hOCR para recuperar información de línea base y métricas de línea junto a las cajas. Extender el modelo con líneas, transformaciones y procedencia. Ubicar el texto con métricas de fuente y línea base; mantener separaciones entre palabras y evitar que el formateador cambie las líneas reconocidas.
5. **Añadir estimación tipográfica explícita.** En PDF con texto nativo, priorizar la extracción de sus fuentes y tamaños. En escaneos, comparar recortes con candidatos tipográficos disponibles y ajustar familia, tamaño, negrita y cursiva usando varias palabras de la misma línea. Guardar confianza y señalar sustituciones. No prometer identificar exactamente fuentes arbitrarias: el OCR actual no aporta esa información y fuentes visualmente similares pueden resultar indistinguibles.
6. **Tratar páginas mixtas y rotadas.** Decidir por regiones cuándo conservar texto nativo y dónde aplicar OCR, evitando duplicados. Soportar recorte/rotación mediante transformaciones verificadas antes de retirar los bloqueos actuales.

## Criterios de aceptación

- Mejor rendimiento demostrado en el mismo equipo y corpus, incluyendo lotes largos y consumo máximo de memoria.
- CER/WER iguales o mejores; revisión manual de casos que empeoren.
- Posición validada mediante superposición visual y comparación con cajas/líneas base de referencia.
- Tamaño comparado con valores conocidos en muestras rasterizadas de distintas fuentes y estilos.
- Familia exacta, sustitución y confianza diferenciadas; conservar correcciones manuales.
- Capa invisible sin cambios de píxeles; cancelación y exportación sin duplicados ni resultados desalineados.

## Referencias

- [Tesseract: Improving the quality of the output](https://tesseract-ocr.github.io/tessdoc/ImproveQuality.html): resolución, inclinación, binarización y segmentación.
- [Tesseract: Command Line Usage](https://tesseract-ocr.github.io/tessdoc/Command-Line-Usage.html): formatos de salida TSV y hOCR.

Este documento describe el código inspeccionado y propuestas pendientes. No constituye una medición de mejoras ni modifica el motor.
