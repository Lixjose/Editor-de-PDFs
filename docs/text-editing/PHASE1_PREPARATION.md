# Preparación de fase 1: fragmentos de estilo con procedencia

Estado: pasos 1A–1D implementados. Los contratos, extracción y borrador tienen un adaptador de inspección de solo lectura; la composición/escritura mixta no está activada.
Esta fase es distinta de la entrega histórica descrita en `../TEXT_ENGINE_PHASE1.md`.

## Objetivo y alcance

Representar y editar un borrador de párrafo con varios estilos sin aplanar fuentes, tamaños, colores o espaciados y sin perder la relación con el contenido original. La primera entrega se limita a escritura horizontal, fuentes estándar Type 1, WinAnsi compatible, matrices de traslación conocidas y texto de relleno RGB/Gray compatible. La edición de un subconjunto adicional se habilitará únicamente al disponer de pruebas de su extracción y escritura.

Los documentos incompatibles conservan sus flujos actuales. No se transforma silenciosamente una edición nativa en máscara, OCR o sustitución de fuente. La ruta XML/TSV se identifica como extracción de respaldo; no proporciona por sí sola rangos editables del stream.

Fuera del primer alcance: CID/Type0, fuentes incrustadas/subconjuntos, Unicode complejo y shaping, trazados de letras, texto vertical, transformaciones arbitrarias, recortes de texto, reflujo vectorial, árbol lógico, fondos complejos y eliminación segura por máscaras. El texto que requiera esas capacidades debe producir un diagnóstico explícito en el nuevo flujo.

## Decisiones de arquitectura

1. Conservar `OriginalTextBlock`, `PdfParagraph`, `IContentEditor`, `OriginalTextEditor`, `ParagraphReflow` y la capa gráfica. Introducir contratos adicionales en Core y adaptadores en Infrastructure, sin cambiar firmas existentes en la preparación.
2. Core no incorpora Avalonia ni PDFsharp. Geometría en puntos de página, origen superior izquierdo; línea base explícita. Revisión `long`. Escala horizontal como factor (1 = 100 %), convertido a porcentaje solo al emitir Tz. Avances de diseño normalizados en em y avances efectivos en puntos, nunca una unidad ambigua.
3. La identidad de fuente incluye documento/revisión, ámbito de recursos y recurso PDF; el nombre de familia no identifica una fuente física. Conservar Tc, Tw original, Tz, Ts, color y modo admitido. No mezclar estado tipográfico original con decisiones de sustitución.
4. La procedencia conserva documento, página, revisión, stream, rango de bytes, ID y huella del bloque, huella del stream y rango de texto fuente. Un fragmento puede tener varias procedencias; fusionar estilos nunca descarta fuentes secundarias.
5. Texto editado y procedencia original son estructuras distintas. Los caracteres insertados tienen identidad de borrador y un ancla, no offsets ficticios en el PDF. Los offsets fuente no se desplazan al editar el borrador; los del texto actual sí.
6. Separar rangos de texto, secuencias de glifos y bytes/códigos PDF. Un cluster puede contener varios glifos y códigos. La primera fase admite solo el subconjunto que pueda mapear de forma comprobada; no afirma soporte general N:M.
7. Colecciones inmutables. Los bytes originales no se exponen como arrays mutables. La sesión de borrador no escribe el documento y cancelar no modifica ni historial ni revisión.
8. El compositor y escritor compartidos son una fase posterior: un TextBox no debe decidir la geometría PDF definitiva. Esta preparación no agrega controles nuevos ni reemplaza los gestos actuales.

## Contratos a implementar

| Contrato | Responsabilidad |
|---|---|
| `TextSourceSpan` | Identidad y rango exacto original con revisión y huellas; ámbito de recurso para fuentes. |
| `PdfRunStyle` | Estado original admitido y referencia de fuente; igualdad por valores semánticos. |
| `PdfTextRun` | Texto actual, estilo, ID de borrador y lista de procedencias/anclas. |
| `TextMapping` | Relación entre rangos UTF-16, clusters, glifos y bytes originales; capacidades explícitas. |
| `ParagraphRunBuffer` | Insertar, pegar, borrar y dividir como operaciones sobre borrador inmutable. |
| `RunEditResult` | Nuevo borrador y diagnósticos estructurados; sin mutación parcial al fallar. |
| `IFontEncodingResolver` | Validar caracteres y resolver bytes para la fuente real; no deducir codificación de su nombre. |

