using Mythlix.Core;
using Mythlix.Infrastructure;
using Mythlix.Infrastructure.Ocr;
using PdfSharp.Drawing;
using PdfSharp.Pdf;
using PdfSharp.Pdf.IO;
using Xunit;

namespace Mythlix.Tests;

public sealed class PageDecorationAndOcrTests : IDisposable {
 readonly string folder = Path.Combine(Path.GetTempPath(), "mythlix-phase2-" + Guid.NewGuid().ToString("N"));

 public PageDecorationAndOcrTests() {
  Directory.CreateDirectory(folder);
 }

 string CreateTestPdf(string name, int pageCount) {
  var path = Path.Combine(folder, name);
  using var pdf = new PdfDocument();
  for (int i = 0; i < pageCount; i++) {
   var page = pdf.AddPage();
   page.Width = XUnit.FromPoint(600);
   page.Height = XUnit.FromPoint(800);
  }
  pdf.Save(path);
  return path;
 }

 [Fact]
 public async Task PdfMergerCombinesMultipleFilesWithProgressAndOrder() {
  var pdf1 = CreateTestPdf("doc1.pdf", 2);
  var pdf2 = CreateTestPdf("doc2.pdf", 3);
  var pdf3 = CreateTestPdf("doc3.pdf", 1);
  var output = Path.Combine(folder, "merged.pdf");

  var merger = new PdfMerger();
  var progressReports = new List<int>();
  var progress = new Progress<int>(v => progressReports.Add(v));

  await merger.MergeAsync(output, new[] { pdf1, pdf2, pdf3 }, CancellationToken.None, progress);

  Assert.True(File.Exists(output));
  using var merged = PdfReader.Open(output, PdfDocumentOpenMode.Import);
  Assert.Equal(6, merged.PageCount);
  Assert.True(File.Exists(pdf1));
  Assert.True(File.Exists(pdf2));
  Assert.True(File.Exists(pdf3));
 }

 [Fact]
 public async Task PdfMergerRejectsFewerThanTwoDocumentsOrMissingFiles() {
  var pdf1 = CreateTestPdf("docSingle.pdf", 1);
  var output = Path.Combine(folder, "merged_fail.pdf");
  var merger = new PdfMerger();

  await Assert.ThrowsAsync<ArgumentException>(() => merger.MergeAsync(output, new[] { pdf1 }));
  await Assert.ThrowsAsync<FileNotFoundException>(() => merger.MergeAsync(output, new[] { pdf1, Path.Combine(folder, "nonexistent.pdf") }));
 }

 [Fact]
 public async Task ApplyHeaderFooterRendersTokensAndSupportsUndo() {
  var pdfPath = CreateTestPdf("header_test.pdf", 3);
  using var doc = new PdfDocumentSession(pdfPath);
  var snapshot = doc.Snapshot();

  var options = new HeaderFooterOptions(
   Format: "Pág. {n} de {total}",
   Position: HeaderFooterPosition.BottomCenter,
   FontSize: 11,
   MarginX: 30,
   MarginY: 30,
   SkipFirstPage: true
  );

  doc.ApplyHeaderFooter(options);
  var output = Path.Combine(folder, "header_out.pdf");
  await doc.SaveCopyAsync(output);

  var renderer = new PopplerRenderer();
  var page1Text = await renderer.ExtractTextAsync(output, 1, CancellationToken.None);
  var page2Text = await renderer.ExtractTextAsync(output, 2, CancellationToken.None);
  var page3Text = await renderer.ExtractTextAsync(output, 3, CancellationToken.None);

  Assert.DoesNotContain("Pág. 1 de 3", page1Text);
  Assert.Contains("Pág. 2 de 3", page2Text);
  Assert.Contains("Pág. 3 de 3", page3Text);

  // Undo verification
  doc.Restore(snapshot);
  var undoneOutput = Path.Combine(folder, "header_undone.pdf");
  await doc.SaveCopyAsync(undoneOutput);
  var undonePage2 = await renderer.ExtractTextAsync(undoneOutput, 2, CancellationToken.None);
  Assert.DoesNotContain("Pág. 2 de 3", undonePage2);
 }

