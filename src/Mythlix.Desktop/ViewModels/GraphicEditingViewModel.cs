using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Mythlix.Core;
namespace Mythlix.Desktop.ViewModels;

public sealed partial class WorkspaceViewModel {
 public ObservableCollection<GraphicObject> PageObjects { get; } = new();
 [ObservableProperty] private GraphicObject? selectedGraphic;
 [ObservableProperty] private PageGeometry geometry = new(612,792,0);
 [ObservableProperty] private string objectText = "Texto nuevo";
 [ObservableProperty] private double objectX;
 [ObservableProperty] private double objectY;
 [ObservableProperty] private double objectWidth = 240;
 [ObservableProperty] private double objectHeight = 72;
 [ObservableProperty] private double objectFontSize = 18;
 [ObservableProperty] private string objectColor = "#4F46E5";
 [ObservableProperty] private double objectStrokeWidth = 2;
 [ObservableProperty] private bool objectBold;
 [ObservableProperty] private bool objectItalic;
 public bool HasGraphic => SelectedGraphic is not null;
 public bool IsTextGraphic => SelectedGraphic?.Kind == GraphicKind.Text;
 public double DisplayHeight => DisplayWidth * Geometry.DisplayHeight / Geometry.DisplayWidth;
 public string GraphicHint => Geometry.CanEdit ? "Contenido añadido · origen superior izquierdo · pt" : "Página con recorte/origen desplazado: edición gráfica no disponible.";

 partial void OnGeometryChanged(PageGeometry value) { OnPropertyChanged(nameof(DisplayHeight)); OnPropertyChanged(nameof(GraphicHint)); }
 partial void OnSelectedGraphicChanged(GraphicObject? value) {
  if(value is not null) {
   SelectedOriginal=null;
   ObjectText=value.Text; ObjectX=value.X; ObjectY=value.Y; ObjectWidth=value.Width; ObjectHeight=value.Height;
   ObjectFontSize=value.FontSize; ObjectColor=value.Color; ObjectStrokeWidth=value.StrokeWidth; ObjectBold=value.Bold; ObjectItalic=value.Italic;
  }
  OnPropertyChanged(nameof(HasGraphic)); OnPropertyChanged(nameof(IsTextGraphic)); OnPropertyChanged(nameof(SelectionLabel));
 }
 void RefreshGraphics(PageGeometry geometry, IReadOnlyList<GraphicObject> objects) {
  var id = SelectedGraphic?.Id;
  PageObjects.Clear();
  if(Selected is not null) {
   Geometry=geometry;
   foreach(var item in objects) PageObjects.Add(item);
  }
  SelectedGraphic=PageObjects.FirstOrDefault(item=>item.Id==id);
 }
 public async Task RunAsync(Func<Task> operation) {
  if(Busy) return;
  Busy=true;
  try { await operation(); }
  catch(Exception ex) { Status=ex is OperationCanceledException ? "Operación cancelada o tiempo agotado." : ex.Message; }
  finally { Busy=false; }
 }
 async Task ChangeGraphicsAsync(IReadOnlyList<GraphicObject> objects, Guid? selectedId) {
  var doc=Selected; if(doc is null) return;
  await Task.Run(()=> {
   var before=doc.Document.Snapshot();
   doc.Document.SetGraphics(CurrentPage,objects);
   doc.Undo.Push(before); doc.Redo.Clear(); LimitUndo(doc);
  });
  doc.Modified=true;doc.Revision++;
  await RenderAsync();
  SelectedGraphic=PageObjects.FirstOrDefault(item=>item.Id==selectedId);
 }
 public Task CommitObjectAsync(GraphicObject item) => RunAsync(()=>ChangeGraphicsAsync(PageObjects.Select(o=>o.Id==item.Id?item:o).ToArray(),item.Id));
 async Task InsertAsync(GraphicKind kind, byte[]? image=null, double ratio=1) {
  if(Selected is null) { Status="Abra un PDF antes de añadir contenido."; return; }
  var item=Geometry.Clamp(new GraphicObject { Kind=kind, Text=ObjectText, ImageData=image,
   Width=kind==GraphicKind.Image?240:kind==GraphicKind.Text?260:160,
   Height=kind==GraphicKind.Image?240/Math.Max(.01,ratio):kind==GraphicKind.Text?80:100 });
  await ChangeGraphicsAsync(PageObjects.Append(item).ToArray(),item.Id);
 }
 public Task InsertImageAsync(byte[] image,double ratio)=>InsertAsync(GraphicKind.Image,image,ratio);
 [RelayCommand] private Task AddTextAsync()=>RunAsync(()=>InsertAsync(GraphicKind.Text));
 [RelayCommand] private Task AddRectangleAsync()=>RunAsync(()=>InsertAsync(GraphicKind.Rectangle));
 [RelayCommand] private Task AddEllipseAsync()=>RunAsync(()=>InsertAsync(GraphicKind.Ellipse));
 [RelayCommand] private Task AddLineAsync()=>RunAsync(()=>InsertAsync(GraphicKind.Line));
 [RelayCommand] private Task ApplyGraphicAsync()=>RunAsync(async()=> {
  if(SelectedGraphic is not {} item) return;
  var updated=item with {X=ObjectX,Y=ObjectY,Width=ObjectWidth,Height=ObjectHeight,Text=ObjectText,
   FontSize=ObjectFontSize,Color=ObjectColor,StrokeWidth=ObjectStrokeWidth,Bold=ObjectBold,Italic=ObjectItalic};
  await ChangeGraphicsAsync(PageObjects.Select(o=>o.Id==item.Id?updated:o).ToArray(),item.Id);
 });
 [RelayCommand] private Task DeleteGraphicAsync()=>RunAsync(async()=> {
  if(SelectedGraphic is {} item) await ChangeGraphicsAsync(PageObjects.Where(o=>o.Id!=item.Id).ToArray(),null);
 });
 [RelayCommand] private Task DuplicateGraphicAsync()=>RunAsync(async()=> {
  if(SelectedGraphic is not {} item) return;
  var copy=Geometry.Clamp(item with {Id=Guid.NewGuid(),X=item.X+16,Y=item.Y+16});
  await ChangeGraphicsAsync(PageObjects.Append(copy).ToArray(),copy.Id);
 });
 [RelayCommand] private Task BringForwardAsync()=>RunAsync(()=>ReorderAsync(1));
 [RelayCommand] private Task SendBackwardAsync()=>RunAsync(()=>ReorderAsync(-1));
 async Task ReorderAsync(int offset) {
  if(SelectedGraphic is not {} item) return;
  var objects=PageObjects.ToList();var index=objects.FindIndex(o=>o.Id==item.Id);var next=index+offset;
  if(next<0 || next>=objects.Count) return;
  (objects[index],objects[next])=(objects[next],objects[index]);await ChangeGraphicsAsync(objects,item.Id);
 }
}
