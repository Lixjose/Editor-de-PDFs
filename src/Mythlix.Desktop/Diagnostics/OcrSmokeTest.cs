using Avalonia;
using Avalonia.Controls;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using Mythlix.Desktop.ViewModels;
using Mythlix.Infrastructure;
namespace Mythlix.Desktop.Diagnostics;
internal static class OcrSmokeTest {
 public static async Task RunAsync(WorkspaceViewModel vm,Window window) {
  if(!string.IsNullOrWhiteSpace(vm.PageText))throw new InvalidOperationException("La entrada de prueba debe ser un escaneo sin texto.");
  await vm.PrepareOcrAsync();vm.OcrLanguage=vm.OcrLanguages.First(l=>l.Code=="spa+eng");
  vm.SelectedOcrMode=vm.OcrModes.First(m=>m.Mode==Mythlix.Core.OcrPageMode.UniformBlock);
  vm.OcrPageRange="1";await vm.RecognizeRangeCommand.ExecuteAsync(null);
  if(vm.OcrPages.Count==0 || vm.OcrPages.Sum(p=>p.Words.Count)==0)throw new InvalidOperationException(vm.Status);
  if(!vm.OcrExportText.Contains('\n') || !vm.OcrReviewSummary.Contains("palabras"))throw new InvalidOperationException("Exportación o resumen OCR incompleto.");
  var count=vm.OcrPages.Count;vm.OcrPageRange="0";await vm.RecognizeRangeCommand.ExecuteAsync(null);
  if(vm.OcrPages.Count!=count)throw new InvalidOperationException("Un intervalo inválido borró la revisión.");
  vm.OcrPageRange="1";vm.Status=vm.OcrStatus;
  vm.SelectedOcrPage!.Words[0].Text="CORREGIDO";
  Directory.CreateDirectory("artifacts");await File.WriteAllTextAsync("artifacts/phase4-ocr.txt",vm.OcrExportText);
  await vm.ExportOcrReconstructionAsync(Path.GetFullPath("artifacts/phase4-editable.pdf"));
  await Dispatcher.UIThread.InvokeAsync(()=> {
   window.UpdateLayout();using var image=new RenderTargetBitmap(new PixelSize((int)window.ClientSize.Width,(int)window.ClientSize.Height));image.Render(window);image.Save("artifacts/phase4-preview.png",PngBitmapEncoderOptions.Default);
  },DispatcherPriority.Background);
  await vm.ApplyOcrCommand.ExecuteAsync(null);
  if(!vm.PageText.Contains("CORREGIDO"))throw new InvalidOperationException(vm.Status);
  await vm.RunAsync(()=>vm.HistoryAsync(false));if(!string.IsNullOrWhiteSpace(vm.PageText))throw new InvalidOperationException("Deshacer no retiró la capa OCR.");
  await vm.RunAsync(()=>vm.HistoryAsync(true));if(!vm.PageText.Contains("CORREGIDO"))throw new InvalidOperationException("Rehacer no recuperó OCR.");
  await vm.Selected!.Document.SaveCopyAsync(Path.GetFullPath("artifacts/phase4-searchable.pdf"));
  await VerifyPartialReviewAsync(vm.Selected!.Source);
  using var reopened=new PdfDocumentSession("artifacts/phase4-editable.pdf");if(reopened.GetGraphics(1).Count==0)throw new InvalidOperationException("La reconstrucción no contiene objetos editables.");
 }
 static async Task VerifyPartialReviewAsync(string source) {
  var path=Path.Combine(Path.GetTempPath(),"mythlix-ocr-partial-"+Guid.NewGuid().ToString("N")+".pdf");
  try {
   using(var doc=new PdfDocumentSession(source)){doc.AddBlank();await doc.SaveCopyAsync(path);}
   using(var review=new WorkspaceViewModel()) {
    await review.OpenAsync(path);await review.PrepareOcrAsync();review.SkipExistingText=false;
    review.PropertyChanged+=(_,e)=>{if(e.PropertyName==nameof(review.OcrProgress) && review.OcrProgress>=50 && review.OcrRunning)review.CancelOcrCommand.Execute(null);};
    await review.RecognizeDocumentCommand.ExecuteAsync(null);
    if(review.OcrPages.Count!=1 || review.Selected!.Modified || !review.OcrStatus.Contains("cancelado"))throw new InvalidOperationException("La cancelación no conservó la revisión parcial sin modificar el PDF.");
   }
   using(var doc=new PdfDocumentSession(path)){doc.Rotate(2);await doc.SaveCopyAsync(path);}
   using(var review=new WorkspaceViewModel()) {
    await review.OpenAsync(path);await review.PrepareOcrAsync();review.SkipExistingText=false;
    await review.RecognizeDocumentCommand.ExecuteAsync(null);
    if(review.OcrPages.Count!=1 || review.Selected!.Modified || !review.OcrStatus.Contains("Se conservan"))throw new InvalidOperationException("Un fallo posterior borró la revisión parcial.");
    await review.ApplyOcrCommand.ExecuteAsync(null);
    if(!review.Selected.Modified || string.IsNullOrWhiteSpace(review.PageText))throw new InvalidOperationException("La revisión parcial no se pudo aplicar.");
   }
  } finally {if(File.Exists(path))File.Delete(path);}
 }

}
