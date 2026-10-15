# Estado funcional

## Implementado

- Solución .NET 10 con Core, Infrastructure, Desktop y pruebas xUnit.
- Interfaz Avalonia inspirada en los adjuntos, pestañas y paneles redimensionables.
- Apertura de varios PDF, renderizado local bajo demanda y miniatura de página actual.
- Navegación, zoom, extracción/copiar texto y búsqueda por páginas.
- Rotación permanente, añadir página en blanco y eliminar página con confirmación.
- Deshacer/rehacer, indicador de cambios, confirmación de descarte.
- Guardar copia mediante temporal y preservar original.
- Manejo de errores, límites de archivo y procesos; rechazo conservador de PDF firmados.
- Scripts de publicación portable Windows y Linux.
- Fase 2: insertar texto, imágenes PNG/JPEG/BMP, rectángulos, elipses y líneas como contenido PDF.
- Selección en lienzo y lista, movimiento por arrastre, redimensionado y propiedades en puntos.
- Tipografía de texto nuevo (tamaño, negrita, cursiva), color y grosor de formas.
- Duplicación, eliminación y orden de objetos nuevos; historial y reapertura editable.
- Fuentes Liberation Sans incorporadas para Windows/Linux, con licencia OFL incluida.
- Fase 3 inicial: análisis y reemplazo auténtico de cadenas originales Tj/TJ simples con fuentes estándar Type 1 y codificación compatible.
- Conservación de posición y fuente, reducción de tamaño segura, validación de ancho y selección obsoleta.
- Interfaz renovada según los últimos adjuntos: menús funcionales, herramientas compactas, miniaturas cercanas reales, resaltado activo, inspector contextual y ajuste de página completa.

- Fase 4: OCR local Tesseract español/inglés, páginas y documentos completos, progreso y cancelación.
- Revisión y corrección de palabras con confianza y avisos inferiores al 70%, exportación de texto UTF-8 con saltos de línea.
- OCR por intervalos, cuatro modos de segmentación, revisión parcial conservada ante cancelación/fallo y validación previa de dimensiones PNG.
- Capa invisible buscable que conserva la imagen, aplicación transaccional, historial y sustitución sin duplicados.
- Reconstrucción en otro PDF con texto editable y advertencia explícita de pérdida de imágenes/formato.

- Fase 5 inicial: duplicar páginas, mover hacia atrás/delante, insertar contenido de otro PDF y extraer intervalos ordenados a un PDF nuevo desde Organizar. Historial para modificaciones; extracción mediante temporal.

- Fase 6 inicial: caché LRU compartida de PNG/texto (32 MiB), PDF temporal reutilizado por revisión, invalidación tras edición y liberación al cerrar.

- Organización: división automática por número de páginas, carpeta nueva, cancelación y publicación del conjunto completo sin sobrescribir destinos.

- Conversión inicial: páginas a PNG por intervalos y orden solicitado, lado mayor configurable (128–2400 px), progreso, cancelación y exportación del estado editado actual a una carpeta nueva.

- Conversión: crear PDF desde PNG/JPEG/BMP, una imagen por página en orden del selector, A4/Carta, margen, orientación automática, progreso y cancelación; apertura del resultado en pestaña nueva.

## Parcial

- Visor: miniaturas bajo demanda para páginas cercanas; sin marcadores, arrastrar/soltar archivos, atajos, ajuste al ancho ni vista doble. Selección de bloques editables en lienzo; copiar texto desde el inspector.
- MVVM: edición gráfica mediante comandos; diálogos de archivos y acciones generales de ventana conservan eventos.
- Historial por snapshots con coste proporcional al documento; no recuperación de sesión.
- Seguridad: límites básicos; sin aislamiento completo del parser ni validación de firmas.
- Tema oscuro; tema claro pendiente.
- Fase 2: edición solo de objetos añadidos con Mythlix; sin giro individual, opacidad, recorte, agrupación, relleno ni otros formatos de imagen. Páginas con recorte/origen desplazado o unidades especiales requieren soporte adicional.
- OCR: sin soporte de páginas rotadas/recortadas, regiones, orientación, deskew o cola de varios documentos; reconstrucción solo textual y revisión no persistente.
- Compatibilidad Windows estructural: código portable, pero requiere Poppler externo y verificación nativa.

## Pendiente

- Extender edición original a fuentes CID, subconjuntos/embebidas, más codificaciones, múltiples operadores y párrafos; cambiar familia, color o posición original. Modificar imágenes existentes.
- Anotaciones PDF estándar, más formatos de imagen y herramientas avanzadas de transformación.
- OCR avanzado: orientación, deskew, regiones, preprocesamiento explícito, cola de varios archivos y recuperación persistente.
- Formularios, cifrado, firmas criptográficas, redacción segura.
- Organización avanzada: arrastrar y soltar, selección múltiple visual, combinación por lotes. La inserción y extracción actuales no importan marcadores ni formularios de nivel documento.
- Conversión a DOCX/HTML/PDF-A, exportación de imágenes adicional a PNG e importación WebP/TIFF/SVG.
- Serilog, persistencia SQLite, extensiones, recuperación y autoguardado.
- Instaladores MSI/DEB/RPM/AppImage y pruebas visuales automatizadas.

No hay herramientas visuales que afirmen implementar estas funciones pendientes. Esta entrega cubre los fundamentos, edición gráfica, cobertura inicial de edición original y OCR local (fases 1–4), y organización inicial de páginas (fase 5); no equivale al producto completo del documento de requisitos.
