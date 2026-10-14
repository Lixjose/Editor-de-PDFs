using Mythlix.Infrastructure;
using PdfSharp.Pdf;
using PdfSharp.Pdf.IO;
using PdfSharp.Drawing;
using Xunit;
namespace Mythlix.Tests;
public sealed class PdfSplitTests : IDisposable {
 readonly string root=Path.Combine(Path.GetTempPath(),"mythlix-split-tests-"+Guid.NewGuid().ToString("N"));
 string Source=>Path.Combine(root,"source.pdf");
 string Destination=>Path.Combine(root,"parts");
 public PdfSplitTests() {
  Directory.CreateDirectory(root);using var pdf=new PdfDocument();
  for(var i=0;i<5;i++){var page=pdf.AddPage();page.Rotate=i%4*90;page.Width=XUnit.FromPoint(400+i*10);}
  pdf.Save(Source);
 }
 [Theory][InlineData(1,5)][InlineData(2,3)][InlineData(5,1)]
 public async Task SplitPreservesPageOrderGeometryAndOriginal(int perPart,int count) {
  var before=File.ReadAllBytes(Source);using var doc=new PdfDocumentSession(Source);
  var files=await doc.SplitAsync(Destination,perPart);
  Assert.Equal(count,files.Count);var index=0;
  foreach(var file in files) {
   using var output=PdfReader.Open(file,PdfDocumentOpenMode.Import);
   Assert.Equal(Math.Min(perPart,5-index),output.PageCount);
   foreach(var page in output.Pages){Assert.Equal(index%4*90,page.Rotate);Assert.Equal(400+index*10,page.Width.Point);index++;}
  }
  Assert.Equal(5,index);Assert.Equal(5,doc.PageCount);Assert.Equal(before,File.ReadAllBytes(Source));
  Assert.Empty(Directory.GetDirectories(root,".mythlix-split-*"));
  doc.Rotate(1);Assert.Equal(90,doc.GetPageGeometry(1).Rotation);
 }
 [Theory][InlineData(0)][InlineData(-1)][InlineData(6)]
 public async Task InvalidPartSizeWritesNothing(int size) {
  using var doc=new PdfDocumentSession(Source);
  await Assert.ThrowsAsync<ArgumentOutOfRangeException>(()=>doc.SplitAsync(Destination,size));
  Assert.False(Directory.Exists(Destination));Assert.Single(Directory.GetFileSystemEntries(root));
 }
 [Fact] public async Task ExistingDestinationIsNeverReplaced() {
  Directory.CreateDirectory(Destination);var marker=Path.Combine(Destination,"keep.txt");await File.WriteAllTextAsync(marker,"keep");
  using var doc=new PdfDocumentSession(Source);
  await Assert.ThrowsAsync<IOException>(()=>doc.SplitAsync(Destination,2));
  Assert.Equal("keep",await File.ReadAllTextAsync(marker));Assert.Single(Directory.GetFiles(Destination));
 }
 [Fact] public async Task FileDestinationAndMissingParentAreRejected() {
  using var doc=new PdfDocumentSession(Source);var before=File.ReadAllBytes(Source);
  await Assert.ThrowsAsync<IOException>(()=>doc.SplitAsync(Source,1));
  await Assert.ThrowsAsync<DirectoryNotFoundException>(()=>doc.SplitAsync(Path.Combine(root,"missing","parts"),1));
  Assert.Equal(before,File.ReadAllBytes(Source));
 }
 [Fact] public async Task CancelledExportLeavesNoPartsOrStaging() {
  using var doc=new PdfDocumentSession(Source);using var cts=new CancellationTokenSource();cts.Cancel();
  await Assert.ThrowsAnyAsync<OperationCanceledException>(()=>doc.SplitAsync(Destination,1,cts.Token));
  Assert.False(Directory.Exists(Destination));Assert.Empty(Directory.GetDirectories(root));
 }
 [Fact] public async Task CancellationAfterFirstPartRemovesStagedFiles() {
  using var doc=new PdfDocumentSession(Source);using var cts=new CancellationTokenSource();
  var progress=new CancelAfterFirst(cts);
  await Assert.ThrowsAnyAsync<OperationCanceledException>(()=>doc.SplitAsync(Destination,1,cts.Token,progress));
  Assert.Equal(1,progress.Completed);Assert.False(Directory.Exists(Destination));Assert.Empty(Directory.GetDirectories(root));
  Assert.Equal(5,doc.PageCount);
 }
 sealed class CancelAfterFirst(CancellationTokenSource cts) : IProgress<int> {
  public int Completed;public void Report(int value){Completed=value;cts.Cancel();}
 }
 public void Dispose()=>Directory.Delete(root,true);
}
