# Validación — 14 de octubre de 2026

Entorno: Fedora 44 x64, SDK local .NET 10.0.402, Avalonia 12.1.1, PDFsharp 6.2.4, Poppler externo.

## Resultados de la fase 1

- `dotnet build Mythlix.sln -m:1 --no-restore`: correcto, **0 advertencias y 0 errores**.
- `dotnet test Mythlix.sln --no-build --no-restore -m:1`: **6 pruebas aprobadas**, 0 fallidas.
- Inicio gráfico Linux: la ventana permaneció abierta durante la comprobación de inicio.
- Prueba integrada de ventana real con `--smoke-test references/verification.pdf`: terminó con código 0 y `MYTHLIX_SMOKE_OK`. Abre un PDF, guarda representación temporal, renderiza página y miniatura con Poppler y extrae texto.

Pruebas de integración:
1. Rotar, eliminar y añadir páginas, guardar copia, conservar bytes originales y restaurar snapshot.
2. Impedir eliminar la última página.
3. Conservar texto original después de rotar y guardar.
4. Renderizado PNG y extracción local.
5. Guardado cancelado sin archivo parcial ni temporales sobrantes.
6. Rechazar documento corrupto.

Las pruebas detectaron que PDFsharp no permite continuar modificando la misma instancia tras Save. La implementación reabre la representación serializada después de cada snapshot; las pruebas anteriores incluyen este caso.

## Límites de verificación de la fase 1

No se ejecutó una prueba manual completa de todos los botones ni regresión visual por capturas. No se verificaron Windows, instaladores, publicación autocontenida, documentos de cientos de páginas, PDF firmados reales, formularios ni archivos complejos de diferentes productores. El inicio en una sesión Wayland utiliza el backend X11/XWayland de Avalonia; no demuestra soporte Wayland nativo.

El aislamiento del entorno impide acceso gráfico X11 y sockets locales del runner xUnit. Las comprobaciones de UI y tests se realizaron fuera del aislamiento con autorización de ejecución. La compilación se realizó con un único nodo MSBuild (`-m:1`).

Se usó un feed temporal local de los paquetes NuGet disponibles en el entorno, con auditoría remota deshabilitada para la restauración offline. No se realizó una auditoría de vulnerabilidades de dependencias. Los comandos normales de README usan la configuración NuGet del usuario y no desactivan auditoría.

## Fase 2 — edición gráfica

La compilación de la fase 2 pasa sin errores ni advertencias. Se amplió la suite a **24 pruebas aprobadas**:

- Inserción de todos los tipos nuevos y texto Unicode extraíble.
- Persistencia y reapertura de texto, imágenes y geometría.
- Sustitución del contenido añadido sin duplicar textos.
- Eliminación de la capa nueva con igualdad visual del renderizado original.
- Conservación de píxeles originales fuera de los límites del texto añadido.
- Actualizaciones repetidas con un solo stream gráfico y sin acumular mapeos de fuentes.
- Conservación de recursos utilizados por otras páginas.
- Deshacer/rehacer mediante snapshots que conservan la escena.
- Reversión completa ante geometría, color o imagen inválidos.
- Rechazo explícito de páginas recortadas.
- Coordenadas de selección y coincidencia con la imagen renderizada a 0, 90, 180 y 270 grados.

Comprobación en ventana real mediante `--phase2-smoke`: inserción de texto, tres formas e imagen; propiedades; duplicar/eliminar; deshacer/rehacer; guardar y reabrir; extracción del texto nuevo. Resultado esperado y observado: `MYTHLIX_PHASE2_SMOKE_OK`, código 0. Captura de la propia ventana en `artifacts/phase2-preview.png`, revisada visualmente.

Esta comprobación usa los comandos MVVM que invocan los controles, pero no automatiza diálogos del explorador ni eventos de ratón. Los gestos de arrastre tienen transformaciones verificadas por pruebas de geometría y renderizado; falta una prueba automatizada completa de interacción física. Windows sigue sin verificarse. La aplicación de trabajo permanece cerrada después de las comprobaciones.

