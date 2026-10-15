using System.Text;
using Mythlix.Core;
using Mythlix.Infrastructure;
using PdfSharp.Pdf;
using PdfSharp.Pdf.IO;
using Xunit;
namespace Mythlix.Tests;
public sealed class OriginalTextEditingTests:IDisposable {
 readonly string directory=Path.Combine(Path.GetTempPath(),"mythlix-original-"+Guid.NewGuid().ToString("N"));
 string Source=>Path.Combine(directory,"original.pdf");
 string Output=>Path.Combine(directory,"edited.pdf");
 const string Simple="BT /F1 18 Tf 1 0 0 1 60 680 Tm (Original document title) Tj ET\nBT /F1 12 Tf 60 620 Td (Unchanged nearby text) Tj ET";
 public OriginalTextEditingTests(){Directory.CreateDirectory(directory);Create(Simple);}
 void Create(string content,string encoding="/WinAnsiEncoding",bool compressed=false,bool shared=false) {
  using var doc=new PdfDocument();var page=doc.AddPage();page.Width=PdfSharp.Drawing.XUnit.FromPoint(612);page.Height=PdfSharp.Drawing.XUnit.FromPoint(792);
  var font=new PdfDictionary(doc);font.Elements.SetName("/Type","/Font");font.Elements.SetName("/Subtype","/Type1");font.Elements.SetName("/BaseFont","/Helvetica");if(encoding.Length>0)font.Elements.SetName("/Encoding",encoding);doc.Internals.AddObject(font);
  var resources=new PdfDictionary(doc);var fonts=new PdfDictionary(doc);fonts.Elements["/F1"]=font.Reference!;resources.Elements["/Font"]=fonts;page.Elements["/Resources"]=resources;
  var stream=page.Contents.AppendContent();stream.CreateStream(Encoding.ASCII.GetBytes(content));
  doc.Options.CompressContentStreams=compressed;
  if(shared){var second=doc.AddPage();second.Width=PdfSharp.Drawing.XUnit.FromPoint(612);second.Height=PdfSharp.Drawing.XUnit.FromPoint(792);second.Elements["/Resources"]=resources;second.Contents.Elements.Add(stream.Reference!);}
  doc.Save(Source);
 }
 [Fact] public async Task ReplacementModifiesOriginalStreamWithoutAddingGraphicsOrAffectingOtherText(){
  var source=await File.ReadAllBytesAsync(Source);using var doc=new PdfDocumentSession(Source);
  var analysis=doc.AnalyzeText(1);Assert.Equal(2,analysis.Blocks.Count);var first=analysis.Blocks[0];
  doc.ReplaceText(1,first,"Edited title",18);await doc.SaveCopyAsync(Output);
  var text=await new PopplerRenderer().ExtractTextAsync(Output,1,CancellationToken.None);Assert.Contains("Edited title",text);Assert.DoesNotContain("Original document title",text);Assert.Contains("Unchanged nearby text",text);
  Assert.Empty(doc.GetGraphics(1));Assert.Equal(source,await File.ReadAllBytesAsync(Source));
  using var parsed=PdfReader.Open(Output,PdfDocumentOpenMode.Modify);Assert.Single(parsed.Pages[0].Contents.Elements);
  var bytes=Encoding.ASCII.GetString(parsed.Pages[0].Contents.Elements.GetDictionary(0)!.Stream!.UnfilteredValue);
  Assert.Contains("(Edited title) Tj",bytes);Assert.Contains("BT /F1 12 Tf 60 620 Td (Unchanged nearby text) Tj ET",bytes);
 }
 [Fact] public async Task WinAnsiAccentsAndEscapesRoundTrip(){
  using var doc=new PdfDocumentSession(Source);doc.ReplaceText(1,doc.AnalyzeText(1).Blocks[0],"Edición (á) \\",12);await doc.SaveCopyAsync(Output);
  var text=await new PopplerRenderer().ExtractTextAsync(Output,1,CancellationToken.None);Assert.Contains("Edición (á) \\",text);
  using var reopened=new PdfDocumentSession(Output);var block=reopened.AnalyzeText(1).Blocks[0];Assert.Equal("Edición (á) \\",block.Text);Assert.Equal(12,block.FontSize);Assert.Equal("Helvetica",block.FontName);
 }
 [Fact] public void RejectsOverflowAndUnavailableGlyphsWithoutChangingText(){
  using var doc=new PdfDocumentSession(Source);var block=doc.AnalyzeText(1).Blocks[0];
  Assert.Throws<InvalidOperationException>(()=>doc.ReplaceText(1,block,new string('W',100),18));
  Assert.Throws<InvalidOperationException>(()=>doc.ReplaceText(1,block,"漢字",18));
  Assert.Throws<InvalidOperationException>(()=>doc.ReplaceText(1,block,"line\nbreak",12));
  Assert.Throws<InvalidOperationException>(()=>doc.ReplaceText(1,block,"Title",36));
  Assert.Equal(block.Text,doc.AnalyzeText(1).Blocks[0].Text);
 }
 [Fact] public void StandardEncodingRejectsNonAscii(){Create(Simple,"");using var doc=new PdfDocumentSession(Source);Assert.Throws<InvalidOperationException>(()=>doc.ReplaceText(1,doc.AnalyzeText(1).Blocks[0],"Edición",12));}
 [Theory]
 [InlineData("1 0 0 1 30 0 cm ")]
 [InlineData("10 Tc ")]
 [InlineData("/GS1 gs ")]
 [InlineData("/Span BMC ")]
 public void ComplexGraphicsStatesAreReportedAsUnsupported(string prefix){Create(prefix+Simple);using var doc=new PdfDocumentSession(Source);var report=doc.AnalyzeText(1);Assert.Empty(report.Blocks);Assert.True(report.UnsupportedBlocks>0);}
 [Fact] public void FragmentedParagraphIsNotMisrepresentedAsEditable(){Create("BT /F1 18 Tf 60 680 Td (One) Tj 0 -24 Td (Two) Tj ET");using var doc=new PdfDocumentSession(Source);Assert.Empty(doc.AnalyzeText(1).Blocks);Assert.Equal(1,doc.AnalyzeText(1).UnsupportedBlocks);}
 [Fact] public void SimpleTjArrayAndHexStringAreEditable(){Create("BT /F1 18 Tf 60 680 Td [<48656C6C6F20776F726C64>] TJ ET");using var doc=new PdfDocumentSession(Source);var b=Assert.Single(doc.AnalyzeText(1).Blocks);Assert.Equal("Hello world",b.Text);doc.ReplaceText(1,b,"Hello",18);Assert.Equal("Hello",Assert.Single(doc.AnalyzeText(1).Blocks).Text);}
 [Fact] public void KerningArraysAreBlocked(){Create("BT /F1 18 Tf 60 680 Td [(Hello) -30 (world)] TJ ET");using var doc=new PdfDocumentSession(Source);Assert.Empty(doc.AnalyzeText(1).Blocks);}
 [Fact] public void CompressedStreamsAndHistoryWork(){Create(Simple,compressed:true);using var doc=new PdfDocumentSession(Source);var before=doc.Snapshot();doc.ReplaceText(1,doc.AnalyzeText(1).Blocks[0],"Changed",12);var after=doc.Snapshot();doc.Restore(before);Assert.Equal("Original document title",doc.AnalyzeText(1).Blocks[0].Text);doc.Restore(after);Assert.Equal("Changed",doc.AnalyzeText(1).Blocks[0].Text);}
 [Fact] public void SharedStreamsAreCopiedPerPage(){Create(Simple,shared:true);using var doc=new PdfDocumentSession(Source);doc.ReplaceText(1,doc.AnalyzeText(1).Blocks[0],"Page one",12);Assert.Equal("Page one",doc.AnalyzeText(1).Blocks[0].Text);Assert.Equal("Original document title",doc.AnalyzeText(2).Blocks[0].Text);}
 [Fact] public void StaleSelectionCannotOverwriteLaterEdits(){using var doc=new PdfDocumentSession(Source);var block=doc.AnalyzeText(1).Blocks[0];doc.ReplaceText(1,block,"New title",12);Assert.Throws<InvalidOperationException>(()=>doc.ReplaceText(1,block,"Old edit",12));Assert.Equal("New title",doc.AnalyzeText(1).Blocks[0].Text);}
 [Fact] public void NewGraphicLayerDoesNotDisableOriginalTextEditing(){using var doc=new PdfDocumentSession(Source);doc.SetGraphics(1,[new(){Kind=GraphicKind.Rectangle,Y=250}]);Assert.Equal(2,doc.AnalyzeText(1).Blocks.Count);doc.ReplaceText(1,doc.AnalyzeText(1).Blocks[0],"Changed",12);Assert.Single(doc.GetGraphics(1));}
 [Theory]
 [InlineData("/Type0","/Helvetica")]
 [InlineData("/Type1","/ABCDEF+Helvetica")]
 public void UnsupportedFontsAreReadOnly(string subtype,string baseFont){
  var changed=Path.Combine(directory,"font.pdf");
  using(var source=PdfReader.Open(Source,PdfDocumentOpenMode.Modify)) {
   var font=source.Pages[0].Resources.Elements.GetDictionary("/Font")!.Elements.GetDictionary("/F1")!;
   font.Elements.SetName("/Subtype",subtype);font.Elements.SetName("/BaseFont",baseFont);source.Save(changed);
  }
  using var doc=new PdfDocumentSession(changed);Assert.Empty(doc.AnalyzeText(1).Blocks);
 }
 [Fact] public void CustomEncodingDoesNotCrashAnalysis(){
  var changed=Path.Combine(directory,"encoding.pdf");
  using(var source=PdfReader.Open(Source,PdfDocumentOpenMode.Modify)) {
   var encoding=new PdfDictionary(source);encoding.Elements.SetName("/Type","/Encoding");encoding.Elements.SetName("/BaseEncoding","/WinAnsiEncoding");
   var font=source.Pages[0].Resources.Elements.GetDictionary("/Font")!.Elements.GetDictionary("/F1")!;font.Elements["/Encoding"]=encoding;source.Save(changed);
  }
  using var doc=new PdfDocumentSession(changed);Assert.Empty(doc.AnalyzeText(1).Blocks);
 }
 [Fact] public void StandardEncodingAmbiguousQuoteCodesAreBlocked(){
  Create("BT /F1 18 Tf 60 680 Td (Quote ' text) Tj ET","");using var doc=new PdfDocumentSession(Source);Assert.Empty(doc.AnalyzeText(1).Blocks);
 }
 [Fact] public async Task RemovingOriginalTextDoesNotHideItUnderAnOverlay(){
  using var doc=new PdfDocumentSession(Source);doc.ReplaceText(1,doc.AnalyzeText(1).Blocks[0],"",18);await doc.SaveCopyAsync(Output);
  var text=await new PopplerRenderer().ExtractTextAsync(Output,1,CancellationToken.None);
  Assert.DoesNotContain("Original document title",text);Assert.Contains("Unchanged nearby text",text);Assert.Empty(doc.GetGraphics(1));
 }
 [Fact] public void ChangingSizeCannotAffectTextThatInheritsFontState(){
  Create("BT /F1 18 Tf 60 680 Td (Editable original title) Tj ET\nBT 60 620 Td (Inherited font size) Tj ET");
  using var doc=new PdfDocumentSession(Source);var block=Assert.Single(doc.AnalyzeText(1).Blocks);Assert.False(block.CanChangeFontSize);
  Assert.Throws<InvalidOperationException>(()=>doc.ReplaceText(1,block,"Title",12));
  doc.ReplaceText(1,block,"Title",18);Assert.Equal("Title",Assert.Single(doc.AnalyzeText(1).Blocks).Text);
 }
 [Fact] public async Task VectorTextReplacementMasksOriginalAndInsertsNewText(){
  using var doc=new PdfDocumentSession(Source);
  var fakeBlock=new OriginalTextBlock("v:1:0","Original document title","Liberation Sans",18,new ObjectBounds(60,100,280,24),"fp1");
  doc.ReplaceText(1,fakeBlock,"Replaced by Vector Engine",18);
  await doc.SaveCopyAsync(Output);
  var text=await new PopplerRenderer().ExtractTextAsync(Output,1,CancellationToken.None);
  Assert.Contains("Replaced by Vector Engine",text);
  Assert.DoesNotContain("Original document title",text);
 }
 [Fact] public async Task LibreOfficePdfCanBeExtractedAndEditedDirectlyWithoutOcr(){
  var libreOfficePath = "/tmp/test-libreoffice.pdf";
  if(!File.Exists(libreOfficePath)) return;
  var renderer = new PopplerRenderer();
  var tsv = await renderer.ExtractTsvAsync(libreOfficePath, 1, CancellationToken.None);
  var analysis = Mythlix.Infrastructure.TextEditing.VectorTextExtractor.Extract(tsv, 1, new PageGeometry(612, 792, 0));
  Assert.NotEmpty(analysis.Blocks);
  var block = analysis.Blocks[0];
  Assert.Contains("LibreOffice", block.Text);

  using var doc = new PdfDocumentSession(libreOfficePath);
  doc.ReplaceText(1, block, "Texto editado directamente sin OCR en LibreOffice", 12);
  var outPath = Path.Combine(directory, "libreoffice-edited.pdf");
  await doc.SaveCopyAsync(outPath);
  var extracted = await renderer.ExtractTextAsync(outPath, 1, CancellationToken.None);
  Assert.Contains("Texto editado directamente sin OCR en LibreOffice", extracted);
 }
 [Fact] public void PdfSharpSupportsMontserratOtfFont() {
  EmbeddedFontResolver.EnsureInitialized();
  var montserratPath = "/usr/share/fonts/julietaula-montserrat-fonts/Montserrat-Regular.otf";
  if(!File.Exists(montserratPath)) return;
  using var doc = new PdfDocument();
  var page = doc.AddPage();
  using var gfx = PdfSharp.Drawing.XGraphics.FromPdfPage(page);
  var font = new PdfSharp.Drawing.XFont("Montserrat", 12);
  gfx.DrawString("Test Montserrat", font, PdfSharp.Drawing.XBrushes.Black, new PdfSharp.Drawing.XPoint(100, 100));
  Assert.NotNull(font);
 }
 [Fact] public void VectorTextExtractorExtractsExactFontFamilyAndSizeFromXml() {
  var sampleXml = """
  <?xml version="1.0" encoding="UTF-8"?>
  <pdf2xml producer="poppler" version="26.01.0">
  <page number="1" position="absolute" top="0" left="0" height="792" width="612">
    <fontspec id="0" size="10" family="ABCDEF+Montserrat" color="#1a202c"/>
    <fontspec id="1" size="18" family="Montserrat-Bold" color="#000000"/>
    <text top="50" left="60" width="220" height="12" font="0">Párrafo con tipografía Montserrat</text>
    <text top="80" left="60" width="180" height="20" font="1"><b>Título en Negrita</b></text>
  </page>
  </pdf2xml>
  """;
  var analysis = Mythlix.Infrastructure.TextEditing.VectorTextExtractor.ExtractFromXml(sampleXml, 1, new PageGeometry(612, 792, 0));
  Assert.Equal(2, analysis.Blocks.Count);

  var first = analysis.Blocks[0];
  Assert.Equal("Montserrat", first.FontName);
  Assert.Equal(10, first.FontSize);
  Assert.Equal("#1a202c", first.Color);
  Assert.False(first.Bold);

  var second = analysis.Blocks[1];
  Assert.Equal("Montserrat", second.FontName);
  Assert.Equal(18, second.FontSize);
  Assert.True(second.Bold);
 }
 public void Dispose()=>Directory.Delete(directory,true);
}
