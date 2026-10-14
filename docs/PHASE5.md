# Fase 5 — Organización inicial de páginas

En el menú **Organizar**:

- **Duplicar página** crea una copia independiente después de la página actual y la selecciona.
- **Mover página hacia atrás/delante** cambia su posición y sigue la página desplazada.
- **Insertar PDF** inserta todas sus páginas después de la actual. Permite combinar contenido de documentos sucesivamente. Aplica el límite de tamaño y el rechazo conservador de firmas del motor actual.
- **Extraer páginas** acepta listas e intervalos, por ejemplo `3,1-2`. Conserva el orden solicitado, rechaza páginas repetidas o inexistentes y escribe un PDF nuevo mediante un temporal. No elimina páginas del documento abierto. Para dividir manualmente, repetir con intervalos distintos.

Las modificaciones se integran en deshacer/rehacer y en el indicador de cambios. Los resultados de revisión OCR se invalidan al modificar la estructura para evitar aplicarlos a una página distinta. El archivo original abierto se conserva.

## Límites

Se trabaja con contenido y recursos de las páginas. No se promete trasladar campos AcroForm, marcadores, destinos internos ni estructura de nivel documento entre archivos. No equivale a combinar formularios. Los documentos firmados siguen bloqueados y no se crean firmas digitales.

La reordenación mediante arrastre, selección múltiple visual, división automática, formularios, conversiones, cifrado y redacción segura siguen pendientes. La fase 6 tampoco se considera completada. Las acciones nuevas están compiladas, pero sus diálogos no tienen verificación visual automatizada.
