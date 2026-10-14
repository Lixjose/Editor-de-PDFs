# PROYECTO: Editor PDF Profesional Multiplataforma

## 1. Objetivo principal

Actúa como un equipo de ingeniería de software especializado en aplicaciones de escritorio, procesamiento de documentos PDF, interfaces gráficas modernas y arquitectura de software.

Tu objetivo es diseñar y desarrollar una aplicación profesional para **Windows y Linux** que permita visualizar, crear, editar, modificar, convertir, organizar y proteger documentos PDF.

El programa debe funcionar como una alternativa avanzada a Adobe Acrobat Pro, Foxit PDF Editor y PDF-XChange Editor, priorizando:

- Edición real de documentos PDF existentes.
- Conservación de la estructura y apariencia originales.
- Interfaz gráfica moderna, intuitiva y minimalista.
- Alto rendimiento y bajo consumo de recursos.
- Funcionamiento local, incluso sin conexión a Internet.
- Arquitectura modular y escalable.
- Compatibilidad completa entre Windows y Linux.
- Capacidad de trabajar con documentos grandes y escaneados.

**Importante:** No desarrollar únicamente un visor PDF con herramientas de anotación. La aplicación debe permitir modificar los elementos internos de un documento PDF siempre que su estructura lo permita.

---

## 2. Tecnologías y arquitectura

Utilizar preferentemente:

- Lenguaje principal: C#.
- Framework: .NET 10.
- Interfaz gráfica: Avalonia UI.
- Patrón de arquitectura: MVVM.
- Renderizado: SkiaSharp o un motor PDF compatible.
- Procesamiento PDF: evaluar PDFium, MuPDF, PDFsharp, iText y otras alternativas.
- Reconocimiento óptico de caracteres: Tesseract OCR.
- Procesamiento de imágenes: ImageSharp o SkiaSharp.
- Persistencia local: SQLite cuando sea necesario.
- Registro de eventos: Serilog.
- Pruebas: xUnit.

Antes de seleccionar bibliotecas, comprobar sus capacidades reales de lectura, edición, representación y escritura de PDF, además de sus licencias.

No asumir que una biblioteca capaz de visualizar documentos también puede editar su contenido original.

Separar el sistema en módulos:

1. Aplicación de escritorio.
2. Motor de documentos PDF.
3. Motor de renderizado.
4. Motor de edición de contenido.
5. Procesamiento OCR.
6. Gestión de imágenes.
7. Formularios y anotaciones.
8. Conversión y exportación.
9. Seguridad documental.
10. Persistencia y recuperación.
11. Gestión de extensiones.

La arquitectura debe permitir reemplazar el motor PDF sin modificar toda la aplicación.

---

## 3. Interfaz de usuario

Diseñar una interfaz inspirada en herramientas modernas de productividad.

### Distribución general

**Barra superior:**
- Archivo.
- Inicio.
- Editar.
- Insertar.
- Organizar.
- Convertir.
- Formularios.
- Herramientas.
- Configuración.

**Panel izquierdo:**
- Miniaturas de páginas.
- Marcadores.
- Capas y elementos.
- Estructura del documento.
- Resultados de búsqueda.

**Área central:**
- Visualizador PDF.
- Edición directamente sobre las páginas.
- Zoom y desplazamiento.
- Selección de elementos.
- Guías y reglas opcionales.

**Panel derecho:**
- Propiedades del objeto seleccionado.
- Tipografía.
- Color.
- Posición.
- Dimensiones.
- Alineación.
- Transparencia.
- Opciones avanzadas.

**Barra inferior:**
- Página actual y total.
- Porcentaje de zoom.
- Estado del documento.
- Indicador de modificaciones sin guardar.
- Progreso de procesos.

### Requisitos visuales

- Diseño minimalista.
- Tema claro y oscuro.
- Iconografía consistente.
- Barras de herramientas contextuales.
- Paneles redimensionables.
- Pestañas para varios documentos.
- Atajos de teclado configurables.
- Compatibilidad con pantallas de alta resolución.
- Adaptación al tamaño de ventana.

El diseño debe evitar ventanas emergentes innecesarias y priorizar la edición directa.

---

## 4. Visualizador PDF avanzado

Implementar:

