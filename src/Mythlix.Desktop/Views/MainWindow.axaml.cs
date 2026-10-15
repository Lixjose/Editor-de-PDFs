using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Platform.Storage;
using Avalonia.VisualTree;
using Mythlix.Desktop.ViewModels;
namespace Mythlix.Desktop.Views;
public partial class MainWindow : Window {
 readonly WorkspaceViewModel vm = new();
 bool closing;
 bool autoFit=true;
 public MainWindow() {
  InitializeComponent(); DataContext=vm; Closing += OnClosing;
  PdfCanvas.TextDoubleClicked += OnTextDoubleClicked;
  DirectEditor.TextChanged += (_,_) => { if(DirectEditor.IsVisible) GrowEditorToText(); };
  PdfCanvas.GraphicTextDoubleClicked += OnGraphicTextDoubleClicked;
  PdfCanvas.CanvasPointerPressed += OnCanvasPointerPressed;
  vm.PropertyChanged+=(_,e)=>{
   if(e.PropertyName is nameof(vm.CurrentPage) or nameof(vm.PageImage) or nameof(vm.Selected)) {
    DirectEditor.IsVisible = false;
    vm.ActiveEditingOriginal = null;
   }
   if(autoFit && e.PropertyName is nameof(vm.Selected))FitPageToView();
  };
  Viewport.PropertyChanged+=(_,e)=>{if(autoFit && e.Property==BoundsProperty)FitPageToView();};
  Opened += async (_,_) => {
   var args=Environment.GetCommandLineArgs().Skip(1).ToArray();
   var path=args.FirstOrDefault(a=>a.EndsWith(".pdf",StringComparison.OrdinalIgnoreCase));
   if(path is null) return;
   await Execute(async()=>{await vm.OpenAsync(path);await vm.RenderAsync();});
   if(args.Contains("--images-to-pdf-smoke")) {
    await vm.RunAsync(async()=> {
     try{await Diagnostics.ImagesToPdfSmokeTest.RunAsync(vm);Console.WriteLine("MYTHLIX_IMAGES_TO_PDF_SMOKE_OK");}
     catch(Exception ex){Console.WriteLine("MYTHLIX_IMAGES_TO_PDF_SMOKE_FAILED: "+ex);Environment.ExitCode=1;}
    });
    closing=true;vm.Dispose();Close();return;
   }
   if(args.Contains("--images-smoke")) {
    try{await Diagnostics.ImageExportSmokeTest.RunAsync(vm);Console.WriteLine("MYTHLIX_IMAGES_SMOKE_OK");}
    catch(Exception ex){Console.WriteLine("MYTHLIX_IMAGES_SMOKE_FAILED: "+ex);Environment.ExitCode=1;}
    closing=true;vm.Dispose();Close();return;
   }
   if(args.Contains("--split-smoke")) {
    try{await Diagnostics.PageSplitSmokeTest.RunAsync(vm);Console.WriteLine("MYTHLIX_SPLIT_SMOKE_OK");}
    catch(Exception ex){Console.WriteLine("MYTHLIX_SPLIT_SMOKE_FAILED: "+ex);Environment.ExitCode=1;}
    closing=true;vm.Dispose();Close();return;
   }
   if(args.Contains("--phase4-smoke")) {
    try{await Diagnostics.OcrSmokeTest.RunAsync(vm,this);Console.WriteLine("MYTHLIX_PHASE4_SMOKE_OK");}
    catch(Exception ex){Console.WriteLine("MYTHLIX_PHASE4_SMOKE_FAILED: "+ex);Environment.ExitCode=1;}
    closing=true;vm.Dispose();Close();return;
   }
   if(args.Contains("--phase3-smoke")) {
    try{await Diagnostics.OriginalTextSmokeTest.RunAsync(vm,this);Console.WriteLine("MYTHLIX_PHASE3_SMOKE_OK");}
    catch(Exception ex){Console.WriteLine("MYTHLIX_PHASE3_SMOKE_FAILED: "+ex);Environment.ExitCode=1;}
    closing=true;vm.Dispose();Close();return;
   }
   if(args.Contains("--phase2-smoke")) {
    try {
     var imagePath=args.First(a=>a.EndsWith(".png",StringComparison.OrdinalIgnoreCase));
     await Diagnostics.GraphicsSmokeTest.RunAsync(vm,this,imagePath);
     Console.WriteLine("MYTHLIX_PHASE2_SMOKE_OK");
    } catch(Exception ex) {Console.WriteLine("MYTHLIX_PHASE2_SMOKE_FAILED: "+ex);Environment.ExitCode=1;}
    closing=true;vm.Dispose();Close();return;
   }
   if(args.Contains("--smoke-test")) {
    var success=vm.PageImage is not null;
    Console.WriteLine(success ? "MYTHLIX_SMOKE_OK" : "MYTHLIX_SMOKE_FAILED: " + vm.Status);
    Environment.ExitCode=success?0:1; closing=true; vm.Dispose(); Close();
   }
  };
 }
 Task Execute(Func<Task> action) => vm.RunAsync(action);
 async void OcrReviewChanged(object? s,SelectionChangedEventArgs e){if(s is ComboBox combo && combo.SelectedItem is OcrReviewPage review && review.Number!=vm.CurrentPage)await Execute(async()=>{vm.CurrentPage=review.Number;await vm.RenderAsync();});}
 async void OcrClick(object? s,RoutedEventArgs e)=>await vm.PrepareOcrAsync();
 async void ReconstructOcrClick(object? s,RoutedEventArgs e)=>await Execute(async()=> {
  if(vm.OcrPages.Count==0){vm.Status="Primero reconozca páginas con OCR.";return;}
  if(!await Confirm("Crear un PDF nuevo solo con el texto reconocido? No conserva imágenes y puede cambiar tipografía o distribución."))return;
  var file=await StorageProvider.SaveFilePickerAsync(new(){Title="Reconstrucción OCR editable",SuggestedFileName="mythlix-ocr-editable.pdf",DefaultExtension="pdf",FileTypeChoices=[FilePickerFileTypes.Pdf]});
  var path=file?.TryGetLocalPath();if(path is null)return;
  if(vm.Selected is {} doc && string.Equals(Path.GetFullPath(path),Path.GetFullPath(doc.Source),OperatingSystem.IsWindows()?StringComparison.OrdinalIgnoreCase:StringComparison.Ordinal))throw new InvalidOperationException("Guarde la reconstrucción con otro nombre.");
  await vm.ExportOcrReconstructionAsync(path);vm.Status="Reconstrucción editable guardada como un documento nuevo.";
 });
 async void ExportOcrClick(object? s,RoutedEventArgs e)=>await Execute(async()=> {
  if(string.IsNullOrWhiteSpace(vm.OcrExportText)){vm.Status="Primero reconozca una página con OCR.";return;}
  var file=await StorageProvider.SaveFilePickerAsync(new(){Title="Exportar texto OCR",SuggestedFileName="mythlix-ocr.txt",DefaultExtension="txt"});
  var path=file?.TryGetLocalPath();if(path is null)return;
  if(vm.Documents.Any(doc=>string.Equals(Path.GetFullPath(path),Path.GetFullPath(doc.Source),OperatingSystem.IsWindows()?StringComparison.OrdinalIgnoreCase:StringComparison.Ordinal)))throw new InvalidOperationException("Guarde el texto con otro nombre para conservar los PDF originales.");
  var temporary=path+"."+Guid.NewGuid().ToString("N")+".tmp";
  try{await File.WriteAllTextAsync(temporary,vm.OcrExportText,System.Text.Encoding.UTF8);File.Move(temporary,path,true);}finally{if(File.Exists(temporary))File.Delete(temporary);}
 });
 async void OpenClick(object? sender,RoutedEventArgs e) => await Execute(async () => {
  var files=await StorageProvider.OpenFilePickerAsync(new(){Title="Abrir PDF",AllowMultiple=true,FileTypeFilter=[FilePickerFileTypes.Pdf]});
  foreach(var file in files) { var path=file.TryGetLocalPath(); if(path is not null) await vm.OpenAsync(path); }
  await vm.RenderAsync();
 });
 async void SaveClick(object? sender,RoutedEventArgs e) => await Execute(async () => {
  var doc=vm.Selected; if(doc is null) return;
  var file=await StorageProvider.SaveFilePickerAsync(new(){Title="Guardar copia PDF",SuggestedFileName=Path.GetFileNameWithoutExtension(doc.Name)+"-mythlix.pdf",DefaultExtension="pdf",FileTypeChoices=[FilePickerFileTypes.Pdf]});
  var path=file?.TryGetLocalPath(); if(path is null) return;
  if(string.Equals(Path.GetFullPath(path),Path.GetFullPath(doc.Source),OperatingSystem.IsWindows()?StringComparison.OrdinalIgnoreCase:StringComparison.Ordinal)) throw new InvalidOperationException("Guarde con otro nombre para conservar el archivo original.");
  await Task.Run(()=>doc.Document.SaveCopyAsync(path)); doc.Modified=false; vm.Status="Copia guardada correctamente.";
 });
 async Task<bool> Confirm(string message) {
  var dialog=new Window{Title="Mythlix",Width=420,Height=180,CanResize=false,WindowStartupLocation=WindowStartupLocation.CenterOwner};
  var yes=new Button{Content="Continuar"}; var no=new Button{Content="Cancelar"};
  yes.Click+=(_,_)=>dialog.Close(true); no.Click+=(_,_)=>dialog.Close(false);
  dialog.Content=new StackPanel{Margin=new Thickness(20),Spacing=14,Children={new TextBlock{Text=message,TextWrapping=Avalonia.Media.TextWrapping.Wrap},new StackPanel{Orientation=Avalonia.Layout.Orientation.Horizontal,Children={yes,no}}}};
  return await dialog.ShowDialog<bool>(this);
 }
 async void CloseClick(object? s,RoutedEventArgs e)=>await Execute(async()=>{if(vm.Selected?.Modified==true && !await Confirm("Cerrar y descartar los cambios sin guardar?"))return;vm.CloseSelected();await vm.RenderAsync();});
 async void OnClosing(object? s,WindowClosingEventArgs e) {
  if(closing) return; e.Cancel=true;
  if(vm.Busy) { vm.Status="Espere a que termine la operación antes de cerrar."; return; }
  if(vm.Documents.Any(d=>d.Modified) && !await Confirm("Salir y descartar los cambios pendientes?"))return;
  closing=true; vm.Dispose(); Close();
 }
 async void TabChanged(object? s,SelectionChangedEventArgs e)=>await Execute(vm.RenderAsync);
 async void ThumbnailChanged(object? s,SelectionChangedEventArgs e) {if(ThumbnailList.SelectedItem is PageThumbnailViewModel page)await Execute(async()=>{vm.CurrentPage=page.Number;await vm.RenderAsync();});}
 void SelectModeClick(object? s,RoutedEventArgs e){vm.SelectedGraphic=null;vm.SelectedOriginal=null;vm.Status="Seleccione un objeto o bloque de texto en la página.";}
 void OriginalModeClick(object? s,RoutedEventArgs e){vm.SelectedOriginal=vm.OriginalBlocks.FirstOrDefault();vm.Status=vm.OriginalStatus;}
 async void PreviousClick(object? s,RoutedEventArgs e)=>await Execute(async()=>{vm.CurrentPage--;await vm.RenderAsync();});
 async void NextClick(object? s,RoutedEventArgs e)=>await Execute(async()=>{vm.CurrentPage++;await vm.RenderAsync();});
 async void DuplicatePageClick(object? s,RoutedEventArgs e)=>await Execute(()=>vm.EditAsync("duplicate"));
 async void MoveBeforeClick(object? s,RoutedEventArgs e)=>await Execute(()=>vm.EditAsync("before"));
 async void MoveAfterClick(object? s,RoutedEventArgs e)=>await Execute(()=>vm.EditAsync("after"));
 async void InsertPdfClick(object? s,RoutedEventArgs e)=>await Execute(async()=> {
  if(vm.Selected is not {} doc)return;
  var files=await StorageProvider.OpenFilePickerAsync(new(){Title="Insertar PDF (contenido de páginas; sin importar marcadores ni formularios)",AllowMultiple=false,FileTypeFilter=[FilePickerFileTypes.Pdf]});
  var path=files.FirstOrDefault()?.TryGetLocalPath(); if(path is null)return;
  await vm.InsertPagesAsync(path);
 });
 async void MergePdfClick(object? s,RoutedEventArgs e)=>await Execute(async()=> {
  var files=await StorageProvider.OpenFilePickerAsync(new(){Title="Seleccionar documentos PDF a combinar (mínimo 2)",AllowMultiple=true,FileTypeFilter=[FilePickerFileTypes.Pdf]});
  if(files.Count<2){vm.Status="Seleccione al menos dos documentos PDF para combinar.";return;}
  var paths=files.Select(f=>f.TryGetLocalPath()??throw new InvalidOperationException("Archivo local no válido.")).ToArray();
  var saveFile=await StorageProvider.SaveFilePickerAsync(new(){Title="Guardar documento combinado",SuggestedFileName="documento-combinado.pdf",DefaultExtension="pdf",FileTypeChoices=[FilePickerFileTypes.Pdf]});
  var destination=saveFile?.TryGetLocalPath();if(destination is null)return;
  using var cancellation=new CancellationTokenSource();
  var progressWindow=new Window{Title="Combinando documentos PDF",Width=400,Height=150,WindowStartupLocation=WindowStartupLocation.CenterOwner,CanResize=false};
  var stop=new Button{Content="Cancelar"};stop.Click+=(_,_)=>{cancellation.Cancel();stop.IsEnabled=false;};
  progressWindow.Closing+=(_,_)=>cancellation.Cancel();
  progressWindow.Content=new StackPanel{Margin=new Thickness(20),Spacing=12,Children={new TextBlock{Text=$"Combinando {paths.Length} documentos…"},new ProgressBar{IsIndeterminate=true},stop}};
  progressWindow.Show(this);
  try {
   await Task.Run(()=>new Mythlix.Infrastructure.PdfMerger().MergeAsync(destination,paths,cancellation.Token));
   await vm.OpenAsync(destination);await vm.RenderAsync();vm.Status=$"Documentos combinados con éxito: {Path.GetFileName(destination)}";
  } catch(OperationCanceledException){vm.Status="Combinación cancelada.";}
  finally{progressWindow.Close();}
 });
 async void PageNumberingClick(object? s,RoutedEventArgs e)=>await Execute(async()=> {
  if(vm.Selected is null)return;
  var dialog=new Window{Title="Numeración de páginas",Width=450,Height=330,WindowStartupLocation=WindowStartupLocation.CenterOwner,CanResize=false};
  var format=new TextBox{Text="Página {n} de {total}"};
  var posCombo=new ComboBox{ItemsSource=new[]{"Inferior Centro","Inferior Derecha","Inferior Izquierda","Superior Centro","Superior Derecha","Superior Izquierda"},SelectedIndex=0,HorizontalAlignment=Avalonia.Layout.HorizontalAlignment.Stretch};
  var skipFirst=new CheckBox{Content="Omitir en la primera página (portada)",IsChecked=false};
  var size=new NumericUpDown{Value=10,Minimum=6,Maximum=36,FormatString="0"};
  var accept=new Button{Content="Aplicar numeración",Classes={"primary"}};var cancel=new Button{Content="Cancelar"};
  accept.Click+=(_,_)=>dialog.Close(true);cancel.Click+=(_,_)=>dialog.Close(false);
  dialog.Content=new StackPanel{Margin=new Thickness(20),Spacing=10,Children={new TextBlock{Text="Formato (use {n} para página y {total} para total):"},format,new TextBlock{Text="Posición:"},posCombo,new TextBlock{Text="Tamaño (pt):"},size,skipFirst,new StackPanel{Orientation=Avalonia.Layout.Orientation.Horizontal,Spacing=10,Margin=new Thickness(0,10,0,0),Children={accept,cancel}}}};
  if(!await dialog.ShowDialog<bool>(this))return;
  var pos=posCombo.SelectedIndex switch {
   1=>Mythlix.Core.HeaderFooterPosition.BottomRight,
   2=>Mythlix.Core.HeaderFooterPosition.BottomLeft,
   3=>Mythlix.Core.HeaderFooterPosition.TopCenter,
   4=>Mythlix.Core.HeaderFooterPosition.TopRight,
   5=>Mythlix.Core.HeaderFooterPosition.TopLeft,
   _=>Mythlix.Core.HeaderFooterPosition.BottomCenter
  };
  await vm.ApplyHeaderFooterAsync(new(Format:format.Text??"Página {n} de {total}",Position:pos,FontSize:(double)(size.Value??10),SkipFirstPage:skipFirst.IsChecked==true));
 });
 async void WatermarkClick(object? s,RoutedEventArgs e)=>await Execute(async()=> {
  if(vm.Selected is null)return;
  var dialog=new Window{Title="Marca de agua de texto",Width=450,Height=350,WindowStartupLocation=WindowStartupLocation.CenterOwner,CanResize=false};
  var text=new TextBox{Text="CONFIDENCIAL"};
  var angle=new NumericUpDown{Value=45,Minimum=-90,Maximum=90,FormatString="0°"};
  var opacity=new NumericUpDown{Value=25,Minimum=5,Maximum=90,FormatString="0%"};
  var color=new TextBox{Text="#DC2626"};
  var size=new NumericUpDown{Value=54,Minimum=12,Maximum=144,FormatString="0"};
  var accept=new Button{Content="Aplicar marca de agua",Classes={"primary"}};var cancel=new Button{Content="Cancelar"};
  accept.Click+=(_,_)=>dialog.Close(true);cancel.Click+=(_,_)=>dialog.Close(false);
  var spAngle = new StackPanel{Children={new TextBlock{Text="Ángulo:"},angle}};
  var spOpacity = new StackPanel{Children={new TextBlock{Text="Opacidad (%):"},opacity}};
  Grid.SetColumn(spOpacity, 1);
  var optionsGrid = new Grid{ColumnDefinitions=new ColumnDefinitions("*,*"),ColumnSpacing=8,Children={spAngle,spOpacity}};
  dialog.Content=new StackPanel{Margin=new Thickness(20),Spacing=10,Children={new TextBlock{Text="Texto de la marca de agua:"},text,new TextBlock{Text="Color (#RRGGBB):"},color,optionsGrid,new TextBlock{Text="Tamaño (pt):"},size,new StackPanel{Orientation=Avalonia.Layout.Orientation.Horizontal,Spacing=10,Margin=new Thickness(0,10,0,0),Children={accept,cancel}}}};
  if(!await dialog.ShowDialog<bool>(this))return;
  await vm.ApplyWatermarkAsync(new(Text:text.Text??"CONFIDENCIAL",Rotation:(double)(angle.Value??45),FontSize:(double)(size.Value??54),Color:color.Text??"#DC2626",Opacity:(double)(opacity.Value??25)/100.0));
 });
 async void ExtractPagesClick(object? s,RoutedEventArgs e)=>await Execute(async()=> {
  if(vm.Selected is not {} doc)return;
  var dialog=new Window{Title="Extraer contenido de páginas (sin marcadores ni formularios)",Width=470,Height=190,WindowStartupLocation=WindowStartupLocation.CenterOwner};
  var input=new TextBox{Text=vm.CurrentPage.ToString(),PlaceholderText="Ejemplo: 1,3-5"};
  var accept=new Button{Content="Elegir destino"};var cancel=new Button{Content="Cancelar"};
  accept.Click+=(_,_)=>dialog.Close(input.Text);cancel.Click+=(_,_)=>dialog.Close(null);
  dialog.Content=new StackPanel{Margin=new Thickness(20),Spacing=12,Children={new TextBlock{Text="Páginas e intervalos, separados por comas (1,3-5):"},input,new StackPanel{Orientation=Avalonia.Layout.Orientation.Horizontal,Spacing=10,Children={accept,cancel}}}};
  var value=await dialog.ShowDialog<string?>(this);if(value is null)return;
  var pages=Mythlix.Core.PageSelection.Parse(value,doc.Document.PageCount);
  var file=await StorageProvider.SaveFilePickerAsync(new(){Title="Extraer páginas",SuggestedFileName="paginas.pdf",DefaultExtension="pdf",FileTypeChoices=[FilePickerFileTypes.Pdf]});
  var path=file?.TryGetLocalPath();if(path is null)return;
  if(vm.Documents.Any(d=>string.Equals(Path.GetFullPath(path),Path.GetFullPath(d.Source),OperatingSystem.IsWindows()?StringComparison.OrdinalIgnoreCase:StringComparison.Ordinal)))throw new InvalidOperationException("Elija otro nombre para conservar los originales abiertos.");
  await Task.Run(()=>doc.Document.ExtractPagesAsync(path,pages));vm.Status="Páginas extraídas; el documento abierto conserva todas sus páginas.";
 });
 async void SplitPdfClick(object? s,RoutedEventArgs e)=>await Execute(async()=> {
  if(vm.Selected is not {} doc)return;
  var dialog=new Window{Title="Dividir PDF",Width=490,Height=245,WindowStartupLocation=WindowStartupLocation.CenterOwner};
  var count=new NumericUpDown{Minimum=1,Maximum=doc.Document.PageCount,Value=1,FormatString="0"};
  var accept=new Button{Content="Elegir carpeta contenedora"};var cancel=new Button{Content="Cancelar"};
  accept.Click+=(_,_)=>dialog.Close((int)(count.Value??1));cancel.Click+=(_,_)=>dialog.Close(null);
  dialog.Content=new StackPanel{Margin=new Thickness(20),Spacing=12,Children={new TextBlock{Text="Número de páginas por parte:"},count,new TextBlock{Text="Se exporta el contenido de páginas a una carpeta nueva.\nNo se trasladan formularios ni marcadores.",TextWrapping=Avalonia.Media.TextWrapping.Wrap},new StackPanel{Orientation=Avalonia.Layout.Orientation.Horizontal,Spacing=10,Children={accept,cancel}}}};
  var perPart=await dialog.ShowDialog<int?>(this);if(perPart is null)return;
  var folders=await StorageProvider.OpenFolderPickerAsync(new(){Title="Carpeta contenedora de las partes",AllowMultiple=false});
  var parent=folders.FirstOrDefault()?.TryGetLocalPath();if(parent is null)return;
  var destination=Path.Combine(parent,"Mythlix-partes-"+DateTime.Now.ToString("yyyyMMdd-HHmmss")+"-"+Guid.NewGuid().ToString("N")[..6]);
  using var cancellation=new CancellationTokenSource();
  var progress=new Window{Title="Dividiendo PDF",Width=400,Height=150,WindowStartupLocation=WindowStartupLocation.CenterOwner,CanResize=false};
  var stop=new Button{Content="Cancelar"};stop.Click+=(_,_)=>{cancellation.Cancel();stop.IsEnabled=false;};
  progress.Closing+=(_,_)=>cancellation.Cancel();
  progress.Content=new StackPanel{Margin=new Thickness(20),Spacing=12,Children={new TextBlock{Text="Exportando las partes a una carpeta nueva…"},new ProgressBar{IsIndeterminate=true},stop}};
  progress.Show(this);
  try {
   var results=await Task.Run(()=>doc.Document.SplitAsync(destination,perPart.Value,cancellation.Token));
   vm.Status=$"División completada: {results.Count} PDF en {destination}";
  } catch(OperationCanceledException) {vm.Status="División cancelada; no se publicaron partes.";}
  finally {progress.Close();}
 });
 async void ImagesToPdfClick(object? s,RoutedEventArgs e)=>await Execute(async()=> {
  var files=await StorageProvider.OpenFilePickerAsync(new(){Title="Seleccionar imágenes (orden del selector)",AllowMultiple=true,
   FileTypeFilter=[new FilePickerFileType("PNG, JPEG o BMP"){Patterns=["*.png","*.jpg","*.jpeg","*.bmp"]}]});
  if(files.Count==0)return;
  var paths=files.Select(f=>f.TryGetLocalPath()??throw new InvalidOperationException("Seleccione archivos locales.")).ToArray();
  if(paths.Length>100)throw new InvalidOperationException("Seleccione un máximo de 100 imágenes.");
  var dialog=new Window{Title="Crear PDF desde imágenes",Width=510,Height=365,WindowStartupLocation=WindowStartupLocation.CenterOwner};
  var paper=new ComboBox{ItemsSource=new[]{"A4","Carta"},SelectedIndex=0};
  var margin=new NumericUpDown{Minimum=0,Maximum=100,Value=24,FormatString="0"};
  var order=new TextBox{Text=string.Join("\n",paths.Select((p,i)=>$"{i+1}. {Path.GetFileName(p)}")),IsReadOnly=true,Height=90,AcceptsReturn=true};
  var accept=new Button{Content="Guardar PDF nuevo"};var cancel=new Button{Content="Cancelar"};
  accept.Click+=(_,_)=>dialog.Close(true);cancel.Click+=(_,_)=>dialog.Close(false);
  dialog.Content=new StackPanel{Margin=new Thickness(20),Spacing=10,Children={new TextBlock{Text="Una imagen por página, en este orden:"},order,paper,new TextBlock{Text="Margen en puntos (orientación automática):"},margin,new StackPanel{Orientation=Avalonia.Layout.Orientation.Horizontal,Spacing=10,Children={accept,cancel}}}};
  if(!await dialog.ShowDialog<bool>(this))return;
  var file=await StorageProvider.SaveFilePickerAsync(new(){Title="Guardar con un nombre nuevo",SuggestedFileName="imagenes.pdf",DefaultExtension="pdf",FileTypeChoices=[FilePickerFileTypes.Pdf]});
  var destination=file?.TryGetLocalPath();if(destination is null)return;
  using var cancellation=new CancellationTokenSource();
  var progress=new Window{Title="Creando PDF",Width=400,Height=160,WindowStartupLocation=WindowStartupLocation.CenterOwner,CanResize=false};
  var label=new TextBlock{Text=$"Procesando 0 de {paths.Length} imágenes…"};var bar=new ProgressBar{Maximum=paths.Length};
  var stop=new Button{Content="Cancelar"};stop.Click+=(_,_)=>{cancellation.Cancel();stop.IsEnabled=false;};
  progress.Closing+=(_,_)=>cancellation.Cancel();progress.Content=new StackPanel{Margin=new Thickness(20),Spacing=12,Children={label,bar,stop}};progress.Show(this);
  var updates=new Progress<int>(n=>{bar.Value=n;label.Text=$"Procesando {n} de {paths.Length} imágenes…";});
  var selectedPaper=paper.SelectedIndex==0?Mythlix.Infrastructure.ImagePdfPaper.A4:Mythlix.Infrastructure.ImagePdfPaper.Letter;
  var selectedMargin=(double)(margin.Value??24);
  try {
   await Task.Run(()=>new Mythlix.Infrastructure.ImagesToPdfConverter().ConvertAsync(paths,destination,selectedPaper,selectedMargin,cancellation.Token,updates));
  } catch(OperationCanceledException) {vm.Status="Conversión cancelada; no se creó el PDF.";return;}
  finally {progress.Close();}
  await vm.OpenAsync(destination);await vm.RenderAsync();vm.Status=$"PDF creado con {paths.Length} páginas: {destination}";
 });
 async void ExportImagesClick(object? s,RoutedEventArgs e)=>await Execute(async()=> {
  if(vm.Selected is not {} doc)return;
  var dialog=new Window{Title="Exportar páginas como PNG",Width=510,Height=330,WindowStartupLocation=WindowStartupLocation.CenterOwner};
  var pages=new TextBox{Text=$"1-{doc.Document.PageCount}",PlaceholderText="Ejemplo: 3,1-2"};
  var size=new NumericUpDown{Minimum=128,Maximum=2400,Value=1600,Increment=100,FormatString="0"};
  var accept=new Button{Content="Elegir carpeta contenedora"};var cancel=new Button{Content="Cancelar"};
  accept.Click+=(_,_)=>dialog.Close(true);cancel.Click+=(_,_)=>dialog.Close(false);
  dialog.Content=new StackPanel{Margin=new Thickness(20),Spacing=12,Children={new TextBlock{Text="Páginas e intervalos (1,3-5):"},pages,new TextBlock{Text="Lado mayor en píxeles (128–2400):"},size,new TextBlock{Text="Se conserva la proporción. Cada página se exporta como una imagen.",TextWrapping=Avalonia.Media.TextWrapping.Wrap},new StackPanel{Orientation=Avalonia.Layout.Orientation.Horizontal,Spacing=10,Children={accept,cancel}}}};
  if(!await dialog.ShowDialog<bool>(this))return;
  var selection=pages.Text??"";var parsed=Mythlix.Core.PageSelection.Parse(selection,doc.Document.PageCount);
  var folders=await StorageProvider.OpenFolderPickerAsync(new(){Title="Carpeta contenedora de las imágenes",AllowMultiple=false});
  var parent=folders.FirstOrDefault()?.TryGetLocalPath();if(parent is null)return;
  var destination=Path.Combine(parent,"Mythlix-imagenes-"+DateTime.Now.ToString("yyyyMMdd-HHmmss")+"-"+Guid.NewGuid().ToString("N")[..6]);
  using var cancellation=new CancellationTokenSource();
  var progress=new Window{Title="Exportando imágenes",Width=410,Height=160,WindowStartupLocation=WindowStartupLocation.CenterOwner,CanResize=false};
  var label=new TextBlock{Text=$"Exportando 0 de {parsed.Count} páginas…"};
  var bar=new ProgressBar{Minimum=0,Maximum=parsed.Count};
  var stop=new Button{Content="Cancelar"};stop.Click+=(_,_)=>{cancellation.Cancel();stop.IsEnabled=false;};
  progress.Closing+=(_,_)=>cancellation.Cancel();
  progress.Content=new StackPanel{Margin=new Thickness(20),Spacing=12,Children={label,bar,stop}};
  progress.Show(this);
  var updates=new Progress<int>(n=>{bar.Value=n;label.Text=$"Exportando {n} de {parsed.Count} páginas…";});
  try {
   var results=await vm.ExportImagesAsync(selection,(int)(size.Value??1600),destination,cancellation.Token,updates);
   vm.Status=$"Exportación completada: {results.Count} PNG en {destination}";
  } catch(OperationCanceledException) {vm.Status="Exportación cancelada; no se publicaron imágenes.";}
  finally {progress.Close();}
 });
 async void RotateClick(object? s,RoutedEventArgs e)=>await Execute(()=>vm.EditAsync("rotate"));
 async void AddClick(object? s,RoutedEventArgs e)=>await Execute(()=>vm.EditAsync("add"));
 async void DeleteClick(object? s,RoutedEventArgs e)=>await Execute(async()=>{if(vm.Selected is not null && await Confirm("Eliminar la página actual? Puede deshacer esta operación."))await vm.EditAsync("delete");});
 async void UndoClick(object? s,RoutedEventArgs e)=>await Execute(()=>vm.HistoryAsync(false));
 async void RedoClick(object? s,RoutedEventArgs e)=>await Execute(()=>vm.HistoryAsync(true));
 async void SearchClick(object? s,RoutedEventArgs e)=>await Execute(vm.SearchAsync);
 void ZoomInClick(object? s,RoutedEventArgs e){autoFit=false;vm.Zoom=Math.Min(300,vm.Zoom+25);}
 void ZoomOutClick(object? s,RoutedEventArgs e){autoFit=false;vm.Zoom=Math.Max(25,vm.Zoom-25);}
 void ResetZoomClick(object? s,RoutedEventArgs e){autoFit=false;vm.Zoom=100;}
 void FitPageClick(object? s,RoutedEventArgs e){autoFit=true;FitPageToView();}
 void FitPageToView(){
  if(vm.Selected is null || Viewport.Bounds.Width<=70 || Viewport.Bounds.Height<=82)return;
  var targetZoom = Math.Clamp(Math.Min((Viewport.Bounds.Width-68)/720,(Viewport.Bounds.Height-80)/(720*vm.Geometry.DisplayHeight/vm.Geometry.DisplayWidth))*100,25,300);
  if(Math.Abs(targetZoom - vm.Zoom) >= 2.0) {
   vm.Zoom = targetZoom;
  }
 }
 async void InsertImageClick(object? s,RoutedEventArgs e)=>await Execute(async()=> {
  if(vm.Selected is null) {vm.Status="Abra un PDF antes de insertar imágenes.";return;}
  var files=await StorageProvider.OpenFilePickerAsync(new(){Title="Insertar imagen",AllowMultiple=false,
   FileTypeFilter=[new FilePickerFileType("PNG, JPEG o BMP"){Patterns=["*.png","*.jpg","*.jpeg","*.bmp"]}]});
  var file=files.FirstOrDefault();if(file is null)return;
  await using var stream=await file.OpenReadAsync();
  using var memory=new MemoryStream();var buffer=new byte[81920];
  int count;while((count=await stream.ReadAsync(buffer))>0) {
   if(memory.Length+count>10*1024*1024)throw new InvalidOperationException("Límite de imagen: 10 MB.");
   await memory.WriteAsync(buffer.AsMemory(0,count));
  }
  var bytes=memory.ToArray();
  var ratio=await Task.Run(()=> {
   using var image=new Avalonia.Media.Imaging.Bitmap(new MemoryStream(bytes));
   if((long)image.PixelSize.Width*image.PixelSize.Height>25_000_000)throw new InvalidOperationException("Límite de imagen: 25 megapíxeles.");
   return (double)image.PixelSize.Width/image.PixelSize.Height;
  });
  await vm.InsertImageAsync(bytes,ratio);
 });
 bool isDirectCanceling;
 bool isCommitting;
 Mythlix.Core.OriginalTextBlock? activeEditingOriginal;
 Mythlix.Core.GraphicObject? activeEditingGraphic;

