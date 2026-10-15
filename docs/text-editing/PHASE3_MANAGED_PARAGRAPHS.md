# Fase 3A — Reapertura de composiciones propias verificadas

Esta numeración continúa las fases 1 y 2 de `text-editing/`; no sustituye la fase histórica de interfaz descrita en `../PHASE3.md`.

## Entrega

Se pueden guardar, cerrar y volver a editar los párrafos compuestos por el nuevo escritor, aunque ocupen varias líneas. Se conserva el texto lógico, incluidos CR, LF, CRLF, líneas vacías y saltos terminales; los cortes automáticos no se convierten en saltos duros. La ventana recupera el rectángulo, línea base, interlineado y alineación anteriores. No se agrupan columnas ni párrafos por proximidad visual.

La ruta sigue siendo **Texto original → Análisis de fragmentos → Analizar fragmentos → Editar borrador y recomponer…**. Los bloques originales de una línea conservan su flujo. Para abrir uno de varias líneas se exige una composición persistida y verificada de esta entrega. Los archivos generados antes de incorporar estos datos no se reconocen automáticamente como párrafos propios.

## Datos y comprobaciones

Cada stream escrito puede contener `/MythlixParagraphsV1`, una cadena JSON con registros versionados. Cada registro conserva la huella SHA-256 del BT/ET, identidad del bloque, texto lógico, opciones de composición, estilo de líneas vacías y huellas de las fuentes PDF. Un segundo SHA-256 comprueba la integridad del registro completo. Son comprobaciones de integridad, no firmas ni pruebas criptográficas de autoría.

`PdfDocumentSession.InspectParagraphDraft(page, revision, blockId)` implementa la capacidad adicional `IParagraphDraftInspector`. Es una consulta de solo lectura y devuelve `ParagraphDraftInspection`: borrador, métricas y opciones guardadas, o diagnósticos sin un borrador parcial. El proceso comprueba:

- Versión, estructura, límites e integridad de los registros.
- Coincidencia del bloque con los bytes nativos actuales; identidad y huella no se infieren por similitud visual.
- Texto lógico frente a las cadenas PDF reales, excluyendo solo los saltos duros registrados.
- Recursos reales de fuente, incluidos todos sus anchos; no basta con que los caracteres visibles midan igual.
- Recomposición válida dentro de la página, texto, estilos, posiciones y avances por carácter, y ajustes TJ de justificación.

`ParagraphLayoutVerifier` comparte la última comprobación entre escritura y reapertura. Recalcular un sello de metadatos no evita contrastar la geometría con el PDF. Metadatos inconsistentes, fuentes modificadas o versiones desconocidas producen un diagnóstico y no habilitan una escritura de respaldo.

No se guardan IDs de documento o revisiones como autorizaciones reutilizables. Tras abrir el archivo se generan referencias de fuente, fuentes de procedencia y anclas ligadas al contexto vivo de esa nueva sesión. La confirmación sigue exigiendo la revisión actual de su propietario.

## Procedencia, fuentes y persistencia

Los saltos registrados se restauran como texto estructural con anclas reales, sin inventar bytes fuente. El inventario original completo se conserva aunque la restauración divida fragmentos. `ParagraphRunBuffer.WithEmptyStyle` recupera el estilo de escritura de las líneas vacías sin descartar ese inventario.

Un estilo de línea vacía puede usar una fuente sin glifos visibles en el párrafo. El proveedor de métricas permite solicitar ese recurso adicional de forma explícita, validando contexto, ámbito y diccionario real. No busca una fuente sustituta por familia ni amplía automáticamente el soporte a recursos incompatibles.

La copia candidata registra la composición y verifica que pueda recuperarse sin pérdida antes de adoptarla. Cuando se edita un párrafo anterior, se actualizan las identidades de los registros posteriores mediante sus huellas nativas. No se conservan entradas del texto sustituido. El array Contents, stream y valor de metadatos se mantienen aislados de páginas que compartan recursos o contenido.

El guardado y las instantáneas de Deshacer/Rehacer incluyen los registros. Las fuentes físicas elegidas para la vista siguen siendo sustitutos visuales explícitos y no se importan al PDF ni se almacenan como recursos de escritura.

## Límites

- Solo composiciones de esta versión verificables y bloques nativos originales de una línea. Un BT/ET multilínea ajeno no define por sí mismo un párrafo.
- Se conservan WinAnsi/Type1 estándar, geometría sin rotación y la política conservadora de colisiones. No se añade soporte de escritura sobre fondos dibujados, CID, fuentes incrustadas ni shaping complejo.
- Se rechazan metadatos de más de 4 MiB por stream, más de 256 registros, más de 500 000 unidades UTF-16 por párrafo o más de 256 referencias de fuente por registro. Los límites nativos de contenido siguen vigentes.
- Un párrafo completamente vacío no tiene glifos nativos seleccionables para reabrirse con esta ruta.
- Los programas externos pueden eliminar o invalidar estos datos. La reapertura no intenta reconstruirlos ni reparar la agrupación en silencio.

## Validación

Las pruebas cubren guardado/reapertura con nueva identidad de sesión, segunda edición multilínea, justificación, estilos mixtos, CR/LF/CRLF y líneas vacías, estilo vacío con fuente no visible, historial, múltiples párrafos en un stream y aislamiento de páginas compartidas. Rechazan registros alterados o versiones desconocidas, cambios de recursos y geometría nativa modificada incluso tras recalcular los sellos. Los bloques multilínea sin evidencia permanecen rechazados.

La prueba de ventana `--composition-reopen-smoke references/Mythlix-Documento-Tecnico.pdf` crea un documento temporal, lo edita, guarda, reabre y vuelve a editar, añade un salto duro y comprueba Deshacer/Rehacer. Genera `/tmp/mythlix-composition-reopened.png` para revisión visual.

Resultado de esta entrega: compilación sin errores ni advertencias y **278 pruebas aprobadas** con `DOTNET_CLI_HOME=/tmp/mythlix-cli .sdk/dotnet test Mythlix.sln -m:1 --no-restore`. La prueba de ventana terminó con `MYTHLIX_COMPOSITION_REOPEN_SMOKE_OK` y su captura se revisó visualmente.
