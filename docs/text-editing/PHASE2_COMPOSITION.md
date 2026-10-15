# Fase 2 — Composición compartida de estilos mixtos

## Estado y entregas

- **2A, implementado:** compositor métrico WinAnsi independiente de la UI, métricas de recursos reales y propuestas de composición inmutables.
- **2B, vista previa experimental integrada:** fuente física explícita, adaptador sin recalcular cortes y pruebas de estilos mixtos en cuatro alineaciones. Admite bloques originales de una sola línea, recomponiéndolos a petición. No afirma equivalencia con los contornos originales.
- **2C, integrada en alcance inicial:** motor transaccional y ventana de borrador con composición, confirmación explícita e historial del PDF. Se conserva la restricción de un bloque original de una línea y se rechazan colisiones y fondos dibujados.

El motor y el editor existentes siguen funcionando. La vista previa se abre a petición. La composición solo se escribe tras revisar y aplicar explícitamente el borrador; no se llama automáticamente desde RenderAsync.

## Contratos y uso

`IRunMetricsProvider` amplía `IFontEncodingResolver`. `PdfDocumentSession.CreateRunMetricsProvider(page, revision)` devuelve una instantánea de los recursos nativos compatibles utilizados por la extracción de la fase 1. Los anchos declarados prevalecen sobre los estándares; ascenso/descenso proceden de las métricas de la fuente estándar admitida. No se consultan medidas de Avalonia ni se elige una sustitución por nombre de familia.

```csharp
var analysis = session.AnalyzeTextRuns(page, revision);
// El llamador debe seleccionar los fragmentos de UN párrafo verificado.
var draft = ParagraphRunBuffer.FromRuns(selectedRuns);
var metrics = session.CreateRunMetricsProvider(page, revision);
var options = new ParagraphCompositionOptions(bounds, firstBaselineY, lineHeightPt, TextAlignment.Justify);
var proposal = ParagraphComposer.Compose(draft, options, metrics);
```

`ParagraphCompositionSnapshot` conserva texto, contexto, inventario original completo, opciones, líneas, segmentos y diagnósticos. Cada segmento referencia su propietario inmutable, rango de texto local y rango global, bytes WinAnsi, avances efectivos y ajustes de justificación por carácter. No se modifica la procedencia para simular nuevos bytes originales.

Las posiciones y dimensiones están en puntos de página, desde la esquina superior izquierda. La línea base del segmento excluye Ts; su propietario conserva el desplazamiento. La escala horizontal es un factor (1 = 100 %). Los avances incluyen ancho nominal, Tc, Tw original y Tz, también el avance de Tc tras el último carácter. `AdvanceBounds` es una caja métrica de avance y altura tipográfica; no es una caja exacta de tinta ni comprueba colisiones con el contenido de la página.

## Comportamiento

- El corte considera palabras completas incluso cuando atraviesan cambios de estilo. No se divide una palabra por el mero cambio de fuente.
- Una palabra que excede el ancho conserva todos sus caracteres y genera `HorizontalOverflow`, sin comprimir fuentes ni dividirla arbitrariamente.
- Los espacios repetidos se conservan, incluidos los iniciales y finales. Pueden repartirse en líneas si su avance no cabe; no se recortan silenciosamente. NBSP no ofrece una oportunidad de corte.
- CR, LF y CRLF son saltos duros: se conservan con su rango y no se emiten como bytes de cadena PDF. Se conservan líneas vacías y la línea final tras un salto terminal.
- Izquierda/centro/derecha usan los mismos avances. La justificación solo amplía espacios ASCII internos en líneas con corte suave. No justifica la última línea de cada párrafo, ni espacios iniciales/finales. Conserva el Tw original y añade el extra ya escalado en puntos.
- Los segmentos siguientes comienzan tras la suma exacta de avances anteriores. Cambios de color, fuente y tamaño permanecen en el propietario de cada segmento.
- Los límites verticales consideran tamaños y Ts. Se informan `VerticalOverflow` y `LineOverlap` si el interlineado no separa las cajas métricas. Las líneas vacías usan el estilo explícito del buffer.
- Codificación no compatible, recurso/revisión ajenos o métricas no verificadas producen diagnósticos sin composición parcial. Avances no positivos se rechazan en este primer alcance.
- No se publica geometría infinita o NaN. Las propuestas con desbordamiento finito conservan sus líneas para diagnóstico, pero `IsValid` es falso.

