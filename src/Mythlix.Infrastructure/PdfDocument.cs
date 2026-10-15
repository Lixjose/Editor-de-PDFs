using Mythlix.Core;
using PdfSharp.Pdf;
using PdfSharp.Pdf.IO;
namespace Mythlix.Infrastructure;
public sealed partial class PdfDocumentSession : IPdfDocument, IContentEditor, ISearchablePdfEditor, IPageDecorator, Mythlix.Core.TextEditing.ITextRunInspector, Mythlix.Core.TextEditing.IParagraphCompositionWriter {
 PdfDocument document;
 readonly Guid textDocumentId = Guid.NewGuid();
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
 /// <summary>Additional read-only native run analysis. Revision is supplied by the owning workspace.</summary>
 public Mythlix.Core.TextEditing.NativeRunAnalysis AnalyzeTextRuns(int page, long revision) {
  ValidatePage(page);
  return TextEditing.NativeRunExtractor.Analyze(document.Pages[page-1],GetPageGeometry(page),
   new Mythlix.Core.TextEditing.TextDocumentContext(textDocumentId,page,revision));
 }
 /// <summary>Read-only inspection with native object identities and diagnostics.</summary>
 public Mythlix.Core.TextEditing.TextRunInspectionReport InspectTextRuns(int page, long revision) {
  ValidatePage(page);
  return TextEditing.TextRunInspectionAdapter.Inspect(document.Pages[page-1],GetPageGeometry(page),
   new Mythlix.Core.TextEditing.TextDocumentContext(textDocumentId,page,revision));
 }
 /// <summary>Encoding snapshot for validated native runs; no document writes or fallback fonts.</summary>
 public Mythlix.Core.TextEditing.IFontEncodingResolver CreateRunEncodingResolver(int page, long revision) {
  var analysis=AnalyzeTextRuns(page,revision);
  if(!analysis.IsSupported) throw new InvalidOperationException("La página no admite validación WinAnsi nativa: " + analysis.Diagnostics[0].Code);
  return TextEditing.NativeWinAnsiEncodingResolver.Create(document.Pages[page-1],analysis);
 }
 /// <summary>Resource-scoped metric snapshot for the shared WinAnsi compositor.</summary>
 public Mythlix.Core.TextEditing.IRunMetricsProvider CreateRunMetricsProvider(int page, long revision) =>
  (Mythlix.Core.TextEditing.IRunMetricsProvider)CreateRunEncodingResolver(page,revision);
 public TextAnalysis AnalyzeText(int page) => TextEditing.OriginalTextEditor.Analyze(document.Pages[page-1],GetPageGeometry(page));
 public void ReplaceText(int page,OriginalTextBlock block,string replacement,double fontSize) =>
  ReplaceText(page, block, new TextEditOptions(replacement, fontSize, Strict: true));
 public void ReplaceText(int page,OriginalTextBlock block,TextEditOptions options) {
  var before=Snapshot();
  try {
   if(block.Id.StartsWith("v:")) {
    ReplaceVectorText(page, block, options);
   } else {
    TextEditing.OriginalTextEditor.Replace(document,document.Pages[page-1],GetPageGeometry(page),block,options);
   }
  }
  catch {Restore(before);throw;}
 }
 public void ReplaceParagraph(int page, PdfParagraph paragraph, TextEditOptions options) {
  if(paragraph.Lines.Count==0 || paragraph.Lines.Any(b=>b.Id.StartsWith("v:"))) throw new InvalidOperationException("El párrafo requiere bloques de texto nativos compatibles.");
  var before=Snapshot();
  try {
   var originals=AnalyzeText(page).Blocks;
   foreach(var line in paragraph.Lines)
    if(!originals.Any(b=>b.Id==line.Id && b.Fingerprint==line.Fingerprint)) throw new InvalidOperationException("El párrafo cambió; vuelva a seleccionarlo.");
   var geometry=GetPageGeometry(page);
   var newCount=TextEditing.OriginalTextEditor.ReplacementLineCount(paragraph.Lines[0],options,paragraph.Bounds.Width);
   var delta=(Math.Max(1,newCount)-paragraph.Lines.Count)*paragraph.LineHeight;
   if(Math.Abs(delta)>.01) {
    var p=document.Pages[page-1];
    var analysis=AnalyzeText(page);
    if(geometry.Rotation!=0 || !geometry.CanEdit || analysis.UnsupportedBlocks>0 )
     throw new InvalidOperationException("Desbordamiento: no se puede desplazar automáticamente una página con bloques no compatibles, gráficos o rotación.");
    var ids=paragraph.Lines.Select(b=>b.Id).ToHashSet();
    var bottom=paragraph.Bounds.Y+paragraph.Bounds.Height;
    var seeds=originals.Where(b=>!ids.Contains(b.Id) && b.Bounds.Y>=bottom-0.5
      && Math.Max(0,Math.Min(b.Bounds.X+b.Bounds.Width,paragraph.Bounds.X+paragraph.Bounds.Width)-Math.Max(b.Bounds.X,paragraph.Bounds.X))>=paragraph.Bounds.Width*.6).ToArray();
    // Include short final rows of the same following paragraph, keeping its lines together.
    var lower=seeds.SelectMany(b=>PdfParagraph.Around(b,originals).Lines)
     .Where(b=>!ids.Contains(b.Id) && b.Bounds.Y>=bottom-.5).DistinctBy(b=>b.Id).ToArray();
    var left=Math.Min(paragraph.Bounds.X,lower.Select(b=>b.Bounds.X).DefaultIfEmpty(paragraph.Bounds.X).Min());
    var right=Math.Max(paragraph.Bounds.X+paragraph.Bounds.Width,lower.Select(b=>b.Bounds.X+b.Bounds.Width).DefaultIfEmpty(paragraph.Bounds.X+paragraph.Bounds.Width).Max());
    var corridor=new ObjectBounds(left,paragraph.Bounds.Y,right-left,Math.Max(0,geometry.Height-36-paragraph.Bounds.Y));
    var graphicCollision=GetGraphics(page).Any(g=>g.X-g.StrokeWidth<=right && g.X+g.Width+g.StrokeWidth>=left
      && g.Y-g.StrokeWidth<=geometry.Height-36 && g.Y+g.Height+g.StrokeWidth>=paragraph.Bounds.Y);
    if(graphicCollision || TextEditing.FixedGraphicsGuard.Blocks(p,geometry,corridor))
     throw new InvalidOperationException("Desbordamiento: hay gráficos en el corredor de desplazamiento o gráficos cuya posición no se puede verificar.");
    if(bottom+delta>geometry.Height-36 || lower.Any(b=>b.Bounds.Y+b.Bounds.Height>geometry.Height-36 || b.Bounds.Y+delta<bottom+delta-0.5 || b.Bounds.Y+b.Bounds.Height+delta>geometry.Height-36))
     throw new InvalidOperationException("Desbordamiento: el texto invadiría el margen inferior de 36 pt.");
    var moving=lower.Select(b=>b.Bounds with {Y=b.Bounds.Y+delta}).Append(paragraph.Bounds with {Height=paragraph.Bounds.Height+delta}).ToArray();
    var fixedRows=originals.Where(b=>!ids.Contains(b.Id) && !lower.Any(l=>l.Id==b.Id));
    if(fixedRows.Any(b=>moving.Any(r=>Math.Min(r.X+r.Width,b.Bounds.X+b.Bounds.Width)>Math.Max(r.X,b.Bounds.X) && Math.Min(r.Y+r.Height,b.Bounds.Y+b.Bounds.Height)>Math.Max(r.Y,b.Bounds.Y)+.5)))
     throw new InvalidOperationException("Desbordamiento: el desplazamiento chocaría con otro bloque fijo.");
    TextEditing.OriginalTextEditor.ShiftLines(document,p,geometry,lower,delta);
   }
   // Remove later rows first and refresh fingerprints after each stream mutation.
   foreach(var line in paragraph.Lines.Skip(1).Reverse()) {
    var current=AnalyzeText(page).Blocks.Single(b=>b.Id==line.Id);
    ReplaceText(page,current,new TextEditOptions("",current.FontSize));
   }
   var first=AnalyzeText(page).Blocks.Single(b=>b.Id==paragraph.Lines[0].Id);
   ReplaceText(page,first,options with {MaxWidth=paragraph.Bounds.Width, X=paragraph.Bounds.X, Baseline=paragraph.Lines[0].EffectiveBaseline, LineHeight=paragraph.LineHeight});
  } catch {Restore(before);throw;}
 }
 public void MoveTextBlocks(int page, IReadOnlyList<TextBlockMove> moves) {
  if(moves.Count == 0) return;
  var before = Snapshot();
  try {
   foreach(var move in moves) {
    var opts = new TextEditOptions(
     Text: move.Block.Text,
     FontSize: move.Block.FontSize,
     Color: move.Block.Color,
     Bold: move.Block.Bold,
     Italic: move.Block.Italic,
     Alignment: move.Block.Alignment,
     FontFamily: move.Block.FontName,
     Strict: false,
     X: move.NewX,
     Baseline: move.NewBaseline
    );
    if(move.Block.Id.StartsWith("v:")) {
     ReplaceVectorText(page, move.Block, opts);
    } else {
     TextEditing.OriginalTextEditor.Replace(document, document.Pages[page - 1], GetPageGeometry(page), move.Block, opts);
    }
   }
  } catch {
   Restore(before);
   throw;
  }
 }
 public IReadOnlyDictionary<string, FontVerticalMetrics> GetFontMetrics(int page) {
  var result = new Dictionary<string, FontVerticalMetrics>(StringComparer.OrdinalIgnoreCase);
  foreach(var (name, metrics) in TextEditing.StandardFontMetrics.Fonts) {
   result[name] = new FontVerticalMetrics(metrics.Ascent / 1000.0, metrics.Descent / 1000.0);
  }
  return result;
 }
 void ReplaceVectorText(int page, OriginalTextBlock block, TextEditOptions options) {
  var p = document.Pages[page - 1];
  BlankOriginalTextInStreams(p, page, block);
  var geometry = GetPageGeometry(page);
  var currentGraphics = GetGraphics(page).ToList();
  var pad = 0.5;
  var maskX = Math.Max(0, block.Bounds.X - pad);
  var maskY = Math.Max(0, block.Bounds.Y - pad);
  var mask = new GraphicObject {
   Kind = GraphicKind.Rectangle,
   X = maskX,
   Y = maskY,
   Width = Math.Min(geometry.Width - maskX, block.Bounds.Width + pad * 2),
   Height = Math.Min(geometry.Height - maskY, block.Bounds.Height + pad * 2),
   Color = options.BackgroundColor,
   FillColor = options.BackgroundColor,
   StrokeWidth = 0
  };
  currentGraphics.Add(mask);

  if(!string.IsNullOrEmpty(options.Text)) {
   var textX = Math.Clamp(options.X ?? block.Bounds.X, 0, geometry.Width - 10);
   var textY = Math.Clamp(options.Baseline.HasValue ? options.Baseline.Value - options.FontSize * 0.78 : block.Bounds.Y, 0, geometry.Height - 10);
   var baselineOffset = options.Baseline.HasValue ? (options.Baseline.Value - textY)
    : block.Baseline.HasValue ? (block.Baseline.Value - textY)
    : (block.EffectiveBaseline - textY);
   var textObj = new GraphicObject {
    Kind = GraphicKind.Text,
    X = textX,
    Y = textY,
    Width = Math.Clamp(options.MaxWidth ?? block.Bounds.Width, 4, geometry.Width - textX),
    Height = Math.Clamp(Math.Max(block.Bounds.Height, options.FontSize), 4, geometry.Height - textY),
    Text = options.Text,
    FontSize = options.FontSize,
    Color = string.IsNullOrWhiteSpace(options.Color) ? block.Color : options.Color,
    Bold = options.Bold,
    Italic = options.Italic,
    Alignment = options.Alignment,
    FontFamily = string.IsNullOrWhiteSpace(options.FontFamily) || options.FontFamily == "Conservar fuente" ? block.FontName : options.FontFamily,
    Baseline = baselineOffset,
    HorizontalScale = block.HorizontalScale,
    ConstrainTextWidth = true,
    StrokeWidth = 0
   };
   currentGraphics.Add(textObj);
  }
  SetGraphics(page, currentGraphics);
 }
 void BlankOriginalTextInStreams(PdfPage page, int pageNumber, OriginalTextBlock block) {
  var geom = GetPageGeometry(pageNumber);
  for(var i = 0; i < page.Contents.Elements.Count; i++) {
   var dict = page.Contents.Elements.GetDictionary(i);
   if(dict is null || dict.Elements.GetBoolean("/MythlixGraphicsV1") || dict.Elements.GetBoolean("/MythlixOcrV1")) continue;
   var bytes = dict.Stream?.UnfilteredValue;
   if(bytes is null || bytes.Length == 0) continue;

   try {
    var tokens = TextEditing.ContentTokenizer.Read(bytes);
    for(var s = 0; s < tokens.Count; s++) {
     if(tokens[s].Value != "BT" || tokens[s].Bytes is not null) continue;
     var e = s + 1;
     while(e < tokens.Count && tokens[e].Value != "ET") e++;
     if(e >= tokens.Count) break;

     var innerTokens = tokens.Skip(s + 1).Take(e - s - 1).ToArray();
     // A text object can contain an entire paragraph. Removing BT..ET in
     // that case also deletes every neighbouring line. Keep its operators
     // intact and let the local mask in ReplaceVectorText cover the edit.
     // This also preserves relative positioning and inherited text state.
     var showCount = innerTokens.Count(t => t.Bytes is null && t.Value is "Tj" or "TJ" or "\"" or "'");
     if(showCount != 1) { s = e; continue; }
     bool isMatch = false;

     for(var k = 0; k < innerTokens.Length; k++) {
      if(innerTokens[k].Bytes is null && innerTokens[k].Value is "Td" or "TD" or "Tm") {
       if(k >= 2 && innerTokens[k-2].Number(out var tx) && innerTokens[k-1].Number(out var ty)) {
        var topY = geom.Height - ty;
        if(tx >= block.Bounds.X - 30 && tx <= block.Bounds.X + block.Bounds.Width + 30 &&
           topY >= block.Bounds.Y - 12 && topY <= block.Bounds.Y + block.Bounds.Height + 15) {
         isMatch = true;
         break;
        }
       }
      }
     }

     if(!isMatch && !string.IsNullOrWhiteSpace(block.Text)) {
      foreach(var pLine in block.Text.Split('\n')) {
       if(pLine.Trim().Length < 4) continue;
       var sample = pLine.Trim()[..Math.Min(pLine.Trim().Length, 8)];
       if(innerTokens.Any(t => t.Bytes is not null && System.Text.Encoding.ASCII.GetString(t.Bytes).Contains(sample))) {
        isMatch = true;
        break;
       }
      }
     }

     if(isMatch) {
      var btStart = tokens[s].Start;
      var etEnd = tokens[e].Start + tokens[e].Length;
      var length = etEnd - btStart;

      var blanked = bytes.ToArray();
      var replacement = System.Text.Encoding.ASCII.GetBytes("BT () Tj ET");
      for(var k = 0; k < length; k++) blanked[btStart + k] = (byte)' ';
      for(var k = 0; k < replacement.Length && k < length; k++) blanked[btStart + k] = replacement[k];

      var updated = new PdfDictionary(document);
      foreach(var key in dict.Elements.Keys) if(key is not ("/Filter" or "/DecodeParms" or "/Length" or "/DL")) updated.Elements[key] = dict.Elements[key];
      updated.CreateStream(blanked);
      document.Internals.AddObject(updated);
      page.Contents.Elements[i] = updated.Reference!;
      break;
     }
     s = e;
    }
   } catch {}
  }
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
