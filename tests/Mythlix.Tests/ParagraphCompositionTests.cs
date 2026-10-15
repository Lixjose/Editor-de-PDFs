using Mythlix.Core;
using Mythlix.Core.TextEditing;
using Mythlix.Infrastructure;
using PdfSharp.Pdf;
using System.Text;
using Xunit;

namespace Mythlix.Tests;

public sealed class ParagraphCompositionTests : IDisposable {
 readonly string directory=Path.Combine(Path.GetTempPath(),"mythlix-compose-"+Guid.NewGuid().ToString("N"));
 readonly PdfDocumentSession document;
 readonly IRunMetricsProvider provider;
 readonly ParagraphRunBuffer original;
 public ParagraphCompositionTests() {
  Directory.CreateDirectory(directory);var path=Path.Combine(directory,"source.pdf");
  using(var pdf=new PdfDocument()) {
   var page=pdf.AddPage();page.Width=PdfSharp.Drawing.XUnit.FromPoint(612);page.Height=PdfSharp.Drawing.XUnit.FromPoint(792);
   var resources=new PdfDictionary(pdf);var fonts=new PdfDictionary(pdf);
   foreach(var (key,family) in new[] {("/F1","/Courier"),("/F2","/Courier-Bold")}) {
    var font=new PdfDictionary(pdf);font.Elements.SetName("/Type","/Font");font.Elements.SetName("/Subtype","/Type1");
    font.Elements.SetName("/BaseFont",family);font.Elements.SetName("/Encoding","/WinAnsiEncoding");
    pdf.Internals.AddObject(font);fonts.Elements[key]=font.Reference!;
   }
   resources.Elements["/Font"]=fonts;page.Elements["/Resources"]=resources;
   page.Contents.AppendContent().CreateStream(Encoding.ASCII.GetBytes("BT /F1 10 Tf 50 700 Td (AB ) Tj /F2 10 Tf (CD ) Tj /F1 10 Tf (EF GH) Tj ET"));
   pdf.Save(path);
  }
  document=new(path);provider=document.CreateRunMetricsProvider(1,3);
  original=ParagraphRunBuffer.FromRuns(document.AnalyzeTextRuns(1,3).Runs.Select(r=>r.Run));
 }
 ParagraphRunBuffer Draft(string text)=>original.ReplaceRange(new(0,original.Text.Length),text,provider).Buffer;
 static ParagraphCompositionOptions Options(double width=42,TextAlignment alignment=TextAlignment.Left,double height=200,double leading=14) => new(new(50,10,width,height),20,leading,alignment);

 [Fact] public void WrapsAcrossMixedRunsWithExactPositionsAndAllSourceOwners() {
  var result=ParagraphComposer.Compose(original,Options(),provider);
  Assert.True(result.IsValid);Assert.Equal(2,result.Lines.Length);
  Assert.Equal("AB CD ",result.Lines[0].Text);Assert.Equal("EF GH",result.Lines[1].Text);
  Assert.Equal(2,result.Lines[0].Segments.Length);Assert.Equal(68,result.Lines[0].Segments[1].StartX);
  Assert.Equal(34,result.Lines[1].BaselineY);
  Assert.True(result.Lines[0].Segments[1].Owner.Style.Bold);
  Assert.Equal(original.OriginalSources,result.OriginalSources);
  Assert.Same(original.Runs[1],result.Lines[0].Segments[1].Owner);
  Assert.Equal(original.Text,string.Concat(result.Lines.Select(l=>l.Text)));
 }

 [Fact] public void JustifiesEligibleInternalSpacesButNotLastLineOrTrailingSpaces() {
  var result=ParagraphComposer.Compose(original,Options(alignment:TextAlignment.Justify),provider);
  Assert.True(result.IsValid);
  Assert.Equal(42,result.Lines[0].AdvanceBounds.Width,6);
  Assert.Equal(74,result.Lines[0].Segments[1].StartX,6);
  Assert.Equal(new[] {0d,0d,6d},result.Lines[0].Segments[0].ExtraSpacingPt.ToArray());
  Assert.All(result.Lines[0].Segments[1].ExtraSpacingPt,v=>Assert.Equal(0,v));
  Assert.All(result.Lines[1].Segments.SelectMany(s=>s.ExtraSpacingPt),v=>Assert.Equal(0,v));
 }

 [Theory]
 [InlineData(TextAlignment.Left,50)]
 [InlineData(TextAlignment.Center,65)]
 [InlineData(TextAlignment.Right,80)]
 public void AlignmentsUseSharedAdvanceGeometry(TextAlignment alignment,double expectedX) {
  var result=ParagraphComposer.Compose(Draft("AB"),Options(alignment:alignment),provider);
  Assert.Equal(expectedX,result.Lines[0].Segments[0].StartX);
  Assert.Equal(12,result.Lines[0].AdvanceBounds.Width);
 }

 [Fact] public void PreservesRepeatedSpacesHardBreaksAndBlankLinesWithoutEncodingControlBytes() {
  var draft=Draft("A  B\r\n\nC\r");var result=ParagraphComposer.Compose(draft,Options(100),provider);
  Assert.True(result.IsValid);Assert.Equal(4,result.Lines.Length);
  Assert.Equal(new[] {"A  B","","C",""},result.Lines.Select(l=>l.Text).ToArray());
  Assert.Equal(new TextRange(4,2),result.Lines[0].HardBreakRange);
  Assert.All(result.Lines,l=>Assert.True(l.IsLastLineOfParagraph));
  Assert.DoesNotContain(result.Lines.SelectMany(l=>l.Segments).SelectMany(s=>s.EncodedBytes),b=>b is 10 or 13);
  var rebuilt=string.Concat(result.Lines.Select(l=>l.Text+(l.HardBreakRange is {} range?draft.Text.Substring(range.Start,range.Length):"")));
  Assert.Equal(draft.Text,rebuilt);
 }