- Apertura de PDF mediante explorador de archivos.
- Arrastrar y soltar documentos.
- Varias pestañas abiertas.
- Miniaturas de páginas.
- Zoom configurable.
- Ajustar al ancho o alto.
- Ajustar página completa.
- Rotación de visualización.
- Visualización de una o dos páginas.
- Desplazamiento continuo.
- Búsqueda de texto.
- Selección y copia de texto.
- Navegación mediante marcadores.
- Historial de navegación.
- Renderizado progresivo.

El sistema debe soportar documentos extensos sin cargar todas las páginas como imágenes de alta resolución simultáneamente.

Implementar caché de páginas, renderizado bajo demanda y liberación inteligente de memoria.

---

## 5. Editor avanzado de texto PDF

Este es uno de los módulos más importantes.

Permitir seleccionar directamente texto existente dentro de un PDF y modificarlo.

### Funcionalidades

- Seleccionar bloques de texto.
- Editar palabras y párrafos.
- Reemplazar contenido existente.
- Agregar nuevos cuadros de texto.
- Eliminar texto.
- Mover bloques.
- Redimensionar bloques.
- Copiar y pegar.
- Cambiar fuentes.
- Cambiar tamaño.
- Negrita, cursiva y subrayado.
- Color y transparencia.
- Alineación horizontal.
- Espaciado de líneas.
- Espaciado de caracteres.
- Rotación.
- Buscar y reemplazar.
- Compatibilidad con caracteres especiales y Unicode.

### Conservación del formato

Cuando se modifica texto existente, intentar conservar:

- Fuente original.
- Tamaño.
- Color.
- Posicionamiento.
- Alineación.
- Distribución.
- Rotación.
- Espaciado.

Reconocer que los documentos PDF no siempre contienen párrafos editables como un procesador de texto.

Desarrollar un sistema que identifique operadores de texto, fuentes, coordenadas, fragmentos y bloques visuales.

Cuando no sea posible reconstruir un párrafo con seguridad, permitir editar elementos individuales o utilizar una reconstrucción controlada.

No implementar falsamente la edición ocultando el texto original mediante rectángulos blancos. Si se utiliza una superposición, debe identificarse expresamente como una operación diferente a la modificación del contenido original.

---

## 6. Inserción y edición de imágenes

Permitir insertar imágenes sobre cualquier página PDF.

### Formatos

- JPG.
- JPEG.
- PNG.
- WebP.
- BMP.
- TIFF.
- SVG mediante conversión o renderizado adecuado.

### Herramientas

- Insertar imagen.
- Arrastrar imagen al documento.
- Redimensionar.
- Rotar.
- Recortar.
- Mover.
- Eliminar.
- Reemplazar imágenes existentes.
- Duplicar.
- Cambiar opacidad.
- Mantener proporciones.
- Alinear con otros objetos.
- Organizar delante o detrás.
- Agrupar elementos cuando sea posible.
- Extraer imágenes del documento.

Al exportar el documento, mantener una resolución apropiada y evitar recomprimir innecesariamente las imágenes originales.

---

## 7. OCR: Reconocimiento de texto

Implementar reconocimiento óptico de caracteres para convertir documentos escaneados en documentos con texto seleccionable.

### Funciones

- Reconocimiento de páginas individuales.
- Reconocimiento de documentos completos.
- Procesamiento por lotes.
- OCR en español e inglés.
- Selección de idioma.
- Detección automática de orientación.
- Corrección de inclinación.
- Mejora de contraste.
- Eliminación de ruido.
- Detección de regiones de texto.
- Visualización de resultados.
- Corrección manual de reconocimiento.
- Generación de PDF buscable.
- Exportación del texto reconocido.

### Modos OCR

**Modo 1: PDF buscable**

Conservar la imagen original e incorporar una capa de texto seleccionable y buscable, correctamente alineada.

**Modo 2: Reconstrucción editable**

Detectar bloques de texto y permitir reconstruirlos como objetos editables.

Advertir que la reconstrucción editable puede modificar la tipografía o distribución visual.

El OCR debe ejecutarse localmente sin enviar documentos a servicios externos.

---

## 8. Organización de páginas

Implementar herramientas para:

- Insertar páginas.
- Eliminar páginas.
- Duplicar páginas.
- Reordenar mediante arrastrar y soltar.
- Rotar páginas.
- Extraer páginas.
- Dividir documentos.
- Combinar varios PDF.
- Insertar páginas de otro PDF.
- Crear documentos en blanco.
- Cambiar dimensiones de páginas.
- Recortar márgenes.
- Aplicar numeración.
- Agregar encabezados y pies de página.
- Agregar marcas de agua.
- Exportar intervalos de páginas.