Los contratos base `TextSourceSpan`, `PdfRunStyle` y `PdfTextRun` están implementados en `Mythlix.Core.TextEditing`. `TextMapping`, el buffer y el resolvedor se implementarán al disponer de extracción y operaciones reales; no hay esqueletos que devuelvan éxito o métricas ficticias.

## Reglas de edición

- Inserción: hereda el estilo izquierdo en una frontera; al inicio hereda el primer estilo. Un párrafo vacío mantiene un estilo de escritura explícito.
- Pegado inicial: solo texto plano con el estilo del punto de inserción. Mantener saltos duros separados de cortes suaves de composición; no aplicar Trim ni colapsar espacios de manera implícita.
- Borrado: respeta fronteras de grafemas/clusters; conserva las procedencias de ambos extremos y permite fusionar estilos iguales sin perder su origen.
- División: conserva el vínculo a los rangos fuente y ajusta los rangos del borrador. Un salto de párrafo no se confunde con reflujo.
- Caracteres no codificables: diagnóstico bloqueante para confirmar; el borrador puede conservarlos para corrección. No reemplazar por '?' ni elegir otra fuente en silencio.
- Ninguna operación de borrador modifica el PDF, su revisión, la selección original ni Undo/Redo.

## Secuencia de implementación

| Paso | Entrega revisable | Condición de salida |
|---|---|---|
| 1A | Contratos inmutables y validación de rangos/unidades | Core sigue sin dependencias gráficas/PDF; rangos inválidos rechazados. |
| 1B | Extracción nativa de varios Tj/TJ y cambios de estilo | Bytes/rangos y estado correctos; fuentes y streams compartidos aislados; incógnitas explícitas. |
| 1C | Buffer de mutaciones y validación WinAnsi | Pruebas de inserción, pegado, borrado, división, estilos y procedencias completas. |
| 1D | Adaptador de solo lectura y diagnóstico por bloque | El flujo anterior continúa funcionando; el nuevo análisis no habilita escritura incompleta. |

Composición compartida, commit del nuevo modelo y activación de UI requieren entregas posteriores y validación visual propia. No habilitar un botón de edición mixta antes de completar la cadena de extracción, edición, composición y escritura.

## Matriz de aceptación

| Caso | Resultado exigido |
|---|---|
| Normal + negrita + cursiva en un mismo BT/ET | Tres estilos y procedencias reales, sin convertir todo a un estilo. |
| Cambios de tamaño/color y Tc/Tw/Tz/Ts | Estado conservado por fragmento; tipografía no estimada como si fuera exacta. |
| Fuentes/streams compartidos | Editar una página no afecta otra. |
| Inserción en frontera y párrafo vacío | Estilo heredado determinista y documentado. |
| Borrado entre tres estilos | Sin texto perdido ni procedencias descartadas. |
| ñ, acentos y caracteres no WinAnsi | Bytes correctos o diagnóstico bloqueante, nunca pérdida silenciosa. |
| Emoji, combinación diacrítica, ligadura | No partir unidades inválidas; capacidad no soportada informada. |
| Espacios repetidos y saltos duros | Conservación en borrador y distinción de cortes suaves. |
| Selección de revisión antigua | Confirmación futura rechazada antes de escribir. |
| Cancelar/Undo/Redo | Documento e historial coherentes; bloques originales conservados. |
| Fondo azul/texto blanco, columnas/listas | Sin máscara blanca, fusión de columnas ni desplazamientos involuntarios. |
| Latencia de edición | Conservar la superposición hasta recibir el render nuevo; medir sobre el mismo PDF y equipo. |

## Pruebas de base disponibles

