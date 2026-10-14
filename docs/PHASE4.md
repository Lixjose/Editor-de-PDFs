# Fase 4 — OCR local

Mythlix reconoce páginas escaneadas con Tesseract, en español, inglés o ambos, sin enviar imágenes ni documentos a servicios externos.

## Uso

1. Abra un PDF escaneado. Hay un ejemplo en `references/Mythlix-Escaneo-OCR.pdf`.
2. Abra **OCR → Reconocimiento local**. El motor comprueba los idiomas instalados.
3. Elija idioma y pulse **Página actual** o **Todo el PDF**.
4. Por defecto se omiten páginas que ya tienen texto seleccionable. Desmarque la opción para volver a reconocer una página, incluso si ya tiene una capa OCR. En documentos mixtos con encabezado seleccionable y cuerpo escaneado, esta comprobación omite toda la página, no solo el encabezado.
5. Seleccione la página de resultados y corrija las palabras en el inspector. El porcentaje refleja la confianza del reconocimiento original, no la certeza de una corrección manual.
6. Elija una salida:
   - **Crear capa buscable** conserva la página original y añade texto invisible, seleccionable y buscable. Guarde una copia mediante la acción general de Mythlix. Deshacer/rehacer incluye el lote completo.
   - **Exportar texto reconocido** guarda UTF-8, con separación por páginas. La exportación del último resultado sigue disponible después de aplicar la capa.
   - **Reconstrucción editable** crea otro PDF solo con el texto reconocido, como objetos editables de Mythlix. No conserva imágenes, fondos o gráficos; la fuente y la distribución pueden cambiar. La aplicación pide confirmación de este modo antes de exportar.

Puede cancelar reconocimiento o aplicación de la capa. Cancelar reconocimiento descarta sus borradores sin alterar el PDF. Cancelar aplicación revierte las páginas ya procesadas y mantiene la revisión disponible. Si cambia el documento después de reconocerlo, la aplicación exige repetir OCR para evitar usar coordenadas obsoletas.

La revisión es temporal: no se guarda en la sesión después de cerrar o cambiar de documento. El PDF buscable y el PDF reconstruido sí conservan su contenido al guardarse.

## Instalación

Se requieren Tesseract y sus modelos `spa` y `eng`. En este proyecto se extrajeron paquetes oficiales de Fedora a `.tools/ocr`, sin instalar programas del sistema. El motor local 5.5.3 utiliza bibliotecas nativas ya presentes en este host. Este runtime local se ignora en Git y no forma parte de la publicación .NET.

Para otra máquina, instale Tesseract y los modelos con su gestor de paquetes (por ejemplo `tesseract-ocr`, `tesseract-ocr-spa` y `tesseract-ocr-eng` en Debian/Ubuntu; los nombres varían según la distribución). En Windows, instale una distribución compatible de Tesseract y sus DLL/modelos.

Puede configurar:

- `MYTHLIX_TESSERACT`: ruta completa al ejecutable.
- `MYTHLIX_TESSDATA`: directorio de archivos `.traineddata`.

Sin override, Linux busca el runtime local del proyecto y después el ejecutable en PATH. Windows usa PATH. La distribución local Fedora no debe copiarse como un runtime universal para otras distribuciones o Windows.

Los modelos se descargan solo al preparar la instalación. Reconocimiento y exportación funcionan sin conexión una vez instalados.

## Arquitectura

- `IOcrEngine`, `OcrWord` y `OcrPageResult` en Core separan reconocimiento de UI y composición PDF.
- `TesseractEngine` inicia un proceso local con argumentos separados, valida idiomas y interpreta TSV con cajas y confianza. Usa temporales privados, limpieza en finally, cancelación con terminación del proceso y límite de tres minutos por proceso. Limita OpenMP a dos hilos.
- Poppler renderiza una página por vez a aproximadamente 300 DPI, con un máximo de 4000 píxeles en el lado mayor.
- `SearchableLayerWriter` incorpora una fuente Unicode y escala cada palabra a su caja reconocida. Usa modo PDF **3 Tr** (texto invisible), conserva los streams originales y marca únicamente su propia capa. Las coordenadas son aproximaciones basadas en las cajas OCR.
- Al repetir OCR se sustituye la capa propia, con limpieza de sus mapeos de recursos. Los recursos por página se aíslan para evitar modificar otra página que comparte un diccionario.
- `SetOcrPages` aplica un lote en una transacción con un backup. Cancelación o fallo restaura el documento. El historial registra el lote como una sola acción.
- `OcrReconstruction` agrupa palabras cercanas en filas y compone otro PDF con cuadros de texto editables. No intenta recuperar la estructura exacta ni las imágenes del escaneo.
- `OcrViewModel` gestiona progreso, revisión, corrección y versión del documento. Las operaciones costosas se ejecutan fuera del hilo UI.