Incluir operaciones sobre múltiples páginas seleccionadas.

---

## 9. Herramientas de diseño y anotación

Permitir insertar objetos gráficos:

- Rectángulos.
- Círculos.
- Elipses.
- Líneas.
- Flechas.
- Polígonos.
- Cuadros de texto.
- Notas.
- Resaltadores.
- Subrayados.
- Tachados.
- Sellos personalizados.

Incorporar:

- Reglas.
- Guías.
- Cuadrícula opcional.
- Ajuste magnético.
- Alineación entre objetos.
- Distribución uniforme.
- Controles de rotación.
- Agrupación cuando sea compatible.
- Administración de objetos superpuestos.

Distinguir entre anotaciones PDF estándar y contenido gráfico integrado permanentemente en la página.

---

## 10. Formularios interactivos

Implementar creación y edición de formularios PDF.

Herramientas:

- Campos de texto.
- Casillas de verificación.
- Botones de opción.
- Listas desplegables.
- Campos de fecha.
- Botones de acción.
- Campos de firma.

Propiedades configurables:

- Nombre interno.
- Etiqueta.
- Valor predeterminado.
- Campo obligatorio.
- Solo lectura.
- Validación de entrada.
- Apariencia.
- Orden de navegación.

Permitir completar, guardar y exportar formularios existentes.

Dar prioridad a formularios AcroForm e identificar por separado las limitaciones de otros formatos.

---

## 11. Firmas y seguridad documental

Implementar:

- Firmas visuales manuscritas.
- Inserción de imágenes de firma.
- Firmas digitales basadas en certificados.
- Validación de firmas digitales.
- Protección mediante contraseña.
- Cifrado compatible con PDF.
- Configuración de permisos del documento.
- Consulta de metadatos.
- Eliminación controlada de metadatos.
- Redacción permanente de información confidencial.

Diferenciar claramente una firma visual de una firma digital criptográfica.

No considerar un rectángulo negro colocado sobre el texto como una redacción segura.

Una redacción debe eliminar efectivamente el contenido afectado y verificar que no pueda recuperarse mediante extracción o búsqueda.

Nunca permitir modificaciones silenciosas que invaliden firmas digitales existentes.

---

## 12. Conversión y exportación

Incorporar conversión entre formatos:

- PDF a imágenes.
- Imágenes a PDF.
- PDF a texto.
- PDF a HTML, cuando sea viable.
- PDF a DOCX, utilizando un convertidor compatible.
- DOCX a PDF, mediante un motor adecuado.
- PDF a PDF/A, con validación de conformidad.
- Exportación de páginas individuales.

Permitir configurar:

- Resolución.
- Calidad.
- Compresión.
- Intervalo de páginas.
- Tamaño del papel.
- Orientación.
- Metadatos.

Evitar prometer conversiones perfectas cuando el formato original no permita conservar toda la estructura.

---

## 13. Historial y recuperación

Implementar un sistema robusto de acciones:

- Deshacer.
- Rehacer.
- Historial de cambios.
- Estado de documento modificado.
- Autoguardado recuperable.
- Recuperación de sesiones.
- Archivos temporales seguros.
- Confirmación al cerrar con cambios pendientes.

Utilizar un patrón de comandos para registrar las operaciones.

Evitar escribir directamente sobre el archivo original antes de completar correctamente el proceso de guardado.

Guardar mediante archivos temporales y reemplazo controlado, manteniendo una copia de seguridad configurable.

---

## 14. Rendimiento

Optimizar el programa para:

- Documentos PDF extensos.
- Imágenes de alta resolución.
- Archivos escaneados.
- Documentos de cientos de páginas.
- Procesamiento OCR prolongado.
- Múltiples documentos abiertos.

Utilizar:

- Procesamiento asíncrono.
- Cancelación de tareas.
- Renderizado parcial.
- Caché con límites de memoria.
- Procesamiento por páginas.
- Indicadores de progreso.
- Manejo de errores.
- Gestión explícita de recursos nativos.

No bloquear el hilo principal de la interfaz durante procesos pesados.

---

## 15. Compatibilidad y distribución

El software debe funcionar en:

**Windows**
- Windows 10 y 11 compatibles con las versiones seleccionadas de .NET y Avalonia.
- Arquitectura x64.
- Instalador ejecutable o MSI.
- Versión portable cuando sea viable.

