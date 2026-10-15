using Mythlix.Core;
using Mythlix.Infrastructure;
using Mythlix.Infrastructure.TextEditing;
using Xunit;

namespace Mythlix.Tests;

public sealed class TextEditingFidelityTests {
 [Fact] public void XmlExtractionPreservesAdjacentStyleChangesAndFractionalSize() {
  const string xml = """
  <pdf2xml><page number="1" width="612" height="792">
   <fontspec id="0" size="11.25" family="ABCDEF+NotoSans-Regular" color="#111111"/>
   <fontspec id="1" size="11.25" family="ABCDEF+NotoSans-Bold" color="#111111"/>
   <fontspec id="2" size="14" family="ABCDEF+NotoSans-Regular" color="#111111"/>
   <text top="50" left="60" width="40" height="12" font="0">Normal</text>
   <text top="50" left="100" width="40" height="12" font="1">Bold</text>
   <text top="50" left="140" width="40" height="16" font="2">Large</text>
  </page></pdf2xml>
  """;
  var blocks = VectorTextExtractor.ExtractFromXml(xml, 1, new(612,792,0)).Blocks;
  Assert.Equal(3, blocks.Count);
  Assert.Equal("NotoSans", blocks[0].FontName);
  Assert.Equal(11.25, blocks[0].FontSize);
  Assert.False(blocks[0].Bold);
  Assert.True(blocks[1].Bold);
  Assert.Equal(14, blocks[2].FontSize);
 }

 [Fact] public void WidthCalibrationUsesExportFontWithoutChangingFontSize() {
  const double size = 11.25;
  var width = TextFontMeasurement.Width("Una linea de ejemplo", "Helvetica", size, false, false);
  var scale = TextFontMeasurement.Calibrate("Una linea de ejemplo", "Helvetica", size, false, false, width * 0.9);
  Assert.Equal(0.9, scale, 6);
  Assert.Equal("Liberation Sans", TextFontMeasurement.Family("ABCDEF+Helvetica"));
 }

 [Fact] public async Task CalibratedExportStaysInsideOriginalLineBox() {
  var directory = Path.Combine(Path.GetTempPath(), "mythlix-width-" + Guid.NewGuid().ToString("N"));
  Directory.CreateDirectory(directory);
  try {
   var path = Path.Combine(directory, "source.pdf");
   using(var source = new PdfSharp.Pdf.PdfDocument()) {
    var page = source.AddPage(); page.Width=PdfSharp.Drawing.XUnit.FromPoint(612); page.Height=PdfSharp.Drawing.XUnit.FromPoint(792); source.Save(path);
   }
   using var doc = new PdfDocumentSession(path);
   const string text = "Texto con ancho conservado";
   var width = TextFontMeasurement.Width(text, "Helvetica", 12, false, false) * 0.9;
   doc.SetGraphics(1, [new() {Kind=GraphicKind.Text,Text=text,X=60,Y=100,Width=width,Height=20,
    FontSize=12,FontFamily="Helvetica",HorizontalScale=0.9,ConstrainTextWidth=true,Baseline=12,Color="#000000"}]);
   var output=Path.Combine(directory,"output.pdf");
   await doc.SaveCopyAsync(output);
   using var bitmap=SkiaSharp.SKBitmap.Decode(await new PopplerRenderer().RenderAsync(output,1,792,CancellationToken.None));
   var dark=new List<int>();
   for(var y=95;y<125;y++) for(var x=0;x<bitmap.Width;x++)
    if(bitmap.GetPixel(x,y).Red<100) dark.Add(x);
   Assert.NotEmpty(dark);
   Assert.InRange(dark.Min(),59,62);
   Assert.InRange(dark.Max(),60+width-5,60+width+1);
  } finally {Directory.Delete(directory,true);}
 }

 [Fact] public async Task ConstrainedTextRejectsOverflowAndPreservesSavedLayer() {
  var directory = Path.Combine(Path.GetTempPath(), "mythlix-fit-" + Guid.NewGuid().ToString("N"));
  Directory.CreateDirectory(directory);
  try {
   var path = Path.Combine(directory, "source.pdf");
   using(var source = new PdfSharp.Pdf.PdfDocument()) { source.AddPage(); source.Save(path); }
   using var doc = new PdfDocumentSession(path);
   var item = new GraphicObject { Kind=GraphicKind.Text, Text="Texto", X=50, Y=100,
    Width=80, Height=20, FontSize=12, HorizontalScale=0.9, ConstrainTextWidth=true };
   doc.SetGraphics(1, [item]);
   Assert.Throws<InvalidOperationException>(() => doc.SetGraphics(1, [item with {Text=new string('W',100)}]));
   Assert.Equal("Texto", Assert.Single(doc.GetGraphics(1)).Text);
   var output = Path.Combine(directory, "saved.pdf");
   await doc.SaveCopyAsync(output);
   using var reopened = new PdfDocumentSession(output);
   var saved = Assert.Single(reopened.GetGraphics(1));
   Assert.Equal(0.9, saved.HorizontalScale);
   Assert.True(saved.ConstrainTextWidth);
   Assert.Contains("Texto", await new PopplerRenderer().ExtractTextAsync(output,1,CancellationToken.None));
  } finally { Directory.Delete(directory, true); }
 }
}
