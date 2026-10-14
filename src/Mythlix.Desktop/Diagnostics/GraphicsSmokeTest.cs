using Avalonia;
using Avalonia.Controls;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using Mythlix.Core;
using Mythlix.Desktop.ViewModels;
using Mythlix.Infrastructure;
namespace Mythlix.Desktop.Diagnostics;

/// <summary>Opt-in end-to-end diagnostic, run only against the supplied verification PDF.</summary>
internal static class GraphicsSmokeTest {
 public static async Task RunAsync(WorkspaceViewModel vm,Window window,string imagePath) {
  if(vm.PageImage is null) throw new InvalidOperationException(vm.Status);
  vm.ObjectText="Edición gráfica con Mythlix";
  await vm.AddTextCommand.ExecuteAsync(null);
  Check(vm,1);
  vm.ObjectX=64;vm.ObjectY=160;vm.ObjectWidth=460;vm.ObjectHeight=60;vm.ObjectFontSize=22;vm.ObjectColor="#252532";vm.ObjectBold=true;
  await vm.ApplyGraphicCommand.ExecuteAsync(null);
  await vm.AddRectangleCommand.ExecuteAsync(null);Check(vm,2);
  await Move(vm,64,250,240,180);
  await vm.AddEllipseCommand.ExecuteAsync(null);Check(vm,3);
  await Move(vm,350,270,160,100);
  await vm.AddLineCommand.ExecuteAsync(null);Check(vm,4);
  await Move(vm,64,470,440,50);
  await vm.RunAsync(()=>vm.InsertImageAsync(File.ReadAllBytes(imagePath),1));Check(vm,5);
  await Move(vm,100,290,120,100);
  await vm.DuplicateGraphicCommand.ExecuteAsync(null);Check(vm,6);
  await vm.DeleteGraphicCommand.ExecuteAsync(null);Check(vm,5);
  await vm.RunAsync(()=>vm.HistoryAsync(false));Check(vm,6);
  await vm.RunAsync(()=>vm.HistoryAsync(true));Check(vm,5);
  vm.SelectedGraphic=vm.PageObjects.First(o=>o.Kind==GraphicKind.Text);
  var output=Path.Combine(Path.GetTempPath(),"mythlix-phase2-smoke.pdf");
  await vm.Selected!.Document.SaveCopyAsync(output);
  using(var reopened=new PdfDocumentSession(output)) {
   if(reopened.GetGraphics(1).Count!=5)throw new InvalidOperationException("Objetos no persistidos.");
  }
  if(!vm.PageText.Contains("Edición gráfica con Mythlix"))throw new InvalidOperationException("Texto no extraíble.");
  await Dispatcher.UIThread.InvokeAsync(()=> {
   window.UpdateLayout();
   using var bitmap=new RenderTargetBitmap(new PixelSize((int)window.ClientSize.Width,(int)window.ClientSize.Height));
   bitmap.Render(window);Directory.CreateDirectory("artifacts");bitmap.Save("artifacts/phase2-preview.png",PngBitmapEncoderOptions.Default);
  },DispatcherPriority.Background);
 }
 static async Task Move(WorkspaceViewModel vm,double x,double y,double width,double height) {
  vm.ObjectX=x;vm.ObjectY=y;vm.ObjectWidth=width;vm.ObjectHeight=height;
  await vm.ApplyGraphicCommand.ExecuteAsync(null);
  if(vm.SelectedGraphic is not {} obj || Math.Abs(obj.X-x)>.01 || Math.Abs(obj.Width-width)>.01)throw new InvalidOperationException(vm.Status);
 }
 static void Check(WorkspaceViewModel vm,int count) {if(vm.PageObjects.Count!=count)throw new InvalidOperationException("Resultado gráfico inesperado: "+vm.Status);}
}