## Límites y garantías pendientes

Esta es composición métrica de un borrador hacia un rectángulo objetivo explícito. **No es reconstrucción automática del aspecto original.** `ParagraphRunBuffer` no almacena las matrices de posición entre cadenas ni los ajustes originales de TJ. La integración futura deberá transportar y preservar esos datos, o rechazar su reinterpretación sin una política explícita de reflujo; no se debe aplicar esta propuesta como sustitución automática de cualquier selección nativa.

No se generan GID a partir de bytes PDF ni se afirma que Unicode, código WinAnsi y GID sean equivalentes. La fuente física, los contornos, el rasterizado, kerning/shaping avanzado, fuentes incrustadas/CID, ligaduras, tintas CMYK y fondos complejos requieren trabajo posterior. Los caracteres WinAnsi de esta entrega son un mapeo 1:1 validado, no una afirmación de soporte Unicode general.

`IsValid` solo describe la composición métrica y sus diagnósticos; no autoriza escribir. La confirmación requerirá comprobar el estado vivo de la sesión, las fuentes, huellas y revisión, márgenes/colisiones y aislamiento de recursos. También deberá convertir correctamente el extra por espacio a Tw/TJ, sin aplicar Tz dos veces, y preservar el fondo.

## Verificación

`ParagraphCompositionTests` cubre mezcla de estilos, avances frente al extractor, alineaciones, justificación, Tw/Tc/Tz/Ts, palabras que atraviesan estilos, espacios y NBSP, CR/LF/CRLF y líneas vacías, desbordamientos, interlineado, fuentes/revisiones ajenas, codificación imposible y overflow numérico. La suite existente sigue protegiendo edición, historial, inspección y conservación del contenido.

La verificación métrica de 2A no prueba fidelidad de contornos ni escritura. El inicio de la validación visual de 2B se detalla a continuación; la escritura sigue en 2C.

## Inicio de 2B — adaptador de vista previa

`Mythlix.Desktop.TextEditing.PreviewFontBinding` carga bytes de un archivo físico mediante el backend de Avalonia, registra SHA-256 y asocia la cara únicamente a un `PdfFontReference` (documento, página, revisión, ámbito y recurso). La asociación es explícita: los recursos estándar no identifican por sí mismos los contornos originales. El llamador debe conservarla viva hasta liberar las vistas que la usan. No se busca una familia ni se hace fallback.

`ParagraphGlyphPreview` consume los segmentos del plan válido: conserva cortes, propietarios y avances efectivos, incluidos los extras de justificación ya calculados. Obtiene GID desde el cmap de la cara física, nunca desde bytes PDF. En este alcance 1:1 los offsets de glifo son cero; Ts desplaza la línea base y Tz escala los contornos. Los avances se dividen por Tz antes del transform para evitar escalar dos veces. El color procede del estilo del segmento. Un recurso ausente/de otra revisión o un glifo ausente rechaza la vista completa y libera los GlyphRun parciales.

La prueba `ParagraphGlyphPreviewTests` dibuja texto blanco con ñ sobre azul y comprueba asociación física, GID, avances con Tz y rechazo de recursos/composición inválida. Genera `/tmp/mythlix-phase2b-preview.png` como evidencia diagnóstica. Esta imagen inicial se inspecciona visualmente; no es una comparación de fidelidad contra el PDF original.

### Vista previa integrada

En **Texto original → Análisis de fragmentos → Editar borrador y recomponer…** se abre una ventana independiente. Exige analizar y seleccionar un bloque compatible de una sola línea; los bloques de varias líneas se rechazan porque BT/ET no define por sí mismo un párrafo. La ventana explica que se recomponen los fragmentos y que no se preservan sus posiciones originales ni TJ. El usuario elige un archivo TTF/OTF por recurso, con nombre y huella visibles, y pulsa Componer y revisar. Puede ajustar posición, dimensiones, línea base, interlineado, alineación y fondo blanco/azul/oscuro de la vista. No hay selección automática de una fuente sustituta.

La vista libera los GlyphRun antes de reemplazar una fuente y al cerrar. Un cambio de documento, página, revisión o bloque cierra la ventana y elimina las suscripciones. Los resultados con diagnósticos no se dibujan. La vista y los cambios de borrador no modifican historial, revisión ni contenido PDF. La confirmación explícita integrada en 2C se describe al final.

