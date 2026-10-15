using System.Text;
using System.Security.Cryptography;
using Mythlix.Core;
using Mythlix.Core.TextEditing;
using Mythlix.Infrastructure;
using Mythlix.Infrastructure.TextEditing;
using PdfSharp.Pdf;
using Xunit;

namespace Mythlix.Tests;

public sealed class NativeRunExtractionTests : IDisposable {
 readonly string directory=Path.Combine(Path.GetTempPath(),"mythlix-native-runs-"+Guid.NewGuid().ToString("N"));
 readonly Guid documentId=Guid.NewGuid();
 public NativeRunExtractionTests() => Directory.CreateDirectory(directory);
 PdfDocument Create(string content, string encoding="/WinAnsiEncoding", bool shared=false) {
  var doc=new PdfDocument();var page=doc.AddPage();
  page.Width=PdfSharp.Drawing.XUnit.FromPoint(612);page.Height=PdfSharp.Drawing.XUnit.FromPoint(792);
  var resources=new PdfDictionary(doc);var fonts=new PdfDictionary(doc);
  foreach(var (name,family) in new[] {("/F1","/Courier"),("/F2","/Courier-Bold"),("/F3","/Courier-Oblique")}) {
   var font=new PdfDictionary(doc);font.Elements.SetName("/Type","/Font");font.Elements.SetName("/Subtype","/Type1");
   font.Elements.SetName("/BaseFont",family);font.Elements.SetName("/Encoding",encoding);
   doc.Internals.AddObject(font);fonts.Elements[name]=font.Reference!;
  }
  resources.Elements["/Font"]=fonts;page.Elements["/Resources"]=resources;
  var stream=page.Contents.AppendContent();stream.CreateStream(Encoding.ASCII.GetBytes(content));
  if(shared) {var other=doc.AddPage();other.Width=page.Width;other.Height=page.Height;
   other.Elements["/Resources"]=resources;other.Contents.Elements.Add(stream.Reference!);}
  return doc;
 }
 NativeRunAnalysis Analyze(PdfDocument doc,int page=1) => NativeRunExtractor.Analyze(doc.Pages[page-1],new(612,792,0),new(documentId,page,42));

 [Fact] public void ExtractsMixedStylesWithoutFlatteningOrInventingSourceRanges() {
  const string content="0.1 0.2 0.3 rg BT /F1 10 Tf 1 0 0 1 50 700 Tm (Normal ) Tj /F2 12 Tf (Bold ) Tj /F3 11.25 Tf 0.5 g (Italic) Tj ET";
  using var doc=Create(content);var result=Analyze(doc);
  Assert.True(result.IsSupported);Assert.True(result.IsReadOnly);Assert.Equal(3,result.Runs.Length);
  Assert.Equal("Normal Bold Italic",string.Concat(result.Runs.Select(r=>r.Run.Text)));
  Assert.False(result.Runs[0].Run.Style.Bold);Assert.True(result.Runs[1].Run.Style.Bold);Assert.True(result.Runs[2].Run.Style.Italic);
  Assert.Equal(11.25,result.Runs[2].Run.Style.FontSizePt);
  Assert.Equal(new RunRgbColor(0.1,0.2,0.3),result.Runs[0].Run.Style.FillColor);
  Assert.Equal(new RunRgbColor(0.5,0.5,0.5),result.Runs[2].Run.Style.FillColor);
  Assert.Equal(92,result.Runs[1].BaselineX,6);Assert.Equal(92,result.Runs[0].BaselineY);
  var bytes=Encoding.ASCII.GetBytes(content);
  foreach(var run in result.Runs) {
   var source=Assert.Single(run.Run.Sources).Source;
   Assert.Equal(Convert.ToHexString(SHA256.HashData(bytes)),source.StreamFingerprint);
   var token=Assert.Single(ContentTokenizer.Read(bytes.AsSpan(source.ByteRange.Start,source.ByteRange.Length).ToArray()));
   Assert.Equal(run.EncodedBytes.ToArray(),token.Bytes);
   Assert.Equal("Tj",Encoding.ASCII.GetString(bytes.AsSpan(run.ShowOperatorRange.Start,run.ShowOperatorRange.Length)));
  }
  Assert.Equal(7,result.Runs[1].Run.Sources[0].Source.SourceTextRange.Start);
 }

 [Fact] public void KeepsTjArrayOffsetsHexStringsAndNumericAdjustments() {
  using var doc=Create("BT /F1 10 Tf 1 0 0 1 50 700 Tm [(A) -200 <42> 100 (C)] TJ ET");
  var result=Analyze(doc);Assert.True(result.IsSupported);
  Assert.Equal(new[] {50d,58d,63d},result.Runs.Select(r=>r.BaselineX).ToArray());
  Assert.Equal(new[] {2d,-1d},result.Adjustments.Select(a=>a.HorizontalAdvancePt).ToArray());
  Assert.Equal("ABC",string.Concat(result.Runs.Select(r=>r.Run.Text)));
  Assert.All(result.Runs,r=>Assert.Equal(2,r.ShowOperatorRange.Length));
 }

