# Exportación local de páginas como PNG

En **Convertir → Exportar páginas como PNG…**, indique páginas e intervalos (`3,1-2`) y el tamaño del lado mayor en píxeles (128–2400). Se mantiene la proporción de cada página y se respeta su rotación. El tamaño se expresa en píxeles, no en DPI.

Elija una carpeta contenedora. Mythlix crea una subcarpeta nueva y archivos como `0001-pagina-0003.png`: el prefijo conserva el orden solicitado y el resto identifica la página de origen. Se exporta el estado actual del documento, incluidas ediciones sin guardar. El PDF abierto no se modifica y no se reemplazan destinos existentes.

La exportación usa Poppler local, una página por vez, sin llenar la caché del visor. El progreso indica páginas procesadas. Cancelar elimina la carpeta temporal; la carpeta final solo se publica cuando se completa todo el conjunto. Un fallo de renderizado o escritura también limpia los resultados parciales. No requiere conexión.

## Alcance y límites

PNG es una representación rasterizada: el texto deja de ser seleccionable dentro de la imagen. La operación exporta la apariencia completa de la página; no extrae los archivos de imagen originales del PDF. No genera JPEG, TIFF, DOCX, HTML o PDF/A. El límite de 2400 píxeles corresponde al renderizador actual y no es una exportación para impresión de gran formato. Un cierre abrupto del proceso puede dejar una carpeta temporal `.mythlix-images-*` en el destino.

## Verificación

Las pruebas cubren orden, nombres, tamaño y selección inválidos, destinos existentes, fallo de renderizado, cancelación después de una imagen y un PNG real con rotación y proporción verificadas mediante decodificación.

Comprobación integrada de exportación tras una edición sin guardar y deshacer:

```bash
dotnet run --project src/Mythlix.Desktop --no-build -- --images-smoke references/Mythlix-Documento-Tecnico.pdf
```

Los diálogos y el botón de cancelación no tienen automatización de clics. El motor de cancelación sí tiene prueba determinista. Windows sigue pendiente de verificación nativa.
