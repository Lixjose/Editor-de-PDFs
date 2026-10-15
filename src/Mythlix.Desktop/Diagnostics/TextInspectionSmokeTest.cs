using Mythlix.Desktop.ViewModels;

namespace Mythlix.Desktop.Diagnostics;

internal static class TextInspectionSmokeTest {
 internal static async Task RunAsync(WorkspaceViewModel vm) {
  var doc=vm.Selected ?? throw new InvalidOperationException("Documento no abierto.");
  var revision=doc.Revision;var undoCount=doc.Undo.Count;var modified=doc.Modified;
  await vm.InspectTextRunsCommand.ExecuteAsync(null);
  if(vm.InspectedTextBlocks.Count==0) throw new InvalidOperationException(vm.Status+"; "+vm.TextInspectionStatus);
  if(doc.Revision!=revision || doc.Undo.Count!=undoCount || doc.Modified!=modified)
   throw new InvalidOperationException("La inspección modificó el documento o su historial.");
  if(vm.InspectedTextBlocks.Any(b=>!b.IsReadOnly)) throw new InvalidOperationException("Bloque de inspección escribible.");
  doc.Revision++;
  if(vm.InspectedTextBlocks.Count!=0) throw new InvalidOperationException("Informe obsoleto tras cambiar la revisión.");
  await vm.InspectTextRunsCommand.ExecuteAsync(null);
  await vm.RunAsync(()=> {
  if(doc.Document.PageCount>1) {
   vm.CurrentPage=2;
   if(vm.InspectedTextBlocks.Count!=0) throw new InvalidOperationException("Informe conservado al cambiar de página.");
   vm.CurrentPage=1;
  }
  var replacement=new DocumentViewModel(doc.Source);
  vm.Documents.Add(replacement);vm.Selected=replacement;
  if(vm.InspectedTextBlocks.Count!=0) throw new InvalidOperationException("Informe conservado al cambiar de documento.");
  return Task.CompletedTask;
  });
 }
}
