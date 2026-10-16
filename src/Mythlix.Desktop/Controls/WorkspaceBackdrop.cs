using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
namespace Mythlix.Desktop.Controls;
public sealed class WorkspaceBackdrop:Control {
 public static readonly StyledProperty<IBrush> SurfaceProperty=AvaloniaProperty.Register<WorkspaceBackdrop,IBrush>(nameof(Surface),Brush.Parse("#101114"));
 public static readonly StyledProperty<IBrush> DotProperty=AvaloniaProperty.Register<WorkspaceBackdrop,IBrush>(nameof(Dot),Brush.Parse("#30343C"));
 public IBrush Surface{get=>GetValue(SurfaceProperty);set=>SetValue(SurfaceProperty,value);}
 public IBrush Dot{get=>GetValue(DotProperty);set=>SetValue(DotProperty,value);}
 static WorkspaceBackdrop()=>AffectsRender<WorkspaceBackdrop>(SurfaceProperty,DotProperty);
 public override void Render(DrawingContext context) {
  context.FillRectangle(Surface,new Rect(Bounds.Size));
  var brush=Dot;
  for(double y=10;y<Bounds.Height;y+=22)for(double x=10;x<Bounds.Width;x+=22)context.DrawEllipse(brush,null,new Point(x,y),.65,.65);
 }
}
