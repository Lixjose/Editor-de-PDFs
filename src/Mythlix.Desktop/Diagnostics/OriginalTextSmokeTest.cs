using Avalonia;
using Avalonia.Controls;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using Mythlix.Desktop.ViewModels;
using Mythlix.Infrastructure;
namespace Mythlix.Desktop.Diagnostics;
internal static class OriginalTextSmokeTest {
 public static async Task RunAsync(WorkspaceViewModel vm,Window window) {
  var block=vm.OriginalBlocks.FirstOrDefault(b=>b.Text.StartsWith("Seleccione"))??throw new InvalidOperationException(vm.OriginalStatus);
  const string replacement="Texto original editado con Mythlix";
  vm.SelectedOriginal=block;vm.OriginalDraftText=replacement;
  await vm.ApplyOriginalCommand.ExecuteAsync(null);
  if(!vm.PageText.Contains(replacement)||vm.PageText.Contains(block.Text))throw new InvalidOperationException(vm.Status);
  await vm.RunAsync(()=>vm.HistoryAsync(false));if(!vm.PageText.Contains(block.Text))throw new InvalidOperationException("Deshacer no restauró el texto original.");
  await vm.RunAsync(()=>vm.HistoryAsync(true));if(!vm.PageText.Contains(replacement))throw new InvalidOperationException("Rehacer no restauró el cambio.");
  Directory.CreateDirectory("artifacts");var path=Path.GetFullPath("artifacts/phase3-edited.pdf");await vm.Selected!.Document.SaveCopyAsync(path);
  using(var reopened=new PdfDocumentSession(path))if(!reopened.AnalyzeText(1).Blocks.Any(b=>b.Text==replacement))throw new InvalidOperationException("La reapertura perdió la edición.");
  vm.SelectedOriginal=vm.OriginalBlocks.First(b=>b.Text==replacement);
  await Dispatcher.UIThread.InvokeAsync(()=> {
   window.UpdateLayout();using var bitmap=new RenderTargetBitmap(new PixelSize((int)window.ClientSize.Width,(int)window.ClientSize.Height));
   bitmap.Render(window);bitmap.Save("artifacts/phase3-preview.png",PngBitmapEncoderOptions.Default);
  },DispatcherPriority.Background);
 }
}