## Límites

- Solo páginas sin rotación, recorte u origen desplazado en esta entrega. Se informa y se bloquea la operación, en lugar de colocar texto mal alineado.
- Sin detección automática de orientación, corrección explícita de inclinación, limpieza manual de ruido ni selección de regiones todavía. Tesseract usa su procesamiento interno; no se garantiza precisión en documentos degradados, manuscritos, columnas o tipografías complejas.
- Un archivo por lote; no hay cola de varios archivos.
- Hasta 500 páginas y 100 000 palabras por reconocimiento completo; 20 000 palabras por página, imagen de hasta 24 megapíxeles y 64 MB de entrada.
- La reconstrucción usa el límite de 200 objetos gráficos por página. Pueden cambiar saltos y espaciado y perderse información no textual.
- La corrección manual permite sustituir palabras, no editar sus cajas. La confianza es una estimación del motor.
- El historial sigue usando snapshots y puede resultar costoso para archivos grandes. No hay recuperación persistente de trabajos OCR.
- No incluye firmas, redacción segura ni cumplimiento PDF/A. Los archivos firmados conservan las restricciones de apertura de las fases anteriores.
- Las restricciones de salida/stdout se comprueban tras la lectura; no hay un límite estricto de memoria del proceso nativo. La seguridad completa ante archivos hostiles sigue pendiente.

## Verificación

```bash
dotnet build Mythlix.sln -m:1
dotnet test Mythlix.sln -m:1
dotnet run --project src/Mythlix.Desktop --no-build -- \
  --phase4-smoke references/Mythlix-Escaneo-OCR.pdf
```

El modo de prueba usa un escaneo de verificación, reconoce el documento, corrige una palabra, exporta texto y reconstrucción, aplica la capa, comprueba deshacer/rehacer y guarda un PDF buscable. Genera:

- `artifacts/phase4-preview.png`
- `artifacts/phase4-ocr.txt`
- `artifacts/phase4-searchable.pdf`
- `artifacts/phase4-editable.pdf`

La ventana diagnóstica se cierra al terminar. La compilación nueva requiere reiniciar una instancia anterior de Mythlix.

Documentación primaria: [CLI, TSV e idiomas de Tesseract](https://tesseract-ocr.github.io/tessdoc/Command-Line-Usage.html), [calidad y limitaciones de entrada](https://tesseract-ocr.github.io/tessdoc/ImproveQuality.html). Tesseract y sus modelos se distribuyen bajo Apache 2.0; la fuente incorporada mantiene su aviso OFL. Revise además las licencias de las bibliotecas nativas al distribuir un runtime.

## Revisión y mejoras de OCR

- Selector de segmentación: automático (PSM 3), una columna (4), bloque uniforme (6) y texto disperso (11). El modo se conserva durante todo el lote; no implica detección automática de orientación.
- Reconocimiento de intervalos como `1,3-5`, con validación antes de borrar la revisión anterior y máximo de 500 páginas seleccionadas.
- Exportación con saltos de línea utilizando bloque, párrafo y línea del TSV. La reconstrucción evita unir líneas de bloques distintos. No se reconstruyen tablas ni un diseño DOCX.
- Resumen de palabras y avisos para confianza inferior al 70%; la confianza es la original de Tesseract y no cambia al corregir el texto.
- Cancelación o fallo: conserva las páginas ya reconocidas para revisión, exportación o aplicación. El reconocimiento nunca modifica el PDF; aplicar sigue siendo transaccional. La revisión permanece solo en memoria.
- Validación del encabezado PNG y dimensiones antes de ejecutar Tesseract: máximo 6000 por lado, 24 megapíxeles y 64 MB de archivo. Las salidas se limitan mientras se leen (16 MB de resultado y 256 KB de diagnóstico); se termina el proceso al excederlos.

Siguen pendientes orientación automática, deskew, regiones, preprocesamiento de imagen, recuperación persistente y soporte de capas OCR sobre páginas rotadas/recortadas. La selección de modo facilita distintos diseños, pero no garantiza mayor precisión en todos los escaneos.