 void OnCanvasPointerPressed() {
  if(!isCommitting && DirectEditor.IsVisible) {
   _ = CommitDirectEditAsync();
  }
 }

 void OnTextDoubleClicked(Mythlix.Core.OriginalTextBlock block, Rect rect) {
  if(isCommitting) return;
  isDirectCanceling = false;
  activeEditingOriginal = block;
  activeEditingGraphic = null;
  vm.ActiveEditingOriginal = block;

  var scale = PdfCanvas.Bounds.Width / Math.Max(1, vm.Geometry.DisplayWidth);
  DirectEditor.TextWrapping = Avalonia.Media.TextWrapping.NoWrap;

  var fontName = block.FontName ?? "Liberation Sans";
  if(fontName.Contains("Times", StringComparison.OrdinalIgnoreCase) ||
     fontName.Contains("Roman", StringComparison.OrdinalIgnoreCase) ||
     fontName.Contains("Serif", StringComparison.OrdinalIgnoreCase) ||
     fontName.Contains("Minion", StringComparison.OrdinalIgnoreCase) ||
     fontName.Contains("Garamond", StringComparison.OrdinalIgnoreCase)) {
   try { DirectEditor.FontFamily = new FontFamily("Liberation Serif, Times New Roman, serif"); } catch {}
  } else if(fontName.Contains("Courier", StringComparison.OrdinalIgnoreCase) ||
            fontName.Contains("Mono", StringComparison.OrdinalIgnoreCase)) {
   try { DirectEditor.FontFamily = new FontFamily("Liberation Mono, Courier New, monospace"); } catch {}
  } else if(fontName.Contains("Montserrat", StringComparison.OrdinalIgnoreCase)) {
   try { DirectEditor.FontFamily = new FontFamily("Montserrat, sans-serif"); } catch {}
  } else {
   try { DirectEditor.FontFamily = new FontFamily("Liberation Sans, Arial, sans-serif"); } catch {}
  }

  DirectEditor.FontWeight = block.Bold ? Avalonia.Media.FontWeight.Bold : Avalonia.Media.FontWeight.Normal;
  DirectEditor.FontStyle = block.Italic ? Avalonia.Media.FontStyle.Italic : Avalonia.Media.FontStyle.Normal;
  try {
   var c = string.IsNullOrWhiteSpace(block.Color) ? "#000000" : block.Color;
   DirectEditor.Foreground = Brush.Parse(c);
  } catch {
   DirectEditor.Foreground = Brushes.Black;
  }
  FitEditorToBox(block.Text, block.FontSize * scale, rect);
  DirectEditor.Text = block.Text;
  DirectEditor.IsVisible = true;

  DirectEditor.Focus();
  DirectEditor.SelectAll();
 }

