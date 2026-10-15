using Mythlix.Desktop.ViewModels;
using Mythlix.Infrastructure;
namespace Mythlix.Desktop.Diagnostics;
internal static class ImagesToPdfSmokeTest {
 public static async Task RunAsync(WorkspaceViewModel vm) {
  var original=vm.Selected??throw new InvalidOperationException("No hay documento.");
  var image=Path.GetFullPath("tests/Mythlix.Tests/Fixtures/image.png");
  var destination=Path.Combine(Path.GetTempPath(),"mythlix-created-"+Guid.NewGuid().ToString("N")+".pdf");
  try {
   await Task.Run(()=>new ImagesToPdfConverter().ConvertAsync([image,image],destination));
   await vm.OpenAsync(destination);await vm.RenderAsync();
   if(vm.Selected?.Document.PageCount!=2 || vm.Selected.Modified || vm.PageImage is null || !vm.Documents.Contains(original))
    throw new InvalidOperationException("El documento convertido no se abrió correctamente.");
   vm.CloseSelected();await vm.RenderAsync();
   if(vm.Selected!=original || vm.PageImage is null)throw new InvalidOperationException("No se conservó la pestaña original.");
  } finally {if(File.Exists(destination))File.Delete(destination);}
 }
}
