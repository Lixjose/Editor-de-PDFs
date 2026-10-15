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
}