 void OnGraphicTextDoubleClicked(Mythlix.Core.GraphicObject obj, Rect rect) {
  if(isCommitting) return;
  isDirectCanceling = false;
  activeEditingOriginal = null;
  activeEditingGraphic = obj;
  vm.ActiveEditingOriginal = null;

  var scale = PdfCanvas.Bounds.Width / Math.Max(1, vm.Geometry.DisplayWidth);
  DirectEditor.TextWrapping = obj.Text.Contains('\n') ? Avalonia.Media.TextWrapping.Wrap : Avalonia.Media.TextWrapping.NoWrap;

  var fontName = string.IsNullOrWhiteSpace(obj.FontFamily) ? "Liberation Sans" : obj.FontFamily;
  if(fontName.Contains("Times", StringComparison.OrdinalIgnoreCase) ||
     fontName.Contains("Roman", StringComparison.OrdinalIgnoreCase) ||
     fontName.Contains("Serif", StringComparison.OrdinalIgnoreCase)) {
   try { DirectEditor.FontFamily = new FontFamily("Times New Roman, Liberation Serif, DejaVu Serif, FreeSerif, serif"); } catch {}
  } else {
   try { DirectEditor.FontFamily = new FontFamily($"{fontName}, Liberation Sans, Arial, DejaVu Sans, sans-serif"); } catch {}
  }

  DirectEditor.FontWeight = obj.Bold ? Avalonia.Media.FontWeight.Bold : Avalonia.Media.FontWeight.Normal;
  DirectEditor.FontStyle = obj.Italic ? Avalonia.Media.FontStyle.Italic : Avalonia.Media.FontStyle.Normal;
  try {
   var c = string.IsNullOrWhiteSpace(obj.Color) ? "#000000" : obj.Color;
   DirectEditor.Foreground = Brush.Parse(c);
  } catch {
   DirectEditor.Foreground = Brushes.Black;
  }
  FitEditorToBox(obj.Text, obj.FontSize * scale, rect);
  DirectEditor.Text = obj.Text;
  DirectEditor.IsVisible = true;

  DirectEditor.Focus();
  DirectEditor.SelectAll();
 }

