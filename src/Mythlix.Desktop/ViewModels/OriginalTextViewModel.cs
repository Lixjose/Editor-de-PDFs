using System.Collections.ObjectModel;
using Avalonia.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Mythlix.Core;
namespace Mythlix.Desktop.ViewModels;
public sealed partial class PageThumbnailViewModel(int number) : ObservableObject,IDisposable {
 public int Number {get;}=number;
 public string Label=>"Página "+Number;
 [ObservableProperty, NotifyPropertyChangedFor(nameof(HasImage))] private Bitmap? image;
 public bool HasImage=>Image is not null;
 public void Dispose(){Image?.Dispose();Image=null;}
}
public sealed partial class WorkspaceViewModel {
 public ObservableCollection<OriginalTextBlock> OriginalBlocks {get;}=new();
 public ObservableCollection<PageThumbnailViewModel> PageThumbnails {get;}=new();
 [ObservableProperty] private OriginalTextBlock? selectedOriginal;
 [ObservableProperty] private string originalDraftText="";
 [ObservableProperty] private double originalDraftSize=14;
 [ObservableProperty] private string originalStatus="Seleccione un documento para analizar su texto.";
 public bool HasOriginal=>SelectedOriginal is not null;
 public PageThumbnailViewModel? ActiveThumbnail=>PageThumbnails.FirstOrDefault(c=>c.Number==CurrentPage);
 public string PageCountLabel=>Selected is null?"Sin documento":Selected.Document.PageCount==1?"1 página":$"{Selected.Document.PageCount} páginas";
 public string OriginalFontLabel=>SelectedOriginal?.FontName??"Fuente original";
 public string OriginalGeometryLabel=>SelectedOriginal is {} b?$"X {b.Bounds.X:0.#} · Y {b.Bounds.Y:0.#} · {b.Bounds.Width:0.#} × {b.Bounds.Height:0.#} pt":"";
 public string SelectionLabel=>HasOriginal?"Texto original · edición directa":HasGraphic?"Contenido añadido":"Seleccione un elemento";
 partial void OnSelectedOriginalChanged(OriginalTextBlock? value) {
  if(value is not null){SelectedGraphic=null;OriginalDraftText=value.Text;OriginalDraftSize=value.FontSize;}
  OnPropertyChanged(nameof(HasOriginal));OnPropertyChanged(nameof(OriginalFontLabel));OnPropertyChanged(nameof(OriginalGeometryLabel));OnPropertyChanged(nameof(SelectionLabel));
 }
 void RefreshOriginal(TextAnalysis analysis) {
  var id=SelectedOriginal?.Id;OriginalBlocks.Clear();foreach(var b in analysis.Blocks)OriginalBlocks.Add(b);
  SelectedOriginal=OriginalBlocks.FirstOrDefault(b=>b.Id==id);OriginalStatus=analysis.Explanation;
 }
 public Task ReplaceOriginalAsync(string text,double size)=>RunAsync(async()=> {
  if(SelectedOriginal is not {} block || Selected is not {} doc || doc.Document is not IContentEditor editor)return;
  var id=block.Id;
  await Task.Run(()=>{var before=doc.Document.Snapshot();editor.ReplaceText(CurrentPage,block,text,size);doc.Undo.Push(before);doc.Redo.Clear();LimitUndo(doc);});
  doc.Modified=true;doc.Revision++;await RenderAsync();SelectedOriginal=OriginalBlocks.FirstOrDefault(b=>b.Id==id);
 });
 [RelayCommand] private Task ApplyOriginalAsync()=>ReplaceOriginalAsync(OriginalDraftText,OriginalDraftSize);
 [RelayCommand] private Task DeleteOriginalAsync()=>ReplaceOriginalAsync("",OriginalDraftSize);
 async Task RenderThumbnailsAsync(DocumentViewModel doc) {
  var visible=Enumerable.Range(Math.Max(1,CurrentPage-2),Math.Min(doc.Document.PageCount,CurrentPage+2)-Math.Max(1,CurrentPage-2)+1).ToHashSet();
  foreach(var card in PageThumbnails.Where(c=>!visible.Contains(c.Number)))card.Dispose();
  foreach(var card in PageThumbnails.Where(c=>visible.Contains(c.Number))) {
   if(card.Image is not null && card.Number!=CurrentPage)continue;
   var data=await renderer.RenderAsync(doc.WorkingPath,card.Number,260,CancellationToken.None);
   var old=card.Image;card.Image=new Bitmap(new MemoryStream(data));old?.Dispose();
  }
 }
 void ResetThumbnails() {foreach(var card in PageThumbnails)card.Dispose();PageThumbnails.Clear();if(Selected is not null)for(var n=1;n<=Selected.Document.PageCount;n++)PageThumbnails.Add(new(n));OnPropertyChanged(nameof(PageCountLabel)); OnPropertyChanged(nameof(ActiveThumbnail));}
}