 [Fact]
 public async Task ApplyWatermarkRendersOnAllPagesWithAngle() {
  var pdfPath = CreateTestPdf("watermark_test.pdf", 2);
  using var doc = new PdfDocumentSession(pdfPath);
  var snapshot = doc.Snapshot();

  var options = new WatermarkOptions(
   Text: "COPIA NO OFICIAL",
   Rotation: 45,
   FontSize: 40,
   Color: "#FF0000",
   Opacity: 0.3
  );

  doc.ApplyWatermark(options);
  var output = Path.Combine(folder, "watermark_out.pdf");
  await doc.SaveCopyAsync(output);

  var renderer = new PopplerRenderer();
  var p1Text = await renderer.ExtractTextAsync(output, 1, CancellationToken.None);
  var p2Text = await renderer.ExtractTextAsync(output, 2, CancellationToken.None);

  // Poppler extracts diagonally rotated text across multiple stepped lines
  var p1Normalized = p1Text.Replace("\n", "").Replace("\r", "").Replace(" ", "");
  var p2Normalized = p2Text.Replace("\n", "").Replace("\r", "").Replace(" ", "");
  Assert.Contains("COPIANOOFICIAL", p1Normalized);
  Assert.Contains("COPIANOOFICIAL", p2Normalized);

  // Undo test
  doc.Restore(snapshot);
  var undoneOutput = Path.Combine(folder, "watermark_undone.pdf");
  await doc.SaveCopyAsync(undoneOutput);
  var undoneP1 = await renderer.ExtractTextAsync(undoneOutput, 1, CancellationToken.None);
  Assert.DoesNotContain("COPIA NO OFICIAL", undoneP1);
 }

 [Fact]
 public void TesseractEngineParsesTsvAndCalculatesBaselineAndFontSize() {
  // TSV with 2 lines, level 5 (words)
  var tsv = @"level	page_num	block_num	par_num	line_num	word_num	left	top	width	height	conf	text
1	1	0	0	0	0	0	0	1000	1200	-1	
2	1	1	0	0	0	50	50	900	200	-1	
3	1	1	1	0	0	50	50	900	200	-1	
4	1	1	1	1	0	50	50	400	60	-1	
5	1	1	1	1	1	50	50	180	60	98.5	Mythlix
5	1	1	1	1	2	250	50	200	60	96.2	Engine
4	1	1	1	2	0	50	150	300	50	-1	
5	1	1	1	2	1	50	150	120	50	95.0	Segunda
5	1	1	1	2	2	190	150	160	50	94.0	Línea
";

  var result = TesseractEngine.ParseTsv(tsv);

  Assert.Equal(4, result.Words.Count);
  Assert.Equal("Mythlix Engine\nSegunda Línea", result.Text);

  var word1 = result.Words[0];
  Assert.Equal("Mythlix", word1.Text);
  Assert.Equal(98.5, word1.Confidence);
  Assert.True(word1.BaselineY > 0, "BaselineY should be calculated");
  Assert.True(word1.EstimatedFontSize > 0, "EstimatedFontSize should be positive");

  // In line 1: both 'Mythlix' (y) and 'Engine' (g) have descenders, so baseline = top + height * 0.82 = 50 + 49.2 = 99.2
  Assert.Equal(50 + 60 * 0.82, word1.BaselineY, 0.1);
  Assert.Equal(60 * 0.9, word1.EstimatedFontSize, 0.1);
 }

 [Fact]
 public async Task RotatedPagesAt90And180And270DegreesSupportSearchableOcrLayer() {
  var pdfPath = Path.Combine(folder, "rotated_pages.pdf");
  using (var pdf = new PdfDocument()) {
   var p0 = pdf.AddPage(); p0.Width = XUnit.FromPoint(600); p0.Height = XUnit.FromPoint(800); p0.Rotate = 0;
   var p90 = pdf.AddPage(); p90.Width = XUnit.FromPoint(600); p90.Height = XUnit.FromPoint(800); p90.Rotate = 90;
   var p180 = pdf.AddPage(); p180.Width = XUnit.FromPoint(600); p180.Height = XUnit.FromPoint(800); p180.Rotate = 180;
   var p270 = pdf.AddPage(); p270.Width = XUnit.FromPoint(600); p270.Height = XUnit.FromPoint(800); p270.Rotate = 270;
   pdf.Save(pdfPath);
  }

  using var doc = new PdfDocumentSession(pdfPath);
  var ocrWord = new OcrWord("RotatedKeyword", 99, 100, 100, 200, 40) { BaselineY = 132, EstimatedFontSize = 34 };
  var ocrResult = new OcrPageResult(1200, 1600, [ocrWord]);

  // Apply OCR to each rotated page
  doc.SetOcr(1, ocrResult);
  doc.SetOcr(2, ocrResult);
  doc.SetOcr(3, ocrResult);
  doc.SetOcr(4, ocrResult);

  var output = Path.Combine(folder, "rotated_ocr_out.pdf");
  await doc.SaveCopyAsync(output);

  var renderer = new PopplerRenderer();
  for (int p = 1; p <= 4; p++) {
   var text = await renderer.ExtractTextAsync(output, p, CancellationToken.None);
   Assert.Contains("RotatedKeyword", text);
  }
 }

 public void Dispose() {
  try {
   if (Directory.Exists(folder)) Directory.Delete(folder, true);
  } catch { }
 }
}
