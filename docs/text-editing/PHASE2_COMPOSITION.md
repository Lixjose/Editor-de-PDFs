# Fase 2 — Composición compartida de estilos mixtos

## Estado y entregas

- **2A, implementado:** compositor métrico WinAnsi independiente de la UI, métricas de recursos reales y propuestas de composición inmutables.
- **2B, pendiente:** resolver la fuente física y sus índices/offsets de glifo, adaptador visual que consuma el plan sin recalcular los cortes y verificación visual.
- **2C, pendiente:** escritor que consuma el mismo plan, confirmación transaccional, revalidación de revisión/huellas/recursos y colisiones. La numeración no implica activación automática de escritura al completar 2A.

El motor y el editor existentes siguen funcionando. Esta entrega no añade botones, sustituye renderizado, habilita escritura mixta ni llama al compositor desde RenderAsync.

## Contratos y uso

`IRunMetricsProvider` amplía `IFontEncodingResolver`. `PdfDocumentSession.CreateRunMetricsProvider(page, revision)` devuelve una instantánea de los recursos nativos compatibles utilizados por la extracción de la fase 1. Los anchos declarados prevalecen sobre los estándares; ascenso/descenso proceden de las métricas de la fuente estándar admitida. No se consultan medidas de Avalonia ni se elige una sustitución por nombre de familia.

```csharp
var analysis = session.AnalyzeTextRuns(page, revision);
// El llamador debe seleccionar los fragmentos de UN párrafo verificado.
var draft = ParagraphRunBuffer.FromRuns(selectedRuns);
var metrics = session.CreateRunMetricsProvider(page, revision);
var options = new ParagraphCompositionOptions(bounds, firstBaselineY, lineHeightPt, TextAlignment.Justify);
var proposal = ParagraphComposer.Compose(draft, options, metrics);
```

`ParagraphCompositionSnapshot` conserva texto, contexto, inventario original completo, opciones, líneas, segmentos y diagnósticos. Cada segmento referencia su propietario inmutable, rango de texto local y rango global, bytes WinAnsi, avances efectivos y ajustes de justificación por carácter. No se modifica la procedencia para simular nuevos bytes originales.

Las posiciones y dimensiones están en puntos de página, desde la esquina superior izquierda. La línea base del segmento excluye Ts; su propietario conserva el desplazamiento. La escala horizontal es un factor (1 = 100 %). Los avances incluyen ancho nominal, Tc, Tw original y Tz, también el avance de Tc tras el último carácter. `AdvanceBounds` es una caja métrica de avance y altura tipográfica; no es una caja exacta de tinta ni comprueba colisiones con el contenido de la página.

## Comportamiento

- El corte considera palabras completas incluso cuando atraviesan cambios de estilo. No se divide una palabra por el mero cambio de fuente.
- Una palabra que excede el ancho conserva todos sus caracteres y genera `HorizontalOverflow`, sin comprimir fuentes ni dividirla arbitrariamente.
- Los espacios repetidos se conservan, incluidos los iniciales y finales. Pueden repartirse en líneas si su avance no cabe; no se recortan silenciosamente. NBSP no ofrece una oportunidad de corte.
- CR, LF y CRLF son saltos duros: se conservan con su rango y no se emiten como bytes de cadena PDF. Se conservan líneas vacías y la línea final tras un salto terminal.
- Izquierda/centro/derecha usan los mismos avances. La justificación solo amplía espacios ASCII internos en líneas con corte suave. No justifica la última línea de cada párrafo, ni espacios iniciales/finales. Conserva el Tw original y añade el extra ya escalado en puntos.
- Los segmentos siguientes comienzan tras la suma exacta de avances anteriores. Cambios de color, fuente y tamaño permanecen en el propietario de cada segmento.
- Los límites verticales consideran tamaños y Ts. Se informan `VerticalOverflow` y `LineOverlap` si el interlineado no separa las cajas métricas. Las líneas vacías usan el estilo explícito del buffer.
- Codificación no compatible, recurso/revisión ajenos o métricas no verificadas producen diagnósticos sin composición parcial. Avances no positivos se rechazan en este primer alcance.
- No se publica geometría infinita o NaN. Las propuestas con desbordamiento finito conservan sus líneas para diagnóstico, pero `IsValid` es falso.

## Límites y garantías pendientes

Esta es composición métrica de un borrador hacia un rectángulo objetivo explícito. **No es reconstrucción automática del aspecto original.** `ParagraphRunBuffer` no almacena las matrices de posición entre cadenas ni los ajustes originales de TJ. La integración futura deberá transportar y preservar esos datos, o rechazar su reinterpretación sin una política explícita de reflujo; no se debe aplicar esta propuesta como sustitución automática de cualquier selección nativa.

No se generan GID a partir de bytes PDF ni se afirma que Unicode, código WinAnsi y GID sean equivalentes. La fuente física, los contornos, el rasterizado, kerning/shaping avanzado, fuentes incrustadas/CID, ligaduras, tintas CMYK y fondos complejos requieren trabajo posterior. Los caracteres WinAnsi de esta entrega son un mapeo 1:1 validado, no una afirmación de soporte Unicode general.

`IsValid` solo describe la composición métrica y sus diagnósticos; no autoriza escribir. La confirmación requerirá comprobar el estado vivo de la sesión, las fuentes, huellas y revisión, márgenes/colisiones y aislamiento de recursos. También deberá convertir correctamente el extra por espacio a Tw/TJ, sin aplicar Tz dos veces, y preservar el fondo.

## Verificación

`ParagraphCompositionTests` cubre mezcla de estilos, avances frente al extractor, alineaciones, justificación, Tw/Tc/Tz/Ts, palabras que atraviesan estilos, espacios y NBSP, CR/LF/CRLF y líneas vacías, desbordamientos, interlineado, fuentes/revisiones ajenas, codificación imposible y overflow numérico. La suite existente sigue protegiendo edición, historial, inspección y conservación del contenido.

No se afirma validación visual de glifos ni escritura del nuevo plan: esas comprobaciones pertenecen a 2B/2C.
