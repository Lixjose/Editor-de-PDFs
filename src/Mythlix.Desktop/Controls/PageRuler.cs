using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
namespace Mythlix.Desktop.Controls;

/// <summary>Page-point ticks share the paper's scroll transform and zoom.</summary>
public sealed class PageRuler : Control {
 public static readonly StyledProperty<double> LengthPtProperty=AvaloniaProperty.Register<PageRuler,double>(nameof(LengthPt),612);
 public static readonly StyledProperty<bool> VerticalProperty=AvaloniaProperty.Register<PageRuler,bool>(nameof(Vertical));
 public static readonly StyledProperty<IBrush> InkProperty=AvaloniaProperty.Register<PageRuler,IBrush>(nameof(Ink),Brushes.Gray);
 public static readonly StyledProperty<IBrush> SurfaceProperty=AvaloniaProperty.Register<PageRuler,IBrush>(nameof(Surface),Brushes.Transparent);
 public double LengthPt{get=>GetValue(LengthPtProperty);set=>SetValue(LengthPtProperty,value);}
 public bool Vertical{get=>GetValue(VerticalProperty);set=>SetValue(VerticalProperty,value);}
 public IBrush Ink{get=>GetValue(InkProperty);set=>SetValue(InkProperty,value);}
 public IBrush Surface{get=>GetValue(SurfaceProperty);set=>SetValue(SurfaceProperty,value);}
 static PageRuler()=>AffectsRender<PageRuler>(LengthPtProperty,VerticalProperty,InkProperty,SurfaceProperty);
 public override void Render(DrawingContext context) {
  context.FillRectangle(Surface,new Rect(Bounds.Size));
  var extent=Vertical?Bounds.Height:Bounds.Width;
  if(!double.IsFinite(LengthPt) || LengthPt<=0 || extent<=0)return;
  var scale=extent/LengthPt;var major=scale<.6?100:50;var step=major/5d;
  for(var point=0d;point<=LengthPt && point/step<10000;point+=step) {
   var pos=point*scale;var large=Math.Abs(point%major)<.001;var size=large?8:4;
   context.DrawLine(new Pen(Ink,.7),Vertical?new(Bounds.Width-size,pos):new(pos,Bounds.Height-size),Vertical?new(Bounds.Width,pos):new(pos,Bounds.Height));
   if(large) {
    var label=new FormattedText(point.ToString("0",CultureInfo.InvariantCulture),CultureInfo.InvariantCulture,FlowDirection.LeftToRight,Typeface.Default,9,Ink);
    context.DrawText(label,Vertical?new(1,pos+2):new(pos+3,1));
   }
  }
 }
}