 [Fact] public void AdvancesIncludeCharacterSpacingAfterLastCharacterAndPreserveOriginalWordSpacing() {
  using var doc=Create("BT /F1 10 Tf 2 Tc 3 Tw 50 Tz 4 Ts 1 0 0 1 50 700 Tm (A ) Tj (B) Tj ET");
  var result=Analyze(doc);Assert.True(result.IsSupported);
  Assert.Equal(new[] {4d,5.5},result.Runs[0].CharacterAdvancesPt.ToArray());
  Assert.Equal(59.5,result.Runs[1].BaselineX);
  Assert.Equal(92,result.Runs[0].BaselineY);Assert.Equal(4,result.Runs[0].Run.Style.BaselineRisePt);
  Assert.Equal(3,result.Runs[0].Run.Style.WordSpacingPt);
 }

 [Fact] public void TracksLineMatrixAndQuoteOperatorsWithoutUsingAdvancedTextPositionAsLineOrigin() {
  using var doc=Create("BT /F1 10 Tf 1 0 0 1 50 700 Tm (AA) Tj 10 -20 TD (B) Tj (C) ' 3 2 (D) \" T* (E) Tj ET");
  var result=Analyze(doc);Assert.True(result.IsSupported);
  Assert.Equal(new[] {50d,60d,60d,60d,60d},result.Runs.Select(r=>r.BaselineX).ToArray());
  Assert.Equal(new[] {92d,112d,132d,152d,172d},result.Runs.Select(r=>r.BaselineY).ToArray());
  Assert.Equal(3,result.Runs[3].Run.Style.WordSpacingPt);Assert.Equal(2,result.Runs[3].Run.Style.CharacterSpacingPt);
 }

 [Fact] public void GraphicsStateAndFontStateAreInheritedAcrossTextObjectsAndContentStreams() {
  using var doc=Create("/F1 10 Tf 1 Tc 0.2 g q 3 Tc BT 1 0 0 1 50 700 Tm (A) Tj ET Q");
  doc.Pages[0].Contents.AppendContent().CreateStream(Encoding.ASCII.GetBytes("BT 1 0 0 1 50 680 Tm (B) Tj ET"));
  var result=Analyze(doc);Assert.True(result.IsSupported);
  Assert.Equal(3,result.Runs[0].Run.Style.CharacterSpacingPt);Assert.Equal(1,result.Runs[1].Run.Style.CharacterSpacingPt);
  Assert.Equal(new RunRgbColor(0.2,0.2,0.2),result.Runs[1].Run.Style.FillColor);
  Assert.Equal(1,result.Runs[1].Run.Sources[0].Source.StreamIndex);
 }

 [Fact] public void EscapedAndWinAnsiStringsRetainDecodedBytesAndSpaces() {
  using var doc=Create("BT /F1 10 Tf 50 700 Td (A\\(\\)\\361  B) Tj () Tj ET");
  var result=Analyze(doc);Assert.True(result.IsSupported);
  Assert.Equal("A()ñ  B",Assert.Single(result.Runs).Run.Text);
  Assert.Contains((byte)241,result.Runs[0].EncodedBytes);
 }

 [Theory]
 [InlineData("1 0 0 1 0 0 cm", "UnsupportedOperator")]
 [InlineData("/GS1 gs", "UnsupportedOperator")]
 [InlineData("/X1 Do", "UnsupportedOperator")]
 [InlineData("/P BMC", "UnsupportedOperator")]
 [InlineData("W n", "UnsupportedOperator")]
 public void UnknownPageStateRejectsPreviouslyExtractedRuns(string unsupported,string code) {
  using var doc=Create("BT /F1 10 Tf 50 700 Td (A) Tj ET "+unsupported);
  var result=Analyze(doc);Assert.False(result.IsSupported);Assert.Empty(result.Runs);Assert.Empty(result.Adjustments);
  Assert.Equal(code,Assert.Single(result.Diagnostics).Code);
 }

 [Theory]
 [InlineData("BT /F1 10 Tf 50 700 Td (A) Tj")]
 [InlineData("BT /F1 10 Tf [12 (A) Tj ET")]
 [InlineData("BT /F1 10 Tf 2 0 0 1 50 700 Tm (A) Tj ET")]
 [InlineData("BT /F1 10 Tf 50 700 Td 3 Tr (A) Tj ET")]
 [InlineData("BT /F1 10 Tf 50 700 Td 0 Tz (A) Tj ET")]
 [InlineData("BT /F1 10 Tf 50 700 Td <81> Tj ET")]
 [InlineData("Q BT /F1 10 Tf 50 700 Td (A) Tj ET")]
 public void MalformedOrUnsupportedTextReturnsDiagnosticsInsteadOfPartialContent(string content) {
  using var doc=Create(content);var result=Analyze(doc);
  Assert.False(result.IsSupported);Assert.Empty(result.Runs);Assert.NotEmpty(result.Diagnostics);
 }

