using Mythlix.Core;
using PdfSharp.Drawing;
using PdfSharp.Pdf;
namespace Mythlix.Infrastructure.Ocr;
public static class OcrReconstruction {
 /// <summary>Creates a separate text-only PDF with editable Mythlix objects. Never replaces the scan.</summary>
 public static void Save(string destination,IReadOnlyList<(PageGeometry Geometry,OcrPageResult Result)> pages) {
  if(pages.Count==0)throw new InvalidOperationException("No hay páginas OCR para reconstruir.");
  using var doc=new PdfDocument();
  foreach(var (geometry,result) in pages) {
   result.Validate();var page=doc.AddPage();page.Width=XUnit.FromPoint(geometry.Width);page.Height=XUnit.FromPoint(geometry.Height);
   var rows=new List<List<OcrWord>>();
   foreach(var word in result.Words.Where(w=>!string.IsNullOrWhiteSpace(w.Text))) {
    var row=rows.LastOrDefault();var last=row?.LastOrDefault();
    if(last is null || (last.Line>0 && word.Line>0 && (last.Block,last.Paragraph,last.Line)!=(word.Block,word.Paragraph,word.Line)) || Math.Abs(last.Y-word.Y)>Math.Max(last.Height,word.Height)*.6 || word.X<last.X || word.X-last.X-last.Width>Math.Max(last.Height,word.Height)*3)rows.Add([word]);
    else row!.Add(word);
   }
   var objects=rows.Select(row=> {
    var x=row.Min(w=>w.X)*geometry.Width/result.ImageWidth;var y=row.Min(w=>w.Y)*geometry.Height/result.ImageHeight;
    var width=(row.Max(w=>w.X+w.Width)*geometry.Width/result.ImageWidth)-x;
    var height=row.Max(w=>w.Height)*geometry.Height/result.ImageHeight;
    return geometry.Clamp(new GraphicObject{Kind=GraphicKind.Text,Text=string.Join(" ",row.Select(w=>w.Text)),X=x,Y=y,Width=Math.Max(4,width+4),Height=Math.Max(8,height*1.6),FontSize=Math.Clamp(height*.85,4,144),Color="#202124"});
   }).ToArray();
   GraphicLayerWriter.Write(page,objects,new(geometry.Width,geometry.Height,0));
  }
  var temp=destination+"."+Guid.NewGuid().ToString("N")+".tmp";
  try{doc.Save(temp);File.Move(temp,destination,true);}finally{if(File.Exists(temp))File.Delete(temp);}
 }
}
