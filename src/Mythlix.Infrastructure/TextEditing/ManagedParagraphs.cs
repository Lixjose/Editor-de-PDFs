using System.Text.Json;
using System.Text;
using System.Security.Cryptography;
using Mythlix.Core;
using Mythlix.Core.TextEditing;
using PdfSharp.Pdf;
namespace Mythlix.Infrastructure.TextEditing;

/// <summary>Persistent layout evidence, never trusted without checking native bytes, fonts and glyph positions.
/// A checksum is an integrity check, not a signature or a proof of authorship.</summary>
internal static class ManagedParagraphs {
 internal const string Key="/MythlixParagraphsV1";
 internal sealed record SavedOptions(double X,double Y,double Width,double Height,double Baseline,double Leading,TextAlignment Alignment) {
  internal ParagraphCompositionOptions Restore()=>new(new(X,Y,Width,Height),Baseline,Leading,Alignment);
 }
 internal sealed record SavedStyle(string Resource,string Family,string Encoding,double Size,double Tc,double Tw,double Scale,double Rise,
  double Red,double Green,double Blue,bool Bold,bool Italic) {
  internal PdfRunStyle Restore(TextDocumentContext context,string scope)=>new(new(context,scope,Resource),Family,Encoding,Size,
   new(Red,Green,Blue),Bold,Italic,Tc,Tw,Scale,Rise);
 }
 internal sealed record Entry(int Version,string BlockHash,string BlockId,string Text,SavedOptions Options,SavedStyle EmptyStyle,Dictionary<string,string> ResourceHashes,string EvidenceHash="");
 static string Digest(Entry entry)=>Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(entry with {EvidenceHash=""}))));
 static Entry[] Read(PdfDictionary stream) {
  if(!stream.Elements.ContainsKey(Key))return [];
  try {
   var json=stream.Elements.GetString(Key);
   if(json.Length>4*1024*1024)throw new InvalidOperationException("Metadatos de párrafo demasiado grandes.");
   var entries=JsonSerializer.Deserialize<Entry[]>(json)??throw new JsonException();
   if(entries.Length>256 || entries.Any(e=>e is null || e.Version!=1 || e.BlockHash is null || e.BlockHash.Length!=64 ||
    e.BlockId is null || e.Text is null || e.Text.Length>500000 || e.Options is null || e.EmptyStyle is null || e.ResourceHashes is null || e.ResourceHashes.Count>256 || e.EvidenceHash is null || e.EvidenceHash!=Digest(e)))throw new JsonException();
   return entries;
  } catch(Exception ex) when(ex is JsonException or InvalidCastException or FormatException) {
   throw new InvalidOperationException("Los metadatos del párrafo no tienen un formato compatible.",ex);
  }
 }
 internal static ParagraphDraftInspection Inspect(PdfPage page,PageGeometry geometry,NativeRunAnalysis analysis,string blockId) {
  try {
   if(!analysis.IsSupported)throw new InvalidOperationException("La página no admite análisis nativo de fragmentos.");
   var runs=analysis.Runs.Where(r=>r.Run.Sources[0].Source.BlockId==blockId).ToArray();
   if(runs.Length==0)throw new InvalidOperationException("El bloque no contiene fragmentos nativos.");
   var source=runs[0].Run.Sources[0].Source;
   var stream=page.Contents.Elements.GetDictionary(source.StreamIndex)!;
   var all=Read(stream);
   var matches=all.Where(e=>e.BlockHash==source.BlockFingerprint).ToArray();
   if(matches.Length==0 && all.Any(e=>e.BlockId==blockId))
    throw new InvalidOperationException("La huella del párrafo guardado ya no coincide con sus bytes nativos.");
   if(matches.Length>1)throw new InvalidOperationException("Hay varias descripciones para el mismo bloque; no se agrupará automáticamente.");
   var buffer=ParagraphRunBuffer.FromRuns(runs.Select(r=>r.Run));
   if(matches.Length==0) {
    if(runs.Any(r=>Math.Abs(r.BaselineY-runs[0].BaselineY)>0.01))
     throw new InvalidOperationException("El bloque tiene varias líneas sin una composición persistida y verificada.");
    return new(buffer,NativeWinAnsiEncodingResolver.Create(page,analysis),null,false,[]);
   }
   var entry=matches[0];
   if(entry.BlockId!=blockId)throw new InvalidOperationException("La identidad del párrafo guardado cambió.");
   var (_,scope)=NativeRunExtractor.Resources(page);
   var emptyStyle=entry.EmptyStyle.Restore(analysis.Context,scope);
   var metrics=NativeWinAnsiEncodingResolver.Create(page,analysis,[emptyStyle.Font]);
   if(entry.Text.Replace("\r","").Replace("\n","")!=buffer.Text)
    throw new InvalidOperationException("El texto lógico guardado no coincide con los bytes nativos.");
   // Restore only explicitly recorded hard breaks; source spans still address real PDF strings.
   for(var i=0;i<entry.Text.Length;) {
    if(entry.Text[i] is not ('\r' or '\n')) {i++;continue;}
    var length=entry.Text[i]=='\r' && i+1<entry.Text.Length && entry.Text[i+1]=='\n'?2:1;
    var inserted=buffer.InsertText(i,entry.Text.Substring(i,length),metrics);
    if(!inserted.Applied || !inserted.IsEncodingValid)throw new InvalidOperationException("No se pudieron reconstruir los saltos duros.");
    buffer=inserted.Buffer;i+=length;
   }
   buffer=buffer.WithEmptyStyle(emptyStyle);
   var options=entry.Options.Restore();
   if(options.Bounds.X+options.Bounds.Width>geometry.Width || options.Bounds.Y+options.Bounds.Height>geometry.Height)
    throw new InvalidOperationException("El rectángulo guardado excede la página.");
   var plan=ParagraphComposer.Compose(buffer,options,metrics);
   if(!plan.IsValid || entry.ResourceHashes.Count!=plan.FontResourceFingerprints.Count ||
    plan.FontResourceFingerprints.Any(p=>!entry.ResourceHashes.TryGetValue(p.Key.ResourceName,out var hash) || p.Value!=hash))
    throw new InvalidOperationException("Las fuentes o la composición guardada ya no coinciden con la página.");
   ParagraphLayoutVerifier.Verify(plan,runs,analysis.Adjustments.Where(a=>a.StreamIndex==source.StreamIndex && a.BlockId==blockId));
   return new(buffer,metrics,options,true,[]);
  } catch(Exception ex) when(ex is InvalidOperationException or ArgumentException or FormatException or InvalidCastException) {
   return new(null,null,null,false,[new("UnverifiedParagraph",new(0,0),ex.Message)]);
  }
 }
 internal static void Record(PdfDictionary stream,string replacedHash,ParagraphRunBuffer draft,ParagraphCompositionSnapshot plan,NativeTextRun[] emitted,NativeRunAnalysis result,int streamIndex) {
  var existing=Read(stream).Where(e=>e.BlockHash!=replacedHash).ToList();
  if(emitted.Length>0) {
   var hashes=emitted.Select(r=>r.Run.Sources[0].Source.BlockFingerprint).Distinct().ToArray();
   if(hashes.Length!=1)throw new InvalidOperationException("La composición no quedó en un único objeto de texto.");
   existing.RemoveAll(e=>e.BlockHash==hashes[0]);
   var o=plan.Options;var s=draft.EmptyStyle;
   existing.Add(new(1,hashes[0],emitted[0].Run.Sources[0].Source.BlockId,draft.Text,new(o.Bounds.X,o.Bounds.Y,o.Bounds.Width,o.Bounds.Height,o.FirstBaselineY,o.LineHeightPt,o.Alignment),
    new(s.Font.ResourceName,s.FamilyName,s.EncodingName,s.FontSizePt,s.CharacterSpacingPt,s.WordSpacingPt,s.HorizontalScale,s.BaselineRisePt,
     s.FillColor.Red,s.FillColor.Green,s.FillColor.Blue,s.Bold,s.Italic),plan.FontResourceFingerprints.ToDictionary(p=>p.Key.ResourceName,p=>p.Value)));
  }
  for(var i=0;i<existing.Count;i++) {
   var matches=result.Runs.Where(r=>r.Run.Sources[0].Source.StreamIndex==streamIndex &&
    r.Run.Sources[0].Source.BlockFingerprint==existing[i].BlockHash).Select(r=>r.Run.Sources[0].Source.BlockId).Distinct().ToArray();
   if(matches.Length!=1)throw new InvalidOperationException("Un párrafo guardado perdió su identidad al escribir.");
   existing[i]=existing[i] with {BlockId=matches[0],EvidenceHash=""};
   existing[i]=existing[i] with {EvidenceHash=Digest(existing[i])};
  }
  var json=JsonSerializer.Serialize(existing);
  if(existing.Count>256 || json.Length>4*1024*1024 || draft.Text.Length>500000)throw new InvalidOperationException("Límite de metadatos de párrafo.");
  stream.Elements.SetString(Key,json);
 }
}
