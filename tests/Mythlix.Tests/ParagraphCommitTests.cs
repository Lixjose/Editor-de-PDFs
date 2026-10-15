using System.Text;
using Mythlix.Core;
using Mythlix.Core.TextEditing;
using Mythlix.Infrastructure;
using PdfSharp.Pdf;
using PdfSharp.Pdf.IO;
using Xunit;

namespace Mythlix.Tests;

public sealed class ParagraphCommitTests : IDisposable {
 readonly string directory=Path.Combine(Path.GetTempPath(),"mythlix-commit-"+Guid.NewGuid().ToString("N"));
 readonly string path;
 public ParagraphCommitTests() {Directory.CreateDirectory(directory);path=Path.Combine(directory,"source.pdf");}
 PdfDocumentSession Open(string extra="",bool shared=false) {
  using(var pdf=new PdfDocument()) {
   var page=pdf.AddPage();page.Width=PdfSharp.Drawing.XUnit.FromPoint(612);page.Height=PdfSharp.Drawing.XUnit.FromPoint(792);
   var resources=new PdfDictionary(pdf);var fonts=new PdfDictionary(pdf);
   foreach(var (key,family) in new[]{("/F1","/Courier"),("/F2","/Courier-Bold")}) {
    var font=new PdfDictionary(pdf);font.Elements.SetName("/Type","/Font");font.Elements.SetName("/Subtype","/Type1");
    font.Elements.SetName("/BaseFont",family);font.Elements.SetName("/Encoding","/WinAnsiEncoding");
    pdf.Internals.AddObject(font);fonts.Elements[key]=font.Reference!;
   }
   resources.Elements["/Font"]=fonts;page.Elements["/Resources"]=resources;
   page.Contents.AppendContent().CreateStream(Encoding.ASCII.GetBytes("BT /F1 12 Tf 0.7 Tc 2 Tw 80 Tz 50 700 Td (AB ) Tj /F2 12 Tf 3 Ts 0.8 0 0 rg (CD EF GH) Tj ET\n"+extra));
   if(shared) {
    var second=pdf.AddPage();second.Width=page.Width;second.Height=page.Height;
    var sharedContents=new PdfArray(pdf);
    foreach(var item in page.Contents.Elements) sharedContents.Elements.Add(item);
    pdf.Internals.AddObject(sharedContents);
    page.Elements["/Contents"]=sharedContents.Reference!;
    second.Elements["/Resources"]=resources;second.Elements["/Contents"]=sharedContents.Reference!;
   }
   pdf.Save(path);
  }
  return new(path);
 }
 static ParagraphRunBuffer Draft(PdfDocumentSession doc)=>ParagraphRunBuffer.FromRuns(doc.AnalyzeTextRuns(1,7).Runs.Where(r=>r.Run.Sources[0].Source.BlockId=="0:0").Select(r=>r.Run));
 static ParagraphCompositionOptions Options(TextAlignment align=TextAlignment.Left)=>new(new(50,100,75,120),120,20,align);
 static ParagraphCompositionSnapshot Plan(PdfDocumentSession doc,ParagraphRunBuffer draft,TextAlignment align=TextAlignment.Left)=>ParagraphComposer.Compose(draft,Options(align),doc.CreateRunMetricsProvider(1,7));
 static string Live(PdfDocumentSession doc,int page=1)=>string.Join("|",doc.AnalyzeTextRuns(page,7).Runs.Select(r=>$"{r.Run.Text}/{r.Run.Style}/{r.BaselineX}/{r.BaselineY}/{r.Run.Sources[0].Source.StreamFingerprint}"));

