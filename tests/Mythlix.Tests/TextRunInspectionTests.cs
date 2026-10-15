using System.Text;
using Mythlix.Core;
using Mythlix.Core.TextEditing;
using Mythlix.Infrastructure;
using PdfSharp.Pdf;
using Xunit;

namespace Mythlix.Tests;

public sealed class TextRunInspectionTests : IDisposable {
 readonly string directory=Path.Combine(Path.GetTempPath(),"mythlix-inspection-"+Guid.NewGuid().ToString("N"));
 string Source=>Path.Combine(directory,"source.pdf");
 const string Mixed="BT /F1 12 Tf 50 700 Td (Normal ) Tj /F2 12 Tf (bold) Tj ET";
 const string Plain="BT /F1 12 Tf 50 650 Td (Plain companion) Tj ET";
 public TextRunInspectionTests() => Directory.CreateDirectory(directory);
 void Create(string content,bool shared=false) {
  using var doc=new PdfDocument();var page=doc.AddPage();
  page.Width=PdfSharp.Drawing.XUnit.FromPoint(612);page.Height=PdfSharp.Drawing.XUnit.FromPoint(792);
  var resources=new PdfDictionary(doc);var fonts=new PdfDictionary(doc);
  foreach(var (key,family) in new[] {("/F1","/Helvetica"),("/F2","/Helvetica-Bold")}) {
   var font=new PdfDictionary(doc);font.Elements.SetName("/Type","/Font");font.Elements.SetName("/Subtype","/Type1");
   font.Elements.SetName("/BaseFont",family);font.Elements.SetName("/Encoding","/WinAnsiEncoding");
   doc.Internals.AddObject(font);fonts.Elements[key]=font.Reference!;
  }
  resources.Elements["/Font"]=fonts;page.Elements["/Resources"]=resources;
  var stream=page.Contents.AppendContent();stream.CreateStream(Encoding.ASCII.GetBytes(content));
  if(shared) {var second=doc.AddPage();second.Width=page.Width;second.Height=page.Height;
   second.Elements["/Resources"]=resources;second.Contents.Elements.Add(stream.Reference!);}
  doc.Save(Source);
 }

 [Fact] public void ReportGroupsByOriginalObjectAndDoesNotEnableMixedWriting() {
  Create(Mixed+"\n"+Plain);using var doc=new PdfDocumentSession(Source);
  ITextRunInspector inspector=doc;
  var report=inspector.InspectTextRuns(1,9);
  Assert.True(report.IsReadOnly);Assert.True(report.NativeAnalysisSupported);Assert.Equal(2,report.Blocks.Length);
  var mixed=report.Blocks[0];Assert.True(mixed.HasMixedStyles);Assert.False(mixed.LegacyEditable);
  Assert.Equal("Normal bold",mixed.Text);Assert.Equal(2,mixed.Runs.Length);Assert.True(mixed.IsReadOnly);
  Assert.True(report.Blocks[1].LegacyEditable);
  var original=Assert.Single(doc.AnalyzeText(1).Blocks);
  Assert.Equal(InspectionMatchKind.VerifiedNative,report.MatchOriginal(original,report.Context).Kind);
 }

 [Fact] public void ContextAndStreamFingerprintPreventStaleOrCrossPageMatches() {
  Create(Plain,true);using var doc=new PdfDocumentSession(Source);
  var report=doc.InspectTextRuns(1,9);var original=Assert.Single(doc.AnalyzeText(1).Blocks);
  Assert.Equal(InspectionMatchKind.StaleSelection,report.MatchOriginal(original,new(report.Context.DocumentId,2,9)).Kind);
  Assert.Equal(InspectionMatchKind.StaleSelection,report.MatchOriginal(original,new(report.Context.DocumentId,1,10)).Kind);
  Assert.Equal(InspectionMatchKind.StaleSelection,report.MatchOriginal(original,new(Guid.NewGuid(),1,9)).Kind);
  Assert.Equal(InspectionMatchKind.StaleSelection,report.MatchOriginal(original with {Fingerprint="old"},report.Context).Kind);
 }

