using Avalonia.Media.Imaging;
using Mythlix.Desktop.ViewModels;
using Mythlix.Infrastructure;
namespace Mythlix.Desktop.Diagnostics;
internal static class ImageExportSmokeTest {
 public static async Task RunAsync(WorkspaceViewModel vm) {
  var doc=vm.Selected??throw new InvalidOperationException("No hay documento.");
  var destination=Path.Combine(Path.GetTempPath(),"mythlix-image-smoke-"+Guid.NewGuid().ToString("N"));
  await vm.EditAsync("rotate");var revision=doc.Revision;
  try {
   var paths=await vm.ExportImagesAsync($"{doc.Document.PageCount},1",800,destination);
   if(paths.Count!=2)throw new InvalidOperationException("Exportación incompleta.");
   var expected=await new PopplerRenderer().RenderAsync(doc.WorkingPath,1,800,default);
   var exported=await File.ReadAllBytesAsync(paths[1]);
   if(!expected.SequenceEqual(exported))throw new InvalidOperationException("La exportación perdió las ediciones actuales.");
   foreach(var path in paths) {
    using var image=new Bitmap(path);
    if(Math.Max(image.PixelSize.Width,image.PixelSize.Height)!=800)throw new InvalidOperationException("Resolución incorrecta.");
   }
   if(doc.Revision!=revision || !doc.Modified)throw new InvalidOperationException("Exportar alteró el estado del documento.");
   await vm.HistoryAsync(false);
  } finally {if(Directory.Exists(destination))Directory.Delete(destination,true);}
 }
}
