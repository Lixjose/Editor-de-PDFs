using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
namespace Mythlix.Desktop.Controls;
public sealed class WorkspaceBackdrop:Control {
 public override void Render(DrawingContext context) {
  context.FillRectangle(Brush.Parse("#0D0E11"),new Rect(Bounds.Size));
  var brush=Brush.Parse("#343538");
  for(double y=10;y<Bounds.Height;y+=22)for(double x=10;x<Bounds.Width;x+=22)context.DrawEllipse(brush,null,new Point(x,y),.65,.65);
 }
}
