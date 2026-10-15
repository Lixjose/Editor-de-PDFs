# Crear PDF desde imágenes

Use **Convertir → Crear PDF desde imágenes…**, incluso sin un PDF abierto. Seleccione archivos locales PNG, JPEG o BMP. El diálogo muestra el orden recibido del selector: cada imagen produce una página. No hay reordenación manual en ese diálogo; puede reorganizar las páginas después con Organizar.

Elija A4 o Carta y un margen de 0 a 100 puntos (24 por defecto). Las imágenes apaisadas producen páginas horizontales; el resto, verticales. Cada imagen se centra y ajusta proporcionalmente al espacio disponible, sin recorte. No se usan los DPI de la imagen para decidir el tamaño de papel ni se aplica orientación EXIF automática.

Guarde con un nombre nuevo. Los destinos existentes se rechazan para preservar archivos. La conversión muestra progreso por página y permite cancelar. Solo publica el PDF cuando termina, mediante un archivo temporal junto al destino y renombrado sin sobrescritura. Al finalizar se abre en una pestaña nueva, conservando las pestañas existentes.

## Límites

- Entre 1 y 100 imágenes; cada archivo debe ocupar como máximo 10 MiB y tener como máximo 25 megapíxeles. La comprobación de dimensiones ocurre al cargar la imagen con PDFsharp, no constituye una protección previa a la decodificación.
- Se procesa una imagen por vez, pero el PDF en construcción conserva sus recursos; no se garantiza un presupuesto total de memoria para lotes grandes.
- No se añade OCR: el resultado muestra las imágenes y no crea texto seleccionable. Puede aplicar OCR después.
- No admite aún WebP, TIFF o SVG, tamaño de papel personalizado, edición de imágenes originales ni opciones de compresión/calidad. La codificación de los recursos queda a cargo de PDFsharp; no se promete identidad binaria de la imagen incrustada.
- Cancelación o error elimina el temporal durante la ejecución normal. Un cierre abrupto puede dejar un archivo `.tmp`.
- Windows y los clics de los diálogos siguen pendientes de verificación.

Comprobación integrada en ventana:

```bash
dotnet run --project src/Mythlix.Desktop --no-build -- --images-to-pdf-smoke references/Mythlix-Documento-Tecnico.pdf
```
