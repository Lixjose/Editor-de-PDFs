# Fase 3 — Edición del texto original e interfaz

Esta entrega incorpora una primera edición auténtica del contenido original. El editor detecta bloques compatibles y sustituye los bytes de su operador de texto dentro del stream de la página. No inserta un cuadro de texto nuevo, no tapa el original ni rasteriza el PDF para guardarlo.

## Uso

1. Abra un PDF. Puede probar `references/Mythlix-Documento-Tecnico.pdf`, un documento real de tres páginas incluido para verificación.
2. Seleccione un bloque en el lienzo o en **Texto original** del inspector. **T Original** selecciona el primer bloque compatible.
3. Edite su contenido y pulse **Aplicar al texto original**. La fuente y la posición se conservan.
4. Si el nuevo texto no cabe, acórtelo o reduzca el tamaño. Nunca se ensancha silenciosamente un párrafo ni se desplaza texto vecino.
5. El tamaño solo puede reducirse respecto al bloque actual. Se bloquea si otros elementos pueden heredar el estado de fuente.
6. Use deshacer/rehacer y guarde una copia. La edición permanece al reabrir el PDF.

**Eliminar texto original** vacía el operando de texto seleccionado. No constituye redacción segura ni verifica todas las copias posibles del contenido, metadata o historial.

## Cobertura de edición directa

- Fuentes Type 1 estándar: las variantes de Helvetica, Courier y Times, sin programas de fuentes embebidos ni ToUnicode.
- WinAnsi permite acentos y caracteres representables con métricas conocidas. StandardEncoding/default admite el subconjunto ASCII no ambiguo; se excluyen comillas cuyo mapeo difiere de ASCII.
- Un solo `Tj` o `TJ` con una única cadena por bloque `BT/ET`. Cadenas literales con escapes/octales y cadenas hexadecimales.
- Fuente explícita `Tf` y posición explícita `Td/TD` o `Tm` sin escala, giro ni sesgo interno. La rotación de la página sigue siendo compatible.
- Espaciado, escala horizontal, elevación y modo de renderizado de texto por defecto.
- Streams comprimidos o sin comprimir. Se vuelve a analizar después de guardar.
- Tamaño de fuente 4–144 pt. El texto debe permanecer dentro del ancho original.

Se mantiene como solo lectura, sin simular edición:

- Fuentes CID/Type 0, TrueType, fuentes de subconjuntos y fuentes embebidas.
- Codificaciones personalizadas/Differences/ToUnicode y caracteres sin un mapeo seguro.
- Bloques con varios operadores de texto, kerning numérico en `TJ`, párrafos fragmentados o texto de varias líneas en un mismo bloque.
- Transformaciones `cm`, recortes, estados gráficos `gs`, marcado BMC/BDC, cambios de espaciado/renderizado y contenido de imágenes inline que no pueda analizarse con seguridad.
- Texto dentro de Form XObjects; no se recorre su contenido en esta entrega.
- Páginas recortadas, con origen desplazado o unidades especiales, conforme a los límites de geometría de fase 2.

Si aparece un XObject o texto sin fuente explícita, el reemplazo compatible puede seguir funcionando, pero se bloquea el cambio de tamaño para no afectar elementos que heredan el estado de fuente. El inspector refleja ese bloqueo.

Esta es la cobertura inicial de la fase 3, no un editor universal de párrafos PDF. Cambiar la familia o el color del texto original, moverlo, reconstruir párrafos y editar fuentes de subconjuntos requieren continuar el motor.

## Preservación y límites

El lexer conserva offsets de bytes. Solo se sustituye la cadena seleccionada y, cuando corresponde, el tamaño en `Tf`; el resto del stream se mantiene byte a byte descomprimido. El stream afectado se copia antes de modificarlo, para no cambiar páginas que comparten ese objeto.

Un SHA-256 del stream impide aplicar una selección obsoleta. El guardado general de PDFsharp puede reorganizar objetos y compresión, por lo que la copia no promete identidad binaria con el original. Deshacer/rehacer usa los snapshots de la sesión.

Límites de análisis: 8 MB por stream descomprimido, 16 MB de contenido analizado por página y 100 000 tokens por stream. Estos límites se comprueban después de descomprimir; no sustituyen el aislamiento de un parser nativo ni ofrecen protección completa contra documentos maliciosos.

Las métricas de fuentes estándar provienen de tablas de ReportLab. Se incluye su aviso BSD en `src/Mythlix.Infrastructure/TextEditing/REPORTLAB-LICENSE.txt`, copiado con la aplicación. No se añadió una dependencia de ejecución de ReportLab ni se ejecuta Python al usar Mythlix.

## Interfaz basada en los adjuntos

- Menús Archivo, Editar, Insertar y Organizar con acciones implementadas.
- Barra de herramientas compacta, guardar copia y deshacer/rehacer.
- Paleta obsidiana, acento índigo, coordenadas cian, separadores finos e inspector compacto.
- Miniaturas reales en el panel izquierdo y resaltado de página activa.
- Carga de miniaturas cercanas a la página actual; máximo cinco imágenes de miniaturas retenidas. Las páginas lejanas muestran su número hasta seleccionarlas.
- Lienzo sobre fondo de puntos, paneles redimensionables y ajuste de página completa automático.
- Separación visible entre texto original y objetos añadidos; tipografía original, texto, tamaño y geometría contextual.
- Zoom y estado del documento en la barra inferior.

Los menús OCR o herramientas de seguridad todavía no se presentan como acciones disponibles. La apariencia del documento abierto se conserva: el tema oscuro no recolorea sus páginas.

En Linux se priorizan los ejecutables Poppler de `/usr/bin` si están instalados. `MYTHLIX_POPPLER_BIN` conserva prioridad para elegir una instalación explícita y PATH sigue siendo la alternativa. Esta decisión corrige los problemas de sustitución/espaciado de fuentes observados en el Poppler incluido en el entorno de ejecución.

## Verificar

```bash
dotnet build Mythlix.sln -m:1
dotnet test Mythlix.sln -m:1
dotnet run --project src/Mythlix.Desktop --no-build -- \
  --phase3-smoke references/Mythlix-Documento-Tecnico.pdf
```

La comprobación de ventana usa el PDF de verificación: reemplaza texto original, prueba deshacer/rehacer, guarda y reabre `artifacts/phase3-edited.pdf`, y genera `artifacts/phase3-preview.png`. Cierra la ventana al terminar. No use este modo diagnóstico con documentos de trabajo.

Referencia primaria de operadores y estado de texto: [PDF Association — Operators & Operands](https://pdfa.org/download-area/cheat-sheets/OperatorsAndOperands.pdf). Código y licencia de métricas: [ReportLab](https://www.reportlab.com/opensource/).