 [Theory]
 [InlineData(TextAlignment.Left)] [InlineData(TextAlignment.Center)] [InlineData(TextAlignment.Right)] [InlineData(TextAlignment.Justify)]
 public async Task CommitsSharedPlanAndUndoRestoresSharedPages(TextAlignment align) {
  using var doc=Open(shared:true);var original=Draft(doc);var provider=doc.CreateRunMetricsProvider(1,7);
  var draft=original.InsertText(3,"nuevo ñ ",provider).Buffer;
  var plan=Plan(doc,draft,align);Assert.True(plan.IsValid);Assert.True(plan.Lines.Length>1);
  var page2=Live(doc,2);
  var renderer=new PopplerRenderer();
  var page2Pixels=await renderer.RenderAsync(path,2,612,CancellationToken.None);
  var receipt=doc.CommitParagraphComposition(draft,plan,7,true);
  Assert.Equal(7,receipt.PreviousRevision);Assert.Equal(8,receipt.NextRevision);
  Assert.Equal(draft.Text,string.Concat(doc.AnalyzeTextRuns(1,8).Runs.Select(r=>r.Run.Text)));
  Assert.Equal(page2,Live(doc,2));
  var saved=Path.Combine(directory,"written.pdf");await doc.SaveCopyAsync(saved);
  using(var expectedImage=SkiaSharp.SKBitmap.Decode(page2Pixels))
  using(var actualImage=SkiaSharp.SKBitmap.Decode(await renderer.RenderAsync(saved,2,612,CancellationToken.None)))
   Assert.Equal(expectedImage.Bytes,actualImage.Bytes);
  var rendered=await renderer.RenderAsync(saved,1,612,CancellationToken.None);
  await File.WriteAllBytesAsync(Path.Combine(Path.GetTempPath(),$"mythlix-phase2c-{align}.png"),rendered);
  using(var image=SkiaSharp.SKBitmap.Decode(rendered))
   Assert.Equal(0,Enumerable.Range(0,image.Width*(int)Math.Floor(100*image.Height/792d)).Count(i=>image.GetPixel(i%image.Width,i/image.Width)!=SkiaSharp.SKColors.White));
  using(var reopened=new PdfDocumentSession(saved)) Assert.Equal(draft.Text,string.Concat(reopened.AnalyzeTextRuns(1,8).Runs.Select(r=>r.Run.Text)));
  doc.Restore(receipt.UndoSnapshot.ToArray());
  Assert.Equal(original.Text,string.Concat(doc.AnalyzeTextRuns(1,7).Runs.Select(r=>r.Run.Text)));
  Assert.Equal(page2,Live(doc,2));
 }
 [Fact] public void RejectsStaleRevisionForeignDocumentAndImplicitReflowWithoutChangingContent() {
  using var doc=Open();var draft=Draft(doc);var plan=Plan(doc,draft);var before=Live(doc);
  Assert.Throws<InvalidOperationException>(()=>doc.CommitParagraphComposition(draft,plan,8,true));
  Assert.Throws<InvalidOperationException>(()=>doc.CommitParagraphComposition(draft,plan,7,false));
  using var foreign=new PdfDocumentSession(path);
  Assert.Throws<InvalidOperationException>(()=>foreign.CommitParagraphComposition(draft,plan,7,true));
  Assert.Equal(before,Live(doc));
 }
 [Fact] public void RejectsIncompleteInventoriesAndForgedGeometry() {
  using var doc=Open();var draft=Draft(doc);var plan=Plan(doc,draft);var before=Live(doc);
  var partial=ParagraphRunBuffer.FromRuns(draft.Runs.Take(1));var partialPlan=Plan(doc,partial);
  Assert.Throws<InvalidOperationException>(()=>doc.CommitParagraphComposition(partial,partialPlan,7,true));
  var first=plan.Lines[0];var shifted=first.Segments[0] with {StartX=300};
  var forged=plan with {Lines=plan.Lines.SetItem(0,first with {Segments=first.Segments.SetItem(0,shifted)})};
  Assert.Throws<InvalidOperationException>(()=>doc.CommitParagraphComposition(draft,forged,7,true));
  Assert.Equal(before,Live(doc));
 }
 [Theory]
 [InlineData("BT /F1 12 Tf 60 650 Td (Vecino) Tj ET")]
 [InlineData("0 0 1 rg 0 0 612 792 re f")]
 public void RejectsTextAndGraphicsCollisions(string extra) {
  using var doc=Open(extra);var draft=Draft(doc);var plan=Plan(doc,draft);var before=Live(doc);
  Assert.Throws<InvalidOperationException>(()=>doc.CommitParagraphComposition(draft,plan,7,true));
  Assert.Equal(before,Live(doc));
 }
 [Fact] public void DetectsFontChangesEvenWhenWidthsAreEqual() {
  using var doc=Open();var draft=Draft(doc);var plan=Plan(doc,draft);
  using(var changed=PdfReader.Open(path,PdfDocumentOpenMode.Modify)) {
   changed.Pages[0].Resources.Elements.GetDictionary("/Font")!.Elements.GetDictionary("/F1")!.Elements.SetName("/BaseFont","/Courier-Oblique");
   using var memory=new MemoryStream();changed.Save(memory,false);doc.Restore(memory.ToArray());
  }
  var before=Live(doc);
  Assert.Throws<InvalidOperationException>(()=>doc.CommitParagraphComposition(draft,plan,7,true));
  Assert.Equal(before,Live(doc));
 }
 [Fact] public async Task ReplacementRemovesOriginalSearchableTextAndPreservesDistantContent() {
  using var doc=Open("BT 400 100 Td (Intacto) Tj ET");var original=Draft(doc);
  var draft=original.ReplaceRange(new(0,original.Text.Length),"Solo nuevo",doc.CreateRunMetricsProvider(1,7)).Buffer;
  var plan=Plan(doc,draft);doc.CommitParagraphComposition(draft,plan,7,true);
  var output=Path.Combine(directory,"replacement.pdf");await doc.SaveCopyAsync(output);
  var text=await new PopplerRenderer().ExtractTextAsync(output,1,CancellationToken.None);
  Assert.DoesNotContain("AB",text);Assert.DoesNotContain("CD EF GH",text);
  Assert.Contains("Solo",text);Assert.Contains("nuevo",text);Assert.Contains("Intacto",text);
 }
 [Fact] public void RejectsChangedStreamAndOutsidePageBounds() {
  using var doc=Open();var draft=Draft(doc);var plan=Plan(doc,draft);
  var outside=ParagraphComposer.Compose(draft,new(new(600,100,75,120),120,20),doc.CreateRunMetricsProvider(1,7));
  Assert.Throws<InvalidOperationException>(()=>doc.CommitParagraphComposition(draft,outside,7,true));
  using(var changed=PdfReader.Open(path,PdfDocumentOpenMode.Modify)) {
   var replacement=new PdfDictionary(changed);
   replacement.CreateStream(Encoding.ASCII.GetBytes("BT /F1 12 Tf 50 700 Td (Cambiado) Tj ET"));
   changed.Internals.AddObject(replacement);changed.Pages[0].Contents.Elements[0]=replacement.Reference!;
   using var memory=new MemoryStream();changed.Save(memory,false);doc.Restore(memory.ToArray());
  }
  var before=Live(doc);
  Assert.Throws<InvalidOperationException>(()=>doc.CommitParagraphComposition(draft,plan,7,true));
  Assert.Equal(before,Live(doc));
 }
 [Fact] public void EmptyParagraphDeletesAllOriginalStringsAndUndoRestoresThem() {
  using var doc=Open();var original=Draft(doc);
  var draft=original.DeleteRange(new(0,original.Text.Length),doc.CreateRunMetricsProvider(1,7)).Buffer;
  var plan=Plan(doc,draft);Assert.True(plan.IsValid);
  var receipt=doc.CommitParagraphComposition(draft,plan,7,true);
  Assert.Empty(doc.AnalyzeTextRuns(1,8).Runs);
  doc.Restore(receipt.UndoSnapshot.ToArray());
  Assert.Equal(original.Text,string.Concat(doc.AnalyzeTextRuns(1,7).Runs.Select(r=>r.Run.Text)));
 }
 [Fact] public void WrittenSingleLineRetainsOneMixedObjectForSubsequentEdits() {
  using var doc=Open();var draft=Draft(doc);
  var options=new ParagraphCompositionOptions(new(50,100,200,120),120,20);
  var plan=ParagraphComposer.Compose(draft,options,doc.CreateRunMetricsProvider(1,7));
  doc.CommitParagraphComposition(draft,plan,7,true);
  var analysis=doc.AnalyzeTextRuns(1,8);
  Assert.Single(analysis.Runs.SelectMany(r=>r.Run.Sources.Select(s=>s.Source.BlockId)).Distinct());
  var next=ParagraphRunBuffer.FromRuns(analysis.Runs.Select(r=>r.Run));
  var provider=doc.CreateRunMetricsProvider(1,8);
  next=next.InsertText(next.Text.Length," X",provider).Buffer;
  var nextPlan=ParagraphComposer.Compose(next,options,provider);
  var receipt=doc.CommitParagraphComposition(next,nextPlan,8,true);
  Assert.Equal(9,receipt.NextRevision);
  Assert.Equal(next.Text,string.Concat(doc.AnalyzeTextRuns(1,9).Runs.Select(r=>r.Run.Text)));
 }
 [Fact] public void FailedCandidateLeavesOriginalLiveContent() {
  using var doc=Open();var original=Draft(doc);
  var draft=ParagraphRunBuffer.FromRuns(original.Runs.Select(r=>new PdfTextRun(r.DraftId,r.Text,
   new PdfRunStyle(r.Style.Font,r.Style.FamilyName,r.Style.EncodingName,1e-20,r.Style.FillColor,r.Style.Bold,r.Style.Italic),r.Sources)));
  var plan=Plan(doc,draft);Assert.True(plan.IsValid);var before=Live(doc);
  // The candidate serializes this tiny font size as zero. Re-parsing must reject it before swapping sessions.
  Assert.Throws<InvalidOperationException>(()=>doc.CommitParagraphComposition(draft,plan,7,true));
  Assert.Equal(before,Live(doc));
 }
 public void Dispose()=>Directory.Delete(directory,true);
}