SkiaSharp se añadió únicamente al proyecto de pruebas para comparar píxeles y verificar las posiciones renderizadas; la aplicación ya lo utiliza a través de Avalonia. El feed temporal offline y la limitación de sockets del runner siguen siendo los descritos anteriormente.

## Fase 3 e interfaz renovada

- Compilación final: **0 advertencias, 0 errores**.
- Regresión final: **45 pruebas aprobadas**, 0 fallidas.
- Ventana real: `MYTHLIX_PHASE3_SMOKE_OK`. Reemplazo de texto original, deshacer/rehacer, copia guardada y reapertura con contenido editado.
- Regresión de comandos gráficos en la nueva interfaz: `MYTHLIX_PHASE2_SMOKE_OK`.
- Captura revisada: `artifacts/phase3-preview.png`. Incluye miniaturas reales, selección de texto original, inspector contextual, paleta índigo y ajuste de página.
- PDF de entrada reproducible: `references/Mythlix-Documento-Tecnico.pdf`, tres páginas. Resultado de edición: `artifacts/phase3-edited.pdf`.

Los nuevos casos cubren streams originales sin una capa de superposición, preservación literal de operadores vecinos, acentos WinAnsi, escapes, cadenas hexadecimales, TJ simple, compresión, páginas que comparten streams, selección obsoleta, eliminación del operando y snapshots. Se prueban los bloqueos para desbordamiento, caracteres no disponibles, tamaños mayores, texto fragmentado, kerning, transformaciones, estado gráfico, fuentes CID/subconjuntos, codificaciones personalizadas, comillas ambiguas StandardEncoding y cambios de tamaño que pueden afectar texto que hereda la fuente.

Durante la revisión se comparó el Poppler incluido en el entorno con `/usr/bin/pdftoppm`: el primero presentaba espaciado incorrecto al sustituir fuentes estándar. La selección del renderizador ahora prioriza Poppler instalado en Linux (salvo override explícito). Las pruebas finales y la captura revisada se realizaron con esa selección corregida.

Los avisos OFL y BSD se verificaron en la salida de la aplicación. No se realizó auditoría completa de dependencias ni distribución de instaladores. Windows y otras distribuciones siguen pendientes de validación nativa. La cobertura de edición original es la descrita en PHASE3.md; no se afirma edición universal de fuentes ni de párrafos.

## Fase 4 — OCR local

- Compilación: **0 advertencias, 0 errores**.
- Regresión: **55 pruebas aprobadas**, 0 fallidas.
- Ventana real: `MYTHLIX_PHASE4_SMOKE_OK`. Reconocimiento español/inglés del escaneo, corrección de una palabra, exportación de texto y reconstrucción editable, aplicación de capa buscable, deshacer/rehacer y guardado.
- Entrada sin texto extraíble: `references/Mythlix-Escaneo-OCR.pdf`.
- Resultados: `artifacts/phase4-preview.png`, `artifacts/phase4-ocr.txt` y `artifacts/phase4-searchable.pdf`.

Las pruebas incluyen reconocimiento real con Tesseract, igualdad exacta de los píxeles renderizados antes y después de añadir texto invisible, reemplazo sin duplicados, validación de coordenadas, análisis TSV independiente de la cultura y reconstrucción con objetos de texto editables. Un lote cancelado después de procesar la primera página restaura el documento completo; otro caso verifica capas independientes en dos páginas con recursos compartidos.

La comprobación gráfica invoca comandos MVVM y captura la ventana; no automatiza todos los gestos ni los diálogos del explorador. Se verificó Linux con Tesseract y modelos instalados dentro de `.tools/ocr`; ese runtime local no constituye un instalador multiplataforma. Windows sigue sin verificarse. Las páginas rotadas o recortadas se rechazan y la orientación automática, corrección de inclinación y regiones OCR siguen pendientes. Los límites y la instalación se describen en PHASE4.md.