 [Fact] public void AWordCrossingStyleBoundariesIsNotSplitAtTheStyleChange() {
  var a=original.Runs[0];var b=original.Runs[1];
  var runs=new[] {new PdfTextRun(Guid.NewGuid(),"ABC",a.Style,[],true,new(a.Sources[0].Source,0)),
   new PdfTextRun(Guid.NewGuid(),"DEF",b.Style,[],true,new(b.Sources[0].Source,3))};
  var result=ParagraphComposer.Compose(ParagraphRunBuffer.FromRuns(runs),Options(24),provider);
  Assert.True(result.HasHorizontalOverflow);Assert.Equal("ABCDEF",Assert.Single(result.Lines).Text);
  Assert.Equal(2,result.Lines[0].Segments.Length);
 }

 [Fact] public void OriginalWordSpacingScaleAndRiseAffectMetricsWithoutChangingStyle() {
  var source=original.Runs[0];var s=source.Style;
  var style=new PdfRunStyle(s.Font,s.FamilyName,s.EncodingName,10,s.FillColor,characterSpacingPt:2,wordSpacingPt:3,horizontalScale:0.5,baselineRisePt:4);
  var run=new PdfTextRun(Guid.NewGuid(),"A B",style,[],true,new(source.Sources[0].Source,0));
  var metrics=provider.Measure(style,run.Text);
  Assert.Equal(new[] {4d,5.5,4d},metrics.AdvancesPt.ToArray());
  var result=ParagraphComposer.Compose(ParagraphRunBuffer.FromRuns([run]),new(new(50,0,42,200),20,14),provider);
  Assert.True(result.IsValid);Assert.Equal(13.5,result.Lines[0].AdvanceBounds.Width);
  Assert.Equal(20,result.Lines[0].BaselineY);Assert.Equal(10.29,result.Lines[0].AscentPt,6);
  Assert.Same(style,result.Lines[0].Segments[0].Owner.Style);
 }

 [Fact] public void OverflowAndLineOverlapAreDiagnosticsWithoutTruncatingText() {
  var longWord=ParagraphComposer.Compose(Draft("ABCDEFGHIJ"),Options(18),provider);
  Assert.True(longWord.HasHorizontalOverflow);Assert.Equal("ABCDEFGHIJ",longWord.Lines[0].Text);
  var vertical=ParagraphComposer.Compose(Draft("A\nB\nC"),Options(height:15,leading:4),provider);
  Assert.True(vertical.HasVerticalOverflow);Assert.Contains(vertical.Diagnostics,d=>d.Code=="LineOverlap");Assert.Equal(3,vertical.Lines.Length);
 }

 [Fact] public void NonbreakingSpaceDoesNotBecomeAWrappingOpportunity() {
  var result=ParagraphComposer.Compose(Draft("AB\u00a0CD"),Options(18),provider);
  Assert.True(result.HasHorizontalOverflow);Assert.Equal("AB\u00a0CD",Assert.Single(result.Lines).Text);
 }

 [Fact] public void UnencodableOrStaleResourcesNeverProducePartialComposition() {
  var invalid=ParagraphComposer.Compose(Draft("AB😀CD"),Options(),provider);
  Assert.False(invalid.IsValid);Assert.Empty(invalid.Lines);Assert.Null(invalid.ComposedBounds);
  var stale=ParagraphComposer.Compose(original,Options(),document.CreateRunMetricsProvider(1,4));
  Assert.False(stale.IsValid);Assert.Empty(stale.Lines);
 }

 [Fact] public void EmptyDraftProducesOneLineAndRetainsSourceInventory() {
  var empty=Draft("");var result=ParagraphComposer.Compose(empty,Options(),provider);
  Assert.True(result.IsValid);Assert.Empty(Assert.Single(result.Lines).Segments);
  Assert.Equal(original.OriginalSources,result.OriginalSources);
 }

 [Fact] public void NumericOverflowReturnsDiagnosticsAndDoesNotThrowOrPublishInvalidGeometry() {
  var source=original.Runs[0];var s=source.Style;
  var huge=new PdfRunStyle(s.Font,s.FamilyName,s.EncodingName,double.MaxValue,s.FillColor);
  var run=new PdfTextRun(Guid.NewGuid(),"AAA",huge,[],true,new(source.Sources[0].Source,0));
  var result=ParagraphComposer.Compose(ParagraphRunBuffer.FromRuns([run]),Options(),provider);
  Assert.False(result.IsValid);Assert.Empty(result.Lines);Assert.Null(result.ComposedBounds);
 }

 [Fact] public void MetricCompositionMatchesExtractorPenAdvancesAndNeverChangesThePdf() {
  var before=document.AnalyzeTextRuns(1,3);
  var result=ParagraphComposer.Compose(original,Options(500),provider);
  Assert.True(result.IsValid);
  for(var i=0;i<before.Runs.Length;i++) {
   Assert.Equal(before.Runs[i].BaselineX,result.Lines[0].Segments[i].StartX,6);
   Assert.Equal(before.Runs[i].CharacterAdvancesPt.ToArray(),result.Lines[0].Segments[i].AdvancesPt.ToArray());
  }
  var after=document.AnalyzeTextRuns(1,3);
  Assert.Equal(before.Runs[0].Run.Sources[0].Source,after.Runs[0].Run.Sources[0].Source);
 }
 public void Dispose() {document.Dispose();Directory.Delete(directory,true);}
}
