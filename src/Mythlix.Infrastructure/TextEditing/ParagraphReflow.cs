using System.Text;
using Mythlix.Core;
namespace Mythlix.Infrastructure.TextEditing;

public sealed record ReflowLine(string Text, byte[] EncodedBytes, double Width, double XOffset, double WordSpacing = 0);

public static class ParagraphReflow {
 public static IReadOnlyList<ReflowLine> Reflow(string text, double fontSize, double[] widths, Encoding encoding, double maxWidth, TextAlignment alignment, double characterSpacing = 0, double wordSpacing = 0) {
  if(string.IsNullOrEmpty(text)) return [];
  if(!double.IsFinite(fontSize) || fontSize <= 0 || !double.IsFinite(maxWidth)) throw new ArgumentOutOfRangeException(nameof(fontSize));
  if(alignment==TextAlignment.Justify)wordSpacing=0;
  var limit=maxWidth>0?maxWidth:1000;
  var lines=new List<ReflowLine>();
  foreach(var paragraph in text.Replace("\r\n","\n").Replace('\r','\n').Split('\n')) {
   var words=paragraph.Split((char[]?)null,StringSplitOptions.RemoveEmptyEntries);
   if(words.Length==0) {lines.Add(new("",[],0,0));continue;}
   var current="";
   foreach(var word in words) {
    var proposed=current.Length==0?word:current+" "+word;
    if(current.Length>0 && Measure(proposed)>limit) {
     Add(current,false);current=word;
    } else current=proposed;
   }
   Add(current,true);
  }
  return lines;
  double Measure(string value)=>MeasureText(value,fontSize,widths,encoding)+Math.Max(0,value.Length-1)*characterSpacing+value.Count(c=>c==' ')*wordSpacing;
  void Add(string value,bool last) {
   var width=Measure(value);
   var spaces=value.Count(c=>c==' ');
   var spacing=alignment==TextAlignment.Justify && !last && spaces>0 ? wordSpacing+Math.Max(0,(limit-width)/spaces):wordSpacing;
   var offset=alignment switch {TextAlignment.Center=>Math.Max(0,(limit-width)/2),TextAlignment.Right=>Math.Max(0,limit-width),_=>0};
   lines.Add(new(value,encoding.GetBytes(value),width,offset,spacing));
  }
 }
 public static double MeasureText(string text,double fontSize,double[] widths,Encoding encoding) {
  return encoding.GetBytes(text).Sum(b=>b<widths.Length && widths[b]>0 && double.IsFinite(widths[b])?widths[b]:500)*fontSize/1000;
 }
}
