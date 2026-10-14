# Fase 5 — Organización inicial de páginas

En el menú **Organizar**:

- **Duplicar página** crea una copia independiente después de la página actual y la selecciona.
- **Mover página hacia atrás/delante** cambia su posición y sigue la página desplazada.
- **Insertar PDF** inserta todas sus páginas después de la actual. Permite combinar contenido de documentos sucesivamente. Aplica el límite de tamaño y el rechazo conservador de firmas del motor actual.
- **Dividir PDF en partes** exporta grupos consecutivos de un número configurable de páginas. La última parte puede ser menor. Elija una carpeta contenedora: se crea una subcarpeta nueva con nombres que indican el número de parte y el intervalo. Incluye cancelación y publicación del conjunto mediante renombrado de carpeta; un fallo o cancelación limpia las partes temporales. No modifica el documento abierto ni reemplaza carpetas existentes.
- **Extraer páginas** acepta listas e intervalos, por ejemplo `3,1-2`. Conserva el orden solicitado, rechaza páginas repetidas o inexistentes y escribe un PDF nuevo mediante un temporal. No elimina páginas del documento abierto. Para dividir manualmente, repetir con intervalos distintos.

Las modificaciones se integran en deshacer/rehacer y en el indicador de cambios. Los resultados de revisión OCR se invalidan al modificar la estructura para evitar aplicarlos a una página distinta. El archivo original abierto se conserva.

## Límites

Se trabaja con contenido y recursos de las páginas. No se promete trasladar campos AcroForm, marcadores, destinos internos ni estructura de nivel documento entre archivos. No equivale a combinar formularios. Los documentos firmados siguen bloqueados y no se crean firmas digitales.

La reordenación mediante arrastre, selección múltiple visual, formularios, conversiones, cifrado y redacción segura siguen pendientes. La fase 6 tampoco se considera completada. La división tiene pruebas automatizadas del motor y comprobación integrada en ventana; sus diálogos y los de extracción/inserción todavía no tienen automatización de interacción.
