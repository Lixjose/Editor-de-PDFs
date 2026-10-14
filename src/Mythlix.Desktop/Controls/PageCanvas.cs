using System.ComponentModel;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Mythlix.Core;
using Mythlix.Desktop.ViewModels;
namespace Mythlix.Desktop.Controls;

/// <summary>Displays the PDF render and selection geometry; pointer gestures commit through MVVM.</summary>
public sealed class PageCanvas : Control {
 public static readonly StyledProperty<WorkspaceViewModel?> WorkspaceProperty = AvaloniaProperty.Register<PageCanvas,WorkspaceViewModel?>(nameof(Workspace));
 public WorkspaceViewModel? Workspace {get=>GetValue(WorkspaceProperty);set=>SetValue(WorkspaceProperty,value);}
 GraphicObject? dragObject;
 ObjectBounds? preview;
 Point start;
 bool resizing;
 bool moved;
 protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change) {
  base.OnPropertyChanged(change);
  if(change.Property==WorkspaceProperty) {
   if(change.OldValue is WorkspaceViewModel old) old.PropertyChanged-=WorkspaceChanged;
   if(change.NewValue is WorkspaceViewModel current) current.PropertyChanged+=WorkspaceChanged;
   InvalidateVisual();
  }
 }
 void WorkspaceChanged(object? sender,PropertyChangedEventArgs args)=>InvalidateVisual();
 protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e) {if(Workspace is {} vm)vm.PropertyChanged-=WorkspaceChanged;base.OnDetachedFromVisualTree(e);}
 protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e) {base.OnAttachedToVisualTree(e);if(Workspace is {} vm){vm.PropertyChanged-=WorkspaceChanged;vm.PropertyChanged+=WorkspaceChanged;}}
 public override void Render(DrawingContext context) {
  base.Render(context);
  var vm=Workspace;if(vm?.PageImage is not {} image)return;
  context.DrawImage(image,new Rect(0,0,image.PixelSize.Width,image.PixelSize.Height),new Rect(Bounds.Size));
  if(vm.SelectedOriginal is {} original) {
   var factor=Bounds.Width/vm.Geometry.DisplayWidth;var box=vm.Geometry.ToDisplay(original.Bounds);
   var rect=new Rect(box.X*factor,box.Y*factor,box.Width*factor,box.Height*factor);
   context.DrawRectangle(new SolidColorBrush(Color.Parse("#194F46E5")),new Pen(Brush.Parse("#C3C0FF"),1.5),rect);return;
  }
  if(vm.SelectedGraphic is not {} selected || !vm.Geometry.CanEdit)return;
  var scale=Bounds.Width/vm.Geometry.DisplayWidth;
  var b=preview??vm.Geometry.ToDisplay(new(selected.X,selected.Y,selected.Width,selected.Height));
  var r=new Rect(b.X*scale,b.Y*scale,b.Width*scale,b.Height*scale);
  var pen=new Pen(Brush.Parse("#4F46E5"),1.5);
  context.DrawRectangle(null,pen,r);
  foreach(var point in new[]{r.TopLeft,r.TopRight,r.BottomLeft,r.BottomRight})
   context.DrawRectangle(Brushes.White,pen,new Rect(point.X-4,point.Y-4,8,8));
  // Bottom-right handle resizes; the other handles show the object's bounds.
 }
 protected override void OnPointerPressed(PointerPressedEventArgs e) {
  base.OnPointerPressed(e);
  var vm=Workspace;if(vm is null || vm.Busy || !vm.Geometry.CanEdit || !e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)return;
  var scale=Bounds.Width/vm.Geometry.DisplayWidth;
  var p=e.GetPosition(this);start=new(p.X/scale,p.Y/scale);
  resizing=false;
  if(vm.SelectedGraphic is {} current) {
   var b=vm.Geometry.ToDisplay(new(current.X,current.Y,current.Width,current.Height));
   resizing=Math.Abs(p.X-(b.X+b.Width)*scale)<=9 && Math.Abs(p.Y-(b.Y+b.Height)*scale)<=9;
  }
  if(!resizing)vm.SelectedGraphic=vm.PageObjects.Reverse().FirstOrDefault(item=>vm.Geometry.ToDisplay(new(item.X,item.Y,item.Width,item.Height)).Contains(start.X,start.Y));
  if(vm.SelectedGraphic is null)vm.SelectedOriginal=vm.OriginalBlocks.Reverse().FirstOrDefault(item=>vm.Geometry.ToDisplay(item.Bounds).Contains(start.X,start.Y));
  dragObject=vm.SelectedGraphic; preview=null; moved=false;
  if(dragObject is not null)e.Pointer.Capture(this);
  InvalidateVisual();e.Handled=true;
 }
 protected override void OnPointerMoved(PointerEventArgs e) {
  base.OnPointerMoved(e);
  var vm=Workspace;if(vm is null || dragObject is null || vm.Busy)return;
  var scale=Bounds.Width/vm.Geometry.DisplayWidth;var p=e.GetPosition(this);
  var dx=p.X/scale-start.X;var dy=p.Y/scale-start.Y;
  if(Math.Abs(dx)+Math.Abs(dy)<.5 && !moved)return;
  moved=true;
  var b=vm.Geometry.ToDisplay(new(dragObject.X,dragObject.Y,dragObject.Width,dragObject.Height));
  preview=resizing ? b with {Width=Math.Clamp(b.Width+dx,4,vm.Geometry.DisplayWidth-b.X),Height=Math.Clamp(b.Height+dy,4,vm.Geometry.DisplayHeight-b.Y)}
   : b with {X=Math.Clamp(b.X+dx,0,vm.Geometry.DisplayWidth-b.Width),Y=Math.Clamp(b.Y+dy,0,vm.Geometry.DisplayHeight-b.Height)};
  InvalidateVisual();
 }
 protected override async void OnPointerReleased(PointerReleasedEventArgs e) {
  base.OnPointerReleased(e);
  var vm=Workspace;var item=dragObject;var b=preview;var changed=moved;
  dragObject=null;preview=null;moved=false;e.Pointer.Capture(null);
  if(vm is not null && item is not null && b is {} display && changed) {
   var raw=vm.Geometry.FromDisplay(display);
   await vm.CommitObjectAsync(item with {X=raw.X,Y=raw.Y,Width=raw.Width,Height=raw.Height});
  }
  InvalidateVisual();e.Handled=true;
 }
 protected override void OnPointerCaptureLost(PointerCaptureLostEventArgs e) {dragObject=null;preview=null;moved=false;InvalidateVisual();base.OnPointerCaptureLost(e);}
}
