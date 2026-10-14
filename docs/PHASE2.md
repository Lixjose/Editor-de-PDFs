# Fase 2 — Edición gráfica

## Qué permite esta entrega

- Añadir cuadros de texto multilínea con Liberation Sans incorporada al PDF, tamaño 4–144 pt, negrita/cursiva y color hexadecimal.
- Insertar PNG, JPEG y BMP. La inserción mantiene la proporción inicial; el redimensionado posterior permite dimensiones independientes.
- Añadir rectángulos, elipses y líneas diagonales con color y grosor de trazo.
- Seleccionar objetos en el lienzo o en la lista del inspector.
- Mover mediante arrastre; redimensionar mediante el tirador inferior derecho. Durante el gesto se muestra la caja propuesta; el PDF se actualiza al soltar.
- Ajustar posición X/Y y ancho/alto en puntos desde el inspector y confirmar con **Aplicar propiedades**.
- Modificar el texto nuevo, duplicar y eliminar objetos añadidos, adelantar/atrasar su orden entre los objetos nuevos.
- Deshacer/rehacer las operaciones gráficas mediante el historial del documento.
- Guardar una copia y reabrirla conservando los objetos editables en Mythlix.

Los objetos se incorporan como **contenido PDF de página**, no como anotaciones. El texto nuevo es extraíble y seleccionable en otros lectores PDF. No se rasteriza el documento para guardarlo. Estas funciones actúan sobre contenido añadido con Mythlix; no editan texto ni imágenes del PDF original.

## Uso

1. Abra un PDF y navegue hasta la página deseada.
2. Use **Texto**, **Imagen**, **Rectángulo**, **Elipse** o **Línea** en la segunda barra.
3. Seleccione el objeto; arrástrelo o use sus propiedades en la pestaña **Objetos** del inspector.
4. Cambie el texto, el color, las dimensiones o la tipografía y pulse **Aplicar propiedades**.
5. Use **Duplicar**, **Eliminar objeto**, **Adelante/Atrás**, o las acciones generales de deshacer/rehacer.
6. Guarde una copia. Al abrirla otra vez, la lista reconoce los objetos añadidos. El orden de la lista coincide con el orden de pintura: el último está delante.

Coordenadas del inspector: puntos PDF, desde la esquina superior izquierda de la página **sin rotar**. El lienzo transforma la caja de selección para páginas con rotación de 90, 180 y 270 grados. Los cambios de geometría deben mantener el objeto dentro de la página; las entradas inválidas producen un mensaje y revierten la operación.

## Implementación

`GraphicObject`, `ObjectBounds` y `PageGeometry` están en Core. Definen objetos inmutables, límites y transformaciones entre coordenadas PDF y vista rotada.

`GraphicLayerWriter` mantiene un stream de contenido propio marcado con `/MythlixGraphicsV1`. Al modificar un objeto reemplaza ese stream, conservando los demás streams. `/MythlixObjectsV1` guarda los datos editables de la escena en la página; `/MythlixResourcesV1` registra los recursos que añadió esa escena. No se trata de una capa OCG con visibilidad controlable ni de una anotación PDF.

Los recursos de la página se copian antes de alterarlos para evitar cambios sobre diccionarios compartidos entre páginas. Al reconstruir la escena se retiran sus mapeos anteriores de fuentes e imágenes para evitar acumulación de recursos. PDFsharp puede ajustar internamente streams y estructura al guardar; no se promete identidad binaria de la copia. El original permanece intacto.

`EmbeddedFontResolver` incorpora cuatro variantes sin depender de fuentes instaladas en Windows o Linux. Liberation Sans conserva su licencia SIL Open Font License 1.1 en `src/Mythlix.Infrastructure/Fonts/OFL.txt`, que se copia con la aplicación. No se añadieron dependencias de producción para la fase 2.

`GraphicEditingViewModel` implementa comandos MVVM para inserción, propiedades, borrado, duplicación y orden. `PageCanvas` dibuja el PDF y las cajas de selección; convierte los gestos en operaciones del modelo. Diálogos de archivos y eventos generales de ventana siguen en la vista. La serialización, composición y lectura de escenas se ejecutan fuera del hilo UI.

## Límites explícitos

- Máximo 200 objetos por página; escena serializada hasta 32 MB.
- Imagen hasta 10 MB y 25 megapíxeles. No hay WebP, TIFF o SVG en esta fase.
- Páginas con CropBox distinto del MediaBox, origen desplazado o UserUnit distinto de 1 no permiten edición gráfica todavía; la aplicación informa de la limitación.
- No hay giro individual, recorte de imagen, opacidad, rellenos de formas, flechas/polígonos, agrupación ni alineación automática.
- El formato de escena es propio de Mythlix. Otros editores pueden eliminar su metadata; el contenido visual sigue siendo PDF estándar, pero la reapertura editable en Mythlix no está garantizada después de modificarlo con otra aplicación.
- Eliminar objetos nuevos no es una herramienta de redacción segura. La seguridad criptográfica y la eliminación verificable de información corresponden a fases futuras.
- El zoom y la liberación de bitmaps conservan las limitaciones de la fase 1. No hay caché LRU, autoguardado ni historial incremental.
- El motor no recupera objetos originales ni reconstruye párrafos: eso es el objetivo de la fase 3.

## Verificación reproducible

```bash
dotnet build Mythlix.sln -m:1
dotnet test Mythlix.sln -m:1
# En una sesión gráfica Linux con Poppler:
dotnet run --project src/Mythlix.Desktop --no-build -- \
  --phase2-smoke references/verification.pdf tests/Mythlix.Tests/Fixtures/image.png
```

La comprobación de ventana se activa solo con `--phase2-smoke`. Ejecuta los comandos de edición, valida la reapertura del resultado, genera `artifacts/phase2-preview.png` y cierra la aplicación. Utilice el PDF de verificación, no un documento de trabajo. El resultado de prueba se escribe en `/tmp/mythlix-phase2-smoke.pdf` en Linux.

Fuentes primarias consultadas:
- [Formatos de imagen de PDFsharp Core](https://docs.pdfsharp.net/PDFsharp/Topics/Bitmap-Images/Formats.html).
- [Resolución de fuentes PDFsharp](https://docs.pdfsharp.net/PDFsharp/Topics/Fonts/Font-Resolving.html).
- [Código de streams de PDFsharp 6.2.4](https://github.com/empira/PDFsharp/blob/v6.2.4/src/foundation/src/PDFsharp/src/PdfSharp/Pdf.Advanced/PdfContents.cs).
- [Controles dibujados de Avalonia](https://docs.avaloniaui.net/docs/custom-controls/custom-drawn-controls).
- [Eventos y captura de puntero de Avalonia](https://docs.avaloniaui.net/docs/input-interaction/pointer).
- [Licencia Liberation Fonts](https://github.com/liberationfonts/liberation-fonts/blob/main/LICENSE).
