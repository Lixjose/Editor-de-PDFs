using System.Globalization;
using System.Text;
using Mythlix.Core;
using Mythlix.Core.TextEditing;
using PdfSharp.Pdf;

namespace Mythlix.Infrastructure.TextEditing;

internal static class ParagraphCompositionWriter {
 internal sealed record Target(int Stream,TextRange Range,NativeRunAnalysis Analysis);
 static void Require(bool condition,string message) {if(!condition) throw new InvalidOperationException(message);}
 internal static Target Validate(PdfPage page,PageGeometry geometry,TextDocumentContext context,
  ParagraphRunBuffer draft,ParagraphCompositionSnapshot plan,bool allowExplicitReflow) {
  Require(allowExplicitReflow,"La confirmación requiere aceptar explícitamente el reflujo.");
  Require(draft.Context==context && plan.Context==context,"Documento, página o revisión obsoletos.");
  Require(plan.IsValid,"La composición contiene diagnósticos.");
  var analysis=NativeRunExtractor.Analyze(page,geometry,context);
  Require(analysis.IsSupported,"La página ya no admite escritura nativa de fragmentos.");
  var sources=draft.OriginalSources;
  Require(!sources.IsEmpty && sources.All(s=>s.Context==context),"Inventario original ausente o ajeno.");
  var first=sources[0];
  Require(sources.All(s=>s.BlockId==first.BlockId && s.StreamIndex==first.StreamIndex),"Solo se confirma un objeto de texto completo.");
  var original=analysis.Runs.Where(r=>r.Run.Sources.Any(s=>s.Source.BlockId==first.BlockId && s.Source.StreamIndex==first.StreamIndex)).ToArray();
  Require(original.Length>0,"El objeto original ya no existe.");
  var liveSources=original.SelectMany(r=>r.Run.Sources.Select(s=>s.Source)).Distinct().ToArray();
  Require(sources.Length==liveSources.Length && sources.All(liveSources.Contains),"El inventario está incompleto o cambió su contenido/huella.");
  Require(original.All(r=>Math.Abs(r.BaselineY-original[0].BaselineY)<0.01),"El primer escritor requiere un objeto original de una sola línea.");
  var metrics=NativeWinAnsiEncodingResolver.Create(page,analysis);
  var canonical=ParagraphComposer.Compose(draft,plan.Options,metrics);
  Require(Equivalent(plan,canonical),"El plan, las métricas o los recursos cambiaron; recomponga antes de confirmar.");
  var bounds=plan.Options.Bounds;
  Require(geometry.Rotation==0 && geometry.CanEdit && bounds.X+bounds.Width<=geometry.Width && bounds.Y+bounds.Height<=geometry.Height,
   "El rectángulo debe estar dentro de una página sin rotación y con geometría compatible.");
  // Conservative clearance: metric bounds are not exact ink bounds. Reserve a full em around text.
  var pad=draft.Runs.Select(r=>r.Style.FontSizePt).Append(draft.EmptyStyle.FontSizePt).Max();
  var corridor=new ObjectBounds(bounds.X-pad,bounds.Y-pad,bounds.Width+2*pad,bounds.Height+2*pad);
  foreach(var other in analysis.Runs.Except(original)) {
   var style=other.Run.Style;var m=metrics.Measure(style,other.Run.Text);
   var otherBounds=new ObjectBounds(other.BaselineX-style.FontSizePt,other.BaselineY-style.BaselineRisePt-m.AscentPt-style.FontSizePt,
    other.AdvancePt+2*style.FontSizePt,m.AscentPt+m.DescentPt+2*style.FontSizePt);
   Require(!Intersects(corridor,otherBounds),"El área de recomposición invade otro objeto de texto.");
  }
  Require(!FixedGraphicsGuard.Blocks(page,geometry,corridor),"Hay gráficos o un fondo en el área de recomposición sin aislamiento verificado.");
  var bytes=page.Contents.Elements.GetDictionary(first.StreamIndex)!.Stream!.UnfilteredValue;
  Token? start=null;var ordinal=0;TextRange? range=null;
  foreach(var token in ContentTokenizer.Read(bytes).Where(t=>t.Bytes is null)) {
   if(token.Value=="BT") start=token;
   if(token.Value=="ET" && start is not null) {
    if($"{first.StreamIndex}:{ordinal}"==first.BlockId) range=new(start.Start,token.Start+token.Length-start.Start);
    ordinal++;start=null;
   }
  }
  Require(range is not null,"No se encontró el rango del objeto original.");
  return new(first.StreamIndex,range!,analysis);
 }
 static bool Intersects(ObjectBounds a,ObjectBounds b)=>a.X<b.X+b.Width && a.X+a.Width>b.X && a.Y<b.Y+b.Height && a.Y+a.Height>b.Y;
 static bool Equivalent(ParagraphCompositionSnapshot a,ParagraphCompositionSnapshot b) {
  if(!b.IsValid || a.Context!=b.Context || a.Text!=b.Text || a.ComposedBounds!=b.ComposedBounds ||
   !a.OriginalSources.SequenceEqual(b.OriginalSources) || a.Lines.Length!=b.Lines.Length ||
   a.FontResourceFingerprints.Count!=b.FontResourceFingerprints.Count ||
   a.FontResourceFingerprints.Any(p=>!b.FontResourceFingerprints.TryGetValue(p.Key,out var v) || v!=p.Value)) return false;
  for(var i=0;i<a.Lines.Length;i++) {
   var x=a.Lines[i];var y=b.Lines[i];
   if((x with {Segments=default})!=(y with {Segments=default}) || x.Segments.Length!=y.Segments.Length) return false;
   for(var j=0;j<x.Segments.Length;j++) {
    var s=x.Segments[j];var t=y.Segments[j];
    if((s with {EncodedBytes=default,AdvancesPt=default,ExtraSpacingPt=default})!=(t with {EncodedBytes=default,AdvancesPt=default,ExtraSpacingPt=default}) ||
     !s.EncodedBytes.SequenceEqual(t.EncodedBytes) || !s.AdvancesPt.SequenceEqual(t.AdvancesPt) || !s.ExtraSpacingPt.SequenceEqual(t.ExtraSpacingPt)) return false;
   }
  }
  return true;
 }
 static string N(double value) {
  Require(double.IsFinite(value),"Operando PDF no finito.");
  // PDF numbers have no exponent syntax. Retain sub-point precision without scientific notation.
  return value.ToString("0.################",CultureInfo.InvariantCulture);
 }
 internal static TextRange Write(PdfDocument document,PdfPage page,PageGeometry geometry,Target target,
  ParagraphRunBuffer draft,ParagraphCompositionSnapshot plan) {
  // Detach the Contents array as well as the stream; either can be shared across pages.
  var array=page.Elements.GetArray("/Contents");
  var detached=new PdfArray(document);
  if(array is not null) foreach(var item in array.Elements) detached.Elements.Add(item);
  else detached.Elements.Add(page.Elements["/Contents"] ?? throw new InvalidOperationException("Contenido ausente."));
  page.Elements["/Contents"]=detached;
  var old=page.Contents.Elements.GetDictionary(target.Stream)!;
  var bytes=old.Stream!.UnfilteredValue;
  // Keep original text state operators and final graphics state. Empty only the original strings.
  foreach(var range in draft.OriginalSources.Select(s=>s.ByteRange).Distinct().OrderByDescending(r=>r.Start))
   bytes=bytes[..range.Start].Concat("()"u8.ToArray()).Concat(bytes[range.End..]).ToArray();
  // Offsets changed after emptying strings: find this object's ET again.
  var ordinal=0;var insertion=-1;
  foreach(var token in ContentTokenizer.Read(bytes).Where(t=>t.Bytes is null && t.Value=="ET")) {
   if($"{target.Stream}:{ordinal++}"==draft.OriginalSources[0].BlockId) {insertion=token.Start+token.Length;break;}
  }
  Require(insertion>=0,"Objeto original ausente durante escritura.");
  var output=new StringBuilder("\nq BT\n");
  foreach(var segment in plan.Lines.SelectMany(l=>l.Segments)) {
   var s=segment.Owner.Style;var c=s.FillColor;
   output.Append($"{s.Font.ResourceName} {N(s.FontSizePt)} Tf {N(s.CharacterSpacingPt)} Tc {N(s.WordSpacingPt)} Tw {N(s.HorizontalScale*100)} Tz {N(s.BaselineRisePt)} Ts 0 Tr {N(c.Red)} {N(c.Green)} {N(c.Blue)} rg 1 0 0 1 {N(segment.StartX)} {N(geometry.Height-segment.BaselineY)} Tm [");
   output.Append('<');
   for(var i=0;i<segment.EncodedBytes.Length;i++) {
    output.Append(segment.EncodedBytes[i].ToString("X2",CultureInfo.InvariantCulture));
    var extra=segment.ExtraSpacingPt[i];
    if(extra!=0) output.Append("> ").Append(N(-extra*1000/s.FontSizePt/s.HorizontalScale)).Append(" <");
   }
   output.Append('>');
   output.Append("] TJ\n");
  }
  output.Append("ET Q\n");
  var added=Encoding.ASCII.GetBytes(output.ToString());
  var updated=new PdfDictionary(document);
  foreach(var key in old.Elements.Keys) if(key is not ("/Filter" or "/DecodeParms" or "/Length" or "/DL")) updated.Elements[key]=old.Elements[key];
  updated.CreateStream(bytes[..insertion].Concat(added).Concat(bytes[insertion..]).ToArray());
  document.Internals.AddObject(updated);page.Contents.Elements[target.Stream]=updated.Reference!;
  return new(insertion,added.Length);
 }
}
