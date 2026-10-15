# Evaluación de gráficos distantes, máscara interior y metadatos

## 1. Desbloqueo espacial

El mapa de ocupación visual propuesto es útil como señal de rechazo, pero no demuestra ausencia de gráficos: las cajas de texto enmascaradas pueden ocultar imágenes o trazados y un gráfico del color del fondo puede ser invisible en el bitmap. La resolución y el antialiasing también impiden afirmar «cero corrupción» a partir de una tolerancia de píxeles. No se autoriza el reflujo únicamente con esa prueba.

Se implementó una alternativa geométrica conservadora. Se localizan rectángulos y caminos rectos sin transformaciones, respetando q/Q, ancho de línea y una expansión conservadora por el límite de inglete. Se permite el reflujo si esos gráficos quedan fuera del corredor. El corredor incluye la caja del párrafo y el ancho de las filas inferiores que se moverán, hasta el margen inferior; así una fila más ancha no pasa por un gráfico lateral.

Los objetos de la capa Mythlix se comprueban con sus límites conocidos. CTM generales, XObjects/Do, imágenes, sombreado, recortes, curvas y estados no interpretados siguen bloqueando la operación. Por tanto una línea divisoria distante puede dejar de bloquear; un logotipo arbitrario con CTM/XObjects todavía puede bloquear. No se añadieron excepciones basadas solo en aspecto visual.

Pruebas: línea de encabezado distante, rectángulo lateral distante, trazo dentro del corredor, trazo grueso cuyo inglete alcanza el corredor y XObject de posición desconocida; los rechazos restauran la página.

## 2. Interior de la máscara

Después de muestrear el perímetro uniforme se evalúan todos los píxeles interiores. Se proyecta cada RGB sobre el segmento fondo–texto y se admite una desviación de hasta 10 niveles por canal. Más del 2% de píxeles inesperados, o una proporción suficiente de píxeles no opacos, impide abrir el editor sobre una máscara sólida; se mantiene la edición desde el panel. El cálculo no asigna arrays por píxel.

El analizador ahora obtiene el color de relleno nativo de rg/g/k en orden, antes del operador que muestra el texto. La conversión CMYK es aproximada; no se incorpora gestión de perfiles de color.

Esto es una heurística adicional, no una certificación: una marca gris detrás de texto negro puede pertenecer al mismo segmento RGB, y detalles de otro color inferiores al umbral pueden pasar. Por ello no se reutiliza esta prueba para desbloquear gráficos desconocidos.

## 3. Metadatos

Se escribe por línea `%MythlixLayout W=… L=… X=… H=…`, con una huella SHA-256 truncada a 16 caracteres de texto, fuente y tamaño. Solo se usan el ancho y el interlineado si X coincide dentro de 0.01 pt y la huella coincide, además de validar valores finitos y límites de página. El formato antiguo sin coordenada/huella ya no se considera verificado.

Se conserva la escritura en streams crudos, sin reserializar un AST de contenido que pudiera descartar comentarios. Las pruebas comprimen explícitamente el stream con zlib y `/FlateDecode`, lo reabren y verifican W/L. También simulan cambios externos de X o del texto y comprueban que se ignore el marco anterior. Una modificación externa solo de Y no invalida la huella; no se presenta como detección de cualquier edición posible.

## 4. Tw

Se conserva el comportamiento existente recomendado: Tw constante al guardar líneas aisladas y recálculo en párrafos justificados. No se inyectan espacios artificiales, LetterSpacing global ni funciones de fuente. Las cifras de cobertura del 98%/95% del texto recibido no se usan como resultados medidos del proyecto.

## Validación

Compilación Desktop correcta; 147 pruebas aprobadas. Se excluyeron las tres pruebas que requieren Tesseract ausente en el entorno. No se realizó comprobación visual interactiva del editor en esta sesión.
