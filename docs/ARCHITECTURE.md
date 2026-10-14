# Arquitectura y viabilidad

## Selección de tecnologías

| Componente | Capacidades reales | Licencia / decisión |
|---|---|---|
| Avalonia 12.1.1 + Skia | UI de escritorio en Windows y Linux; Skia renderiza controles, no interpreta por sí solo PDF | MIT; seleccionada |
| PDFsharp **6.2.4** | Lectura y escritura PDF; modificar estructura, páginas y añadir contenido. No reconstruye párrafos ni proporciona renderizado de páginas | El `.nuspec` de este paquete declara MIT. Fijado; no actualizar automáticamente |
| Poppler | Renderizado raster bajo demanda y extracción de texto; no motor de edición | GPL; herramienta externa instalada por el usuario, no distribuida con Mythlix |
| PDFium | Renderizado, extracción, APIs de objetos de página; reemplazos de texto requieren gestionar codificación y fuentes | Licencia BSD y avisos de terceros; candidato a sustituir Poppler tras integrar binarios x64 |
| MuPDF | Renderizado, extracción y operaciones sobre PDF | AGPL/comercial; evaluar obligaciones/coste antes de integrar |
| iText | Escritura, formularios y operaciones de contenido; la edición de párrafos conservando formato necesita lógica adicional | AGPL/comercial; evaluar contrato comercial según distribución |
| Tesseract | OCR local; no conserva automáticamente diseño ni fuentes | Apache 2.0; fase futura, requiere modelos de idioma y motor de composición |

Atención: la documentación actual de PDFsharp 6.5 indica AGPL/comercial. No debe extrapolarse a la versión 6.2.4 seleccionada, cuyo paquete local contiene declaración MIT. Auditar versiones y avisos transitivos para cualquier distribución final. No se ha elegido una licencia para el código de Mythlix.

Fuentes primarias:
- https://avaloniaui.net/ y https://docs.avaloniaui.net/docs/supported-platforms
- https://docs.pdfsharp.net/General/Overview/FAQ.html
- https://github.com/empira/PDFsharp
- https://poppler.freedesktop.org/
- https://pdfium.googlesource.com/pdfium/+/refs/heads/main/LICENSE
- https://mupdf.com/licensing/
- https://itextpdf.com/how-buy/legal
- https://github.com/tesseract-ocr/tesseract

## Capas

`Mythlix.Core` define contratos `IPdfDocument`, `IPdfRenderer`, `IContentEditor`, `IOcrEngine`. No depende de Avalonia ni bibliotecas PDF.

`Mythlix.Infrastructure` implementa PDFsharp y el adaptador de procesos Poppler. Los argumentos se pasan mediante `ArgumentList`, sin intérprete de comandos. Cada proceso tiene un límite de 45 segundos y se termina junto con sus descendientes al cancelar. El contenido de errores de Poppler no se expone para evitar registrar documentos.

`Mythlix.Desktop` aloja la vista Avalonia y modelos observables MVVM. Los eventos de vista gestionan diálogos nativos y delegan operaciones a los modelos. `DocumentViewModel` conserva la sesión, página y pilas de historial. Las tareas pesadas no se ejecutan en el hilo de UI. `WorkspaceViewModel` coordina documentos y renderizado. La fase 2 incorpora comandos MVVM para edición gráfica y `PageCanvas` para selección y gestos. Los servicios de diálogos e inyección de dependencias siguen pendientes.

Solo la página seleccionada y su miniatura se conservan como imágenes; al reemplazarlas se liberan los bitmaps. No se precargan cientos de páginas. El zoom actual escala una imagen de hasta 1600 píxeles; el renderizador permite hasta 2400. La fase 6 inicial incorpora una caché LRU compartida de PNG y texto extraído con límite de 32 MiB; no incluye bitmaps decodificados ni renderizado progresivo. La fase 3 añade hasta cinco miniaturas cercanas bajo demanda.

Deshacer utiliza instantáneas de PDF, sin rasterizar el documento exportado. El historial de deshacer reduce entradas cuando supera 64 MB; una sola entrada puede superar el presupuesto. Este mecanismo no es óptimo para documentos grandes y será sustituido por comandos de edición incrementales. El límite inicial por archivo es 256 MB; no hay límite agregado entre pestañas.

Guardado: archivo temporal en el directorio de destino y renombrado al completar la escritura. El original está protegido en el flujo UI. Un archivo temporal de trabajo por sesión, actualizado solo cuando cambia la revisión, alimenta Poppler y se elimina al cerrar normalmente. No hay recuperación tras cierre abrupto, autoguardado, cifrado de temporales ni garantía de escritura durable ante corte eléctrico. No se ejecutan acciones ni JavaScript del PDF desde la UI.

## Edición gráfica (fase 2)

La capa de objetos añadidos se compone en un stream propio, con metadata de escena persistente y recursos por página aislados. Incluye texto, imágenes PNG/JPEG/BMP y formas; no altera objetos originales. Detalles, límites y uso en [PHASE2.md](PHASE2.md). Las cuatro variantes de Liberation Sans se incorporan como recursos y conservan la licencia OFL 1.1.

## Edición original (fase 3 inicial)

`OriginalTextEditor` implementa `IContentEditor` a través de la sesión. `ContentTokenizer` identifica operandos y offsets sin reserializar operadores no editados. Las métricas estándar y el ancho original restringen el reemplazo. Se copia el stream afectado para proteger páginas que comparten recursos, y se verifica SHA-256 contra selecciones obsoletas.

`OriginalTextViewModel` añade selección, inspector y comandos de texto original. Las miniaturas cercanas se cargan bajo demanda, con liberación fuera de la ventana de cinco páginas. La interfaz incorpora menús y un lienzo con ajuste de página. Cobertura y límites en [PHASE3.md](PHASE3.md).

## OCR local (fase 4)

`TesseractEngine` implementa el contrato OCR mediante CLI local, con idiomas y cajas TSV. `OcrViewModel` añade revisión y corrección y comprueba la versión de la sesión antes de aplicar. `SearchableLayerWriter` escribe texto invisible con fuente Unicode en una capa propia, conservando el escaneo. El lote es transaccional y cancelable. `OcrReconstruction` exporta un documento separado con objetos editables, sin imágenes. Véase [PHASE4.md](PHASE4.md).

## Fases siguientes

La edición auténtica exige analizar streams, matrices CTM, operadores BT/ET/Tj/TJ, fuentes embebidas y ToUnicode. Debe bloquearse el reemplazo que no pueda conservar codificación y métricas; las superposiciones son operaciones independientes. `IContentEditor` ya implementa el subconjunto conservador de fase 3; la ampliación a fuentes embebidas, Unicode avanzado y párrafos sigue pendiente.

El OCR ya procesa por páginas y conserva el escaneo en modo buscable explícito; orientación, deskew y regiones requieren ampliar el módulo. AcroForms, conversiones, firmas y redacción requieren adaptadores especializados y pruebas de preservación. La redacción debe eliminar contenido, nunca limitarse a cubrirlo.

Publicación inicial mediante carpetas autocontenidas. Instaladores y pruebas Windows/Linux X11/Wayland forman una fase independiente. No se afirma compatibilidad verificada fuera del host Linux actual.
