using System.Text.Json;
using Mythlix.Core;
using PdfSharp.Drawing;
using PdfSharp.Drawing.Layout;
using PdfSharp.Pdf;
namespace Mythlix.Infrastructure;

internal static class GraphicLayerWriter {
 const string MetadataKey = "/MythlixObjectsV1";
 const string LayerKey = "/MythlixGraphicsV1";
 const string ResourcesKey = "/MythlixResourcesV1";
 static readonly string[] ResourceGroups = ["/Font","/XObject","/ExtGState","/ColorSpace","/Pattern","/Shading","/Properties"];
 public static IReadOnlyList<GraphicObject> Read(PdfPage page, PageGeometry geometry) {
  var json = page.Elements.GetString(MetadataKey);
  if(string.IsNullOrEmpty(json)) return [];
  if(json.Length > 32 * 1024 * 1024) throw new InvalidOperationException("La capa gráfica supera el límite de recursos.");
  var items = JsonSerializer.Deserialize<GraphicObject[]>(json) ?? [];
  if(items.Length > 200 || items.Select(o => o.Id).Distinct().Count() != items.Length)
   throw new InvalidOperationException("Capa gráfica inválida o con más de 200 objetos.");
  foreach(var item in items) item.Validate(geometry);
  return items;
 }
 public static void Write(PdfPage page, IReadOnlyList<GraphicObject> items, PageGeometry geometry) {
  if(!geometry.CanEdit) throw new InvalidOperationException("Esta fase no edita páginas con recorte u origen de coordenadas desplazado.");
  if(items.Count > 200 || items.Select(o => o.Id).Distinct().Count() != items.Count)
   throw new InvalidOperationException("Máximo de 200 objetos por página; los identificadores deben ser únicos.");
  foreach(var item in items) item.Validate(geometry);
  var metadata = JsonSerializer.Serialize(items);
  if(metadata.Length > 32 * 1024 * 1024) throw new InvalidOperationException("La capa gráfica supera 32 MB.");
  // Isolate resource mappings for this page: imported PDFs may share them between pages.
  var resources = IsolateResources(page);
  var ownedJson = page.Elements.GetString(ResourcesKey);
  if(ownedJson.Length > 16384) throw new InvalidOperationException("Registro de recursos gráficos inválido.");
  var owned = string.IsNullOrEmpty(ownedJson) ? new Dictionary<string,string[]>() :
   JsonSerializer.Deserialize<Dictionary<string,string[]>>(ownedJson) ?? new();
  foreach(var (group,names) in owned) {
   if(!ResourceGroups.Contains(group)) throw new InvalidOperationException("Grupo de recursos gráficos inválido.");
   var entries = resources.Elements.GetDictionary(group);
   if(entries is not null) foreach(var name in names) entries.Elements.Remove(name);
  }
  page.Elements.Remove(ResourcesKey);
  var before = ResourceGroups.ToDictionary(group=>group,group=>resources.Elements.GetDictionary(group)?.Elements.Keys.ToHashSet() ?? []);
  for(var i=page.Contents.Elements.Count-1;i>=0;i--) {
   var stream = page.Contents.Elements.GetDictionary(i);
   if(stream?.Elements.GetBoolean(LayerKey) == true) page.Contents.Elements.RemoveAt(i);
  }
  page.Elements.Remove(MetadataKey);
  if(items.Count == 0) return;
  EmbeddedFontResolver.EnsureInitialized();
  // Append only our own new stream. Never replace the original page contents.
  using(var graphics = XGraphics.FromPdfPage(page, XGraphicsPdfPageOptions.Append)) {
   foreach(var item in items) Draw(graphics,item);
  }
  page.Contents.Elements.GetDictionary(page.Contents.Elements.Count-1)!.Elements.SetBoolean(LayerKey, true);
  page.Elements.SetString(MetadataKey, metadata);
  var added = ResourceGroups.ToDictionary(group=>group,group=>
   (page.Elements.GetDictionary("/Resources")?.Elements.GetDictionary(group)?.Elements.Keys ?? [])
    .Where(name=>!before[group].Contains(name)).ToArray());
  page.Elements.SetString(ResourcesKey,JsonSerializer.Serialize(added));
 }
 internal static PdfDictionary IsolateResources(PdfPage page) {
  PdfDictionary? node=page;
  PdfDictionary? original=null;
  for(var depth=0;node is not null && depth<64;depth++) {
   original=node.Elements.GetDictionary("/Resources");
   if(original is not null)break;
   node=node.Elements.GetDictionary("/Parent");
  }
  var result=new PdfDictionary(page.Owner);
  if(original is not null) foreach(var key in original.Elements.Keys) {
   if(ResourceGroups.Contains(key) && original.Elements.GetDictionary(key) is {} group) {
    var copy=new PdfDictionary(page.Owner);
    foreach(var name in group.Elements.Keys)copy.Elements[name]=group.Elements[name];
    result.Elements[key]=copy;
   } else result.Elements[key]=original.Elements[key];
  }
  page.Elements["/Resources"]=result;return result;
 }
 static void Draw(XGraphics graphics, GraphicObject item) {
  var rgb=Convert.ToUInt32(item.Color[1..],16);
  var color=XColor.FromArgb((int)((rgb>>16)&255),(int)((rgb>>8)&255),(int)(rgb&255));
  var pen=new XPen(color,item.StrokeWidth);
  var rectangle=new XRect(item.X,item.Y,item.Width,item.Height);
  switch(item.Kind) {
   case GraphicKind.Text:
    var style = (item.Bold ? XFontStyleEx.Bold : XFontStyleEx.Regular) | (item.Italic ? XFontStyleEx.Italic : XFontStyleEx.Regular);
    var family = string.IsNullOrWhiteSpace(item.FontFamily) ? "Liberation Sans" : item.FontFamily;
    var font=new XFont(family,item.FontSize,style,new XPdfFontOptions(PdfFontEncoding.Unicode));
    var state=graphics.Save(); graphics.IntersectClip(rectangle);
    var formatter = new XTextFormatter(graphics) {
     Alignment = item.Alignment switch {
      TextAlignment.Center => XParagraphAlignment.Center,
      TextAlignment.Right => XParagraphAlignment.Right,
      TextAlignment.Justify => XParagraphAlignment.Justify,
      _ => XParagraphAlignment.Left
     }
    };
    formatter.DrawString(item.Text,font,new XSolidBrush(color),rectangle,XStringFormats.TopLeft);
    graphics.Restore(state); break;
   case GraphicKind.Image:
    using(var memory=new MemoryStream(item.ImageData!,0,item.ImageData!.Length,false,true))
    using(var image=XImage.FromStream(memory)) {
     if((long)image.PixelWidth*image.PixelHeight>25_000_000) throw new InvalidOperationException("Límite de imagen: 25 megapíxeles.");
     graphics.DrawImage(image,rectangle);
    }
    break;
   case GraphicKind.Rectangle: graphics.DrawRectangle(pen,rectangle); break;
   case GraphicKind.Ellipse: graphics.DrawEllipse(pen,rectangle); break;
   case GraphicKind.Line: graphics.DrawLine(pen,item.X,item.Y,item.X+item.Width,item.Y+item.Height); break;
   default: throw new InvalidOperationException("Tipo de objeto no compatible.");
  }
 }
}
