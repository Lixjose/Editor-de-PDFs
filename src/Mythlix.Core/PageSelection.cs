using System.Globalization;
namespace Mythlix.Core;
public static class PageSelection {
 public static IReadOnlyList<int> Parse(string text, int pageCount) {
  var result=new List<int>(); var seen=new HashSet<int>();
  foreach(var part in text.Split(',')) {
   var bounds=part.Trim().Split('-');
   if(bounds.Length>2 || !int.TryParse(bounds[0],NumberStyles.None,CultureInfo.InvariantCulture,out var first)) throw new ArgumentException("Use páginas como 1,3-5.");
   var last=first;
   if(bounds.Length==2 && !int.TryParse(bounds[1],NumberStyles.None,CultureInfo.InvariantCulture,out last)) throw new ArgumentException("Intervalo inválido.");
   if(first<1 || last<first || last>pageCount) throw new ArgumentException("Intervalo fuera del documento.");
   for(var page=first;page<=last;page++) { if(!seen.Add(page)) throw new ArgumentException("No repita páginas."); result.Add(page); }
  }
  return result;
 }
}