 // Border (1px each side) of the inline editor; the template padding is 0.
 const double EditorChrome = 2;
 Rect editorBox;

 FormattedText MeasureEditorText(string text, double size) =>
  new(string.IsNullOrEmpty(text) ? " " : text, System.Globalization.CultureInfo.CurrentCulture, FlowDirection.LeftToRight,
   new Typeface(DirectEditor.FontFamily, DirectEditor.FontStyle, DirectEditor.FontWeight), size, Brushes.Black);

 /// <summary>
 /// Sizes the inline editor so the text keeps the original visual size: the font is never
 /// enlarged beyond the PDF size and is shrunk if the substitute font is wider than the
 /// original box. The box height follows the real line height so no scrollbar appears.
 /// </summary>
 void FitEditorToBox(string text, double desiredSize, Rect rect) {
  editorBox = rect;
  var size = Math.Max(1, desiredSize);
  var measured = MeasureEditorText(text, size);
  var textWidth = measured.WidthIncludingTrailingWhitespace;
  if(rect.Width > 0 && textWidth > rect.Width) {
   size = Math.Max(1, size * rect.Width / textWidth);
   measured = MeasureEditorText(text, size);
  }
  DirectEditor.FontSize = size;
  DirectEditor.MinHeight = 0;
  DirectEditor.MinWidth = 0;
  var height = Math.Max(rect.Height, measured.Height) + EditorChrome;
  var width = Math.Max(rect.Width, measured.WidthIncludingTrailingWhitespace) + EditorChrome + 4;
  Canvas.SetLeft(DirectEditor, rect.X - EditorChrome / 2);
  // Center the line vertically over the original box so the text stays in place.
  Canvas.SetTop(DirectEditor, Math.Max(0, rect.Y - (height - rect.Height) / 2));
  DirectEditor.Width = width;
  DirectEditor.Height = height;
 }

