using Mythlix.Core;
using Mythlix.Infrastructure;
using PdfSharp.Pdf;
using SkiaSharp;
using Xunit;
namespace Mythlix.Tests;
public sealed class PdfImageExportTests : IDisposable {
 readonly string root=Path.Combine(Path.GetTempPath(),"mythlix-images-tests-"+Guid.NewGuid().ToString("N"));
 string Destination=>Path.Combine(root,"images");
 public PdfImageExportTests()=>Directory.CreateDirectory(root);
 sealed class Renderer : IPdfRenderer {
  public List<int> Pages=new();public int FailOn;
  public Task<byte[]> RenderAsync(string path,int page,int width,CancellationToken token) {
   Pages.Add(page);if(Pages.Count==FailOn)throw new InvalidOperationException("Rendering failed");
   return Task.FromResult(new byte[]{(byte)page});
  }
  public Task<string> ExtractTextAsync(string path,int page,CancellationToken token)=>throw new NotSupportedException();
 }
 [Fact] public async Task SelectionOrderAndNamesArePreserved() {
  var renderer=new Renderer();var paths=await new PdfImageExporter(renderer).ExportAsync("source",3,[3,1],Destination,1600);
  Assert.Equal(new[]{3,1},renderer.Pages);Assert.Equal("0001-pagina-0003.png",Path.GetFileName(paths[0]));
  Assert.Equal(new byte[]{3},await File.ReadAllBytesAsync(paths[0]));Assert.Equal(new byte[]{1},await File.ReadAllBytesAsync(paths[1]));
  Assert.Empty(Directory.GetDirectories(root,".mythlix-images-*"));
 }
 [Theory][InlineData(127)][InlineData(2401)]
 public async Task InvalidSizeDoesNotRender(int size) {
  var renderer=new Renderer();await Assert.ThrowsAsync<ArgumentOutOfRangeException>(()=>new PdfImageExporter(renderer).ExportAsync("source",3,[1],Destination,size));
  Assert.Empty(renderer.Pages);Assert.Empty(Directory.GetFileSystemEntries(root));
 }
 [Theory][InlineData(new int[0])][InlineData(new[]{1,1})][InlineData(new[]{0})][InlineData(new[]{4})]
 public async Task InvalidPagesDoNotRender(int[] pages) {
  var renderer=new Renderer();await Assert.ThrowsAsync<ArgumentException>(()=>new PdfImageExporter(renderer).ExportAsync("source",3,pages,Destination,1600));
  Assert.Empty(renderer.Pages);Assert.Empty(Directory.GetFileSystemEntries(root));
 }
 [Fact] public async Task RenderingFailureRemovesAllStagedImages() {
  var renderer=new Renderer{FailOn=2};await Assert.ThrowsAsync<InvalidOperationException>(()=>new PdfImageExporter(renderer).ExportAsync("source",3,[1,2,3],Destination,1600));
  Assert.Equal(new[]{1,2},renderer.Pages);Assert.Empty(Directory.GetFileSystemEntries(root));
 }
 sealed class CancelProgress(CancellationTokenSource cts) : IProgress<int> { public void Report(int value)=>cts.Cancel(); }
 [Fact] public async Task CancellationAfterFirstImagePublishesNothing() {
  using var cts=new CancellationTokenSource();var renderer=new Renderer();
  await Assert.ThrowsAnyAsync<OperationCanceledException>(()=>new PdfImageExporter(renderer).ExportAsync("source",3,[1,2],Destination,1600,cts.Token,new CancelProgress(cts)));
  Assert.Single(renderer.Pages);Assert.Empty(Directory.GetFileSystemEntries(root));
 }
 [Fact] public async Task ExistingDestinationIsProtected() {
  Directory.CreateDirectory(Destination);var marker=Path.Combine(Destination,"keep");await File.WriteAllTextAsync(marker,"original");var renderer=new Renderer();
  await Assert.ThrowsAsync<IOException>(()=>new PdfImageExporter(renderer).ExportAsync("source",3,[1],Destination,1600));
  Assert.Empty(renderer.Pages);Assert.Equal("original",await File.ReadAllTextAsync(marker));
 }
 [Fact] public async Task RealPngPreservesAspectRotationAndSource() {
  var source=Path.Combine(root,"source.pdf");
  using(var pdf=new PdfDocument()) {var page=pdf.AddPage();page.Width=PdfSharp.Drawing.XUnit.FromPoint(400);page.Height=PdfSharp.Drawing.XUnit.FromPoint(800);page.Rotate=90;pdf.Save(source);}
  var original=await File.ReadAllBytesAsync(source);
  var paths=await new PdfImageExporter(new PopplerRenderer()).ExportAsync(source,1,[1],Destination,800);
  using var bitmap=SKBitmap.Decode(paths.Single());Assert.NotNull(bitmap);Assert.Equal(800,bitmap.Width);Assert.Equal(400,bitmap.Height);
  Assert.Equal(original,await File.ReadAllBytesAsync(source));
 }
 public void Dispose()=>Directory.Delete(root,true);
}
