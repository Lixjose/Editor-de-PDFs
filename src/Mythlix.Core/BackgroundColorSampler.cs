namespace Mythlix.Core;

/// <summary>Accepts only a nearly uniform opaque perimeter; never invents image detail.</summary>
public static class BackgroundColorSampler {
 public static uint? UniformColor(IEnumerable<uint> samples) {
  var colors=samples.ToArray();
  if(colors.Length<12 || colors.Any(c=>(c>>24)!=255))return null;
  var dominant=colors.GroupBy(c=>c).OrderByDescending(g=>g.Count()).First().Key;
  var close=colors.Count(c=>Enumerable.Range(0,3).All(channel=>Math.Abs((int)((c>>(channel*8))&255)-(int)((dominant>>(channel*8))&255))<=6));
  return close>=colors.Length*.95?dominant:null;
 }
 public static bool InteriorMatches(IEnumerable<uint> pixels,uint background,uint foreground) {
  double blue=background&255,green=(background>>8)&255,red=(background>>16)&255;
  double db=(foreground&255)-blue,dg=((foreground>>8)&255)-green,dr=((foreground>>16)&255)-red;
  var length=db*db+dg*dg+dr*dr;
  long count=0,unexpected=0;
  foreach(var pixel in pixels) {
   count++;
   double b=pixel&255,g=(pixel>>8)&255,r=(pixel>>16)&255;
   var t=length>0?Math.Clamp(((b-blue)*db+(g-green)*dg+(r-red)*dr)/length,0,1):0;
   if((pixel>>24)!=255 || Math.Abs(b-blue-t*db)>10 || Math.Abs(g-green-t*dg)>10 || Math.Abs(r-red-t*dr)>10)unexpected++;
  }
  return count>0 && unexpected<=count*.02;
 }
}
