using Mythlix.Infrastructure;
using PdfSharp.Pdf;
using PdfSharp.Pdf.IO;
using Xunit;
namespace Mythlix.Tests;
public sealed class PdfIntegrationTests : IDisposable {
 readonly string folder=Path.Combine(Path.GetTempPath(),"mythlix-tests-"+Guid.NewGuid().ToString("N"));
 string Source=>Path.Combine(folder,"source.pdf");
 public PdfIntegrationTests(){Directory.CreateDirectory(folder);using var pdf=new PdfDocument();pdf.AddPage();pdf.AddPage();pdf.Save(Source);}
 [Fact] public async Task EditingAndSavingPreserveOriginalAndUndoRestoresStructure(){
  var original=await File.ReadAllBytesAsync(Source);
  using var session=new PdfDocumentSession(Source);var snapshot=session.Snapshot();
  session.Rotate(1);session.Delete(2);session.AddBlank();
  var copy=Path.Combine(folder,"copy.pdf");await session.SaveCopyAsync(copy);
  using(var result=PdfReader.Open(copy,PdfDocumentOpenMode.Import)){Assert.Equal(2,result.PageCount);Assert.Equal(90,result.Pages[0].Rotate);}
  Assert.Equal(original,await File.ReadAllBytesAsync(Source));
  session.Restore(snapshot);await session.SaveCopyAsync(copy);
  using var restored=PdfReader.Open(copy,PdfDocumentOpenMode.Import);Assert.Equal(0,restored.Pages[0].Rotate);
 }
 [Fact] public void LastPageCannotBeDeleted(){using var session=new PdfDocumentSession(Source);session.Delete(2);Assert.Throws<InvalidOperationException>(()=>session.Delete(1));Assert.Equal(1,session.PageCount);}
 [Fact] public async Task OriginalTextSurvivesPageRotationAndWriting(){
  var path=Path.Combine(folder,"text.pdf");
  var objects=new[]{"<< /Type /Catalog /Pages 2 0 R >>","<< /Type /Pages /Kids [3 0 R] /Count 1 >>","<< /Type /Page /Parent 2 0 R /MediaBox [0 0 612 792] /Resources << /Font << /F1 4 0 R >> >> /Contents 5 0 R >>","<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica >>"};
  const string content="BT /F1 18 Tf 72 700 Td (Mythlix integration text) Tj ET";
  var data=new System.Text.StringBuilder("%PDF-1.4\n");var offsets=new List<int>();
  foreach(var obj in objects){offsets.Add(data.Length);data.Append($"{offsets.Count} 0 obj\n{obj}\nendobj\n");}
  offsets.Add(data.Length);data.Append($"5 0 obj\n<< /Length {content.Length} >>\nstream\n{content}\nendstream\nendobj\n");
  var xref=data.Length;data.Append("xref\n0 6\n0000000000 65535 f \n");foreach(var offset in offsets)data.Append($"{offset:0000000000} 00000 n \n");
  data.Append($"trailer\n<< /Size 6 /Root 1 0 R >>\nstartxref\n{xref}\n%%EOF\n");File.WriteAllText(path,data.ToString(),System.Text.Encoding.ASCII);
  using var doc=new PdfDocumentSession(path);doc.Rotate(1);var copy=Path.Combine(folder,"text-copy.pdf");await doc.SaveCopyAsync(copy);
  var text=await new PopplerRenderer().ExtractTextAsync(copy,1,CancellationToken.None);Assert.Contains("Mythlix integration text",text);
 }
 [Fact] public async Task RenderProducesPngAndExtractsTextLocally(){
  var renderer=new PopplerRenderer();var png=await renderer.RenderAsync(Source,1,220,CancellationToken.None);
  Assert.Equal(new byte[]{137,80,78,71,13,10,26,10},png.Take(8));
  Assert.NotNull(await renderer.ExtractTextAsync(Source,1,CancellationToken.None));
 }
 [Fact] public async Task CancelledSaveLeavesNoTemporaryFiles(){using var session=new PdfDocumentSession(Source);using var cts=new CancellationTokenSource();cts.Cancel();await Assert.ThrowsAnyAsync<OperationCanceledException>(()=>session.SaveCopyAsync(Path.Combine(folder,"cancel.pdf"),cts.Token));Assert.False(File.Exists(Path.Combine(folder,"cancel.pdf")));Assert.Empty(Directory.GetFiles(folder,"*.tmp"));}
 [Fact] public void CorruptPdfIsRejected(){var path=Path.Combine(folder,"broken.pdf");File.WriteAllText(path,"invalid pdf");Assert.ThrowsAny<Exception>(()=>new PdfDocumentSession(path));}
 public void Dispose()=>Directory.Delete(folder,true);
}
