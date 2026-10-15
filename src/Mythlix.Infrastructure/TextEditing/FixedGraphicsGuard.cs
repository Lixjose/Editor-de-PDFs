using Mythlix.Core;
using PdfSharp.Pdf;
namespace Mythlix.Infrastructure.TextEditing;

/// <summary>Bounds untransformed paths. Unknown graphics remain a blocking condition.</summary>
internal static class FixedGraphicsGuard {
 public static bool Blocks(PdfPage page,PageGeometry geometry,ObjectBounds corridor) {
  double width=1,miter=10;
  var stack=new Stack<(double Width,double Miter)>();
  var operands=new List<double>();
  var points=new List<(double X,double Y)>();
  bool text=false;
  bool Intersects(ObjectBounds b)=>b.X<=corridor.X+corridor.Width && b.X+b.Width>=corridor.X && b.Y<=corridor.Y+corridor.Height && b.Y+b.Height>=corridor.Y;
  bool Read(int n)=>operands.Count==n && operands.All(double.IsFinite);
  for(var stream=0;stream<page.Contents.Elements.Count;stream++) {
   var dict=page.Contents.Elements.GetDictionary(stream);
   if(dict?.Elements.GetBoolean("/MythlixGraphicsV1")==true)continue;
   var bytes=dict?.Stream?.UnfilteredValue;if(bytes is null)continue;
   foreach(var token in ContentTokenizer.Read(bytes)) {
    if(token.Bytes is not null) {if(!text)return true;continue;}
    if(token.Value=="BT") {if(text)return true;text=true;operands.Clear();continue;}
    if(token.Value=="ET") {if(!text)return true;text=false;operands.Clear();continue;}
    if(text)continue;
    if(token.Number(out var number)){operands.Add(number);continue;}
    switch(token.Value) {
     case "q": if(!Read(0))return true;stack.Push((width,miter));break;
     case "Q": if(!Read(0) || stack.Count==0)return true;(width,miter)=stack.Pop();break;
     case "w": if(!Read(1) || operands[0]<0)return true;width=operands[0];break;
     case "M": if(!Read(1) || operands[0]<1)return true;miter=operands[0];break;
     case "J": case "j": if(!Read(1))return true;break;
     case "g": case "G": if(!Read(1))return true;break;
     case "rg": case "RG": if(!Read(3))return true;break;
     case "k": case "K": if(!Read(4))return true;break;
     case "m": case "l":
      if(!Read(2))return true;points.Add((operands[0],operands[1]));break;
     case "re":
      if(!Read(4))return true;
      points.Add((operands[0],operands[1]));points.Add((operands[0]+operands[2],operands[1]+operands[3]));break;
     case "h": if(!Read(0))return true;break;
     case "n": if(!Read(0))return true;points.Clear();break;
     case "S": case "s": case "f": case "F": case "f*": case "B": case "B*": case "b": case "b*":
      if(!Read(0) || points.Count==0)return true;
      var stroke=token.Value is "S" or "s" or "B" or "B*" or "b" or "b*";
      var pad=stroke?Math.Max(1,width)*miter/2:0;
      var left=points.Min(p=>p.X)-pad;var right=points.Max(p=>p.X)+pad;
      var bottom=points.Min(p=>p.Y)-pad;var top=points.Max(p=>p.Y)+pad;
      if(Intersects(new(left,geometry.Height-top,right-left,top-bottom)))return true;
      points.Clear();break;
     // CTM, XObjects, clipping, shading, images and unrecognized state cannot be bounded here.
     default:return true;
    }
    operands.Clear();
   }
  }
  return text || stack.Count!=0 || operands.Count!=0 || points.Count!=0;
 }
}
