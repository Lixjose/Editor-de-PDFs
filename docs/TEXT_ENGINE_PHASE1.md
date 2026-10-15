# Fase 1 — Motor de Edición de Texto con Reflujo y Formato Enriquecido

## Resumen de Capacidades Implementadas

Esta entrega implementa el motor de texto solicitado en el Punto 1, superando las limitaciones de reemplazo rígido de cadena única previa e incorporando:

1. **Reflujo de Párrafos (*Paragraph Reflow & Word-Wrapping*)**:
   - `ParagraphReflow` mide cada palabra con las métricas de la fuente (`widths[b] * fontSize / 1000`).
   - Ajusta automáticamente el texto largo al ancho del párrafo (`maxWidth`) generando saltos de línea suaves.
   - Soporta saltos de línea explícitos (`\n`, `\r\n`).
   - Calcula el interlineado (*leading* tipográfico proporcional) y la posición exacta de cada línea mediante operadores de matriz de texto `Tm`.

2. **Detección y Sustitución Criteriosa de Fuentes (*Font Substitution Service*)**:
   - Clasificación semántica de familias: Sans-Serif (Helvetica / Arial / Liberation Sans), Serif (Times-Roman / Georgia) y Monospaced (Courier / Consolas).
   - Conmutación automática de variantes tipográficas:
     - Negrita: `-Bold`
     - Cursiva / Oblicua: `-Italic` / `-Oblique`
     - Negrita + Cursiva: `-BoldItalic` / `-BoldOblique`
   - Registro dinámico y seguro de nuevos recursos tipográficos en `/Resources /Font` de la página sin mutar páginas compartidas.

3. **Formato Completo de Texto**:
   - **Tamaño**: permite escalar fuentes tanto hacia abajo como hacia arriba (4 a 144 pt). El reflujo recalcula la geometría y distribución vertical.
   - **Color**: detección y aplicación del operador `r g b rg` en streams de texto desde códigos `#RRGGBB`.
   - **Alineación**: soporte para `Left`, `Center`, `Right` y `Justify`, calculando los desplazamientos horizontales `XOffset` de cada línea.

4. **Cuadros de Texto Nuevos (T+) Enriquecidos**:
   - Soporte de alineación (`Alignment`) y familia tipográfica (`FontFamily`) en `GraphicObject` y `GraphicLayerWriter` con `XTextFormatter.Alignment`.

5. **Compatibilidad y Preservación**:
   - La API mantiene compatibilidad regresiva estricta con llamadas anteriores.
   - 100% de las pruebas unitarias y smoke tests preexistentes continúan pasando.

## Verificación

```bash
dotnet test Mythlix.sln -m:1 --filter "FullyQualifiedName~TextReflowEngineTests"
dotnet run --project src/Mythlix.Desktop --no-build -- --phase3-smoke references/Mythlix-Documento-Tecnico.pdf
```
