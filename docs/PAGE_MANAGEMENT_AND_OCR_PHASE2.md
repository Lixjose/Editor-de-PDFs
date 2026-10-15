# Fase 2: OCR Industrial y Gestión Avanzada de Páginas

Este documento describe las mejoras y nuevas capacidades implementadas en **Fase 2**, abordando los puntos críticos competitivos **3 (Gestión de páginas)** y **7 (OCR Industrial)**, y resolviendo directamente los hallazgos técnicos registrados en [OCR_ANALYSIS.md](file:///home/joselix/Documentos/Proyectos/PDF%20Mythlix/Editor-de-PDFs/docs/OCR_ANALYSIS.md).

---

## 1. Gestión Avanzada de Páginas (Punto 3)

### 1.1 Combinación de documentos (`IPdfMerger` / `PdfMerger`)
- **Contrato:** [IPdfMerger](file:///home/joselix/Documentos/Proyectos/PDF%20Mythlix/Editor-de-PDFs/src/Mythlix.Core/PageDecoration.cs) y su implementación de grado industrial [PdfMerger](file:///home/joselix/Documentos/Proyectos/PDF%20Mythlix/Editor-de-PDFs/src/Mythlix.Infrastructure/PdfMerger.cs).
- **Características:**
  - Valida la existencia, integridad y ausencia de firmas digitales restrictivas antes de iniciar.
  - Combina 2 o más archivos PDF en el orden exacto especificado por el usuario.
  - Escritura atómica a través de archivo temporal con sufijo `.tmp` y reemplazo seguro para evitar corrupción en caso de error o cancelación.
  - Soporte de cancelación cooperativa (`CancellationToken`) e informe progresivo porcentual (`IProgress<int>`).
- **UI:** Integrado en el menú *Organizar -> Combinar documentos...* con selector múltiple de archivos de origen y selector de archivo de salida.

### 1.2 Numeración de páginas, encabezados y pies (`PageDecorator.ApplyHeaderFooter`)
- **Contrato:** [HeaderFooterOptions](file:///home/joselix/Documentos/Proyectos/PDF%20Mythlix/Editor-de-PDFs/src/Mythlix.Core/PageDecoration.cs) y [IPageDecorator](file:///home/joselix/Documentos/Proyectos/PDF%20Mythlix/Editor-de-PDFs/src/Mythlix.Core/PageDecoration.cs).
- **Características:**
  - Permite formateo dinámico con comodines `{n}` (página actual) y `{total}` (número total de páginas).
  - 6 posiciones estándar: `TopLeft`, `TopCenter`, `TopRight`, `BottomLeft`, `BottomCenter`, `BottomRight`.
  - Opción de omitir la primera página (`SkipFirstPage = true`), ideal para portadas y carátulas.
  - Márgenes horizontales y verticales configurables en puntos.
  - Tipografía estándar integrada (Liberation Sans) con aislamiento de recursos por página.
  - Soporte completo de Deshacer / Rehacer (`Snapshot` / `Restore`).
- **UI:** Menú *Organizar -> Numeración y pie de página...* con cuadro de diálogo interactivo.

### 1.3 Marcas de agua configurables (`PageDecorator.ApplyWatermark`)
- **Contrato:** [WatermarkOptions](file:///home/joselix/Documentos/Proyectos/PDF%20Mythlix/Editor-de-PDFs/src/Mythlix.Core/PageDecoration.cs).
- **Características:**
  - Ángulo de rotación configurable (por ejemplo, 45° diagonal).
  - Opacidad alfa auténtica mediante canal alfa en `XColor.FromArgb`.
  - Color hexadecimal `#RRGGBB` y tamaño de fuente configurable (12 a 144 pt).
  - Posicionamiento centrado en página respetando las dimensiones y rotación de cada página.
  - Soporte completo de Deshacer / Rehacer.
- **UI:** Menú *Organizar -> Marca de agua de texto...* con preajustes recomendados (CONFIDENCIAL, COPIA, BORRADOR).

### 1.4 Reordenación visual de miniaturas
- En el panel lateral de miniaturas, cada miniatura cuenta con botones accesibles `↑` y `↓` para desplazar de inmediato la página seleccionada hacia arriba o hacia abajo sin recurrir a menús complejos.

---

## 2. OCR Industrial (Punto 7)

### 2.1 Estimación de línea base (`BaselineY`) y tamaño tipográfico (`EstimatedFontSize`)
- **Problema previo (según [OCR_ANALYSIS.md](file:///home/joselix/Documentos/Proyectos/PDF%20Mythlix/Editor-de-PDFs/docs/OCR_ANALYSIS.md)):**
  Tesseract reporta el cuadro delimitador de tinta (`w.Height`), el cual varía drásticamente entre palabras con ascendentes/descendentes y palabras minúsculas cortas (ej. "ac" vs "pl"), provocando saltos verticales espurios en la capa de texto invisible.
- **Solución implementada:**
  - `TesseractEngine.ParseTsv` ahora agrupa palabras por línea (`Block`, `Paragraph`, `Line`).
  - Calcula la línea base tipográfica común de la línea (`BaselineY`), teniendo en cuenta descenders (`g`, `j`, `p`, `q`, `y`, `,`, `;`).
  - Asigna `EstimatedFontSize = avgHeight * 0.9` consistente a lo largo de la línea.
  - [SearchableLayerWriter](file:///home/joselix/Documentos/Proyectos/PDF%20Mythlix/Editor-de-PDFs/src/Mythlix.Infrastructure/Ocr/SearchableLayerWriter.cs) posiciona el operador de texto (`1 0 0 1 X Y Tm`) alineado a la línea base calculada, garantizando selección fluida y alineación de resaltado idéntica a Adobe Acrobat.

### 2.2 Soporte de páginas rotadas (90°, 180°, 270°) y orientación mixta
- **Problema previo:**
  El motor bloqueaba de forma tajante cualquier página con `Rotate != 0` lanzando `InvalidOperationException`.
- **Solución implementada:**
  - Se eliminó la restricción arbitraria de rotación.
  - En [SearchableLayerWriter](file:///home/joselix/Documentos/Proyectos/PDF%20Mythlix/Editor-de-PDFs/src/Mythlix.Infrastructure/Ocr/SearchableLayerWriter.cs), las coordenadas de display de Tesseract se transforman al espacio de usuario PDF mediante la matriz de rotación nativa `geometry.FromDisplay`, ajustando el ángulo de orientación del texto invisible para que coincida exactamente con la orientación visual del documento escaneado.
  - Soporte de documentos con orientación mixta verificado tanto en código como en pruebas automatizadas.

### 2.3 Caché de modelos/idiomas
- [TesseractEngine](file:///home/joselix/Documentos/Proyectos/PDF%20Mythlix/Editor-de-PDFs/src/Mythlix.Infrastructure/Ocr/TesseractEngine.cs) cachea la lista de idiomas (`_cachedLanguages`) evitando el lanzamiento recurrente de procesos CLI `tesseract --list-langs` en cada página procesada durante operaciones por lotes.

---

## 3. Verificación y Pruebas Automatizadas

- **Suite de pruebas:** [PageDecorationAndOcrTests.cs](file:///home/joselix/Documentos/Proyectos/PDF%20Mythlix/Editor-de-PDFs/tests/Mythlix.Tests/PageDecorationAndOcrTests.cs) (6 pruebas nuevas) y actualización de [OcrTests.cs](file:///home/joselix/Documentos/Proyectos/PDF%20Mythlix/Editor-de-PDFs/tests/Mythlix.Tests/OcrTests.cs).
- **Cobertura de pruebas:**
  - `PdfMergerCombinesMultipleFilesWithProgressAndOrder`: Fusión de múltiples PDF y conteo de páginas.
  - `PdfMergerRejectsFewerThanTwoDocumentsOrMissingFiles`: Manejo robusto de errores de entrada.
  - `ApplyHeaderFooterRendersTokensAndSupportsUndo`: Reemplazo de tokens `{n}` y `{total}`, omisión de primera página y Undo.
  - `ApplyWatermarkRendersOnAllPagesWithAngle`: Marca de agua rotada, opacidad y Undo.
  - `TesseractEngineParsesTsvAndCalculatesBaselineAndFontSize`: Cálculo de línea base y tamaño tipográfico por línea.
  - `RotatedPagesAt90And180And270DegreesSupportSearchableOcrLayer`: Escritura y extracción Poppler en 0°, 90°, 180° y 270°.
  - `RotatedPageSupportsOcrLayerTransformation`: Validación de soporte de rotación en `OcrTests`.
- **Resultado de compilación:** 0 errores de compilación (`dotnet build`).
- **Resultado de pruebas:** 118 pruebas unitarias pasando (`dotnet test`).
- **Smoke test:** `MYTHLIX_PHASE3_SMOKE_OK` ejecutado y validado.
