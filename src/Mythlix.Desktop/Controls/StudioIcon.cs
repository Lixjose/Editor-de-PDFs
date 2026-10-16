using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
namespace Mythlix.Desktop.Controls;

/// <summary>Local vector toolbar glyphs, independent of platform icon fonts.</summary>
public sealed class StudioIcon : Control {
 public static readonly StyledProperty<string> KindProperty=AvaloniaProperty.Register<StudioIcon,string>(nameof(Kind),"select");
 public static readonly StyledProperty<IBrush> StrokeProperty=AvaloniaProperty.Register<StudioIcon,IBrush>(nameof(Stroke),Brushes.Gray);
 public string Kind {get=>GetValue(KindProperty);set=>SetValue(KindProperty,value);}
 public IBrush Stroke {get=>GetValue(StrokeProperty);set=>SetValue(StrokeProperty,value);}
 static readonly Dictionary<string,Geometry> Shapes=new() {
  ["select"]=Geometry.Parse("M5 3 L19 14 L12 15 L9 21 Z"),
  ["text"]=Geometry.Parse("M4 5 L20 5 M12 5 L12 20 M8 20 L16 20"),
  ["image"]=Geometry.Parse("M3 4 L21 4 L21 20 L3 20 Z M4 17 L9 12 L13 16 L17 10 L21 15 M7 8 L8 8"),
  ["rectangle"]=Geometry.Parse("M4 5 L20 5 L20 19 L4 19 Z"),
  ["ellipse"]=Geometry.Parse("M12 4 C23 4 23 20 12 20 C1 20 1 4 12 4 Z"),
  ["line"]=Geometry.Parse("M5 19 L19 5"),
  ["undo"]=Geometry.Parse("M9 5 L4 10 L9 15 M4 10 L15 10 C22 10 22 20 15 20"),
  ["redo"]=Geometry.Parse("M15 5 L20 10 L15 15 M20 10 L9 10 C2 10 2 20 9 20"),
  ["save"]=Geometry.Parse("M4 3 L17 3 L21 7 L21 21 L3 21 L3 3 Z M7 3 L7 9 L16 9 L16 3 M7 21 L7 14 L17 14 L17 21"),
  ["panels"]=Geometry.Parse("M3 4 L21 4 L21 20 L3 20 Z M9 4 L9 20"),
  ["properties"]=Geometry.Parse("M4 7 L20 7 M4 17 L20 17 M9 4 L9 10 M15 14 L15 20"),
  ["theme"]=Geometry.Parse("M12 3 C7 3 3 7 3 12 C3 17 7 21 12 21 C17 21 21 17 21 12 C15 16 8 9 12 3 Z"),
  ["plus"]=Geometry.Parse("M5 12 L19 12 M12 5 L12 19"),
  ["minus"]=Geometry.Parse("M5 12 L19 12"),
  ["fit"]=Geometry.Parse("M9 3 L3 3 L3 9 M15 3 L21 3 L21 9 M3 15 L3 21 L9 21 M21 15 L21 21 L15 21 M8 8 L16 8 L16 16 L8 16 Z"),
  ["align-left"]=Geometry.Parse("M4 5 L20 5 M4 10 L15 10 M4 15 L20 15 M4 20 L15 20"),
  ["align-center"]=Geometry.Parse("M4 5 L20 5 M7 10 L17 10 M4 15 L20 15 M7 20 L17 20"),
  ["align-right"]=Geometry.Parse("M4 5 L20 5 M9 10 L20 10 M4 15 L20 15 M9 20 L20 20"),
  ["align-justify"]=Geometry.Parse("M4 5 L20 5 M4 10 L20 10 M4 15 L20 15 M4 20 L20 20"),
  ["rotate"]=Geometry.Parse("M5 7 C9 1 19 3 21 11 C23 19 13 24 7 18 M5 2 L5 8 L11 8")
 };
 static StudioIcon()=>AffectsRender<StudioIcon>(KindProperty,StrokeProperty);
 public StudioIcon(){Width=18;Height=18;IsHitTestVisible=false;}
 public override void Render(DrawingContext context) {
  if(!Shapes.TryGetValue(Kind,out var geometry))return;
  using(context.PushTransform(Matrix.CreateScale(Bounds.Width/24,Bounds.Height/24)))context.DrawGeometry(null,new Pen(Stroke,1.7, lineCap:PenLineCap.Round,lineJoin:PenLineJoin.Round),geometry);
 }
}