 [Fact] public void FallbackRowsAreNotLinkedByGeometryOrMatchingText() {
  Create(Plain);using var doc=new PdfDocumentSession(Source);
  var report=doc.InspectTextRuns(1,0);var original=Assert.Single(doc.AnalyzeText(1).Blocks);
  var match=report.MatchOriginal(original with {Id="v:1:0"},report.Context);
  Assert.Equal(InspectionMatchKind.ExtractedFallback,match.Kind);Assert.Null(match.Block);
  Assert.Equal(InspectionMatchKind.NotFound,report.MatchOriginal(original with {Id="99:99"},report.Context).Kind);
 }

 [Fact] public void UnsupportedObjectHasLocalDiagnosticAndOtherObjectsAreExplicitlyUnresolved() {
  Create(Plain+"\nBT /F1 12 Tf 50 600 Td 3 Tr (Hidden) Tj ET");
  using var doc=new PdfDocumentSession(Source);var report=doc.InspectTextRuns(1,0);
  Assert.False(report.NativeAnalysisSupported);Assert.Equal(2,report.Blocks.Length);
  Assert.All(report.Blocks,b=>Assert.Empty(b.Runs));
  Assert.Equal("PageAnalysisRejected",Assert.Single(report.Blocks[0].Diagnostics).Code);
  Assert.Equal("RenderingMode",Assert.Single(report.Blocks[1].Diagnostics).Code);
  Assert.True(report.Blocks[0].LegacyEditable); // New inspection does not change old capability.
 }

 [Fact] public void UnsupportedStateOutsideTextProducesPageAndBlockDiagnostics() {
  Create("1 0 0 1 0 0 cm "+Plain);using var doc=new PdfDocumentSession(Source);
  var report=doc.InspectTextRuns(1,0);
  Assert.Equal("UnsupportedOperator",Assert.Single(report.PageDiagnostics).Code);
  Assert.Equal("PageAnalysisRejected",Assert.Single(Assert.Single(report.Blocks).Diagnostics).Code);
 }

 [Fact] public void MalformedTextIsReportedRatherThanPresentedAsACompleteInventory() {
  Create(Plain+"\nBT /F1 12 Tf 50 600 Td (Missing end) Tj");using var doc=new PdfDocumentSession(Source);
  var report=doc.InspectTextRuns(1,0);
  Assert.False(report.NativeAnalysisSupported);
  Assert.Contains(report.PageDiagnostics,d=>d.Code=="InventoryUnavailable");
  Assert.All(report.Blocks,b=>Assert.False(b.NativeAnalysisSupported));
 }

 [Fact] public async Task InspectionPreservesPixelsRevisionAndLegacyContentAcrossSharedPages() {
  Create(Mixed+"\n"+Plain,true);using var doc=new PdfDocumentSession(Source);
  var before=doc.AnalyzeText(1);var report=doc.InspectTextRuns(1,5000000000);
  Assert.Equal(5000000000,report.Context.Revision);
  Assert.Equal(before.Blocks,doc.AnalyzeText(1).Blocks);
  var output=Path.Combine(directory,"output.pdf");await doc.SaveCopyAsync(output);
  var renderer=new PopplerRenderer();
  for(var page=1;page<=2;page++)
   Assert.Equal(await renderer.RenderAsync(Source,page,792,CancellationToken.None),await renderer.RenderAsync(output,page,792,CancellationToken.None));
  Assert.Equal(report.Blocks[0].StreamFingerprint,doc.InspectTextRuns(2,5000000000).Blocks[0].StreamFingerprint);
 }

 [Fact] public void OwnedLayersNeverAppearAsOriginalBlocks() {
  Create(Plain);using var doc=new PdfDocumentSession(Source);
  doc.SetGraphics(1,[new(){Kind=GraphicKind.Text,Text="Added layer",Y=300}]);
  var report=doc.InspectTextRuns(1,1);
  Assert.Single(report.Blocks);Assert.Contains(report.PageDiagnostics,d=>d.Code=="OwnedLayer");
  Assert.All(report.Blocks,b=>Assert.DoesNotContain("Added layer",b.Text));
 }
 public void Dispose()=>Directory.Delete(directory,true);
}
