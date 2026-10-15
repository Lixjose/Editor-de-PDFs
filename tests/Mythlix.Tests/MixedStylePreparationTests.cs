using System.Text;
using Mythlix.Infrastructure;
using PdfSharp.Pdf;
using Xunit;

namespace Mythlix.Tests;

/// <summary>Preservation baseline for phase 1; deliberately does not enable mixed-style editing.</summary>
public sealed class MixedStylePreparationTests : IDisposable {
 readonly string directory = Path.Combine(Path.GetTempPath(), "mythlix-mixed-baseline-" + Guid.NewGuid().ToString("N"));
 string Source => Path.Combine(directory, "source.pdf");
 string Output => Path.Combine(directory, "output.pdf");
 public MixedStylePreparationTests() => Directory.CreateDirectory(directory);

 void Create(bool blue) {
  using var pdf = new PdfDocument();
  var page = pdf.AddPage();
  page.Width = PdfSharp.Drawing.XUnit.FromPoint(612);
  page.Height = PdfSharp.Drawing.XUnit.FromPoint(792);
  var resources = new PdfDictionary(pdf);
  var fonts = new PdfDictionary(pdf);
  foreach(var (key, name) in new[] {("/F1", "/Helvetica"), ("/F2", "/Helvetica-Bold"), ("/F3", "/Helvetica-Oblique")}) {
   var font = new PdfDictionary(pdf);
   font.Elements.SetName("/Type", "/Font");
   font.Elements.SetName("/Subtype", "/Type1");
   font.Elements.SetName("/BaseFont", name);
   font.Elements.SetName("/Encoding", "/WinAnsiEncoding");
   pdf.Internals.AddObject(font);
   fonts.Elements[key] = font.Reference!;
  }
  resources.Elements["/Font"] = fonts;
  page.Elements["/Resources"] = resources;
  var background = blue ? "0.12549 0.30588 0.46667 rg 40 650 530 65 re f\n" : "";
  var foreground = blue ? "1 1 1 rg" : "0 0 0 rg";
  // Fonts change inside the same text object, with a separate compatible line below.
  var content = background + $"BT {foreground} /F1 12 Tf 1 0 0 1 60 680 Tm (Normal ) Tj " +
   "/F2 12 Tf (negrita ) Tj /F3 12 Tf (cursiva) Tj ET\n" +
   "BT 0 0 0 rg /F1 12 Tf 1 0 0 1 60 600 Tm (Editable companion line) Tj ET";
  page.Contents.AppendContent().CreateStream(Encoding.ASCII.GetBytes(content));
  pdf.Save(Source);
 }

 [Theory]
 [InlineData(false)]
 [InlineData(true)]
 public async Task RoundTripPreservesMixedStylesAndDoesNotEnableUnsupportedEditing(bool blue) {
  Create(blue);
  var renderer = new PopplerRenderer();
  var original = await renderer.RenderAsync(Source, 1, 792, CancellationToken.None);
  using var doc = new PdfDocumentSession(Source);
  var analysis = doc.AnalyzeText(1);
  Assert.True(analysis.UnsupportedBlocks > 0);
  Assert.DoesNotContain(analysis.Blocks, b => b.Text.Contains("negrita"));
  Assert.Contains(analysis.Blocks, b => b.Text == "Editable companion line");
  await doc.SaveCopyAsync(Output);
  var text = await renderer.ExtractTextAsync(Output, 1, CancellationToken.None);
  Assert.Contains("Normal negrita cursiva", text);
  Assert.Equal(original, await renderer.RenderAsync(Output, 1, 792, CancellationToken.None));
 }

 [Theory]
 [InlineData(false)]
 [InlineData(true)]
 public async Task EditingCompanionAndRestoringHistoryPreservesMixedContent(bool blue) {
  Create(blue);
  var renderer = new PopplerRenderer();
  var original = await renderer.RenderAsync(Source, 1, 792, CancellationToken.None);
  using var doc = new PdfDocumentSession(Source);
  var before = doc.Snapshot();
  var companion = Assert.Single(doc.AnalyzeText(1).Blocks, b => b.Text == "Editable companion line");
  doc.ReplaceText(1, companion, "Edited companion", 12);
  await doc.SaveCopyAsync(Output);
  var editedText = await renderer.ExtractTextAsync(Output, 1, CancellationToken.None);
  Assert.Contains("Normal negrita cursiva", editedText);
  Assert.Contains("Edited companion", editedText);
  // Pixel preservation in the mixed-style region, even while another line is edited.
  using var expected = SkiaSharp.SKBitmap.Decode(original);
  using var actual = SkiaSharp.SKBitmap.Decode(await renderer.RenderAsync(Output, 1, 792, CancellationToken.None));
  for(var y = 75; y < 145; y++)
   for(var x = 30; x < 580; x++) Assert.Equal(expected.GetPixel(x,y), actual.GetPixel(x,y));
  doc.Restore(before);
  await doc.SaveCopyAsync(Output);
  Assert.Equal(original, await renderer.RenderAsync(Output, 1, 792, CancellationToken.None));
 }

 public void Dispose() => Directory.Delete(directory, true);
}