`MixedStylePreparationTests` construye PDFs mínimos con estilos mixtos en un mismo objeto de texto, sobre blanco o azul. Comprueba que el roundtrip conserve texto y píxeles, que el analizador nativo no declare editable ese objeto todavía y que editar/deshacer una línea compatible no altere el contenido mixto.

Estas pruebas protegen el comportamiento actual; no prueban que la edición mixta esté implementada. La matriz anterior detalla pruebas futuras y se completará con cada entrega.

```bash
DOTNET_CLI_HOME=/tmp/mythlix-cli .sdk/dotnet build Mythlix.sln -m:1 --no-restore
DOTNET_CLI_HOME=/tmp/mythlix-cli .sdk/dotnet test Mythlix.sln -m:1 --no-restore
```

## Riesgos que deben resolverse antes de integrar

- El detector de fondo rechazó los bloques del PDF técnico durante el diagnóstico anterior; la comprobación de transición aisló la confirmación. Mantener esta incidencia visible y crear una reproducción independiente antes de afirmar validación completa de apertura de edición.
- La ruta de máscara puede conservar texto subyacente en búsqueda/copia; no es borrado seguro ni se usará para afirmar edición nativa.
- La documentación histórica contiene estados antiguos. Este documento describe solo la nueva preparación de estilos mixtos y no sustituye los resultados verificados de las otras funciones.


## Resultado del paso 1A

Se añadieron `TextRange`, `TextDocumentContext`, `TextSourceSpan`, `PdfFontReference`, `RunRgbColor`, `PdfRunStyle`, `RunSourceSlice`, `RunInsertionAnchor` y `PdfTextRun` en Core, sin dependencias externas nuevas.

Los constructores validan rangos sin overflow, identidad y revisión, estados numéricos finitos, colores RGB y escala positiva. La igualdad de estilos compara valores y ámbito de fuente. Los fragmentos toman una copia inmutable de las procedencias recibidas; los originales deben estar cubiertos por rangos ordenados, sin huecos ni solapamiento. Los insertados necesitan un ancla explícita. No se permiten rangos que corten pares sustitutos UTF-16.

El modelo puede almacenar texto de borrador todavía no codificable, sin afirmar que tenga glifos o bytes PDF disponibles. La validación completa de grafemas, codificación y mapeo queda para los pasos 1B/1C. No se agregaron métricas ni codificaciones simuladas. El primer color de dominio es RGB; el adaptador deberá convertir Gray de forma explícita y no declarar soporte de espacios de color arbitrarios.

`TextRunContractTests` verifica preservación de múltiples procedencias, aislamiento de colecciones, identidad de fuente por ámbito, revisiones `long`, anclas, rangos inválidos y fronteras UTF-16. El siguiente paso es 1B: extracción nativa de múltiples operadores y cambios de estilo como ruta adicional de solo lectura.


## Resultado del paso 1B

`PdfDocumentSession.AnalyzeTextRuns(page, revision)` expone una ruta adicional de solo lectura. La sesión aporta un ID estable durante su vida; la revisión `long` debe proceder del propietario de la sesión. No modifica `IContentEditor`, no se invoca automáticamente durante RenderAsync y no habilita escrituras ni controles nuevos.

`NativeRunExtractor` interpreta varios Tj/TJ y cambios de fuente/estado dentro de BT/ET. Admite fuentes estándar Type 1 de las familias Helvetica, Courier y Times, con WinAnsi explícito, sin fuentes incrustadas ni ToUnicode. Conserva Tc, Tw, Tz, Ts, Tf, relleno RGB/Gray, TL y movimientos Tm (solo traslación), Td, TD, T* y comillas. El estado heredado y q/Q se siguen entre objetos y streams completos.

Cada cadena no vacía produce un `PdfTextRun` con estilo, bytes decodificados inmutables, avances por carácter y posición. Los avances están en puntos e incluyen Tc tras cada carácter, también el último: se utilizan para encontrar el comienzo exacto de la siguiente cadena. La línea base almacenada excluye Ts; el desplazamiento vertical permanece en el estilo. No se publican índices de glifo ni se afirma soporte de shaping.

