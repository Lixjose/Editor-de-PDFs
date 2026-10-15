using System.Runtime.InteropServices;
using Avalonia;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Mythlix.Core;
namespace Mythlix.Desktop.Controls;

internal static class EditingBackground {
 public static IBrush? Sample(Bitmap bitmap,Rect displayBox,Size displaySize,Color foreground) {
  if(displaySize.Width<=0 || displaySize.Height<=0)return null;
  var size=bitmap.PixelSize;
  using var copy=new WriteableBitmap(size,new Vector(96,96),PixelFormat.Bgra8888,AlphaFormat.Unpremul);
  using var pixels=copy.Lock();
  bitmap.CopyPixels(pixels);
  var left=(int)Math.Floor(displayBox.Left/displaySize.Width*size.Width)-3;
  var top=(int)Math.Floor(displayBox.Top/displaySize.Height*size.Height)-3;
  var right=(int)Math.Ceiling(displayBox.Right/displaySize.Width*size.Width)+3;
  var bottom=(int)Math.Ceiling(displayBox.Bottom/displaySize.Height*size.Height)+3;
  var samples=new List<uint>();
  void Read(int x,int y) {
   if(x>=0 && x<size.Width && y>=0 && y<size.Height)samples.Add(unchecked((uint)Marshal.ReadInt32(pixels.Address,y*pixels.RowBytes+x*4)));
  }
  for(var offset=0;offset<2;offset++) {
   for(var x=left;x<=right;x++){Read(x,top+offset);Read(x,bottom-offset);}
   for(var y=top+2;y<bottom-1;y++){Read(left+offset,y);Read(right-offset,y);}
  }
  var color=BackgroundColorSampler.UniformColor(samples);
  if(!color.HasValue)return null;
  IEnumerable<uint> Interior() {
   for(var y=Math.Max(0,top+3);y<=Math.Min(size.Height-1,bottom-3);y++)
    for(var x=Math.Max(0,left+3);x<=Math.Min(size.Width-1,right-3);x++)
     yield return unchecked((uint)Marshal.ReadInt32(pixels.Address,y*pixels.RowBytes+x*4));
  }
  return BackgroundColorSampler.InteriorMatches(Interior(),color.Value,foreground.ToUInt32())?new SolidColorBrush(Color.FromUInt32(color.Value)):null;
 }
}
