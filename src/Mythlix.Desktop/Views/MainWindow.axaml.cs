using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using Mythlix.Desktop.ViewModels;
namespace Mythlix.Desktop.Views;
public partial class MainWindow : Window {
 readonly WorkspaceViewModel vm = new();
 bool closing;
 bool autoFit=true;
 public MainWindow() {
  InitializeComponent(); DataContext=vm; Closing += OnClosing;
  vm.PropertyChanged+=(_,e)=>{if(autoFit && e.PropertyName is nameof(vm.PageImage) or nameof(vm.Geometry))FitPageToView();};
  Viewport.PropertyChanged+=(_,e)=>{if(autoFit && e.Property==BoundsProperty)FitPageToView();};
  Opened += async (_,_) => {
   var args=Environment.GetCommandLineArgs().Skip(1).ToArray();
   var path=args.FirstOrDefault(a=>a.EndsWith(".pdf",StringComparison.OrdinalIgnoreCase));
   if(path is null) return;
   await Execute(async()=>{await vm.OpenAsync(path);await vm.RenderAsync();});
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
  vm.Zoom=Math.Clamp(Math.Min((Viewport.Bounds.Width-68)/720,(Viewport.Bounds.Height-80)/(720*vm.Geometry.DisplayHeight/vm.Geometry.DisplayWidth))*100,25,300);
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
}