La procedencia identifica el stream y el rango de bytes del token literal/hexadecimal original (incluidos sus delimitadores), la huella SHA-256 del stream y del objeto BT/ET, y el rango UTF-16 dentro de la concatenación de cadenas de ese objeto. No se insertan espacios o saltos inventados entre cadenas. El operador de visualización tiene su rango independiente. Cada número de TJ se conserva con su rango, valor PDF y desplazamiento efectivo en puntos, sin mezclarlo con bytes ni caracteres.

El ámbito de recursos se resuelve desde la página o sus ancestros sin crear diccionarios. Los anchos declarados, cuando existen, prevalecen sobre las métricas estándar y se validan antes de usarlos. La revisión y página distinguen referencias aunque compartan recursos.

Un operador o estado no compatible rechaza la página completa con un diagnóstico estructurado y vacía los resultados parciales. Entre las restricciones están CTM, recortes, XObjects, marcado lógico, estados gráficos externos, color no admitido, rotación, texto que no sea relleno, objetos divididos entre streams y capas Mythlix/OCR añadidas. No se ignoran estas capas suponiendo que no afecten al estado. El editor anterior sigue disponible con sus capacidades propias.

Pruebas `NativeRunExtractionTests`: estilos mixtos, tamaños decimales, estado heredado, recursos heredados, cadenas escapadas y hexadecimales, WinAnsi, TJ y sus ajustes, avances y líneas base, fuentes/codificaciones no compatibles, contenido inválido, streams comprimidos/compartidos y comparación de píxeles tras analizar y guardar. Las pruebas históricas de preparación continúan comprobando que el escritor anterior no declare editables objetos mixtos.

El siguiente paso es 1C: buffer de mutaciones y validación WinAnsi sobre estos contratos. La composición compartida y la escritura mixta todavía no están implementadas.


## Resultado del paso 1C

`ParagraphRunBuffer` es un borrador inmutable en Core. `InsertText`, `PasteText`, `DeleteRange`, `ReplaceRange` y `SplitRunAt` devuelven `RunEditResult` sin modificar el búfer anterior ni la sesión PDF. La división de fragmento no inserta un salto; los saltos duros se introducen explícitamente como texto. El adaptador futuro deberá proporcionar únicamente los fragmentos del párrafo seleccionado: `FromRuns` no reconoce párrafos ni inventa separadores.

Las posiciones de edición son UTF-16 y se validan contra grafemas Unicode mediante `StringInfo`, incluidos pares sustitutos, marcas combinadas, secuencias emoji y CRLF. UTF-16 inválido o un rango que corte un grafema devuelve el borrador anterior sin aplicar la operación. Los espacios y saltos introducidos se conservan literalmente.

La inserción hereda el estilo izquierdo en una frontera y el primero al comienzo. Un borrador vacío mantiene estilo y ancla explícitos. Los insertados solo tienen ancla, no bytes originales ficticios. Al recortar texto original se estrecha su rango de texto fuente y se mantiene el rango del token PDF contenedor, porque los escapes impiden deducir offsets de bytes a partir de índices UTF-16.

Los fragmentos originales adyacentes de estilo igual se fusionan manteniendo todas sus procedencias. Los insertados solo se fusionan cuando comparten exactamente la misma ancla; anclas distintas permanecen separadas. El inventario `OriginalSources` conserva también las procedencias borradas para la validación futura de revisión y huellas. El buffer rechaza mapeos de fuente ambiguos o no exactos; no afirma soportar ligaduras N:M.

`IFontEncodingResolver` vive en Core. `PdfDocumentSession.CreateRunEncodingResolver(page, revision)` crea una instantánea inmutable de los recursos usados por la extracción nativa compatible. `NativeWinAnsiEncodingResolver` consulta el ámbito/recurso real y copia sus tablas de anchos, en lugar de decidir por nombre de familia. No acepta otra página/revisión/recurso, controles ni códigos o anchos no compatibles; no emplea fallback de fuente ni de codificación. Si hay un error devuelve diagnósticos y cero bytes, nunca una codificación parcial.

