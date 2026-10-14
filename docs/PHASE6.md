# Fase 6 — Optimización inicial

El visor reutiliza el PDF temporal mientras no cambie la revisión del documento. Navegar, buscar o volver a una pestaña no serializa de nuevo el documento completo. OCR comparte esa preparación; sus imágenes de alta resolución se procesan fuera de la caché.

`CachedPdfRenderer` conserva PNG y texto extraído en una caché LRU compartida por el espacio de trabajo, limitada a 32 MiB. La clave distingue documento, página y resolución. Los resultados mayores que el presupuesto se devuelven sin conservarse. Las imágenes se devuelven como copias para proteger los bytes almacenados.

Editar contenido, organizar páginas, aplicar OCR y deshacer/rehacer cambian la revisión: antes de regenerar el temporal se invalidan todas sus entradas y miniaturas. Cerrar una pestaña elimina sus entradas; cerrar el espacio de trabajo vacía la caché. Un resultado pendiente anterior a una invalidación no puede reintroducirse en la caché.

## Verificación y límites

Cinco pruebas nuevas verifican reutilización, aislamiento de bytes, orden LRU entre documentos, presupuesto, invalidación por archivo y resolución, cancelación y resultados pendientes. El límite corresponde a los datos almacenados; no representa un límite del consumo total del proceso ni de imágenes decodificadas o buffers transitorios. No se ha medido una aceleración estadística en documentos extensos.

Esta es una entrega inicial de la fase 6. Siguen pendientes renderizado progresivo, optimización del historial, recuperación de sesión, instaladores y validación nativa en Windows y otras distribuciones. Las funciones avanzadas pendientes de la fase 5 siguen registradas en STATUS.md.