La matriz nueva genera un PDF nativo con Helvetica normal/negrita/cursiva/negrita-cursiva, colores, tamaños, Tc, Tw, Tz y Ts, extrae sus recursos reales, compone varias líneas y dibuja usando las cuatro variantes físicas elegidas de Liberation Sans. Cubre izquierda/centro/derecha/justificación, GID, propietarios, avances y conservación del archivo y del texto. Las imágenes `/tmp/mythlix-phase2b-mixed-{Left,Center,Right,Justify}.png` permiten revisión visual; no prueban equivalencia de rasterizado con Helvetica original.

`--composition-preview-smoke references/Mythlix-Documento-Tecnico.pdf` carga la ventana con un PDF mixto temporal controlado, verifica que no se dibuje sin fuentes explícitas, genera una vista con fuentes asociadas y comprueba conservación de contenido/estilos/huellas e historial. Un incremento explícito de revisión comprueba el cierre y liberación de la vista obsoleta.

La entrega de 2B cubre recomposición experimental de solo lectura. Reconstrucción automática de párrafos, fuentes originales incrustadas/CID, shaping y comparaciones de equivalencia de contornos permanecen fuera del alcance. La vista previa no autoriza escribir el plan. La API inicial de 2C se documenta a continuación.


## 2C — confirmación nativa transaccional (motor)

`IParagraphCompositionWriter` y `PdfDocumentSession.CommitParagraphComposition(draft, plan, currentRevision, allowExplicitReflow)` confirman el plan de 2A. Este primer alcance requiere un objeto BT/ET original completo de una sola línea, WinAnsi/Type1 estándar y reflujo explícito. No agrupa automáticamente párrafos, importa fuentes sustitutas ni utiliza máscaras.

El propietario debe serializar el acceso a la sesión y proporcionar **su revisión viva**, no copiar la revisión del plan como si fuera actual. La sesión comprueba ID de documento, página y revisión, inventario completo de procedencias, rangos y huellas del bloque/stream, disponibilidad de recursos y geometría. El plan ahora registra huellas inmutables de los recursos tipográficos: familia, todos los anchos y diccionario de fuente admitido. Se recompone únicamente para revalidar que el borrador, sus métricas, cortes, bytes, avances y ajustes coinciden con el plan recibido; el escritor consume los segmentos recibidos y no decide cortes distintos.

El rectángulo debe caber en una página sin rotación. Los textos ajenos y gráficos que intersecten el área se rechazan con un margen conservador de un em para evitar tratar las cajas de avance como contornos exactos. Esta primera política **también rechaza fondos dibujados que intersecten el área**; no afirma soporte de escritura sobre fondos complejos ni sustituye el fondo por blanco.

Tras validar se obtiene una instantánea recuperable y se abre una copia candidata. Se aíslan el array Contents y el stream modificado, conservando los recursos de fuente. Se vacían únicamente los tokens de cadena originales; sus operadores de estado y posiciones permanecen. Después de su ET se inserta un único objeto BT/ET con los segmentos y sus estados explícitos, aislado por q/Q. Esto conserva juntos los estilos para una edición posterior si el resultado sigue siendo de una sola línea. Se conservan los operadores exteriores y el estado heredado por objetos posteriores. Los ajustes extra de justificación se emiten como TJ con `-extraPt * 1000 / fontSizePt / horizontalScale`; Tc y Tw originales se mantienen y Tz no se aplica dos veces.

La copia candidata se analiza de nuevo antes de adoptarla. Se verifican por carácter texto, estilo, posición y avance, más los ajustes TJ; también se comprueban los fragmentos ajenos. Si algo falla, se descarta la candidata y la sesión conserva su contenido anterior. La instantánea de PDFsharp puede normalizar la representación interna, por lo que se garantiza conservación semántica, no identidad binaria de la serialización. El archivo fuente no se sobrescribe.

El resultado `ParagraphCommitReceipt` contiene una instantánea inmutable para Undo, la revisión anterior y la siguiente. La API no altera las pilas del ViewModel ni su revisión: el propietario debe registrar el recibo y adoptar `NextRevision` juntos tras el éxito. Cancelar antes de llamar o cualquier rechazo no debe producir una entrada de historial. La conexión con la edición e historial de la interfaz está implementada como se detalla a continuación.

