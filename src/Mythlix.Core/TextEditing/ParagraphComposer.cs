using System.Collections.Immutable;

namespace Mythlix.Core.TextEditing;

/// <summary>Shared metric composition for the validated horizontal single-byte subset.
/// Preserves whitespace, styles and draft ranges; never writes a PDF or asks a UI control to wrap.</summary>
public static class ParagraphComposer {
 sealed record Atom(PdfTextRun Owner,int Local,int Global,char Character,byte Code,double Advance,double Ascent,double Descent);

 public static ParagraphCompositionSnapshot Compose(ParagraphRunBuffer buffer,ParagraphCompositionOptions options,IRunMetricsProvider provider) {
  ArgumentNullException.ThrowIfNull(buffer);ArgumentNullException.ThrowIfNull(options);ArgumentNullException.ThrowIfNull(provider);
  var errors=ImmutableArray.CreateBuilder<RunEditDiagnostic>();
  var atoms=new Atom?[buffer.Text.Length];
  var defaultMetrics=provider.Measure(buffer.EmptyStyle,"");
  errors.AddRange(defaultMetrics.Diagnostics);
  int global=0;
  foreach(var run in buffer.Runs) {
   // Even a run consisting only of hard breaks must have a valid resource identity.
   var fontCheck=provider.Measure(run.Style,"");
   foreach(var error in fontCheck.Diagnostics) errors.Add(error with {Range=new(global,run.Text.Length)});
   int start=0;
   for(var i=0;i<=run.Text.Length;i++) {
    if(i<run.Text.Length && run.Text[i] is not ('\r' or '\n')) continue;
    if(i>start) {
     var metrics=provider.Measure(run.Style,run.Text[start..i]);
     foreach(var error in metrics.Diagnostics) errors.Add(error with {Range=new(global+start+error.Range.Start,error.Range.Length)});
     if(metrics.IsSupported) {
      if(metrics.EncodedBytes.Length!=i-start || metrics.AdvancesPt.Length!=i-start ||
       metrics.AdvancesPt.Any(v=>!double.IsFinite(v)||v<=0) || !double.IsFinite(metrics.AscentPt) ||
       !double.IsFinite(metrics.DescentPt) || metrics.AscentPt<=0 || metrics.DescentPt<0)
       errors.Add(new("UnsupportedMetrics",new(global+start,i-start),"Se requiere un mapeo WinAnsi 1:1 y métricas finitas verificadas."));
      else for(var j=start;j<i;j++) atoms[global+j]=new(run,j,global+j,run.Text[j],metrics.EncodedBytes[j-start],
       metrics.AdvancesPt[j-start],metrics.AscentPt,metrics.DescentPt);
     }
    }
    start=i+1;
   }
   global+=run.Text.Length;
  }
  if(!defaultMetrics.IsSupported || errors.Count>0) return Snapshot([],null);
  if(!double.IsFinite(defaultMetrics.AscentPt) || !double.IsFinite(defaultMetrics.DescentPt) || defaultMetrics.AscentPt<=0 || defaultMetrics.DescentPt<0) {
   errors.Add(new("UnsupportedMetrics",new(0,0),"Métricas de línea vacía no válidas."));return Snapshot([],null);
  }
  var lines=ImmutableArray.CreateBuilder<ComposedTextLine>();
  var current=new List<Atom>();
  double width=0;
  int lineStart=0;
  for(var index=0;index<buffer.Text.Length;) {
   var ch=buffer.Text[index];
   if(ch is '\r' or '\n') {
    var length=ch=='\r' && index+1<buffer.Text.Length && buffer.Text[index+1]=='\n'?2:1;
    Flush(CompositionBreakKind.Hard,new(index,length));index+=length;lineStart=index;continue;
   }
   if(ch==' ') {
    var atom=atoms[index]!;
    if(current.Count>0 && width+atom.Advance>options.Bounds.Width) Flush(CompositionBreakKind.Soft,null);
    current.Add(atom);width+=atom.Advance;index++;continue;
   }
   // A word can span multiple styles. Never split it merely at a run boundary.
   int end=index;double wordWidth=0;
   while(end<buffer.Text.Length && buffer.Text[end] is not (' ' or '\r' or '\n')) {wordWidth+=atoms[end]!.Advance;end++;}
   if(current.Count>0 && width+wordWidth>options.Bounds.Width) Flush(CompositionBreakKind.Soft,null);
   for(var j=index;j<end;j++) current.Add(atoms[j]!);
   width+=wordWidth;index=end;
  }
  Flush(CompositionBreakKind.End,null);
  var composed=lines.ToImmutable();
  if(composed.IsEmpty || errors.Any(e=>e.Code=="NonFiniteGeometry")) return Snapshot([],null);
  var left=composed.Min(l=>l.AdvanceBounds.X);var top=composed.Min(l=>l.AdvanceBounds.Y);
  var right=composed.Max(l=>l.AdvanceBounds.X+l.AdvanceBounds.Width);var bottom=composed.Max(l=>l.AdvanceBounds.Y+l.AdvanceBounds.Height);
  return Snapshot(composed,new(left,top,right-left,bottom-top));

  ParagraphCompositionSnapshot Snapshot(ImmutableArray<ComposedTextLine> result,ObjectBounds? bounds) =>
   new(buffer.Context,buffer.Text,buffer.OriginalSources,options,result,bounds,errors.ToImmutable());

  void Flush(CompositionBreakKind kind,TextRange? hardBreak) {
   var range=new TextRange(lineStart,current.Count);
   double baseline=options.FirstBaselineY+lines.Count*options.LineHeightPt;
   double ascent=current.Count==0?defaultMetrics.AscentPt+buffer.EmptyStyle.BaselineRisePt:current.Max(a=>a.Ascent+a.Owner.Style.BaselineRisePt);
   double descent=current.Count==0?defaultMetrics.DescentPt-buffer.EmptyStyle.BaselineRisePt:current.Max(a=>a.Descent-a.Owner.Style.BaselineRisePt);
   ascent=Math.Max(0,ascent);descent=Math.Max(0,descent);
   if(!double.IsFinite(width) || !double.IsFinite(baseline) || !double.IsFinite(ascent) || !double.IsFinite(descent)) {
    errors.Add(new("NonFiniteGeometry",range,"La composición excede los límites numéricos."));
    current.Clear();width=0;return;
   }
   if(width>options.Bounds.Width+0.000001) errors.Add(new("HorizontalOverflow",range,"Una palabra o espacio excede el ancho disponible; no se divide ni comprime automáticamente."));
   double extra=0;
   int first=current.FindIndex(a=>a.Character!=' '),last=current.FindLastIndex(a=>a.Character!=' ');
   var gaps=current.Count(a=>a.Character==' ' && a.Global>(first>=0?current[first].Global:int.MaxValue) && a.Global<(last>=0?current[last].Global:int.MinValue));
   if(options.Alignment==TextAlignment.Justify && kind==CompositionBreakKind.Soft && gaps>0 && width<options.Bounds.Width)
    extra=(options.Bounds.Width-width)/gaps;
   double offset=options.Alignment switch {
    TextAlignment.Center=>Math.Max(0,(options.Bounds.Width-width)/2),
    TextAlignment.Right=>Math.Max(0,options.Bounds.Width-width),_=>0
   };
   var segments=ImmutableArray.CreateBuilder<ComposedRunSegment>();
   double x=options.Bounds.X+offset;
   int position=0;
   while(position<current.Count) {
    int end=position+1;
    while(end<current.Count && ReferenceEquals(current[end].Owner,current[position].Owner)) end++;
    var advances=ImmutableArray.CreateBuilder<double>();var extras=ImmutableArray.CreateBuilder<double>();var codes=ImmutableArray.CreateBuilder<byte>();
    for(var i=position;i<end;i++) {
     var addition=current[i].Character==' ' && i>first && i<last?extra:0;
     extras.Add(addition);advances.Add(current[i].Advance+addition);codes.Add(current[i].Code);
    }
    var segment=new ComposedRunSegment(current[position].Owner,new(current[position].Local,end-position),
     new(current[position].Global,end-position),x,baseline,codes.ToImmutable(),advances.ToImmutable(),extras.ToImmutable());
    segments.Add(segment);x+=segment.AdvancePt;position=end;
   }
   if(!double.IsFinite(x) || !double.IsFinite(ascent+descent) || !double.IsFinite(baseline+descent)) {
    errors.Add(new("NonFiniteGeometry",range,"La composición excede los límites numéricos."));current.Clear();width=0;return;
   }
   var bounds=new ObjectBounds(options.Bounds.X+offset,baseline-ascent,x-options.Bounds.X-offset,ascent+descent);
   if(bounds.Y<options.Bounds.Y-0.000001 || bounds.Y+bounds.Height>options.Bounds.Y+options.Bounds.Height+0.000001)
    errors.Add(new("VerticalOverflow",range,"El texto excede los límites verticales disponibles."));
   if(lines.Count>0 && bounds.Y<lines[^1].AdvanceBounds.Y+lines[^1].AdvanceBounds.Height-0.000001)
    errors.Add(new("LineOverlap",range,"El interlineado no separa las métricas verticales de las líneas."));
   lines.Add(new(range,hardBreak,kind,baseline,ascent,descent,bounds,segments.ToImmutable()));
   lineStart+=current.Count;current.Clear();width=0;
  }
 }
}