 [Fact] public void CustomEncodingAndCidFontsAreExplicitlyUnsupported() {
  using var doc=Create("BT /F1 10 Tf 50 700 Td (A) Tj ET",encoding:"/Identity-H");
  Assert.Equal("Encoding",Assert.Single(Analyze(doc).Diagnostics).Code);
  doc.Pages[0].Resources.Elements.GetDictionary("/Font")!.Elements.GetDictionary("/F1")!.Elements.SetName("/Subtype","/Type0");
  Assert.Equal("Font",Assert.Single(Analyze(doc).Diagnostics).Code);
 }

 [Fact] public void DeclaredWidthsOverrideStandardMetricsAndMissingWidthsAreRejected() {
  using var doc=Create("BT /F1 10 Tf 50 700 Td (A) Tj (A) Tj ET");
  var font=doc.Pages[0].Resources.Elements.GetDictionary("/Font")!.Elements.GetDictionary("/F1")!;
  font.Elements.SetInteger("/FirstChar",65);font.Elements.SetInteger("/LastChar",65);var widths=new PdfArray(doc);widths.Elements.Add(new PdfInteger(900));font.Elements["/Widths"]=widths;
  var result=Analyze(doc);Assert.True(result.IsSupported);Assert.Equal(59,result.Runs[1].BaselineX);
  widths.Elements[0]=new PdfInteger(0);
  Assert.False(Analyze(doc).IsSupported);
 }

 [Fact] public async Task SharedCompressedStreamsAreReadOnlyWithPageScopedIdentity() {
  using var original=Create("BT /F1 10 Tf 50 700 Td (A) Tj /F2 10 Tf (B) Tj ET",shared:true);
  var path=Path.Combine(directory,"source.pdf");original.Options.CompressContentStreams=true;original.Save(path);
  using var session=new PdfDocumentSession(path);
  var first=session.AnalyzeTextRuns(1,5000000000);var second=session.AnalyzeTextRuns(2,5000000000);
  Assert.True(first.IsSupported);Assert.True(second.IsSupported);
  Assert.Equal(first.Context.DocumentId,second.Context.DocumentId);Assert.NotEqual(first.Runs[0].Run.Style.Font,second.Runs[0].Run.Style.Font);
  Assert.Equal(first.Runs[0].Run.Sources[0].Source.StreamFingerprint,second.Runs[0].Run.Sources[0].Source.StreamFingerprint);
  var output=Path.Combine(directory,"output.pdf");await session.SaveCopyAsync(output);
  var after=session.AnalyzeTextRuns(1,5000000000);
  Assert.Equal(first.Context,after.Context);
  Assert.Equal(first.Runs[0].Run.Sources[0].Source,after.Runs[0].Run.Sources[0].Source);
  Assert.Empty(session.AnalyzeText(1).Blocks); // The existing writer remains conservative.
  var renderer=new PopplerRenderer();
  for(var page=1;page<=2;page++)
   Assert.Equal(await renderer.RenderAsync(path,page,792,CancellationToken.None),await renderer.RenderAsync(output,page,792,CancellationToken.None));
 }
 [Fact] public void SourceResourcesCanBeInheritedWithoutMutatingTheirDictionary() {
  using var doc=Create("BT /F1 10 Tf 50 700 Td (A) Tj ET");
  var page=doc.Pages[0];
  var parent=page.Elements.GetDictionary("/Parent")!;
  parent.Elements["/Resources"]=page.Elements["/Resources"];
  page.Elements.Remove("/Resources");
  var result=Analyze(doc);
  Assert.True(result.IsSupported);
  Assert.Contains("depth:1",result.Runs[0].Run.Style.Font.ResourceScopeId);
  Assert.False(page.Elements.ContainsKey("/Resources"));
 }

 [Fact] public void OwnedLayersAndSplitTextObjectsHaveExplicitDiagnostics() {
  using var doc=Create("BT /F1 10 Tf 50 700 Td (A) Tj ET");
  doc.Pages[0].Contents.Elements.GetDictionary(0)!.Elements.SetBoolean("/MythlixGraphicsV1",true);
  Assert.Equal("OwnedLayer",Assert.Single(Analyze(doc).Diagnostics).Code);
  using var split=Create("BT /F1 10 Tf 50 700 Td (A) Tj");
  split.Pages[0].Contents.AppendContent().CreateStream(Encoding.ASCII.GetBytes("ET"));
  Assert.Equal("ContentBoundary",Assert.Single(Analyze(split).Diagnostics).Code);
 }

 public void Dispose() => Directory.Delete(directory,true);
}