La validación del buffer trata CR, LF y CRLF como saltos de estructura, no como bytes de una cadena PDF. Verifica la identidad de fuente incluso en fragmentos que solo contienen saltos y párrafos vacíos. Los caracteres no codificables pueden permanecer en el borrador para corregirlos: `Applied=true` indica que cambió el borrador, mientras `IsEncodingValid=false` informa de errores. Este indicador no autoriza un commit ni comprueba composición, colisiones o revisión viva del documento.

La instantánea del resolvedor no se actualiza si cambia la sesión PDF. La confirmación posterior deberá comprobar revisión, huellas y geometría de nuevo; esta etapa no escribe streams ni modifica el historial. No hay controles nuevos activados.

`ParagraphRunBufferTests` comprueba herencia de estilos, reemplazos entre estilos, procedencias recortadas y fusionadas, inventario de texto borrado, párrafo vacío, pegado con espacios y saltos, fronteras de grafemas, corrección de texto no codificable, fuentes/revisiones ajenas, anchos declarados incompletos y conservación del documento original.

El siguiente paso es 1D: adaptador de solo lectura y diagnósticos por bloque para conectar esta base de forma controlada. La composición compartida y la escritura mixta siguen pendientes.


## Resultado del paso 1D

Se añadió `ITextRunInspector`, independiente de `IContentEditor`, y `PdfDocumentSession.InspectTextRuns(page, revision)`. El adaptador inventaría objetos BT/ET y reúne sus fragmentos, ajustes TJ y diagnósticos sin convertirlos en bloques editables del motor anterior. `TextBlockInspection` distingue si el análisis nuevo está disponible y si el escritor anterior ya admite ese objeto: son capacidades independientes.

La correspondencia con `OriginalTextBlock` exige contexto de documento/página/revisión, ID de objeto y huella del stream. La ruta XML/TSV (`v:`) no se enlaza por semejanza de texto ni geometría. Esta correspondencia es informativa; no autoriza un commit ni sustituye la validación de recursos y huellas antes de escribir.

Si el extractor rechaza el estado de página, el informe mantiene el diagnóstico global y localiza el error en el objeto correspondiente cuando el offset lo permite. Los demás objetos reciben `PageAnalysisRejected`; no se presentan resultados parciales como completos. Contenido mal formado añade `InventoryUnavailable`. Las capas Mythlix y OCR no se muestran como objetos originales.

La interfaz incorpora un desplegable **Texto original → Fragmentos · solo lectura → Analizar fragmentos**. Permite consultar bloques, contenido de cada fragmento, familia, tamaño y diagnósticos. La selección del informe no mueve, reformatea ni selecciona contenido para escribir. No hay controles de edición mixta. El análisis solo se ejecuta a petición; no se añade al camino habitual de RenderAsync.

El ViewModel comprueba documento/página/revisión antes de publicar un resultado asíncrono. Los informes se limpian al cambiar de documento, página, revisión o al refrescar los bloques tras renderizar. `DocumentViewModel.Revision` es observable para invalidar inmediatamente los informes; las suscripciones se liberan al limpiar/cerrar. Ningún informe se reutiliza automáticamente para otra revisión.

Pruebas `TextRunInspectionTests`: agrupación por objeto, estilos mixtos, compatibilidad anterior, coincidencia por contexto/huella, rechazo de vínculos con respaldo, diagnósticos locales/globales, inventario incompleto, capas propias y conservación de píxeles en páginas compartidas. La prueba de ventana `--text-inspection-smoke references/Mythlix-Documento-Tecnico.pdf` verifica comandos, ausencia de cambios de historial/revisión durante la consulta y limpieza por cambio de contexto. El cambio de revisión dentro de esa prueba es una acción explícita del diagnóstico, no del adaptador.

Los pasos 1A–1D dejan lista la base de estilos mixtos. El siguiente trabajo es definir e implementar la composición compartida (métricas/glifos y geometría), seguida del commit transaccional y sus pruebas visuales. Todavía no se ofrece edición y guardado de párrafos mixtos en la interfaz.

La continuación está documentada en [Fase 2: composición compartida](PHASE2_COMPOSITION.md). La entrega 2A incorpora el compositor métrico; el adaptador de glifos y el commit permanecen pendientes.
