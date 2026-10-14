using Mythlix.Core;
using Mythlix.Infrastructure;
using PdfSharp.Pdf;
using PdfSharp.Pdf.IO;
using Xunit;
namespace Mythlix.Tests;
public sealed class PageOrganizationTests : IDisposable {
 readonly string folder=Path.Combine(Path.GetTempPath(),"mythlix-pages-"+Guid.NewGuid().ToString("N"));
 string Source=>Path.Combine(folder,"input.pdf");
 public PageOrganizationTests() {
  Directory.CreateDirectory(folder);using var pdf=new PdfDocument();
  for(int i=0;i<3;i++)pdf.AddPage().Rotate=i*90;
  pdf.Save(Source);
 }
 [Fact] public async Task DuplicateIsIndependentAndMoveSurvivesReopening() {
  using var doc=new PdfDocumentSession(Source);var before=doc.Snapshot();
  doc.DuplicatePage(2);doc.Rotate(3);
  Assert.Equal(90,doc.GetPageGeometry(2).Rotation);Assert.Equal(180,doc.GetPageGeometry(3).Rotation);
  doc.MovePage(4,1);var output=Path.Combine(folder,"out.pdf");await doc.SaveCopyAsync(output);
  using var reopened=new PdfDocumentSession(output);Assert.Equal(4,reopened.PageCount);Assert.Equal(180,reopened.GetPageGeometry(1).Rotation);
  doc.Restore(before);Assert.Equal(3,doc.PageCount);Assert.Equal(0,doc.GetPageGeometry(1).Rotation);
 }
 [Fact] public async Task InsertAndExtractKeepOrderAndSource() {
  var original=File.ReadAllBytes(Source);using var doc=new PdfDocumentSession(Source);
  doc.InsertPages(Source,1);Assert.Equal(6,doc.PageCount);
  var output=Path.Combine(folder,"selected.pdf");await doc.ExtractPagesAsync(output,[6,1,3]);
  using var result=PdfReader.Open(output,PdfDocumentOpenMode.Import);
  Assert.Equal(new[]{180,0,90},Enumerable.Range(0,3).Select(i=>result.Pages[i].Rotate));
  Assert.Equal(original,File.ReadAllBytes(Source));Assert.Equal(6,doc.PageCount);
 }
 [Fact] public async Task InvalidSelectionAndCancellationDoNotWrite() {
  using var doc=new PdfDocumentSession(Source);var output=Path.Combine(folder,"none.pdf");
  await Assert.ThrowsAsync<ArgumentException>(()=>doc.ExtractPagesAsync(output,[1,1]));
  await Assert.ThrowsAsync<ArgumentOutOfRangeException>(()=>doc.ExtractPagesAsync(output,[4]));
  using var cts=new CancellationTokenSource();cts.Cancel();
  await Assert.ThrowsAnyAsync<OperationCanceledException>(()=>doc.ExtractPagesAsync(output,[1],cts.Token));
  Assert.False(File.Exists(output));Assert.Empty(Directory.GetFiles(folder,"*.tmp"));
  Assert.Throws<ArgumentOutOfRangeException>(()=>doc.MovePage(1,4));Assert.Equal(3,doc.PageCount);
 }
 [Theory][InlineData("")][InlineData("0")][InlineData("3-2")][InlineData("1,1")][InlineData("1-4")][InlineData("1-2-3")]
 public void RejectMalformedRanges(string text)=>Assert.Throws<ArgumentException>(()=>PageSelection.Parse(text,3));
 [Fact] public void ParseKeepsRequestedOrder()=>Assert.Equal(new[]{3,1,2},PageSelection.Parse("3,1-2",3));
 public void Dispose()=>Directory.Delete(folder,true);
}
