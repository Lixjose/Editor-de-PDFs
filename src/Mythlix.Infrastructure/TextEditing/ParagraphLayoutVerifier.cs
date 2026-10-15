using Mythlix.Core.TextEditing;
namespace Mythlix.Infrastructure.TextEditing;

internal static class ParagraphLayoutVerifier {
 internal static void Verify(ParagraphCompositionSnapshot plan,IEnumerable<NativeTextRun> nativeRuns,IEnumerable<NativeTextAdjustment> adjustments) {
  var expected=plan.Lines.SelectMany(l=>l.Segments).ToArray();
  var a=new List<(char Text,PdfRunStyle Style,double X,double Y,double Advance)>();
  foreach(var s in expected) {
   var x=s.StartX;
   for(var i=0;i<s.Text.Length;i++) {a.Add((s.Text[i],s.Owner.Style,x,s.BaselineY,s.AdvancesPt[i]-s.ExtraSpacingPt[i]));x+=s.AdvancesPt[i];}
  }
  var b=new List<(char Text,PdfRunStyle Style,double X,double Y,double Advance)>();
  foreach(var r in nativeRuns) {
   var x=r.BaselineX;
   for(var i=0;i<r.Run.Text.Length;i++) {b.Add((r.Run.Text[i],r.Run.Style,x,r.BaselineY,r.CharacterAdvancesPt[i]));x+=r.CharacterAdvancesPt[i];}
  }
  if(a.Count!=b.Count) throw new InvalidOperationException("Cantidad de caracteres emitidos incorrecta.");
  for(var i=0;i<a.Count;i++)
   if(a[i].Text!=b[i].Text || a[i].Style!=b[i].Style || Math.Abs(a[i].X-b[i].X)>1e-7 || Math.Abs(a[i].Y-b[i].Y)>1e-7 || Math.Abs(a[i].Advance-b[i].Advance)>1e-7)
    throw new InvalidOperationException("El contenido nativo no conserva los caracteres, estilos y posiciones del plan.");
  var extras=expected.SelectMany(s=>s.ExtraSpacingPt).Where(e=>e!=0).ToArray();var actual=adjustments.ToArray();
  if(extras.Length!=actual.Length || extras.Where((e,i)=>Math.Abs(e-actual[i].HorizontalAdvancePt)>1e-7).Any())
   throw new InvalidOperationException("El contenido nativo no conserva los ajustes de justificación.");
 }
}
