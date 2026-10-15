using System.Collections.Immutable;
using System.Security.Cryptography;
using System.Text;
using Mythlix.Core;
using Mythlix.Core.TextEditing;
using PdfSharp.Pdf;

namespace Mythlix.Infrastructure.TextEditing;

/// <summary>Conservative, read-only interpreter for horizontal standard Type 1 WinAnsi text.</summary>
internal static class NativeRunExtractor {
 sealed record State(string Font = "", double Size = 0, double Tc = 0, double Tw = 0,
  double Scale = 1, double Rise = 0, double Leading = 0, double Mode = 0, RunRgbColor? Color = null);
 sealed class Unsupported(string code, string message) : Exception(message) { public string Code { get; } = code; }
 static readonly Encoding WinAnsi;
 static NativeRunExtractor() {
  Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
  WinAnsi=Encoding.GetEncoding(1252,EncoderFallback.ExceptionFallback,DecoderFallback.ExceptionFallback);
 }
 public static NativeRunAnalysis Analyze(PdfPage page, PageGeometry geometry, TextDocumentContext context) {
  var runs=ImmutableArray.CreateBuilder<NativeTextRun>();
  var adjustments=ImmutableArray.CreateBuilder<NativeTextAdjustment>();
  int streamIndex=-1, offset=0;
  try {
   if(!geometry.CanEdit || geometry.Rotation!=0) Fail("Geometry", "Recorte, origen o rotación no compatibles con esta extracción.");
   var (resources,scope)=Resources(page);
   var defaults=resources?.Elements.GetDictionary("/ColorSpace");
   if(defaults is not null && new[] {"/DefaultRGB","/DefaultGray"}.Any(defaults.Elements.ContainsKey))
    Fail("ColorSpace","Espacios de color por defecto redefinidos fuera del alcance.");
   var fonts=resources?.Elements.GetDictionary("/Font");
   var state=new State(Color:new(0,0,0));
   var states=new Stack<State>();
   long total=0;
   var fontCache=new Dictionary<string,(string Family,double[] Widths)>();
   for(streamIndex=0;streamIndex<page.Contents.Elements.Count;streamIndex++) {
    offset=0;
    var dict=page.Contents.Elements.GetDictionary(streamIndex);
    if(dict is null) Fail("Content", "Stream de contenido inválido.");
    // Graphics/OCR layers are excluded only when they are self-contained. Rejecting
    // the page prevents assuming their state cannot affect later original streams.
    if(dict!.Elements.GetBoolean("/MythlixGraphicsV1") || dict.Elements.GetBoolean("/MythlixOcrV1"))
     Fail("OwnedLayer", "La página contiene capas añadidas; su análisis mixto aún no está habilitado.");
    var bytes=dict.Stream?.UnfilteredValue ?? [];
    total+=bytes.Length;
    if(total>16*1024*1024) Fail("Limit", "Contenido de página superior a 16 MB.");
    var tokens=ContentTokenizer.Read(bytes);
    var textEnds=new Dictionary<int,Token>();
    Token? openText=null;
    foreach(var item in tokens.Where(t=>t.Bytes is null)) {
     if(item.Value=="BT") {
      if(openText is not null) Fail("TextObject","BT anidado.");
      openText=item;
     } else if(item.Value=="ET") {
      if(openText is null) Fail("TextObject","ET sin BT.");
      textEnds.Add(openText!.Start,item);openText=null;
     }
    }
    if(openText is not null) Fail("ContentBoundary","BT/ET dividido o incompleto.");
    var fingerprint=Hash(bytes);
    var operands=new List<Token>();
    bool inside=false;
    int blockStart=0,ordinal=0,textOffset=0;
    string blockId="",blockHash="";
    double x=0,y=0,lineX=0,lineY=0;
    foreach(var token in tokens) {
     offset=token.Start;
     if(token.Bytes is not null || token.Number(out _) || token.Value.StartsWith('/') || token.Value is "[" or "]") {
      operands.Add(token); continue;
     }
     var op=token.Value;
     double Number(int i) {
      if(i<0 || i>=operands.Count || !operands[i].Number(out var value)) {Fail("Operands", "Operandos numéricos inválidos.");return 0;}
      return value;
     }
     void Count(int count) {if(operands.Count!=count) Fail("Operands", "Número de operandos inválido para " + op);}
     void InText() {if(!inside) Fail("TextObject", "Operador de texto fuera de BT/ET.");}
     void NextLine() {lineY-=state.Leading;x=lineX;y=lineY;}
     void Show(Token textToken) {
      InText();
      var encoded=textToken.Bytes;
      if(encoded is null) Fail("Operands", "Se esperaba una cadena PDF.");
      if(state.Mode!=0) Fail("RenderingMode", "Solo se admite texto de relleno, Tr=0.");
      if(state.Size<=0 || !double.IsFinite(state.Size) || state.Scale<=0 || !double.IsFinite(state.Scale))
       Fail("TextState", "Fuente, tamaño o escala inválidos.");
      if(!fontCache.TryGetValue(state.Font,out var font)) {
       font=ReadFont(fonts?.Elements.GetDictionary(state.Font));fontCache[state.Font]=font;
      }
      var advances=ImmutableArray.CreateBuilder<double>();
      foreach(var b in encoded!) {
       // Undefined WinAnsi codes and control characters never become invented glyphs.
       if(b<32 || b==127 || b==173 || b is 129 or 141 or 143 or 144 or 157 || font.Widths[b]<=0 || !double.IsFinite(font.Widths[b]))
        Fail("Encoding", "Código WinAnsi sin mapeo o métricas compatibles.");
       var advance=(font.Widths[b]*state.Size/1000+state.Tc+(b==32?state.Tw:0))*state.Scale;
       if(!double.IsFinite(advance)) Fail("TextState", "Avance no finito.");
       advances.Add(advance);
      }
      if(!double.IsFinite(x) || !double.IsFinite(y)) Fail("TextState","Posición no finita.");
      if(encoded!.Length>0) {
       var text=WinAnsi.GetString(encoded);
       var source=new TextSourceSpan(context,streamIndex,new(textToken.Start,textToken.Length),fingerprint,
        blockId,blockHash,new(textOffset,text.Length));
       var style=new PdfRunStyle(new(context,scope,state.Font),font.Family,"WinAnsiEncoding",state.Size,state.Color!,
        bold:font.Family.Contains("Bold",StringComparison.Ordinal),italic:font.Family.Contains("Italic",StringComparison.Ordinal)||font.Family.Contains("Oblique",StringComparison.Ordinal),
        characterSpacingPt:state.Tc,wordSpacingPt:state.Tw,horizontalScale:state.Scale,baselineRisePt:state.Rise);
       var run=new PdfTextRun(Guid.NewGuid(),text,style,[new(new(0,text.Length),source)]);
       runs.Add(new(run,x,geometry.Height-y,encoded.ToImmutableArray(),advances.ToImmutable(),new(token.Start,token.Length)));
       textOffset+=text.Length;
      }
      x+=advances.Sum();
      if(!double.IsFinite(x)) Fail("TextState", "Posición no finita.");
     }
     switch(op) {
      case "BT":
       Count(0);if(inside) Fail("TextObject", "BT anidado.");
       inside=true;blockStart=token.Start;blockId=$"{streamIndex}:{ordinal++}";textOffset=0;
       var end=textEnds[blockStart];
       blockHash=Hash(bytes.AsSpan(blockStart,end.Start+end.Length-blockStart));
       x=y=lineX=lineY=0; break;
      case "ET": Count(0);InText();inside=false;break;
      case "q": Count(0);if(inside) Fail("TextObject","q dentro de texto no compatible.");states.Push(state);break;
      case "Q": Count(0);if(inside || states.Count==0) Fail("GraphicsState","Estado gráfico desequilibrado.");state=states.Pop();break;
      case "Tf": Count(2);if(!operands[0].Value.StartsWith('/')) Fail("Operands","Recurso de fuente inválido.");
       state=state with {Font=operands[0].Value,Size=Number(1)};break;
      case "Tc": Count(1);state=state with {Tc=Number(0)};break;
      case "Tw": Count(1);state=state with {Tw=Number(0)};break;
      case "Tz": Count(1);state=state with {Scale=Number(0)/100};break;
      case "Ts": Count(1);state=state with {Rise=Number(0)};break;
      case "Tr": Count(1);state=state with {Mode=Number(0)};break;
      case "TL": Count(1);state=state with {Leading=Number(0)};break;
      case "Tm":
       InText();Count(6);
       if(Number(0)!=1 || Number(1)!=0 || Number(2)!=0 || Number(3)!=1) Fail("Transform","Solo se admiten matrices de traslación.");
       x=lineX=Number(4);y=lineY=Number(5);break;
      case "Td": case "TD":
       InText();Count(2);var dx=Number(0);var dy=Number(1);
       if(op=="TD") state=state with {Leading=-dy};
       x=lineX+=dx;y=lineY+=dy;break;
      case "T*": InText();Count(0);NextLine();break;
      case "Tj": Count(1);Show(operands[0]);break;
      case "'": InText();Count(1);NextLine();Show(operands[0]);break;
      case "\"": InText();Count(3);state=state with {Tw=Number(0),Tc=Number(1)};NextLine();Show(operands[2]);break;
      case "TJ":
       InText();if(operands.Count<2 || operands[0].Value!="[" || operands[^1].Value!="]") Fail("Operands","Arreglo TJ inválido.");
       foreach(var item in operands.Skip(1).Take(operands.Count-2)) {
        if(item.Bytes is not null) Show(item);
        else if(item.Number(out var adjust)) {
         var delta=-adjust*state.Size*state.Scale/1000;
         if(!double.IsFinite(delta) || state.Size<=0 || state.Scale<=0) Fail("TextState","Ajuste TJ inválido.");
         adjustments.Add(new(streamIndex,blockId,new(item.Start,item.Length),adjust,delta));x+=delta;
        } else Fail("Operands","Elemento TJ no compatible.");
       }
       break;
      case "rg": Count(3);state=state with {Color=new(Number(0),Number(1),Number(2))};break;
      case "g": Count(1);var gray=Number(0);state=state with {Color=new(gray,gray,gray)};break;
      // Ordinary paths do not affect text placement or supported fill color.
      case "m": case "l": Count(2);Path();break;
      case "c": Count(6);Path();break;
      case "v": case "y": case "re": Count(4);Path();break;
      case "h": case "f": case "f*": case "F": case "S": case "s": case "B": case "B*": case "b": case "b*": case "n": Count(0);Path();break;
      case "w": case "J": case "j": case "M": Count(1);Path();break;
      case "RG": Count(3);Path();break;
      case "G": Count(1);Path();break;
      default: Fail("UnsupportedOperator", "Operador fuera del alcance: " + op);break;
     }
     void Path() {if(inside) Fail("TextObject","Gráficos dentro de BT/ET no compatibles.");}
     operands.Clear();
    }
    if(inside || operands.Count>0) Fail("ContentBoundary","Objeto u operandos divididos entre streams no compatibles.");
   }
   if(states.Count!=0) {streamIndex=Math.Max(0,streamIndex-1);Fail("GraphicsState","q/Q sin equilibrar.");}
   return new(context,runs.ToImmutable(),adjustments.ToImmutable(),[]);
  } catch(Unsupported ex) {
   return new(context,[],[],[new(ex.Code,streamIndex>=0?streamIndex:null,offset,ex.Message)]);
  } catch(Exception ex) when(ex is ArgumentException or InvalidOperationException or FormatException or InvalidCastException or OverflowException) {
   return new(context,[],[],[new("InvalidContent",streamIndex>=0?streamIndex:null,offset,"Contenido inválido o no compatible con la extracción nativa.")]);
  }
 }
 static void Fail(string code,string message) => throw new Unsupported(code,message);
 static string Hash(ReadOnlySpan<byte> bytes) => Convert.ToHexString(SHA256.HashData(bytes));
 static (PdfDictionary? Resources,string Scope) Resources(PdfPage page) {
  PdfDictionary? node=page;
  for(var depth=0;node is not null && depth<64;depth++,node=node.Elements.GetDictionary("/Parent")) {
   if(node.Elements.GetDictionary("/Resources") is {} resources)
    return (resources,$"resources:{resources.Reference?.ObjectID.ToString() ?? "direct"}:depth:{depth}");
  }
  return (null,"resources:missing");
 }
 static (string Family,double[] Widths) ReadFont(PdfDictionary? font) {
  if(font is null || font.Elements.GetName("/Subtype")!="/Type1" || font.Elements.ContainsKey("/ToUnicode"))
   Fail("Font","Fuente ausente, CID o codificación fuera del alcance.");
  if(font!.Elements.GetName("/Encoding")!="/WinAnsiEncoding") Fail("Encoding","Solo WinAnsi explícito está habilitado.");
  var family=font.Elements.GetName("/BaseFont").TrimStart('/');
  if(!(family.StartsWith("Helvetica",StringComparison.Ordinal)||family.StartsWith("Times-",StringComparison.Ordinal)||family.StartsWith("Courier",StringComparison.Ordinal)) || !StandardFontMetrics.Fonts.ContainsKey(family))
   Fail("Font","Solo fuentes estándar compatibles, sin subconjuntos.");
  var descriptor=font.Elements.GetDictionary("/FontDescriptor");
  if(descriptor is not null && new[] {"/FontFile","/FontFile2","/FontFile3"}.Any(descriptor.Elements.ContainsKey))
   Fail("Font","Fuentes incrustadas fuera del alcance inicial.");
  var widths=StandardFontMetrics.Fonts[family].Widths.Select(w=>(double)w).ToArray();
  if(font.Elements.GetArray("/Widths") is {} declared) {
   if(!font.Elements.ContainsKey("/FirstChar") || !font.Elements.ContainsKey("/LastChar"))
    Fail("Font","La tabla de anchos requiere FirstChar y LastChar.");
   var first=font.Elements.GetInteger("/FirstChar");
   var last=font.Elements.GetInteger("/LastChar");
   if(first<0 || last<first || last>255 || declared.Elements.Count!=last-first+1) Fail("Font","Tabla de anchos inválida.");
   for(var i=0;i<widths.Length;i++) widths[i]=i>=first && i-first<declared.Elements.Count?declared.Elements.GetReal(i-first):0;
  }
  return (family,widths);
 }
}
