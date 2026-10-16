using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Styling;
using Mythlix.Core;
namespace Mythlix.Desktop.Views;
public partial class MainWindow {
 double pagesWidth=260,inspectorWidth=328;
 void ToggleStudioTheme(object? sender,RoutedEventArgs e) {
  var app=Application.Current!;
  app.RequestedThemeVariant=ActualThemeVariant==ThemeVariant.Dark?ThemeVariant.Light:ThemeVariant.Dark;
  PdfCanvas.InvalidateVisual();
  ThemeLabel.Text=app.RequestedThemeVariant==ThemeVariant.Light?"Oscuro":"Claro";
 }
 void TogglePagesPanel(object? sender,RoutedEventArgs e) {
  var columns=StudioWorkspace.ColumnDefinitions;
  if(PagesPanel.IsVisible) {pagesWidth=Math.Clamp(columns[0].ActualWidth,220,360);columns[0].MinWidth=0;PagesPanel.IsVisible=false;PagesSplitter.IsVisible=false;columns[0].Width=new(0);columns[1].Width=new(0);}
  else {columns[0].MinWidth=220;columns[0].Width=new(pagesWidth);columns[1].Width=new(4);PagesPanel.IsVisible=true;PagesSplitter.IsVisible=true;}
 }
 void ToggleInspectorPanel(object? sender,RoutedEventArgs e) {
  var columns=StudioWorkspace.ColumnDefinitions;
  if(InspectorPanel.IsVisible) {inspectorWidth=Math.Clamp(columns[4].ActualWidth,280,440);columns[4].MinWidth=0;InspectorPanel.IsVisible=false;InspectorSplitter.IsVisible=false;columns[4].Width=new(0);columns[3].Width=new(0);}
  else {columns[4].MinWidth=280;columns[4].Width=new(inspectorWidth);columns[3].Width=new(4);InspectorPanel.IsVisible=true;InspectorSplitter.IsVisible=true;}
 }
 void UpdateStudioGeometry() => StudioGeometry.Text=vm.Selected is null?"":string.Format(CultureInfo.InvariantCulture,"{0:0.#} × {1:0.#} pt",vm.Geometry.Width,vm.Geometry.Height);
 void TrackStudioPointer(object? sender,PointerEventArgs e) {
  if(vm.Selected is null || PdfCanvas.Bounds.Width<=0)return;
  var point=e.GetPosition(PdfCanvas);var scale=PdfCanvas.Bounds.Width/vm.Geometry.DisplayWidth;
  var page=vm.Geometry.FromDisplay(new ObjectBounds(point.X/scale,point.Y/scale,0,0));
  CursorPosition.Text=string.Format(CultureInfo.InvariantCulture,"X: {0:0.#}  Y: {1:0.#} pt",page.X,page.Y);
 }
 void SetStudioTool(bool original) {SelectTool.Classes.Set("active",!original);OriginalTool.Classes.Set("active",original);}
 void UpdateStudioAlignment() {
  var buttons=new[]{OriginalAlignLeft,OriginalAlignCenter,OriginalAlignRight,OriginalAlignJustify};
  for(var i=0;i<buttons.Length;i++)buttons[i].IsChecked=vm.OriginalDraftAlignmentIndex==i;
 }
 void StudioAlignmentClick(object? sender,RoutedEventArgs e) {
  if(sender is Avalonia.Controls.Primitives.ToggleButton button && int.TryParse(button.Tag?.ToString(),out var index))vm.OriginalDraftAlignmentIndex=index;
  UpdateStudioAlignment();
 }
 internal async Task CheckStudioUiAsync() {
  var doc=vm.Selected??throw new InvalidOperationException("Documento no abierto.");
  var revision=doc.Revision;var undo=doc.Undo.Count;var modified=doc.Modified;
  vm.SelectedOriginal=vm.OriginalBlocks.FirstOrDefault(b=>b.Text.StartsWith("Seleccione"))??vm.OriginalBlocks.FirstOrDefault();
  var directory=Path.Combine(Path.GetTempPath(),"mythlix-studio-ui");Directory.CreateDirectory(directory);
  async Task Capture(string name) {
   PdfCanvas.InvalidateVisual();
   await Task.Delay(180);
   await Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(()=>{},Avalonia.Threading.DispatcherPriority.Background);
   UpdateLayout();
   using var image=new Avalonia.Media.Imaging.RenderTargetBitmap(new PixelSize((int)ClientSize.Width,(int)ClientSize.Height),new Vector(96,96));
   image.Render(this);image.Save(Path.Combine(directory,name+".png"),Avalonia.Media.Imaging.PngBitmapEncoderOptions.Default);
  }
  await Capture("dark");
  ToggleStudioTheme(null,new RoutedEventArgs());await Capture("light");
  if(ActualThemeVariant!=ThemeVariant.Light)throw new InvalidOperationException("No se aplicó el tema claro.");
  TogglePagesPanel(null,new RoutedEventArgs());ToggleInspectorPanel(null,new RoutedEventArgs());
  await Capture("canvas-only");
  if(PagesPanel.IsVisible || InspectorPanel.IsVisible)throw new InvalidOperationException("Los paneles no se ocultaron.");
  TogglePagesPanel(null,new RoutedEventArgs());ToggleInspectorPanel(null,new RoutedEventArgs());
  MaxWidth=1040;MaxHeight=760;Width=1040;Height=760;await Capture("compact");
  if(!PagesPanel.IsVisible || !InspectorPanel.IsVisible || Viewport.Bounds.Width<100)throw new InvalidOperationException("Distribución compacta inválida.");
  if(vm.SelectedOriginal is {} orig) {
   var factor=PdfCanvas.Bounds.Width/vm.Geometry.DisplayWidth;
   var box=vm.Geometry.ToDisplay(orig.Bounds);
   OnTextDoubleClicked(orig,new Rect(box.X*factor,box.Y*factor,box.Width*factor,box.Height*factor));
   if(!DirectEditor.IsVisible)throw new InvalidOperationException("El editor directo no se activó.");
   var left=Canvas.GetLeft(DirectEditor);
   if(Math.Abs(left-box.X*factor)>0.01)throw new InvalidOperationException("El editor directo está desplazado en X.");
   await Capture("direct-edit");
   DirectEditor.IsVisible=false;
   vm.ActiveEditingOriginal=null;
  }
  if(doc.Revision!=revision || doc.Undo.Count!=undo || doc.Modified!=modified)throw new InvalidOperationException("La interfaz modificó el documento.");
 }

}
