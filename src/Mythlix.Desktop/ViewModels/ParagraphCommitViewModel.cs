using Mythlix.Core.TextEditing;

namespace Mythlix.Desktop.ViewModels;

public sealed partial class WorkspaceViewModel {
 bool paragraphCommitInProgress;
 /// <summary>Called inside the modal editor's existing RunAsync gate. Do not nest RunAsync here.</summary>
 public async Task ApplyParagraphCompositionAsync(DocumentViewModel owner,ParagraphRunBuffer draft,ParagraphCompositionSnapshot plan) {
  if(paragraphCommitInProgress) throw new InvalidOperationException("Ya hay una confirmación en curso.");
  if(Selected!=owner || CurrentPage!=plan.Context.PageNumber || owner.Revision!=plan.Context.Revision ||
   owner.Document is not IParagraphCompositionWriter writer)
   throw new InvalidOperationException("El documento, la página o la revisión cambió. Vuelva a analizar.");
  paragraphCommitInProgress=true;
  try {
   var revision=owner.Revision;
   var receipt=await Task.Run(()=>writer.CommitParagraphComposition(draft,plan,revision,true));
   owner.Undo.Push(receipt.UndoSnapshot.ToArray());owner.Redo.Clear();LimitUndo(owner);
   owner.Modified=true;owner.Revision=receipt.NextRevision;
   ClearOcr();
   try {await RenderAsync();Status="Texto mixto aplicado. Puede deshacer el cambio.";}
   catch(Exception ex) {Status="Texto aplicado y registrado en Deshacer; no se pudo actualizar la vista: "+ex.Message;}
  } finally {paragraphCommitInProgress=false;}
 }
}
