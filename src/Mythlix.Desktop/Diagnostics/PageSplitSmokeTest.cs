using Mythlix.Desktop.ViewModels;
using Mythlix.Infrastructure;
namespace Mythlix.Desktop.Diagnostics;
internal static class PageSplitSmokeTest {
 public static async Task RunAsync(WorkspaceViewModel vm) {
  var doc=vm.Selected??throw new InvalidOperationException("No hay documento.");
  var directory=Path.Combine(Path.GetTempPath(),"mythlix-split-smoke-"+Guid.NewGuid().ToString("N"));
  var originalPage=vm.CurrentPage;var revision=doc.Revision;var modified=doc.Modified;
  try {
   var paths=await Task.Run(()=>doc.Document.SplitAsync(directory,1));
   if(paths.Count!=doc.Document.PageCount)throw new InvalidOperationException("Número de partes incorrecto.");
   var renderer=new PopplerRenderer();
   for(var i=0;i<paths.Count;i++) {
    using var part=new PdfDocumentSession(paths[i]);
    if(part.PageCount!=1 || part.GetPageGeometry(1)!=doc.Document.GetPageGeometry(i+1))throw new InvalidOperationException("Geometría alterada.");
    var expected=await renderer.ExtractTextAsync(doc.WorkingPath,i+1,default);
    if(await renderer.ExtractTextAsync(paths[i],1,default)!=expected)throw new InvalidOperationException("Texto alterado.");
   }
   vm.CurrentPage=doc.Document.PageCount;await vm.RenderAsync();vm.CurrentPage=originalPage;await vm.RenderAsync();
   if(doc.Revision!=revision || doc.Modified!=modified || vm.PageImage is null)throw new InvalidOperationException("La división alteró el estado del editor.");
  } finally {if(Directory.Exists(directory))Directory.Delete(directory,true);}
 }
}
