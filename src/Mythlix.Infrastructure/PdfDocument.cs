using Mythlix.Core;
using PdfSharp.Pdf;
using PdfSharp.Pdf.IO;
namespace Mythlix.Infrastructure;
public sealed class PdfDocumentSession : IPdfDocument, IContentEditor, ISearchablePdfEditor, IPageDecorator {
 PdfDocument document;
 public PdfDocumentSession(string path) {
  if (new FileInfo(path).Length > 256L * 1024 * 1024) throw new InvalidOperationException("Límite inicial: 256 MB por documento.");
  document = PdfReader.Open(path, PdfDocumentOpenMode.Modify);
  // Signed files remain read-only: this version cannot validate signature permissions.
  if (document.Internals.GetAllObjects().OfType<PdfDictionary>().Any(d => d.Elements.ContainsKey("/ByteRange"))) {
   document.Dispose(); throw new InvalidOperationException("PDF firmado: edición bloqueada para preservar la firma. Use un visor con validación criptográfica.");
  }
  if(document.PageCount == 0) { document.Dispose(); throw new InvalidOperationException("El PDF no contiene páginas."); }
 }
 public int PageCount => document.PageCount;
 public PageGeometry GetPageGeometry(int page) {
  var p = document.Pages[page - 1]; var media = p.MediaBoxReadOnly; var crop = p.CropBoxReadOnly;
  if(crop.Width <= 0 || crop.Height <= 0) crop = media;
  var rotation = ((p.Rotate % 360) + 360) % 360;
  var unit = p.Elements.GetReal("/UserUnit");
  var editable = (unit == 0 || Math.Abs(unit - 1) < .01) && rotation % 90 == 0 && Math.Abs(media.X1) < .01 && Math.Abs(media.Y1) < .01 &&
   Math.Abs(crop.X1-media.X1) < .01 && Math.Abs(crop.Y1-media.Y1) < .01 &&
   Math.Abs(crop.X2-media.X2) < .01 && Math.Abs(crop.Y2-media.Y2) < .01;
  return new(media.Width, media.Height, rotation, editable);
 }
 public IReadOnlyList<GraphicObject> GetGraphics(int page) => GraphicLayerWriter.Read(document.Pages[page-1],GetPageGeometry(page));
 public void SetGraphics(int page, IReadOnlyList<GraphicObject> objects) {
  var before = Snapshot();
  try { GraphicLayerWriter.Write(document.Pages[page-1],objects,GetPageGeometry(page)); }
  catch { Restore(before); throw; }
 }
 public void SetOcr(int page,OcrPageResult result)=>SetOcrPages([(page,result)],CancellationToken.None);
 public void SetOcrPages(IReadOnlyList<(int Page,OcrPageResult Result)> pages,CancellationToken token,IProgress<int>? progress=null){
  token.ThrowIfCancellationRequested();if(pages.Count==0)return;var before=Snapshot();
  try{for(var i=0;i<pages.Count;i++){token.ThrowIfCancellationRequested();var (page,result)=pages[i];Ocr.SearchableLayerWriter.Write(document.Pages[page-1],GetPageGeometry(page),result);progress?.Report(i+1);}token.ThrowIfCancellationRequested();}
  catch{Restore(before);throw;}
 }
 public TextAnalysis AnalyzeText(int page) => TextEditing.OriginalTextEditor.Analyze(document.Pages[page-1],GetPageGeometry(page));
 public void ReplaceText(int page,OriginalTextBlock block,string replacement,double fontSize) =>
  ReplaceText(page, block, new TextEditOptions(replacement, fontSize, Strict: true));
 public void ReplaceText(int page,OriginalTextBlock block,TextEditOptions options) {
  var before=Snapshot();
  try {TextEditing.OriginalTextEditor.Replace(document,document.Pages[page-1],GetPageGeometry(page),block,options);}
  catch {Restore(before);throw;}
 }
 public void ApplyHeaderFooter(HeaderFooterOptions options) {
  var before=Snapshot();
  try { PageDecorator.ApplyHeaderFooter(document, options); }
  catch { Restore(before); throw; }
 }
 public void ApplyWatermark(WatermarkOptions options) {
  var before=Snapshot();
  try { PageDecorator.ApplyWatermark(document, options); }
  catch { Restore(before); throw; }
 }
 public byte[] Snapshot() {
  using var stream = new MemoryStream(); document.Save(stream, false);
  var bytes = stream.ToArray();
  // PDFsharp closes the mutation lifecycle after Save: reopen before further edits.
  Restore(bytes); return bytes;
 }
 public void Restore(byte[] data) { var replacement = PdfReader.Open(new MemoryStream(data), PdfDocumentOpenMode.Modify); document.Dispose(); document = replacement; }
 public void Rotate(int page) => document.Pages[page - 1].Rotate = (document.Pages[page - 1].Rotate + 90) % 360;
 public void Delete(int page) { if(PageCount <= 1) throw new InvalidOperationException("Debe conservar al menos una página."); document.Pages.RemoveAt(page - 1); }
 public void AddBlank() => document.AddPage();
 void ValidatePage(int page) {
  if(page < 1 || page > PageCount) throw new ArgumentOutOfRangeException(nameof(page));
 }
 public void DuplicatePage(int page) {
  ValidatePage(page);
  using var imported = PdfReader.Open(new MemoryStream(Snapshot()), PdfDocumentOpenMode.Import);
  document.Pages.Insert(page, imported.Pages[page-1]);
 }
 public void MovePage(int page, int destination) {
  ValidatePage(page); ValidatePage(destination);
  if(page == destination) return;
  document.Pages.MovePage(page-1,destination-1);
 }
 public void InsertPages(string path, int afterPage) {
  if(afterPage < 0 || afterPage > PageCount) throw new ArgumentOutOfRangeException(nameof(afterPage));
  // Apply the same size and signature policy as opening a document.
  using var source = new PdfDocumentSession(path);
  using var imported = PdfReader.Open(new MemoryStream(source.Snapshot()), PdfDocumentOpenMode.Import);
  var before = Snapshot();
  try { for(int i=0;i<imported.PageCount;i++) document.Pages.Insert(afterPage+i,imported.Pages[i]); }
  catch { Restore(before); throw; }
 }
 public async Task ExtractPagesAsync(string destination, IReadOnlyList<int> pages, CancellationToken token = default) {
  token.ThrowIfCancellationRequested();
  var selection=pages.ToArray();
  if(selection.Length==0 || selection.Distinct().Count()!=selection.Length) throw new ArgumentException("Seleccione páginas sin repetir.",nameof(pages));
  foreach(var page in selection) ValidatePage(page);
  using var imported=PdfReader.Open(new MemoryStream(Snapshot()),PdfDocumentOpenMode.Import);
  using var output=new PdfDocument();
  foreach(var page in selection) { token.ThrowIfCancellationRequested(); output.AddPage(imported.Pages[page-1]); }
  var temporary=destination+"."+Guid.NewGuid().ToString("N")+".tmp";
  try {
   using var buffer=new MemoryStream(); output.Save(buffer,false);
   await File.WriteAllBytesAsync(temporary,buffer.ToArray(),token);
   token.ThrowIfCancellationRequested(); File.Move(temporary,destination,true);
  } finally { if(File.Exists(temporary)) File.Delete(temporary); }
 }
 public async Task<IReadOnlyList<string>> SplitAsync(string destinationFolder, int pagesPerPart, CancellationToken token = default, IProgress<int>? progress = null) {
  token.ThrowIfCancellationRequested();
  if(pagesPerPart < 1 || pagesPerPart > PageCount) throw new ArgumentOutOfRangeException(nameof(pagesPerPart), "Use entre 1 y el total de páginas.");
  var destination = Path.GetFullPath(destinationFolder);
  if(Directory.Exists(destination) || File.Exists(destination)) throw new IOException("La carpeta de destino ya existe. Elija una carpeta nueva.");
  var parent = Path.GetDirectoryName(destination) ?? throw new ArgumentException("Destino inválido.", nameof(destinationFolder));
  if(!Directory.Exists(parent)) throw new DirectoryNotFoundException("La carpeta contenedora no existe.");
  var staging = Path.Combine(parent, ".mythlix-split-" + Guid.NewGuid().ToString("N"));
  using var imported = PdfReader.Open(new MemoryStream(Snapshot()), PdfDocumentOpenMode.Import);
  var names = new List<string>();
  Directory.CreateDirectory(staging);
  try {
   for(var start = 0; start < imported.PageCount; start += pagesPerPart) {
    token.ThrowIfCancellationRequested();
    using var output = new PdfDocument();
    var end = Math.Min(start + pagesPerPart, imported.PageCount);
    for(var page = start; page < end; page++) { token.ThrowIfCancellationRequested(); output.AddPage(imported.Pages[page]); }
    var name = $"parte-{names.Count + 1:D4}-paginas-{start + 1}-{end}.pdf";
    using var buffer = new MemoryStream(); output.Save(buffer, false);
    await File.WriteAllBytesAsync(Path.Combine(staging, name), buffer.ToArray(), token);
    names.Add(name);
    progress?.Report(names.Count);
   }
   token.ThrowIfCancellationRequested();
   // Same-parent rename publishes the complete set without replacing an existing folder.
   Directory.Move(staging, destination);
   return names.Select(name => Path.Combine(destination, name)).ToArray();
  } finally { if(Directory.Exists(staging)) Directory.Delete(staging, true); }
 }
 public async Task SaveCopyAsync(string destination, CancellationToken token = default) {
  var temp = destination + "." + Guid.NewGuid().ToString("N") + ".tmp";
  try { await File.WriteAllBytesAsync(temp, Snapshot(), token); token.ThrowIfCancellationRequested(); File.Move(temp, destination, true); }
  finally { if(File.Exists(temp)) File.Delete(temp); }
 }
 public void Dispose() => document.Dispose();
}
