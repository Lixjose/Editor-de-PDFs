# Mythlix · PDF Studio

Aplicación de escritorio en C# .NET 10, Avalonia 12.1.1 y MVVM, creada a partir de las referencias adjuntas. Fundamentos, edición gráfica y edición inicial del texto original y OCR local (fases 1–4) del editor profesional. Diseño obsidiana, índigo y cian, paneles redimensionables, pestañas y propiedades en español.

## Ejecutar

Requiere .NET SDK 10.0.402 y Poppler (`pdftoppm` y `pdftotext`). En esta máquina hay un SDK local en `.sdk` y Poppler disponible. Desde la carpeta del proyecto:

```bash
export DOTNET_CLI_HOME=/tmp/mythlix-cli
.sdk/dotnet build Mythlix.sln -m:1
./scripts/run.sh
```

Con SDK instalado globalmente, use `dotnet` en lugar de `.sdk/dotnet`. Primera restauración requiere conexión; la aplicación procesa los documentos sin Internet.

Linux: instale `poppler-utils` con el gestor de su distribución. Windows: obtenga una distribución de Poppler compatible, conserve sus DLL junto a los ejecutables y configure `MYTHLIX_POPPLER_BIN` a su carpeta `bin`. Poppler no viene incluido. Las licencias de cualquier distribución de binarios deben revisarse antes de redistribuirla.

## Uso

1. Abra uno o varios PDF. Seleccione la pestaña del documento.
2. Navegue por el índice de páginas o las flechas. La miniatura corresponde a la página actual.
3. La página se ajusta automáticamente. Puede cambiar el zoom entre 25 y 300 % o usar el botón de ajuste de página. El panel derecho permite seleccionar y copiar texto extraído.
4. Busque texto; el buscador avanza a la siguiente página que contiene una coincidencia, recorriendo el documento.
5. Desde **Organizar**, duplique, mueva, inserte o extraiga páginas; use **Dividir PDF en partes** para exportar grupos consecutivos a una carpeta nueva. Rote, añada páginas en blanco o elimine páginas. Deshacer y rehacer restauran la estructura completa del PDF.
6. Añada texto, imágenes o formas desde la segunda barra. Seleccione y arrastre para mover; use el tirador inferior derecho para redimensionar. Ajuste propiedades y texto nuevo en el inspector.
7. Seleccione un bloque en **Texto original**, cambie su texto y aplique. Funciona con bloques simples y fuentes estándar compatibles; los casos no seguros se mantienen en solo lectura.
8. Para escaneos, use **OCR → Reconocimiento local**, revise palabras y cree una capa buscable o exporte una reconstrucción editable.
9. Use **Convertir → Exportar páginas como PNG…** para exportar intervalos con tamaño configurable a una carpeta nueva.
10. Guarde una copia con otro nombre. El PDF original se conserva. La aplicación pregunta antes de descartar cambios.

Los PDF firmados se rechazan en esta fase para evitar invalidar firmas sin un sistema de validación. PDF cifrados que requieran contraseña no tienen diálogo de autenticación aún.

## Verificación y publicación

```bash
dotnet test Mythlix.sln -m:1
./scripts/publish.sh linux-x64
./scripts/publish.sh win-x64
```

En PowerShell: `./scripts/publish.ps1 -Runtime win-x64`. Los scripts publican carpetas portables autocontenidas; no crean instaladores MSI, DEB, RPM o AppImage. El funcionamiento en Windows debe verificarse en una máquina Windows con Poppler.

La fase 6 inicial añade caché LRU de renderizado/texto y reutilización del PDF temporal por revisión.

Consulte [arquitectura y viabilidad](docs/ARCHITECTURE.md), [estado de funciones](docs/STATUS.md), [edición gráfica](docs/PHASE2.md), [texto original e interfaz](docs/PHASE3.md), [OCR e instalación](docs/PHASE4.md) y [optimización](docs/PHASE6.md), [exportación PNG](docs/IMAGE_EXPORT.md) y [validación](docs/VALIDATION.md). Los archivos de referencia se conservan en `references/`.

También puede abrir un PDF al iniciar: `dotnet run --project src/Mythlix.Desktop -- ruta/documento.pdf`. La comprobación automatizada de ventana se invoca con `--smoke-test ruta/documento.pdf` y cierra la aplicación tras renderizar.

Para comprobar la nueva interfaz y la edición original: `dotnet run --project src/Mythlix.Desktop --no-build -- --phase3-smoke references/Mythlix-Documento-Tecnico.pdf`.

OCR requiere Tesseract con modelos español/inglés. Ya están disponibles localmente en esta máquina; consulte PHASE4.md para instalarlo en otra.

Comprobación integrada de división y navegación: `dotnet run --project src/Mythlix.Desktop --no-build -- --split-smoke references/Mythlix-Documento-Tecnico.pdf`.
