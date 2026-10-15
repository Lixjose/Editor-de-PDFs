using Mythlix.Infrastructure;
using PdfSharp.Pdf.IO;
using SkiaSharp;
using Xunit;
namespace Mythlix.Tests;
public sealed class ImagesToPdfTests : IDisposable {
 readonly string root=Path.Combine(Path.GetTempPath(),"mythlix-convert-tests-"+Guid.NewGuid().ToString("N"));
 string Output=>Path.Combine(root,"output.pdf");
 public ImagesToPdfTests()=>Directory.CreateDirectory(root);
 string Image(string name,int width,int height,SKColor color) {
  var path=Path.Combine(root,name);using var bitmap=new SKBitmap(width,height);bitmap.Erase(color);
  using var image=SKImage.FromBitmap(bitmap);using var data=image.Encode(name.EndsWith(".jpg")?SKEncodedImageFormat.Jpeg:SKEncodedImageFormat.Png,95);
  using var stream=File.Create(path);data.SaveTo(stream);return path;
 }
 [Theory][InlineData(ImagePdfPaper.A4)][InlineData(ImagePdfPaper.Letter)]
 public async Task CreatesOrderedPagesWithAspectMarginsAndSourcesIntact(ImagePdfPaper paper) {
  var landscape=Image("landscape.png",200,100,SKColors.Red);var portrait=Image("portrait.jpg",100,200,SKColors.Blue);
  var before=File.ReadAllBytes(landscape);await new ImagesToPdfConverter().ConvertAsync([landscape,portrait],Output,paper,24);
  using(var pdf=PdfReader.Open(Output,PdfDocumentOpenMode.Import)) {
   Assert.Equal(2,pdf.PageCount);Assert.True(pdf.Pages[0].Width.Point>pdf.Pages[0].Height.Point);Assert.True(pdf.Pages[1].Width.Point<pdf.Pages[1].Height.Point);
   Assert.Equal(paper==ImagePdfPaper.Letter?612:595,pdf.Pages[1].Width.Point,0);
  }
  var renderer=new PopplerRenderer();
  for(var page=1;page<=2;page++) {
   using var rendered=SKBitmap.Decode(await renderer.RenderAsync(Output,page,800,default));
   var center=rendered.GetPixel(rendered.Width/2,rendered.Height/2);var corner=rendered.GetPixel(0,0);
   if(page==1)Assert.True(center.Red>240 && center.Blue<15);else Assert.True(center.Blue>240 && center.Red<15);
   Assert.True(corner.Red>240 && corner.Green>240 && corner.Blue>240);
  }
  Assert.Equal(before,File.ReadAllBytes(landscape));Assert.Empty(Directory.GetFiles(root,"*.tmp"));
 }
 [Theory][InlineData(-1)][InlineData(101)][InlineData(double.NaN)]
 public async Task RejectsInvalidMargin(double margin) {
  await Assert.ThrowsAsync<ArgumentException>(()=>new ImagesToPdfConverter().ConvertAsync(["unused.png"],Output,margin:margin));Assert.False(File.Exists(Output));
 }
 [Fact] public async Task RejectsEmptyAndOversizedBatch() {
  var converter=new ImagesToPdfConverter();
  await Assert.ThrowsAsync<ArgumentException>(()=>converter.ConvertAsync([],Output));
  await Assert.ThrowsAsync<ArgumentException>(()=>converter.ConvertAsync(Enumerable.Repeat("unused.png",101).ToArray(),Output));
 }
 [Fact] public async Task InvalidSecondImagePublishesNothing() {
  var valid=Image("valid.png",20,20,SKColors.Red);var invalid=Path.Combine(root,"bad.png");await File.WriteAllTextAsync(invalid,"invalid image");
  await Assert.ThrowsAnyAsync<Exception>(()=>new ImagesToPdfConverter().ConvertAsync([valid,invalid],Output));
  Assert.False(File.Exists(Output));Assert.Empty(Directory.GetFiles(root,"*.tmp"));
 }
 sealed class CancelProgress(CancellationTokenSource cts):IProgress<int>{public void Report(int value)=>cts.Cancel();}
 [Fact] public async Task CancellationAfterFirstPagePublishesNothing() {
  var valid=Image("valid.png",20,20,SKColors.Red);using var cts=new CancellationTokenSource();
  await Assert.ThrowsAnyAsync<OperationCanceledException>(()=>new ImagesToPdfConverter().ConvertAsync([valid,valid],Output,token:cts.Token,progress:new CancelProgress(cts)));
  Assert.False(File.Exists(Output));Assert.Empty(Directory.GetFiles(root,"*.tmp"));
 }
 [Fact] public async Task ExistingOutputIsProtected() {
  await File.WriteAllTextAsync(Output,"keep");await Assert.ThrowsAsync<IOException>(()=>new ImagesToPdfConverter().ConvertAsync(["unused.png"],Output));Assert.Equal("keep",await File.ReadAllTextAsync(Output));
 }
 [Fact] public async Task OversizedImageAndUnsupportedFormatAreRejected() {
  var large=Path.Combine(root,"large.png");using(var stream=File.Create(large))stream.SetLength(10*1024*1024+1);
  await Assert.ThrowsAsync<InvalidOperationException>(()=>new ImagesToPdfConverter().ConvertAsync([large],Output));
  await Assert.ThrowsAsync<ArgumentException>(()=>new ImagesToPdfConverter().ConvertAsync(["image.svg"],Output));Assert.False(File.Exists(Output));
 }
 public void Dispose()=>Directory.Delete(root,true);
}
