using Mythlix.Desktop.ViewModels;
using Mythlix.Desktop.Views;
using Mythlix.Infrastructure;
using PdfSharp.Pdf;

namespace Mythlix.Desktop.Diagnostics;

internal static class CompositionPreviewSmokeTest {
 internal static async Task RunAsync(WorkspaceViewModel vm,MainWindow owner,bool testCommit=false,bool testReopen=false) {
  var path=Path.Combine(Path.GetTempPath(),"mythlix-composition-smoke-"+Guid.NewGuid().ToString("N")+".pdf");
  try {
   using(var pdf=new PdfDocument()) {
    var page=pdf.AddPage();var resources=new PdfDictionary(pdf);var fonts=new PdfDictionary(pdf);
    foreach(var (key,family) in new[]{("/F1","/Helvetica"),("/F2","/Helvetica-Bold")}) {
     var font=new PdfDictionary(pdf);font.Elements.SetName("/Type","/Font");font.Elements.SetName("/Subtype","/Type1");
     font.Elements.SetName("/BaseFont",family);font.Elements.SetName("/Encoding","/WinAnsiEncoding");
     pdf.Internals.AddObject(font);fonts.Elements[key]=font.Reference!;
    }
    resources.Elements["/Font"]=fonts;page.Elements["/Resources"]=resources;
    page.Contents.AppendContent().CreateStream(System.Text.Encoding.ASCII.GetBytes("BT 30 700 Td /F1 16 Tf (Normal ) Tj /F2 20 Tf (negrita) Tj ET"));
    pdf.Save(path);
   }
   await vm.RunAsync(()=>vm.OpenAsync(path));await vm.InspectTextRunsCommand.ExecuteAsync(null);
   var doc=vm.Selected!;var block=vm.SelectedInspectedBlock!;
   var session=(PdfDocumentSession)doc.Document;
   var snapshot=session.AnalyzeTextRuns(1,doc.Revision);var revision=doc.Revision;var undo=doc.Undo.Count;var modified=doc.Modified;
   var window=owner.CreateCompositionPreview();
   try {
    window.Show(owner);
    window.Compose();
    if(window.HasPreview) throw new InvalidOperationException("Vista creada sin fuentes explícitas.");
    foreach(var style in block.Runs.Select(r=>r.Run.Style)) {
     var name=$"Mythlix.Infrastructure.Fonts.LiberationSans-{(style.Bold?"Bold":"Regular")}.ttf";
     using var stream=typeof(PdfDocumentSession).Assembly.GetManifestResourceStream(name)!;
     using var memory=new MemoryStream();stream.CopyTo(memory);
     window.BindFont(style.Font,memory.ToArray(),name);
    }
    window.Compose();
    if(!window.HasPreview) throw new InvalidOperationException("Vista mixta no generada.");
    var after=session.AnalyzeTextRuns(1,doc.Revision);
    if(!snapshot.Runs.Select(r=>(r.Run.Text,r.Run.Style,r.Run.Sources[0].Source.StreamFingerprint)).SequenceEqual(
      after.Runs.Select(r=>(r.Run.Text,r.Run.Style,r.Run.Sources[0].Source.StreamFingerprint))) || revision!=doc.Revision || undo!=doc.Undo.Count || modified!=doc.Modified)
     throw new InvalidOperationException("La vista modificó el documento o el historial.");
    if(testCommit) {
     await window.ApplyAsync();
     if(doc.Revision!=revision || doc.Undo.Count!=undo) throw new InvalidOperationException("Se aplicó sin aceptar reflujo.");
     window.EditText("Normal añadido negrita",new(7,0));
     if(window.HasPreview || window.CanApply) throw new InvalidOperationException("Vista anterior reutilizada tras editar.");
     window.Close();
     if(doc.Revision!=revision || doc.Undo.Count!=undo || doc.Modified!=modified) throw new InvalidOperationException("Cancelar modificó el documento.");
     var editWindow=owner.CreateCompositionPreview();
     await vm.RunAsync(async()=> {
      editWindow.Show(owner);
      foreach(var style in block.Runs.Select(r=>r.Run.Style)) {
       var name=$"Mythlix.Infrastructure.Fonts.LiberationSans-{(style.Bold?"Bold":"Regular")}.ttf";
       using var stream=typeof(PdfDocumentSession).Assembly.GetManifestResourceStream(name)!;
       using var memory=new MemoryStream();stream.CopyTo(memory);editWindow.BindFont(style.Font,memory.ToArray(),name);
      }
      editWindow.DraftTextBox.SelectionStart=7;editWindow.DraftTextBox.SelectionEnd=7;
      editWindow.DraftTextBox.Text="Normal añadido negrita";
      editWindow.Compose();editWindow.AcceptReflow(true);
      if(!editWindow.CanApply) throw new InvalidOperationException("Confirmación válida no habilitada.");
      await Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(()=>{},Avalonia.Threading.DispatcherPriority.Background);
      editWindow.UpdateLayout();
      using(var image=new Avalonia.Media.Imaging.RenderTargetBitmap(new Avalonia.PixelSize((int)editWindow.Bounds.Width,(int)editWindow.Bounds.Height),new Avalonia.Vector(96,96))) {
       image.Render(editWindow);image.Save(Path.Combine(Path.GetTempPath(),"mythlix-composition-editor.png"),Avalonia.Media.Imaging.PngBitmapEncoderOptions.Default);
      }
      await editWindow.ApplyAsync();
     });
     if(editWindow.IsVisible || doc.Revision!=revision+1 || doc.Undo.Count!=undo+1 || !doc.Modified)
      throw new InvalidOperationException("Confirmación o historial incorrectos: "+vm.Status);
     var text=string.Concat(session.AnalyzeTextRuns(1,doc.Revision).Runs.Select(r=>r.Run.Text));
     if(text!="Normal añadido negrita") throw new InvalidOperationException("Texto mixto confirmado incorrecto.");
     if(string.Concat(session.AnalyzeTextRuns(1,doc.Revision).Runs.Where(r=>r.Run.Style.Bold).Select(r=>r.Run.Text))!="negrita")
      throw new InvalidOperationException("El control de edición no conservó el estilo de negrita.");
     await vm.RunAsync(()=>vm.HistoryAsync(false));
     if(string.Concat(session.AnalyzeTextRuns(1,doc.Revision).Runs.Select(r=>r.Run.Text))!="Normal negrita" || doc.Redo.Count!=1)
      throw new InvalidOperationException("Deshacer no recuperó el original.");
     await vm.RunAsync(()=>vm.HistoryAsync(true));
     if(string.Concat(session.AnalyzeTextRuns(1,doc.Revision).Runs.Select(r=>r.Run.Text))!=text || doc.Redo.Count!=0)
      throw new InvalidOperationException("Rehacer no recuperó la edición.");
     if(testReopen)await CheckReopenAsync(vm,owner,directory:Path.GetDirectoryName(path)!,originalText:text);
     return;
    }
    // Explicit diagnostic action: verify stale views close and release their glyphs.
    doc.Revision++;
    if(window.IsVisible || window.HasPreview) throw new InvalidOperationException("Vista obsoleta conservada tras cambiar revisión.");
   } finally {window.Close();}
  } finally {File.Delete(path);}
 }
 static async Task CheckReopenAsync(WorkspaceViewModel vm,MainWindow owner,string directory,string originalText) {
  var saved=Path.Combine(directory,"mythlix-reopened-"+Guid.NewGuid().ToString("N")+".pdf");
  try {
   await vm.RunAsync(async()=> {await vm.Selected!.Document.SaveCopyAsync(saved);await vm.OpenAsync(saved);await vm.RenderAsync();});
   await vm.InspectTextRunsCommand.ExecuteAsync(null);
   var doc=vm.Selected!;var session=(PdfDocumentSession)doc.Document;var block=vm.SelectedInspectedBlock!;
   if(block.Runs.Select(r=>r.BaselineY).Distinct().Count()<2)throw new InvalidOperationException("La prueba no abrió un párrafo de varias líneas.");
   var inspected=session.InspectParagraphDraft(1,doc.Revision,block.BlockId);
   if(!inspected.IsSupported || !inspected.IsManaged || inspected.Draft!.Text!=originalText)
    throw new InvalidOperationException("No se recuperó el texto lógico de la composición guardada.");
   var window=owner.CreateCompositionPreview();
   await vm.RunAsync(async()=> {
    window.Show(owner);
    foreach(var style in block.Runs.Select(r=>r.Run.Style).DistinctBy(s=>s.Font)) {
     var name=$"Mythlix.Infrastructure.Fonts.LiberationSans-{(style.Bold?"Bold":"Regular")}.ttf";
     using var stream=typeof(PdfDocumentSession).Assembly.GetManifestResourceStream(name)!;
     using var memory=new MemoryStream();stream.CopyTo(memory);window.BindFont(style.Font,memory.ToArray(),name);
    }
    if(window.DraftTextBox.Text!=originalText)throw new InvalidOperationException("El editor inventó saltos para los cortes suaves.");
    window.DraftTextBox.SelectionStart=originalText.Length;window.DraftTextBox.SelectionEnd=originalText.Length;
    window.DraftTextBox.Text=originalText+"\r\nOtra línea";
    window.Compose();window.AcceptReflow(true);
    if(!window.CanApply)throw new InvalidOperationException("No se habilitó la segunda edición del párrafo guardado.");
    await Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(()=>{},Avalonia.Threading.DispatcherPriority.Background);
    window.UpdateLayout();
    using(var image=new Avalonia.Media.Imaging.RenderTargetBitmap(new Avalonia.PixelSize((int)window.Bounds.Width,(int)window.Bounds.Height),new Avalonia.Vector(96,96))) {
     image.Render(window);image.Save(Path.Combine(Path.GetTempPath(),"mythlix-composition-reopened.png"),Avalonia.Media.Imaging.PngBitmapEncoderOptions.Default);
    }
    await window.ApplyAsync();
   });
   if(window.IsVisible || doc.Revision!=1 || doc.Undo.Count!=1)throw new InvalidOperationException("La segunda edición no se confirmó: "+vm.Status);
   string LogicalText() {
    var current=session.AnalyzeTextRuns(1,doc.Revision).Runs.First().Run.Sources[0].Source.BlockId;
    var recovered=session.InspectParagraphDraft(1,doc.Revision,current);
    if(!recovered.IsSupported)throw new InvalidOperationException("La composición perdió sus datos al cambiar el historial.");
    return recovered.Draft!.Text;
   }
   if(LogicalText()!=originalText+"\r\nOtra línea")throw new InvalidOperationException("El salto duro no sobrevivió a la escritura.");
   await vm.RunAsync(()=>vm.HistoryAsync(false));
   if(LogicalText()!=originalText)throw new InvalidOperationException("Deshacer no recuperó la composición multilínea anterior.");
   await vm.RunAsync(()=>vm.HistoryAsync(true));
   if(LogicalText()!=originalText+"\r\nOtra línea")throw new InvalidOperationException("Rehacer no recuperó el salto duro.");
  } finally {File.Delete(saved);}
 }

}