## Fase 5 — Organización inicial

- SDK local `.sdk/dotnet` 10.0.402 con `DOTNET_CLI_HOME=/tmp/mythlix-dotnet`.
- `build Mythlix.sln --no-restore -m:1`: 0 advertencias, 0 errores.
- `test tests/Mythlix.Tests --no-build --no-restore -m:1`: 65 aprobadas, 0 fallidas. Se autorizó la ejecución fuera del aislamiento para el socket local de VSTest.
- Diez casos nuevos verifican duplicación independiente, reordenación y reapertura, inserción, orden de extracción, conservación del archivo original, cancelación y entradas inválidas.
- No se verificaron interacciones gráficas de los diálogos nuevos ni ejecución nativa en Windows.

## Revisión OCR posterior a organización de páginas

- Compilación con SDK local 10.0.402: **0 advertencias, 0 errores**.
- Regresión xUnit: **70 aprobadas**, 0 fallidas. Cinco casos nuevos cubren líneas/columnas del TSV, exportación sin metadatos de línea, reconocimiento real PSM 6, rechazo previo de modo/dimensiones inválidos y separación de bloques en reconstrucción.
- Avalonia: **MYTHLIX_PHASE4_SMOKE_OK**. Ahora verifica reconocimiento por intervalo con PSM 6, conservación de revisión ante intervalo inválido, corrección, exportación, reconstrucción, capa buscable, deshacer/rehacer y reapertura.
- La comprobación de ventana también construye un PDF de dos páginas: cancelar después de la primera conserva su revisión sin modificar el PDF; fallar en la segunda por rotación conserva la primera y permite aplicar su capa buscable.
- Captura actualizada y revisada: `artifacts/phase4-preview.png`.
- Se mantienen las pruebas de igualdad de píxeles y ausencia de duplicados de capa. No se midió una mejora estadística de precisión ni se verificó Windows. Orientación, deskew, regiones y recortes siguen pendientes.

## Fase 6 — Caché y preparación por revisión

- Compilación de la solución: **0 advertencias, 0 errores**.
- Regresión xUnit: **75 aprobadas**, 0 fallidas. VSTest necesitó ejecución fuera del aislamiento para su socket local.
- Cinco casos nuevos cubren reutilización de imágenes/texto, protección de bytes, expulsión LRU, límite agregado, invalidación de documentos/resoluciones, cancelación y resultados pendientes tras invalidación.
- Ventana Avalonia con caché activa: **MYTHLIX_PHASE3_SMOKE_OK**, código 0; reemplazo de texto original, deshacer/rehacer, guardado y reapertura correctos.
- `git diff --check`: correcto.
- No se midieron tiempos estadísticos, memoria total del proceso ni rendimiento de documentos extensos. No se verificaron instaladores ni Windows. Véase PHASE6.md para alcance y límites.

## Organización — División automática

- Compilación de solución: **0 advertencias, 0 errores**.
- Regresión xUnit: **85 aprobadas**, 0 fallidas. Diez casos nuevos incluyen tamaños 1/2/5, última parte incompleta, orden/rotación/dimensiones, conservación del original, sesión todavía editable, tamaños inválidos, destinos existentes, padre inexistente y cancelación previa o después de exportar la primera parte temporal.
- Comprobación integrada con ventana Avalonia: **MYTHLIX_SPLIT_SMOKE_OK**, código 0. Divide el documento técnico en tres partes, reabre cada parte, compara geometría y texto extraído con Poppler y navega en el documento abierto sin cambiar revisión ni estado modificado. Limpia los resultados temporales de comprobación.
- VSTest y ventana se ejecutaron fuera del aislamiento por sus requisitos de sockets y acceso gráfico.
- `git diff --check`: correcto. No se automatizaron los clics del diálogo, el explorador de carpetas ni el botón de cancelación; la cancelación del motor sí tiene prueba determinista después de crear la primera parte. No se verificó Windows ni preservación de estructuras de nivel documento (formularios/marcadores).
