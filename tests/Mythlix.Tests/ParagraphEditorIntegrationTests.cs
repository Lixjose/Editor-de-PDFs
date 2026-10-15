using System.Text;
using Mythlix.Core;
using Mythlix.Core.TextEditing;
using Mythlix.Desktop.ViewModels;
using Mythlix.Infrastructure;
using PdfSharp.Pdf;
using Xunit;

namespace Mythlix.Tests;

public sealed class ParagraphEditorIntegrationTests : IDisposable {
 readonly string directory=Path.Combine(Path.GetTempPath(),"mythlix-ui-commit-"+Guid.NewGuid().ToString("N"));
 readonly string path;
 static ParagraphEditorIntegrationTests()=>AvaloniaTestRuntime.Ensure();
 public ParagraphEditorIntegrationTests() {Directory.CreateDirectory(directory);path=Path.Combine(directory,"mixed.pdf");}
 void Create(string text="BT /F1 12 Tf 50 700 Td (AB ) Tj /F2 12 Tf (CD EF) Tj ET") {
  using var pdf=new PdfDocument();var page=pdf.AddPage();page.Width=PdfSharp.Drawing.XUnit.FromPoint(612);page.Height=PdfSharp.Drawing.XUnit.FromPoint(792);
  var resources=new PdfDictionary(pdf);var fonts=new PdfDictionary(pdf);
  foreach(var (key,family) in new[]{("/F1","/Courier"),("/F2","/Courier-Bold")}) {
   var font=new PdfDictionary(pdf);font.Elements.SetName("/Type","/Font");font.Elements.SetName("/Subtype","/Type1");font.Elements.SetName("/BaseFont",family);font.Elements.SetName("/Encoding","/WinAnsiEncoding");
   pdf.Internals.AddObject(font);fonts.Elements[key]=font.Reference!;
  }
  resources.Elements["/Font"]=fonts;page.Elements["/Resources"]=resources;page.Contents.AppendContent().CreateStream(Encoding.ASCII.GetBytes(text));pdf.Save(path);
 }
 static ParagraphRunBuffer Draft(PdfDocumentSession doc)=>ParagraphRunBuffer.FromRuns(doc.AnalyzeTextRuns(1,0).Runs.Where(r=>r.Run.Sources[0].Source.BlockId=="0:0").Select(r=>r.Run));
 static ParagraphCompositionSnapshot Plan(PdfDocumentSession doc,ParagraphRunBuffer draft)=>ParagraphComposer.Compose(draft,new(new(50,100,100,120),120,20),doc.CreateRunMetricsProvider(1,0));
 static string Text(PdfDocumentSession doc,long revision)=>string.Concat(doc.AnalyzeTextRuns(1,revision).Runs.Select(r=>r.Run.Text));
 [Fact] public void MinimalEditsAndDraftHistoryPreserveOriginalStylesAndInventory() {
  Create();using var doc=new PdfDocumentSession(path);var original=Draft(doc);
  var editor=new ParagraphDraftEditor(original,doc.CreateRunEncodingResolver(1,0));
  var edit=editor.UpdateText("AB CX EF");Assert.True(edit.Applied);
  Assert.Same(original.Runs[0],editor.Buffer.Runs[0]);
  Assert.All(editor.Buffer.Runs.Skip(1),r=>Assert.True(r.Style.Bold));
  Assert.Equal(original.OriginalSources,editor.Buffer.OriginalSources);
  var updated=editor.Buffer;Assert.True(editor.Undo());Assert.Same(original,editor.Buffer);
  Assert.True(editor.Redo());Assert.Same(updated,editor.Buffer);
  editor.UpdateText("AB CX EF!");Assert.False(editor.Redo());
 }
 [Fact] public void ExplicitSelectionDisambiguatesRepeatedTextAtStyleBoundaries() {
  Create("BT /F1 12 Tf 50 700 Td (AA) Tj /F2 12 Tf (AA) Tj ET");using var doc=new PdfDocumentSession(path);
  var editor=new ParagraphDraftEditor(Draft(doc),doc.CreateRunEncodingResolver(1,0));
  Assert.True(editor.UpdateText("AAAAA",new(0,0)).Applied);
  Assert.Equal("AAA",string.Concat(editor.Buffer.Runs.Where(r=>!r.Style.Bold).Select(r=>r.Text)));
  Assert.Equal("AA",string.Concat(editor.Buffer.Runs.Where(r=>r.Style.Bold).Select(r=>r.Text)));
 }
 [Fact] public void GraphemeEditsRetainUnsupportedTextForCorrectionAndUndoRestoresStyles() {
  Create();using var doc=new PdfDocumentSession(path);var original=Draft(doc);
  var editor=new ParagraphDraftEditor(original,doc.CreateRunEncodingResolver(1,0));
  var combined=editor.UpdateText("A\u0301B CD EF");Assert.True(combined.Applied);Assert.False(combined.IsEncodingValid);
  var emoji=editor.UpdateText("😀B CD EF");Assert.True(emoji.Applied);Assert.False(emoji.IsEncodingValid);
  var fixedText=editor.UpdateText("AB CD EF");Assert.True(fixedText.Applied);Assert.True(fixedText.IsEncodingValid);
  Assert.True(editor.Undo());Assert.Equal("😀B CD EF",editor.Buffer.Text);
  Assert.True(editor.Undo());Assert.Equal("A\u0301B CD EF",editor.Buffer.Text);
  Assert.True(editor.Undo());Assert.Same(original,editor.Buffer);
 }
 [Fact] public async Task ApplyCreatesExactlyOneHistoryEntryAndUndoRedoRestoreMixedText() {
  Create();using var vm=new WorkspaceViewModel();await vm.OpenAsync(path);
  var owner=vm.Selected!;var doc=(PdfDocumentSession)owner.Document;var original=Draft(doc);
  var editor=new ParagraphDraftEditor(original,doc.CreateRunEncodingResolver(1,0));editor.UpdateText("AB nuevo CD EF",new(3,0));
  var edited=editor.Buffer;await vm.ApplyParagraphCompositionAsync(owner,edited,Plan(doc,edited));
  Assert.Single(owner.Undo);Assert.Empty(owner.Redo);Assert.Equal(1,owner.Revision);Assert.True(owner.Modified);
  Assert.Equal(edited.Text,Text(doc,1));Assert.NotNull(vm.PageImage);
  await vm.HistoryAsync(false);Assert.Empty(owner.Undo);Assert.Single(owner.Redo);Assert.Equal(original.Text,Text(doc,2));
  await vm.HistoryAsync(true);Assert.Single(owner.Undo);Assert.Empty(owner.Redo);Assert.Equal(edited.Text,Text(doc,3));
 }
 [Fact] public async Task RejectedCommitPreservesRevisionUndoRedoAndModifiedFlag() {
  Create("BT /F1 12 Tf 50 700 Td (AB ) Tj /F2 12 Tf (CD EF) Tj ET BT 60 650 Td (Vecino) Tj ET");
  using var vm=new WorkspaceViewModel();await vm.OpenAsync(path);var owner=vm.Selected!;var doc=(PdfDocumentSession)owner.Document;
  owner.Redo.Push(doc.Snapshot());var redo=owner.Redo.Peek();var draft=Draft(doc);var plan=Plan(doc,draft);var before=Text(doc,0);
  await Assert.ThrowsAsync<InvalidOperationException>(()=>vm.ApplyParagraphCompositionAsync(owner,draft,plan));
  Assert.Equal(before,Text(doc,0));Assert.Equal(0,owner.Revision);Assert.False(owner.Modified);Assert.Empty(owner.Undo);Assert.Same(redo,owner.Redo.Peek());
 }
 [Fact] public async Task StaleOwnerAndRevisionCannotCreateHistory() {
  Create();using var vm=new WorkspaceViewModel();await vm.OpenAsync(path);var owner=vm.Selected!;var doc=(PdfDocumentSession)owner.Document;
  var draft=Draft(doc);var plan=Plan(doc,draft);owner.Revision++;
  await Assert.ThrowsAsync<InvalidOperationException>(()=>vm.ApplyParagraphCompositionAsync(owner,draft,plan));
  owner.Revision=0;vm.Selected=null;
  await Assert.ThrowsAsync<InvalidOperationException>(()=>vm.ApplyParagraphCompositionAsync(owner,draft,plan));
  Assert.Empty(owner.Undo);Assert.Empty(owner.Redo);Assert.False(owner.Modified);
 }
 public void Dispose()=>Directory.Delete(directory,true);
}
