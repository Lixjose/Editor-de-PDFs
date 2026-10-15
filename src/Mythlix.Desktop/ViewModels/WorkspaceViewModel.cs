using System.Collections.ObjectModel;
using Avalonia.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;
using Mythlix.Core;
using Mythlix.Infrastructure;
namespace Mythlix.Desktop.ViewModels;
public sealed partial class DocumentViewModel : ObservableObject, IDisposable {
 public string Source { get; }
 public string Name => Path.GetFileName(Source);
 public IPdfDocument Document { get; }
 public string WorkingPath { get; } = Path.Combine(Path.GetTempPath(), "mythlix-" + Guid.NewGuid().ToString("N") + ".pdf");
 public long Revision {get;set;}
 public long PreparedRevision {get;set;} = -1;
 public Stack<byte[]> Undo { get; } = new();
 public Stack<byte[]> Redo { get; } = new();
 [ObservableProperty] private bool modified;
 [ObservableProperty] private int page = 1;
 public DocumentViewModel(string path) { Source = path; Document = new PdfDocumentSession(path); }
 public void Dispose() { Document.Dispose(); if(File.Exists(WorkingPath)) File.Delete(WorkingPath); }
}
public sealed partial class WorkspaceViewModel : ObservableObject, IDisposable {
 readonly PopplerRenderer poppler = new();
 readonly CachedPdfRenderer renderer = new(new PopplerRenderer());
 public ObservableCollection<DocumentViewModel> Documents { get; } = new();
 public ObservableCollection<int> Pages { get; } = new();
 [ObservableProperty] private DocumentViewModel? selected;
 [ObservableProperty] private Bitmap? pageImage;
 [ObservableProperty] private Bitmap? thumbnail;
 [ObservableProperty] private string status = "Abra un PDF para comenzar. Procesamiento local • Mythlix";
 [ObservableProperty] private string pageText = "";
 [ObservableProperty] private string query = "";
 [ObservableProperty] private int currentPage = 1;
 [ObservableProperty] private double zoom = 100;
 [ObservableProperty] private bool busy;
 public bool IsEmpty => Selected is null;
 public string PageLabel => Selected is null ? "Sin documento" : $"Página {CurrentPage} de {Selected.Document.PageCount}";
 public double DisplayWidth => 720 * Zoom / 100;
 partial void OnZoomChanged(double value) { OnPropertyChanged(nameof(DisplayWidth)); OnPropertyChanged(nameof(DisplayHeight)); }
 partial void OnSelectedChanged(DocumentViewModel? value) { CurrentPage = value?.Page ?? 1; RefreshPages(); OnPropertyChanged(nameof(IsEmpty)); SelectedGraphic=null; SelectedOriginal=null; ClearOcr(); }
 public void RefreshPages() { Pages.Clear(); if(Selected is not null) for(int i=1;i<=Selected.Document.PageCount;i++) Pages.Add(i); OnPropertyChanged(nameof(PageLabel)); ResetThumbnails(); }
 public async Task OpenAsync(string path) {
  var existing = Documents.FirstOrDefault(d => d.Source == path);
  if(existing is not null) { Selected = existing; return; }
  var doc = await Task.Run(() => new DocumentViewModel(path)); Documents.Add(doc); Selected = doc;
 }
 async Task PrepareWorkingCopyAsync(DocumentViewModel doc, CancellationToken token = default) {
  if(doc.PreparedRevision == doc.Revision && File.Exists(doc.WorkingPath)) return;
  renderer.Invalidate(doc.WorkingPath);
  if(doc == Selected) ResetThumbnails();
  var revision = doc.Revision;
  await Task.Run(() => doc.Document.SaveCopyAsync(doc.WorkingPath, token), token);
  doc.PreparedRevision = revision;
 }
 public async Task RenderAsync() {
  var doc = Selected; if(doc is null) return;
  CurrentPage = Math.Clamp(CurrentPage, 1, doc.Document.PageCount); doc.Page = CurrentPage;
  await PrepareWorkingCopyAsync(doc);
  var state = await Task.Run(() => {
   return (Geometry:doc.Document.GetPageGeometry(CurrentPage), Objects:doc.Document.GetGraphics(CurrentPage), Text:doc.Document is IContentEditor editor?editor.AnalyzeText(CurrentPage):new TextAnalysis([],0,"Motor sin edición original."));
  });
  var png = await renderer.RenderAsync(doc.WorkingPath, CurrentPage, 1600, CancellationToken.None);
  var thumb = await renderer.RenderAsync(doc.WorkingPath, CurrentPage, 220, CancellationToken.None);
  var text = await renderer.ExtractTextAsync(doc.WorkingPath, CurrentPage, CancellationToken.None);
  var old = PageImage; PageImage = new Bitmap(new MemoryStream(png)); old?.Dispose();
  old = Thumbnail; Thumbnail = new Bitmap(new MemoryStream(thumb)); old?.Dispose();
  await RenderThumbnailsAsync(doc);
  OnPropertyChanged(nameof(ActiveThumbnail)); RefreshOriginal(state.Text); PageText = text; RefreshGraphics(state.Geometry,state.Objects); OnPropertyChanged(nameof(PageLabel)); Status = doc.Modified ? "Cambios pendientes • Guarde una copia" : "Documento listo • procesamiento local";
 }
 public async Task EditAsync(string action) {
  var doc = Selected; if(doc is null) return;
  await Task.Run(() => {
   var before = doc.Document.Snapshot();
   try {
    switch(action) { case "rotate": doc.Document.Rotate(CurrentPage); break; case "delete": doc.Document.Delete(CurrentPage); break; case "add": doc.Document.AddBlank(); break; case "duplicate": doc.Document.DuplicatePage(CurrentPage); break; case "before": doc.Document.MovePage(CurrentPage,Math.Max(1,CurrentPage-1)); break; case "after": doc.Document.MovePage(CurrentPage,Math.Min(doc.Document.PageCount,CurrentPage+1)); break; default: throw new ArgumentException("Acción desconocida.",nameof(action)); }
    doc.Undo.Push(before); doc.Redo.Clear();
    // Bound snapshot history: large files must not consume unbounded memory.
    LimitUndo(doc);
   } catch { doc.Document.Restore(before); throw; }
  });
  if(action=="before")CurrentPage=Math.Max(1,CurrentPage-1);
  if(action=="after")CurrentPage=Math.Min(doc.Document.PageCount,CurrentPage+1);
  if(action=="duplicate")CurrentPage++;
  doc.Modified = true; doc.Revision++; ClearOcr(); RefreshPages(); await RenderAsync();
 }
 public async Task InsertPagesAsync(string path) {
  var doc=Selected;if(doc is null)return;
  await Task.Run(()=> {
   var before=doc.Document.Snapshot();
   doc.Document.InsertPages(path,CurrentPage);
   doc.Undo.Push(before);doc.Redo.Clear();LimitUndo(doc);
  });
  doc.Modified=true;doc.Revision++;ClearOcr();RefreshPages();await RenderAsync();
 }
 public async Task ApplyHeaderFooterAsync(HeaderFooterOptions options) {
  var doc=Selected;if(doc is null || doc.Document is not IPageDecorator decorator)return;
  await Task.Run(()=> {
   var before=doc.Document.Snapshot();
   decorator.ApplyHeaderFooter(options);
   doc.Undo.Push(before);doc.Redo.Clear();LimitUndo(doc);
  });
  doc.Modified=true;doc.Revision++;await RenderAsync();Status="Numeración y encabezados aplicados.";
 }
 public async Task ApplyWatermarkAsync(WatermarkOptions options) {
  var doc=Selected;if(doc is null || doc.Document is not IPageDecorator decorator)return;
  await Task.Run(()=> {
   var before=doc.Document.Snapshot();
   decorator.ApplyWatermark(options);
   doc.Undo.Push(before);doc.Redo.Clear();LimitUndo(doc);
  });
  doc.Modified=true;doc.Revision++;await RenderAsync();Status="Marca de agua aplicada.";
 }
 static void LimitUndo(DocumentViewModel doc) {
  if(doc.Undo.Sum(x=>(long)x.Length)>64L*1024*1024) {var latest=doc.Undo.Pop(); doc.Undo.Clear();doc.Undo.Push(latest);}
 }
 public async Task HistoryAsync(bool redo) {
  var doc = Selected; if(doc is null) return;
  var source = redo ? doc.Redo : doc.Undo; var target = redo ? doc.Undo : doc.Redo;
  if(source.Count == 0) return;
  await Task.Run(() => { target.Push(doc.Document.Snapshot()); doc.Document.Restore(source.Pop()); });
  doc.Modified = true; doc.Revision++; ClearOcr(); RefreshPages(); await RenderAsync();
 }
 public async Task SearchAsync() {
  var doc = Selected; if(doc is null || string.IsNullOrWhiteSpace(Query)) return;
  await PrepareWorkingCopyAsync(doc);
  for(int n=1;n<=doc.Document.PageCount;n++) {
   var page = (CurrentPage + n - 1) % doc.Document.PageCount + 1;
   var text = await renderer.ExtractTextAsync(doc.WorkingPath,page,CancellationToken.None);
   if(text.Contains(Query,StringComparison.OrdinalIgnoreCase)) { CurrentPage=page; await RenderAsync(); Status="Texto encontrado en la página " + page; return; }
  }
  Status="No se encontró el texto. Use OCR para reconocer páginas escaneadas.";
 }
 public void CloseSelected() {
  var doc=Selected; if(doc is null) return; Documents.Remove(doc); Selected=Documents.FirstOrDefault(); renderer.Invalidate(doc.WorkingPath); doc.Dispose();
  if(Selected is null) { var old=PageImage; PageImage=null; old?.Dispose(); old=Thumbnail; Thumbnail=null; old?.Dispose(); PageText=""; PageObjects.Clear(); SelectedGraphic=null; SelectedOriginal=null; OriginalBlocks.Clear(); ResetThumbnails(); Status="Abra un PDF para comenzar."; }
 }
 public void Dispose() { renderer.Clear(); foreach(var card in PageThumbnails)card.Dispose(); foreach(var doc in Documents) doc.Dispose(); PageImage?.Dispose(); Thumbnail?.Dispose(); }
}