`ParagraphCommitTests` cubre las cuatro alineaciones, mezcla de estilos y color, Tc/Tw/Tz/Ts, ñ, inserción, reemplazo y párrafo vacío; revisión/documento ajenos, fuentes cambiadas aun con anchos iguales, streams alterados, inventarios incompletos, planes falsificados, límites de página y colisiones. Comprueba recuperación con Undo, guardado/reapertura y texto buscable sin el original eliminado, así como fallo de una candidata sin alterar el contenido vivo. Las pruebas con array Contents y stream compartidos comparan píxeles de la segunda página antes/después. Los renders `/tmp/mythlix-phase2c-{Left,Center,Right,Justify}.png` permiten revisar la salida y se comprueba que el área original ya no conserve tinta.

Verificación de esta entrega: solución compilada sin errores ni advertencias; **257 pruebas aprobadas** con `DOTNET_CLI_HOME=/tmp/mythlix-cli .sdk/dotnet test Mythlix.sln -m:1 --no-restore`. Se revisó visualmente el render justificado generado por el nuevo escritor.


## 2C — borrador e historial en la interfaz

La ventana incorpora un editor de texto y un historial de borrador independiente del PDF. `ParagraphDraftEditor` aplica únicamente el rango cambiado mediante `ParagraphRunBuffer.ReplaceRange`, conservando los fragmentos y estilos no tocados. Usa la selección para desambiguar texto repetido; el adaptador de teclado captura borrado hacia atrás/delante, y la ruta de diferencias amplía a fronteras de grafemas al corregir combinaciones o pares sustitutos. Inserción y reemplazo heredan el estilo izquierdo según la regla del buffer. Deshacer/Rehacer del borrador recupera instantáneas completas con estilos y procedencias; no son entradas del historial del documento. Los caracteres no WinAnsi pueden permanecer para corrección, pero bloquean composición y aplicación.

El rectángulo inicial se obtiene de las posiciones y métricas del bloque seleccionado, en puntos de la página. Se pueden ajustar X/Y, ancho/alto, línea base, interlineado y alineación. La vista se muestra al lado de los controles; el pie mantiene visibles aceptación, Aplicar y Cancelar. Elegir otro fondo solo afecta a la vista.

Cada cambio de texto, opciones o fuente descarta el plan y los GlyphRun y desmarca la aceptación. Aplicar solo se habilita tras componer sin diagnósticos, generar la vista física y marcar la aceptación del reflujo con recursos PDF originales. Los archivos de fuente elegidos sirven únicamente para la vista y no se importan al PDF. Durante la confirmación se bloquean los controles y el cierre para mantener el acceso serializado a la sesión.

`WorkspaceViewModel.ApplyParagraphCompositionAsync` comprueba propietario, página y revisión viva, llama al escritor y, solo tras el éxito, registra una entrada Undo, vacía Redo, marca el documento modificado y adopta `NextRevision`. Luego actualiza el render. Si ese refresco falla después de una escritura correcta, el cambio sigue registrado en Deshacer y se informa del fallo de vista; no se presenta como una escritura rechazada. La ventana permanece durante esta operación y se cierra al finalizar. Cancelar, un borrador inválido o un rechazo del escritor no crean historial ni cambian revisión/Modified.

La inspección selecciona por defecto el primer objeto compatible con fragmentos, evitando el contenedor original cuyas cadenas quedaron vacías tras una sustitución. La salida nueva mantiene los segmentos en un único BT/ET. Un resultado de varias líneas sigue fuera del alcance de reapertura de este primer editor: no se relaja automáticamente la restricción de agrupación verificada.

`ParagraphEditorIntegrationTests` cubre conservación de estilos/procedencias, selección en texto repetido, corrección de grafemas no codificables, historial del borrador, una única entrada de documento, Deshacer/Rehacer y rechazos sin alterar Undo/Redo/Revision/Modified. `--composition-edit-smoke references/Mythlix-Documento-Tecnico.pdf` verifica la ventana real: fuentes explícitas, aceptación obligatoria, invalidación al editar, cancelar sin efectos, aplicar texto mixto, cierre y Deshacer/Rehacer. Genera `/tmp/mythlix-composition-editor.png` para inspección de la ventana. La prueba usa un PDF temporal controlado.

Validación de la integración: compilación sin errores ni advertencias; **264 pruebas aprobadas**. La prueba de ventana `--composition-edit-smoke` pasa, incluido el evento síncrono de cambio del TextBox y la conservación del fragmento en negrita después de guardar. La captura de la ventana se revisó visualmente.
