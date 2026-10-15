using System.Collections.Immutable;
using Mythlix.Core.TextEditing;
using Mythlix.Infrastructure.TextEditing;
using PdfSharp.Pdf.IO;

namespace Mythlix.Infrastructure;

public sealed partial class PdfDocumentSession {
 /// <summary>Requires serialized access and the owner's live revision. No UI/history changes occur here.
 /// All rejection checks run before serialization; candidate writes never touch live page streams.</summary>
 public ParagraphCommitReceipt CommitParagraphComposition(ParagraphRunBuffer draft,ParagraphCompositionSnapshot plan,
  long currentRevision,bool allowExplicitReflow) {
  ArgumentNullException.ThrowIfNull(draft);ArgumentNullException.ThrowIfNull(plan);
  if(currentRevision==long.MaxValue) throw new InvalidOperationException("La revisión no admite incremento.");
  var page=plan.Context.PageNumber;ValidatePage(page);
  var geometry=GetPageGeometry(page);
  var context=new TextDocumentContext(textDocumentId,page,currentRevision);
  var target=ParagraphCompositionWriter.Validate(document.Pages[page-1],geometry,context,draft,plan,allowExplicitReflow);
  var before=Snapshot();
  var candidate=PdfReader.Open(new MemoryStream(before),PdfDocumentOpenMode.Modify);
  try {
   var written=ParagraphCompositionWriter.Write(candidate,candidate.Pages[page-1],geometry,target,draft,plan);
   var result=NativeRunExtractor.Analyze(candidate.Pages[page-1],geometry,context);
   if(!result.IsSupported) throw new InvalidOperationException("La escritura candidata no supera el análisis nativo.");
   // Verify every emitted segment's state, position and effective character advances after parsing PDF operators.
   var expected=plan.Lines.SelectMany(l=>l.Segments).ToArray();
   var emitted=result.Runs.Where(r=>r.Run.Sources[0].Source.StreamIndex==target.Stream &&
    r.ShowOperatorRange.Start>=written.Start && r.ShowOperatorRange.End<=written.End).ToArray();
   var expectedCharacters=new List<(char Text,PdfRunStyle Style,double X,double Y,double Advance)>();
   foreach(var segment in expected) {
    var x=segment.StartX;
    for(var i=0;i<segment.Text.Length;i++) {
     expectedCharacters.Add((segment.Text[i],segment.Owner.Style,x,segment.BaselineY,segment.AdvancesPt[i]-segment.ExtraSpacingPt[i]));
     x+=segment.AdvancesPt[i];
    }
   }
   var actualCharacters=new List<(char Text,PdfRunStyle Style,double X,double Y,double Advance)>();
   foreach(var run in emitted) {
    var x=run.BaselineX;
    for(var i=0;i<run.Run.Text.Length;i++) {
     actualCharacters.Add((run.Run.Text[i],run.Run.Style,x,run.BaselineY,run.CharacterAdvancesPt[i]));
     x+=run.CharacterAdvancesPt[i];
    }
   }
   if(expectedCharacters.Count!=actualCharacters.Count) throw new InvalidOperationException("Cantidad de caracteres emitidos incorrecta.");
   for(var i=0;i<expectedCharacters.Count;i++) {
    var a=expectedCharacters[i];var b=actualCharacters[i];
    if(a.Text!=b.Text || a.Style!=b.Style || Math.Abs(a.X-b.X)>1e-7 || Math.Abs(a.Y-b.Y)>1e-7 || Math.Abs(a.Advance-b.Advance)>1e-7)
     throw new InvalidOperationException("La escritura no conserva los caracteres, estilos y posiciones del plan aprobado.");
   }
   var expectedExtras=expected.SelectMany(s=>s.ExtraSpacingPt).Where(e=>e!=0).ToArray();
   var actualExtras=result.Adjustments.Where(a=>a.StreamIndex==target.Stream && a.ByteRange.Start>=written.Start && a.ByteRange.End<=written.End).ToArray();
   if(expectedExtras.Length!=actualExtras.Length || expectedExtras.Where((e,i)=>Math.Abs(e-actualExtras[i].HorizontalAdvancePt)>1e-7).Any())
    throw new InvalidOperationException("La escritura no conserva los ajustes de justificación.");
   var originalId=draft.OriginalSources[0].BlockId;
   var untouchedBefore=target.Analysis.Runs.Where(r=>r.Run.Sources[0].Source.StreamIndex!=target.Stream || r.Run.Sources[0].Source.BlockId!=originalId).ToArray();
   var untouchedAfter=result.Runs.Except(emitted).ToArray();
   if(untouchedBefore.Length!=untouchedAfter.Length) throw new InvalidOperationException("Cambió contenido ajeno al objeto seleccionado.");
   for(var i=0;i<untouchedBefore.Length;i++) {
    var a=untouchedBefore[i];var b=untouchedAfter[i];
    if(a.Run.Text!=b.Run.Text || a.Run.Style!=b.Run.Style || Math.Abs(a.BaselineX-b.BaselineX)>1e-7 ||
     Math.Abs(a.BaselineY-b.BaselineY)>1e-7 || !a.EncodedBytes.SequenceEqual(b.EncodedBytes) || !a.CharacterAdvancesPt.SequenceEqual(b.CharacterAdvancesPt))
     throw new InvalidOperationException("La escritura alteró texto ajeno al plan.");
   }
   var receipt=new ParagraphCommitReceipt(before.ToImmutableArray(),currentRevision,checked(currentRevision+1));
   var previous=document;document=candidate;candidate=null!;previous.Dispose();
   return receipt;
  } finally {candidate?.Dispose();}
 }
}