 /// <summary>Grows the editor while typing instead of scrolling, keeping the font size fixed.</summary>
 void GrowEditorToText() {
  var measured = MeasureEditorText(DirectEditor.Text ?? "", DirectEditor.FontSize);
  var width = Math.Max(editorBox.Width, measured.WidthIncludingTrailingWhitespace) + EditorChrome + 4;
  var height = Math.Max(editorBox.Height, measured.Height) + EditorChrome;
  if(Math.Abs(width - DirectEditor.Width) > 0.5) DirectEditor.Width = width;
  if(Math.Abs(height - DirectEditor.Height) > 0.5) DirectEditor.Height = height;
 }

 void DirectEditorKeyDown(object? sender, KeyEventArgs e) {
  if(e.Key == Key.Enter && !e.KeyModifiers.HasFlag(KeyModifiers.Shift)) {
   e.Handled = true;
   _ = CommitDirectEditAsync();
  } else if(e.Key == Key.Escape) {
   e.Handled = true;
   isDirectCanceling = true;
   DirectEditor.IsVisible = false;
   vm.ActiveEditingOriginal = null;
  }
 }

 void DirectEditorLostFocus(object? sender, RoutedEventArgs e) {
  if(!isCommitting && !isDirectCanceling && DirectEditor.IsVisible) {
   _ = CommitDirectEditAsync();
  }
 }

 async Task CommitDirectEditAsync() {
  if(isCommitting || isDirectCanceling || !DirectEditor.IsVisible) return;
  isCommitting = true;
  try {
   DirectEditor.IsVisible = false;
   var targetOrig = activeEditingOriginal;
   var targetGraph = activeEditingGraphic;
   activeEditingOriginal = null;
   activeEditingGraphic = null;
   vm.ActiveEditingOriginal = null;

   var newText = DirectEditor.Text ?? "";
   if(targetOrig is not null) {
    if(newText != targetOrig.Text) {
     await vm.CommitOriginalTextDirectAsync(targetOrig, newText, targetOrig.FontName, targetOrig.FontSize, null, targetOrig.Bold, targetOrig.Italic, targetOrig.Bounds.X, targetOrig.EffectiveBaseline);
    }
   } else if(targetGraph is not null) {
    if(newText != targetGraph.Text) {
     await vm.CommitGraphicTextDirectAsync(targetGraph, newText, targetGraph.FontSize, null, targetGraph.Bold, targetGraph.Italic);
    }
   }
  } finally {
   isCommitting = false;
  }
 }
}
