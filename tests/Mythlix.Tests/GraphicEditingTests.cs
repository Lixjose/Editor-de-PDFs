using System.Text.RegularExpressions;
using Mythlix.Core;
using Mythlix.Infrastructure;
using PdfSharp.Pdf;
using PdfSharp.Pdf.IO;
using Xunit;
namespace Mythlix.Tests;

public sealed class GraphicEditingTests : IDisposable {
 readonly string directory=Path.Combine(Path.GetTempPath(),"mythlix-graphics-"+Guid.NewGuid().ToString("N"));
 readonly PopplerRenderer renderer=new();
 string Source=>Path.Combine(directory,"source.pdf");
 string Output=>Path.Combine(directory,"result.pdf");
 public GraphicEditingTests(){Directory.CreateDirectory(directory);File.Copy(Path.Combine(AppContext.BaseDirectory,"Fixtures","original.pdf"),Source);}
 byte[] Image=>File.ReadAllBytes(Path.Combine(AppContext.BaseDirectory,"Fixtures","image.png"));

 [Fact] public async Task AllGraphicTypesPersistAndUnicodeTextIsSelectable(){
  using(var doc=new PdfDocumentSession(Source)) {
   var geometry=doc.GetPageGeometry(1);Assert.True(geometry.CanEdit);
   doc.SetGraphics(1,[new(){Kind=GraphicKind.Text,Text="Edición gráfica: español áéíóú ñ",X=60,Y=180,Width=450,Height=70,Bold=true},
    new(){Kind=GraphicKind.Image,ImageData=Image,X=80,Y=300,Width=100,Height=100},
    new(){Kind=GraphicKind.Rectangle,X=250,Y=300,Width=100,Height=60},
    new(){Kind=GraphicKind.Ellipse,X=250,Y=400,Width=100,Height=60},
    new(){Kind=GraphicKind.Line,X=80,Y=460,Width=100,Height=70}]);
   await doc.SaveCopyAsync(Output);
  }
  using var reopened=new PdfDocumentSession(Output);Assert.Equal(5,reopened.GetGraphics(1).Count);
  Assert.Equal(Image,reopened.GetGraphics(1).Single(o=>o.Kind==GraphicKind.Image).ImageData);
  var text=await renderer.ExtractTextAsync(Output,1,CancellationToken.None);
  Assert.Contains("Mythlix PDF Studio",text);Assert.Contains("Edición gráfica: español áéíóú ñ",text);
  Assert.NotEmpty(await renderer.RenderAsync(Output,1,800,CancellationToken.None));
 }
 [Fact] public async Task EditingReopenedObjectReplacesOnlyAddedContent(){
  var item=new GraphicObject{Kind=GraphicKind.Text,Text="Texto anterior",X=70,Y=250,Width=350,Height=80};
  using(var doc=new PdfDocumentSession(Source)){doc.SetGraphics(1,[item]);await doc.SaveCopyAsync(Output);}
  using(var doc=new PdfDocumentSession(Output)) {
   var loaded=Assert.Single(doc.GetGraphics(1));doc.SetGraphics(1,[loaded with{Text="Texto actualizado",X=110,Width=320}]);await doc.SaveCopyAsync(Output);
  }
  var text=await renderer.ExtractTextAsync(Output,1,CancellationToken.None);
  Assert.Contains("Mythlix PDF Studio",text);Assert.DoesNotContain("Texto anterior",text);
  Assert.Single(Regex.Matches(text,"Texto actualizado").Cast<Match>());
  using var reopened=new PdfDocumentSession(Output);Assert.Equal(110,Assert.Single(reopened.GetGraphics(1)).X);
 }
 [Fact] public async Task DeletingAddedLayerRestoresOriginalVisualAndText(){
  var original=await renderer.RenderAsync(Source,1,800,CancellationToken.None);
  using var doc=new PdfDocumentSession(Source);
  doc.SetGraphics(1,[new(){Kind=GraphicKind.Rectangle,X=50,Y=50,Width=300,Height=150},new(){Kind=GraphicKind.Text,Text="Eliminar objeto nuevo",Y=200}]);
  await doc.SaveCopyAsync(Output);
  Assert.Contains("Eliminar objeto nuevo",await renderer.ExtractTextAsync(Output,1,CancellationToken.None));
  doc.SetGraphics(1,[]);await doc.SaveCopyAsync(Output);
  Assert.Empty(doc.GetGraphics(1));
  Assert.DoesNotContain("Eliminar objeto nuevo",await renderer.ExtractTextAsync(Output,1,CancellationToken.None));
  Assert.Equal(original,await renderer.RenderAsync(Output,1,800,CancellationToken.None));
 }
 [Fact] public async Task RepeatedMovesKeepOneLayerWithoutDuplicatingText(){
  using var doc=new PdfDocumentSession(Source);
  var item=new GraphicObject{Kind=GraphicKind.Text,Text="Una sola instancia",Y=200};
  for(var i=0;i<12;i++){doc.SetGraphics(1,[item with{X=60+i*5}]);await doc.SaveCopyAsync(Output);}
  var text=await renderer.ExtractTextAsync(Output,1,CancellationToken.None);
  Assert.Single(Regex.Matches(text,"Una sola instancia").Cast<Match>());Assert.Contains("Mythlix PDF Studio",text);
  using var parsed=PdfReader.Open(Output,PdfDocumentOpenMode.Modify);Assert.Equal(2,parsed.Pages[0].Contents.Elements.Count);
  Assert.Equal(2,parsed.Pages[0].Resources.Elements.GetDictionary("/Font")!.Elements.Count);
 }
 [Fact] public async Task SnapshotUndoAndRedoRestoreGraphicLayer(){
  using var doc=new PdfDocumentSession(Source);var before=doc.Snapshot();
  var item=new GraphicObject{Kind=GraphicKind.Text,Text="Historial gráfico",Y=240};
  doc.SetGraphics(1,[item]);var after=doc.Snapshot();
  doc.Restore(before);Assert.Empty(doc.GetGraphics(1));await doc.SaveCopyAsync(Output);
  Assert.DoesNotContain("Historial gráfico",await renderer.ExtractTextAsync(Output,1,CancellationToken.None));
  doc.Restore(after);Assert.Equal(item.Id,Assert.Single(doc.GetGraphics(1)).Id);await doc.SaveCopyAsync(Output);
  Assert.Contains("Historial gráfico",await renderer.ExtractTextAsync(Output,1,CancellationToken.None));
 }
 [Fact] public void InvalidEditRollsBackEntireLayer(){
  using var doc=new PdfDocumentSession(Source);var item=new GraphicObject{Kind=GraphicKind.Rectangle};doc.SetGraphics(1,[item]);
  Assert.Throws<InvalidOperationException>(()=>doc.SetGraphics(1,[item with{X=10000}]));
  Assert.Equal(item.Id,Assert.Single(doc.GetGraphics(1)).Id);Assert.Equal(item.X,Assert.Single(doc.GetGraphics(1)).X);
  Assert.Throws<InvalidOperationException>(()=>doc.SetGraphics(1,[item with{Color="red"}]));
  Assert.Throws<InvalidOperationException>(()=>doc.SetGraphics(1,[item with{Width=double.NaN}]));
 }
 [Fact] public void UnsupportedImageRollsBackWithoutRemovingOriginalLayer(){
  using var doc=new PdfDocumentSession(Source);var item=new GraphicObject{Kind=GraphicKind.Rectangle};doc.SetGraphics(1,[item]);
  Assert.ThrowsAny<Exception>(()=>doc.SetGraphics(1,[item,new(){Kind=GraphicKind.Image,ImageData=[1,2,3,4]}]));
  Assert.Equal(item.Id,Assert.Single(doc.GetGraphics(1)).Id);
 }
 [Fact] public void CroppedPageEditingIsRejectedExplicitly(){
  var path=Path.Combine(directory,"cropped.pdf");using(var source=PdfReader.Open(Source,PdfDocumentOpenMode.Modify)){source.Pages[0].CropBox=new PdfRectangle(new PdfSharp.Drawing.XPoint(20,20),new PdfSharp.Drawing.XPoint(500,700));source.Save(path);}
  using var doc=new PdfDocumentSession(path);Assert.False(doc.GetPageGeometry(1).CanEdit);
  Assert.Throws<InvalidOperationException>(()=>doc.SetGraphics(1,[new(){Kind=GraphicKind.Rectangle}]));
 }
 [Fact] public async Task EditingOnePageDoesNotRemoveResourcesUsedByAnotherPage(){
  var path=Path.Combine(directory,"shared.pdf");
  using(var original=PdfReader.Open(Source,PdfDocumentOpenMode.Modify)) {
   var second=original.AddPage();second.Elements["/Resources"]=original.Pages[0].Elements["/Resources"];original.Save(path);
  }
  using var doc=new PdfDocumentSession(path);
  var one=new GraphicObject{Kind=GraphicKind.Text,Text="Página uno",Y=200};
  var two=new GraphicObject{Kind=GraphicKind.Text,Text="Página dos",Y=200};
  doc.SetGraphics(1,[one]);doc.SetGraphics(2,[two]);doc.SetGraphics(1,[one with{Text="Página uno editada"}]);
  await doc.SaveCopyAsync(Output);
  Assert.Contains("Página uno editada",await renderer.ExtractTextAsync(Output,1,CancellationToken.None));
  Assert.Contains("Página dos",await renderer.ExtractTextAsync(Output,2,CancellationToken.None));
 }
 [Fact] public async Task AddingTextPreservesOriginalPagePixelsOutsideItsBounds(){
  using var before=SkiaSharp.SKBitmap.Decode(await renderer.RenderAsync(Source,1,792,CancellationToken.None));
  using var doc=new PdfDocumentSession(Source);doc.SetGraphics(1,[new(){Kind=GraphicKind.Text,Text="Contenido nuevo",Y=200}]);await doc.SaveCopyAsync(Output);
  using var after=SkiaSharp.SKBitmap.Decode(await renderer.RenderAsync(Output,1,792,CancellationToken.None));
  for(var y=0;y<150;y++)for(var x=0;x<before.Width;x++)Assert.Equal(before.GetPixel(x,y),after.GetPixel(x,y));
 }
 [Theory]
 [InlineData(0)]
 [InlineData(90)]
 [InlineData(180)]
 [InlineData(270)]
 public async Task RenderedImageAlignsWithSelectionOnRotatedPages(int rotation){
  using var doc=new PdfDocumentSession(Source);for(var i=0;i<rotation/90;i++)doc.Rotate(1);
  var item=new GraphicObject{Kind=GraphicKind.Image,ImageData=Image,X=70,Y=130,Width=120,Height=60};doc.SetGraphics(1,[item]);await doc.SaveCopyAsync(Output);
  var geometry=doc.GetPageGeometry(1);var box=geometry.ToDisplay(new(item.X,item.Y,item.Width,item.Height));
  using var bitmap=SkiaSharp.SKBitmap.Decode(await renderer.RenderAsync(Output,1,792,CancellationToken.None));
  var pixel=bitmap.GetPixel((int)(box.X+box.Width/2),(int)(box.Y+box.Height/2));
  Assert.InRange(pixel.Red,220,240);Assert.InRange(pixel.Green,40,65);Assert.InRange(pixel.Blue,60,90);
 }
 [Theory]
 [InlineData(0,70,130,120,60)]
 [InlineData(90,602,70,60,120)]
 [InlineData(180,422,602,120,60)]
 [InlineData(270,130,422,60,120)]
 public void SelectionBoundsFollowRotatedPage(int rotation,double x,double y,double width,double height){
  var geometry=new PageGeometry(612,792,rotation);var original=new ObjectBounds(70,130,120,60);
  var display=geometry.ToDisplay(original);Assert.Equal(new ObjectBounds(x,y,width,height),display);
  Assert.Equal(original,geometry.FromDisplay(display));
 }
 public void Dispose()=>Directory.Delete(directory,true);
}
