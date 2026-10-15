using System.Collections.Immutable;
using Mythlix.Core.TextEditing;
using Mythlix.Infrastructure.TextEditing;
using PdfSharp.Pdf.IO;

namespace Mythlix.Infrastructure;

public sealed partial class PdfDocumentSession {
 /// <summary>Recover native one-line drafts or persisted compositions after full read-only verification.</summary>
 public ParagraphDraftInspection InspectParagraphDraft(int page,long revision,string blockId) {
  ValidatePage(page);ArgumentException.ThrowIfNullOrWhiteSpace(blockId);
  var analysis=AnalyzeTextRuns(page,revision);
  return ManagedParagraphs.Inspect(document.Pages[page-1],GetPageGeometry(page),analysis,blockId);
 }
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
   var emitted=result.Runs.Where(r=>r.Run.Sources[0].Source.StreamIndex==target.Stream &&
    r.ShowOperatorRange.Start>=written.Start && r.ShowOperatorRange.End<=written.End).ToArray();
   ParagraphLayoutVerifier.Verify(plan,emitted,result.Adjustments.Where(a=>a.StreamIndex==target.Stream && a.ByteRange.Start>=written.Start && a.ByteRange.End<=written.End));
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
   ManagedParagraphs.Record(candidate.Pages[page-1].Contents.Elements.GetDictionary(target.Stream)!,draft.OriginalSources[0].BlockFingerprint,draft,plan,emitted,result,target.Stream);
   if(emitted.Length>0) {
    var recovered=ManagedParagraphs.Inspect(candidate.Pages[page-1],geometry,result,emitted[0].Run.Sources[0].Source.BlockId);
    if(!recovered.IsSupported || !recovered.IsManaged || recovered.Draft!.Text!=draft.Text || recovered.SavedOptions!=plan.Options)
     throw new InvalidOperationException("La composición candidata no se puede recuperar sin pérdida: "+string.Join(" ",recovered.Diagnostics.Select(d=>d.Message)));
   }
   var receipt=new ParagraphCommitReceipt(before.ToImmutableArray(),currentRevision,checked(currentRevision+1));
   var previous=document;document=candidate;candidate=null!;previous.Dispose();
   return receipt;
  } finally {candidate?.Dispose();}
 }
}
