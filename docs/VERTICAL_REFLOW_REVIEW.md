# Evaluación y aplicación de la segunda propuesta de reflujo

## Desplazamiento vertical

Se calcula el número de líneas nuevas con el mismo motor y métricas usados al escribir. La diferencia de altura es `(líneas nuevas − líneas anteriores) × interlineado`, con al menos una línea de marco al vaciar un párrafo. Se conservan el ancho y el interlineado en comentarios propios `%MythlixLayout` dentro de los bloques escritos para que contraer y volver a editar no reduzca el marco a la longitud del texto.

La selección de bloques inferiores usa coordenadas desde arriba: `fila.Y >= párrafo.Bottom`, y solapamiento de al menos 60% del ancho del párrafo. El signo Y sugerido en el documento corresponde a coordenadas PDF desde abajo; el modelo interno tiene el eje invertido. Se amplían las filas candidatas para mantener juntas las colas cortas de los párrafos posteriores. Las columnas laterales se filtran antes de agrupar para que una línea a igual altura no interrumpa el agrupamiento de otra columna.

El desplazamiento cambia directamente el operando Y de Tm/Td/TD, sin regenerar las palabras, fuente, color, arrays o espaciados de las filas desplazadas. Se valida todo antes de escribir y se restaura la página ante errores. El reemplazo y todos los desplazamientos forman una sola operación de deshacer.

Se bloquea con mensaje de desbordamiento si hay texto nativo no compatible, geometría no editable, rotación, gráficos añadidos, operadores de trazados/imágenes/sombreado, colisiones con filas fijas o invasión del margen inferior de 36 pt. La inspección espacial se refinó para caminos rectos y rectángulos sin transformaciones, y objetos Mythlix con límites conocidos; los restantes gráficos siguen bloqueando la operación. Véase SPATIAL_GUARD_REVIEW.md. Poppler en este proyecto extrae texto, no un inventario espacial completo de trazados.

Si se bloquea una edición directa, el editor conserva el texto escrito para corregirlo. No se generan páginas nuevas ni se modifica una columna lateral para acomodar el desbordamiento.

## Dos líneas justificadas

Se admite la señal explícita `Tw > 0.05` en la primera línea y la coincidencia de márgenes izquierdos de dos líneas compatibles. También se permite heredar la alineación de un párrafo geométricamente confirmado de al menos tres líneas con el mismo margen izquierdo y derecho (tolerancia 1.5 pt). Sigue siendo una heurística; un Tw positivo no demuestra por sí solo la intención editorial. No se habilita un analizador general de arrays TJ/CID que el motor nativo todavía no interpreta.

## Fondos de edición

Se copia el bitmap ya renderizado a un buffer BGRA conocido y se muestrea un anillo exterior de dos píxeles. Se usa la moda cuando al menos 95% de las muestras están a seis niveles RGB o menos y todas son opacas. El color se comparte entre máscara y TextBox. Es un trabajo lineal pequeño sobre el bitmap, no literalmente un coste cero ni una garantía de cubrir 95% de los documentos.

Si no se identifica un fondo uniforme se mantiene la selección y se informa que debe editarse desde el panel. No se abre una caja con una máscara blanca sobre degradados o imágenes. No se aplica el inpainting propuesto: puede confundir dibujos, caracteres claros y marcas de agua con glifos y fabricar un fondo falso. El muestreo se complementa con una comprobación interior contra el segmento RGB fondo–texto; ambas pruebas son aproximadas. Véase SPATIAL_GUARD_REVIEW.md.

## Espaciado Tw

En edición aislada se conserva el Tw original constante al guardar. En un párrafo justificado se recalcula por línea y se usa cero para la última. No se distribuye Tw por todos los caracteres mediante LetterSpacing. El TextBox mantiene el ancho del marco y la vista previa de párrafos usa la justificación de Avalonia; una línea aislada con Tw arbitrario todavía no tiene una reproducción exacta por palabra. Forzar solo el ancho del control no cambia el avance de sus espacios, por lo que no se presenta esa opción como una corrección visual completa.

La interfaz existente selecciona con un clic y abre la edición con doble clic. No se cambió a edición con un clic por la suposición del documento.

Referencia de la limitación del presentador: [código de TextPresenter de Avalonia 12.1.1](https://github.com/AvaloniaUI/Avalonia/blob/12.1.1/src/Avalonia.Controls/Presenters/TextPresenter.cs).

Validación: 139 pruebas aprobadas (se excluyeron las tres dependientes de Tesseract ausente), incluyendo expansión/contracción, columnas laterales, colas cortas, ediciones sucesivas, rechazo atómico por gráficos/desbordamiento, justificación de dos líneas y clasificación de fondos. Compilación Desktop correcta. No se realizó comprobación visual interactiva de la máscara o del editor en esta sesión.
