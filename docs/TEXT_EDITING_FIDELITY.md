# Fidelidad de la edición de texto

## Diagnóstico

La edición nativa analiza operadores PDF compatibles; el modo vectorial usa la extracción XML de Poppler cuando esos operadores no se pueden editar directamente. Son rutas distintas: el modo nativo puede reconstruir párrafos compatibles; el vectorial añade una máscara local y texto en la capa gráfica.

Problemas encontrados:

- El TextBox de edición crecía hasta el ancho del contenido medido, incluso para una sola línea.
- El dibujo de una cadena con DrawString no impone el ancho del rectángulo. El texto vectorial podía salir de la caja original.
- La vista imponía Liberation Sans a familias no reconocidas mientras la exportación intentaba usar la familia detectada.
- La extracción unía fragmentos adyacentes por familia, ignorando diferencias de tamaño, color, negrita y cursiva.
- La extracción y su procesamiento redondeaban geometría y tamaños.
- El reflujo nativo imponía un mínimo de 100 puntos de ancho aunque la línea original fuera más estrecha.

## Cambios

El editor conserva el ancho de la caja. Vista y exportación normalizan las familias mediante la misma función, conservando nombres de familias personalizadas. Poppler recibe `-nomerge -noroundcoord` y el tamaño XML mantiene su precisión. Los fragmentos de estilos distintos permanecen separados cuando Poppler proporciona elementos independientes.

Las líneas vectoriales calibran una escala horizontal fija mediante el ancho original y la medida de la fuente de exportación (limitada a 0.75–1.25 para evitar interpretar separaciones de columnas como estiramiento). La escala se aplica en la vista y se persiste en la capa gráfica. No cambia al escribir y no reduce automáticamente el tamaño de fuente.

Las sustituciones vectoriales conservan su ancho original. Si el texto nuevo lo excede o contiene saltos de línea, la operación se revierte con un aviso. El editor directo vuelve a abrirse con el texto introducido para permitir corregirlo. Los objetos gráficos creados libremente conservan su comportamiento anterior; los que proceden de una sustitución original mantienen la restricción incluso al reabrir el PDF.

## Límites pendientes

- No se reutilizan de forma general fuentes incrustadas, subconjuntos ni codificaciones complejas del PDF. Una fuente no disponible puede requerir sustitución.
- La línea base vectorial sigue siendo estimada a partir de la caja XML; no equivale a recuperar la matriz de texto original.
- No hay reflujo de párrafos vectoriales con estilos mixtos. Se impide el desbordamiento; para texto más largo hay que acortar o ajustar explícitamente el tamaño.
- Si Poppler devuelve varios estilos dentro de un único elemento XML, no se dispone de cajas individuales para reconstruirlos con exactitud.
- Las máscaras del modo vectorial no eliminan necesariamente el texto subyacente. Este puede permanecer en búsquedas y al copiar, como se documentó en la corrección previa.

## Verificación

Las pruebas comprueban separación de estilos y tamaños decimales, calibración con la fuente de exportación, persistencia de escala y restricción, reversión de desbordamientos y límites de tinta en el PDF renderizado. Se ejecuta también la suite existente para comprobar edición, historial, capas y conservación de líneas vecinas. No se ha validado el PDF de las capturas: solo se proporcionaron imágenes.
