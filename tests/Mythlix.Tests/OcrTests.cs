using Mythlix.Core;
using Mythlix.Infrastructure;
using Mythlix.Infrastructure.Ocr;
using PdfSharp.Drawing;
using PdfSharp.Pdf;
using SkiaSharp;
using Xunit;
namespace Mythlix.Tests;
public sealed class OcrTests:IDisposable {
 readonly string folder=Path.Combine(Path.GetTempPath(),"mythlix-ocr-test-"+Guid.NewGuid().ToString("N"));
 string Source=>Path.Combine(folder,"scan.pdf");string Output=>Path.Combine(folder,"searchable.pdf");
 public OcrTests(){Directory.CreateDirectory(folder);using var doc=new PdfDocument();var page=doc.AddPage();page.Width=XUnit.FromPoint(600);page.Height=XUnit.FromPoint(800);using var image=XImage.FromStream(new MemoryStream(Scan()));using(var g=XGraphics.FromPdfPage(page))g.DrawImage(image,0,0,600,800);doc.Save(Source);}
 public static byte[] Scan(){
  using var bitmap=new SKBitmap(1200,1600);using var canvas=new SKCanvas(bitmap);canvas.Clear(SKColors.White);
  using var stream=typeof(PdfDocumentSession).Assembly.GetManifestResourceStream("Mythlix.Infrastructure.Fonts.LiberationSans-Regular.ttf")!;
  using var face=SKTypeface.FromStream(stream);using var font=new SKFont(face,58);using var paint=new SKPaint{Color=SKColors.Black,IsAntialias=true};
  canvas.DrawText("Documento escaneado",80,180,font,paint);canvas.DrawText("Reconocimiento local en español",80,280,font,paint);
  canvas.DrawText("LOCAL OCR TEST",80,420,font,paint);canvas.DrawText("Texto para buscar y seleccionar",80,540,font,paint);
  using var encoded=bitmap.Encode(SKEncodedImageFormat.Png,100);return encoded.ToArray();
 }
 [Fact] public async Task TesseractRecognizesSpanishAndEnglishLocally(){var engine=new TesseractEngine();var languages=await engine.LanguagesAsync(CancellationToken.None);Assert.Contains("spa",languages);Assert.Contains("eng",languages);var result=await engine.RecognizeAsync(Scan(),"spa+eng",CancellationToken.None);Assert.Contains("Documento",result.Text,StringComparison.OrdinalIgnoreCase);Assert.Contains("LOCAL",result.Text,StringComparison.OrdinalIgnoreCase);Assert.All(result.Words,w=>Assert.InRange(w.Confidence,0,100));}
 [Fact] public async Task SearchableLayerPreservesPixelsAndMakesScanExtractable(){
  var renderer=new PopplerRenderer();Assert.True(string.IsNullOrWhiteSpace(await renderer.ExtractTextAsync(Source,1,CancellationToken.None)));
  var before=await renderer.RenderAsync(Source,1,800,CancellationToken.None);var result=await new TesseractEngine().RecognizeAsync(Scan(),"spa+eng",CancellationToken.None);
  using var doc=new PdfDocumentSession(Source);var snapshot=doc.Snapshot();doc.SetOcr(1,result);await doc.SaveCopyAsync(Output);
  Assert.Equal(before,await renderer.RenderAsync(Output,1,800,CancellationToken.None));Assert.Contains("Documento",await renderer.ExtractTextAsync(Output,1,CancellationToken.None),StringComparison.OrdinalIgnoreCase);
  doc.Restore(snapshot);await doc.SaveCopyAsync(Output);Assert.True(string.IsNullOrWhiteSpace(await renderer.ExtractTextAsync(Output,1,CancellationToken.None)));
 }
 [Fact] public async Task CorrectedWordAndReplacingLayerDoNotDuplicateText(){
  var result=new OcrPageResult(1200,1600,[new("CORREGIDO",99,80,120,330,60)]);using var doc=new PdfDocumentSession(Source);
  doc.SetOcr(1,result);await doc.SaveCopyAsync(Output);doc.SetOcr(1,result with{Words=[new("REVISADO",99,80,120,330,60)]});await doc.SaveCopyAsync(Output);
  var text=await new PopplerRenderer().ExtractTextAsync(Output,1,CancellationToken.None);Assert.DoesNotContain("CORREGIDO",text);Assert.Single(System.Text.RegularExpressions.Regex.Matches(text,"REVISADO").Cast<System.Text.RegularExpressions.Match>());
 }
 [Fact] public void InvalidWordCannotMutateDocument(){using var doc=new PdfDocumentSession(Source);Assert.Throws<InvalidOperationException>(()=>doc.SetOcr(1,new(1200,1600,[new("bad",99,1300,0,20,20)])));Assert.Equal(1,doc.PageCount);}
 [Fact] public async Task CancelledRecognitionAndInvalidLanguageFailCleanly(){var engine=new TesseractEngine();using var cts=new CancellationTokenSource();cts.Cancel();await Assert.ThrowsAnyAsync<OperationCanceledException>(()=>engine.RecognizeAsync(Scan(),"eng",cts.Token));await Assert.ThrowsAsync<InvalidOperationException>(()=>engine.RecognizeAsync(Scan(),"eng;bad",CancellationToken.None));}
 [Fact] public void TsvParsingIsCultureIndependentAndValidatesCoordinates(){var tsv="level\tpage_num\tblock_num\tpar_num\tline_num\tword_num\tleft\ttop\twidth\theight\tconf\ttext\n1\t1\t0\t0\t0\t0\t0\t0\t100\t200\t-1\t\n5\t1\t1\t1\t1\t1\t10\t20\t30\t40\t97.25\thola\n";var result=TesseractEngine.ParseTsv(tsv);Assert.Equal(97.25,Assert.Single(result.Words).Confidence);Assert.Equal("hola",result.Text);}
 [Fact] public async Task ReconstructionCreatesSeparateEditableTextObjects(){
  var result=new OcrPageResult(1200,1600,[new("Documento",99,80,120,300,60),new("revisado",95,400,120,250,60)]);
  OcrReconstruction.Save(Output,[(new(600,800,0),result)]);using var doc=new PdfDocumentSession(Output);
  Assert.NotEmpty(doc.GetGraphics(1));Assert.All(doc.GetGraphics(1),o=>Assert.Equal(GraphicKind.Text,o.Kind));
  Assert.Contains("Documento",await new PopplerRenderer().ExtractTextAsync(Output,1,CancellationToken.None));
 }
 [Fact] public void RotatedPageSupportsOcrLayerTransformation(){using var doc=new PdfDocumentSession(Source);doc.Rotate(1);var ex=Record.Exception(()=>doc.SetOcr(1,new(1200,1600,[new("test",99,80,120,100,60)])));Assert.Null(ex);}
 sealed class InlineProgress(Action<int> action):IProgress<int>{public void Report(int value)=>action(value);}
 [Fact] public async Task CancelledBatchRestoresAlreadyProcessedPages(){
  var two=Path.Combine(folder,"two.pdf");
  using(var original=PdfSharp.Pdf.IO.PdfReader.Open(Source,PdfSharp.Pdf.IO.PdfDocumentOpenMode.Modify)) {
   var second=original.AddPage();second.Width=XUnit.FromPoint(600);second.Height=XUnit.FromPoint(800);
   second.Elements["/Resources"]=original.Pages[0].Elements["/Resources"];second.Contents.Elements.Add(original.Pages[0].Contents.Elements[0]);original.Save(two);
  }
  using var doc=new PdfDocumentSession(two);using var cancellation=new CancellationTokenSource();var result=new OcrPageResult(1200,1600,[new("BATCH",99,80,120,300,60)]);
  Assert.ThrowsAny<OperationCanceledException>(()=>doc.SetOcrPages([(1,result),(2,result)],cancellation.Token,new InlineProgress(n=>{if(n==1)cancellation.Cancel();})));
  await doc.SaveCopyAsync(Output);Assert.True(string.IsNullOrWhiteSpace(await new PopplerRenderer().ExtractTextAsync(Output,1,CancellationToken.None)));
 }
 [Fact] public async Task BatchAddsDistinctSearchableLayersToTwoPages(){
  var two=Path.Combine(folder,"batch.pdf");
  using(var original=PdfSharp.Pdf.IO.PdfReader.Open(Source,PdfSharp.Pdf.IO.PdfDocumentOpenMode.Modify)){
   var second=original.AddPage();second.Width=XUnit.FromPoint(600);second.Height=XUnit.FromPoint(800);second.Elements["/Resources"]=original.Pages[0].Elements["/Resources"];second.Contents.Elements.Add(original.Pages[0].Contents.Elements[0]);original.Save(two);
  }
  using var doc=new PdfDocumentSession(two);var first=new OcrPageResult(1200,1600,[new("FIRST",99,80,120,200,60)]);var secondResult=first with{Words=[new("SECOND",99,80,120,200,60)]};
  doc.SetOcrPages([(1,first),(2,secondResult)],CancellationToken.None);await doc.SaveCopyAsync(Output);
  var renderer=new PopplerRenderer();Assert.Contains("FIRST",await renderer.ExtractTextAsync(Output,1,CancellationToken.None));Assert.Contains("SECOND",await renderer.ExtractTextAsync(Output,2,CancellationToken.None));
 }
 [Fact] public void TsvPreservesLinesAndColumnsInExport() {
  var tsv="1\t1\t0\t0\t0\t0\t0\t0\t1000\t1000\t-1\t\n"+
   "5\t1\t1\t1\t1\t1\t10\t20\t80\t20\t99\tHola\n"+
   "5\t1\t1\t1\t1\t2\t100\t20\t80\t20\t99\tmundo\n"+
   "5\t1\t1\t1\t2\t1\t10\t60\t80\t20\t99\tLínea\n"+
   "5\t1\t2\t1\t1\t1\t500\t20\t80\t20\t65\tColumna\n";
  var result=TesseractEngine.ParseTsv(tsv);
  Assert.Equal("Hola mundo\nLínea\nColumna",result.Text);
  Assert.Equal(2,result.Words.Last().Block);
  Assert.Equal("Corregida",(result with{Words=[result.Words[0] with{Text="Corregida"}]}).Text);
 }
 [Fact] public void TextFallbackKeepsLineBreaksForUnstructuredResults() {
  var result=new OcrPageResult(1000,1000,[new("Primera",99,10,20,80,20),new("línea",99,100,20,80,20),new("Segunda",99,10,60,80,20)]);
  Assert.Equal("Primera línea\nSegunda",result.Text);
 }
 [Fact] public async Task UniformBlockModeRecognizesText() {
  var result=await new TesseractEngine().RecognizeAsync(Scan(),"spa+eng",new(OcrPageMode.UniformBlock),CancellationToken.None);
  Assert.Contains("Documento",result.Text,StringComparison.OrdinalIgnoreCase);Assert.Contains('\n',result.Text);
 }
 [Fact] public async Task InvalidModeAndOversizedImageAreRejectedBeforeRecognition() {
  var engine=new TesseractEngine();
  await Assert.ThrowsAsync<ArgumentException>(()=>engine.RecognizeAsync([],"eng",new((OcrPageMode)999),CancellationToken.None));
  var image=Scan();System.Buffers.Binary.BinaryPrimitives.WriteUInt32BigEndian(image.AsSpan(16,4),100000);
  await Assert.ThrowsAsync<InvalidOperationException>(()=>engine.RecognizeAsync(image,"eng",CancellationToken.None));
  Assert.Throws<InvalidOperationException>(()=>TesseractEngine.ValidateImage(new byte[40]));
 }
 [Fact] public void ReconstructionRespectsTsvLineBoundariesEvenAtSimilarCoordinates() {
  var result=new OcrPageResult(1200,1600,[new("Izquierda",99,80,120,150,60){Block=1,Line=1},new("Derecha",99,240,120,150,60){Block=2,Line=1}]);
  OcrReconstruction.Save(Output,[(new(600,800,0),result)]);using var doc=new PdfDocumentSession(Output);
  Assert.Equal(2,doc.GetGraphics(1).Count);
 }
 public void Dispose()=>Directory.Delete(folder,true);
}