**Linux**
- Fedora.
- Ubuntu.
- Debian.
- Otras distribuciones compatibles.

Generar preferentemente:

- AppImage.
- RPM.
- DEB.

Comprobar el funcionamiento en X11 y Wayland, contemplando XWayland cuando sea necesario.

La aplicación debe funcionar sin conexión a Internet para las funciones principales.

---

## 16. Calidad y seguridad del software

Implementar:

- Manejo seguro de documentos potencialmente maliciosos.
- Límites de recursos para archivos muy grandes.
- Validación de archivos antes de procesarlos.
- Protección ante PDF corruptos.
- Control de dependencias nativas.
- Registro de errores sin almacenar contenido sensible.
- Pruebas unitarias.
- Pruebas de integración.
- Pruebas de regresión visual.
- Pruebas con documentos reales.

Nunca ejecutar automáticamente scripts incrustados en documentos PDF.

Comprobar la licencia de cada dependencia, especialmente si el proyecto se distribuirá como software propietario o comercial.

---

## 17. Estrategia de implementación

El desarrollo debe organizarse en fases funcionales.

### Fase 1: Fundamentos
- Crear solución .NET.
- Implementar Avalonia UI.
- Configurar MVVM.
- Crear estructura modular.
- Implementar apertura de PDF.
- Visualizar páginas.
- Navegación, miniaturas y zoom.
- Guardar copias.

### Fase 2: Edición gráfica
- Insertar imágenes.
- Insertar texto.
- Mover y redimensionar.
- Formas geométricas.
- Herramientas de selección.
- Deshacer y rehacer.

### Fase 3: Edición real
- Analizar estructura PDF.
- Detectar objetos de texto.
- Modificar texto existente.
- Editar propiedades de objetos.
- Conservar formato.
- Guardar cambios sin destruir contenido no editado.

### Fase 4: OCR
- Integrar Tesseract.
- Procesar documentos escaneados.
- Generar PDF buscables.
- Implementar reconstrucción editable.

### Fase 5: Herramientas avanzadas
- Administración de páginas.
- Formularios.
- Conversión.
- Seguridad.
- Firmas digitales.
- Redacción permanente.

### Fase 6: Optimización
- Mejorar rendimiento.
- Resolver incompatibilidades.
- Realizar pruebas.
- Crear instaladores.
- Documentar funcionalidades.

Cada fase debe producir un resultado ejecutable y verificable.

---

## 18. Entregables

Generar:

1. Arquitectura técnica documentada.
2. Estructura completa de carpetas.
3. Código fuente organizado.
4. Interfaz Avalonia funcional.
5. Motor PDF integrado.
6. Sistema de edición.
7. Pruebas automatizadas.
8. Instrucciones de compilación.
9. Scripts de publicación para Windows y Linux.
10. Instaladores.
11. Documentación de uso.
12. Lista transparente de funciones terminadas, parciales y pendientes.

---

## 19. Reglas obligatorias para la IA desarrolladora

- No generar solamente prototipos visuales sin funcionamiento.
- No simular herramientas que todavía no existen.
- No colocar botones sin acciones reales.
- No sustituir edición PDF auténtica por superposiciones sin explicarlo.
- No destruir la estructura original del PDF innecesariamente.
- No rasterizar todas las páginas para implementar la edición.
- No asumir que el OCR recuperará perfectamente el formato original.
- No utilizar dependencias incompatibles con el modelo de licencia elegido.
- No sacrificar compatibilidad entre Windows y Linux por una función secundaria.
- No concentrar toda la lógica en una única clase.
- No omitir la gestión de errores ni las pruebas.

Si una funcionalidad requiere desarrollar un motor adicional o incorporar una biblioteca comercial, documentar las alternativas, limitaciones y costes antes de implementarla.

## 20. Instrucción final

Comienza realizando un análisis técnico de viabilidad.

Identifica las mejores bibliotecas disponibles, sus limitaciones, compatibilidad, licencias y capacidades reales de edición PDF.

Diseña una arquitectura que permita obtener un editor robusto y mantenible.

Después construye la solución inicial en .NET 10 y Avalonia UI, con un visualizador PDF funcional y una estructura preparada para los módulos de edición.

Implementa cada módulo progresivamente, incluyendo pruebas y verificaciones.

**El resultado final debe ser una aplicación de escritorio real, funcional y multiplataforma, no una demostración conceptual ni una simple interfaz gráfica.**