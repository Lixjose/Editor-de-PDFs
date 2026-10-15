using System.Collections.Immutable;
using System.Security.Cryptography;
using Mythlix.Core;
using Mythlix.Core.TextEditing;
using PdfSharp.Pdf;

namespace Mythlix.Infrastructure.TextEditing;

/// <summary>Indexes text objects without converting them into writable OriginalTextBlocks.</summary>
internal static class TextRunInspectionAdapter {
 internal static TextRunInspectionReport Inspect(PdfPage page,PageGeometry geometry,TextDocumentContext context) {
  var native=NativeRunExtractor.Analyze(page,geometry,context);
  var legacy=OriginalTextEditor.Analyze(page,geometry);
  var runsByBlock=native.Runs.ToLookup(r=>r.Run.Sources[0].Source.BlockId);
  var adjustmentsByBlock=native.Adjustments.ToLookup(a=>a.BlockId);
  var legacyById=legacy.Blocks.ToDictionary(b=>b.Id);
  var blocks=ImmutableArray.CreateBuilder<TextBlockInspection>();
  var pageErrors=native.Diagnostics.ToBuilder();
  long total=0;
  for(var streamIndex=0;streamIndex<page.Contents.Elements.Count;streamIndex++) {
   try {
    var dictionary=page.Contents.Elements.GetDictionary(streamIndex);
    if(dictionary is null) throw new InvalidOperationException("Stream inválido.");
    // Owned layers have their own editor and identities; they must not appear as originals.
    if(dictionary.Elements.GetBoolean("/MythlixGraphicsV1") || dictionary.Elements.GetBoolean("/MythlixOcrV1")) continue;
    var bytes=dictionary.Stream?.UnfilteredValue ?? [];
    total+=bytes.Length;
    if(total>16*1024*1024) throw new InvalidOperationException("Límite de contenido.");
    var tokens=ContentTokenizer.Read(bytes);
    var streamHash=Convert.ToHexString(SHA256.HashData(bytes));
    Token? open=null;var ordinal=0;
    foreach(var token in tokens.Where(t=>t.Bytes is null)) {
     if(token.Value=="BT") {
      if(open is not null) throw new InvalidOperationException("BT anidado.");
      open=token;
     } else if(token.Value=="ET") {
      if(open is null) throw new InvalidOperationException("ET sin BT.");
      var range=new TextRange(open.Start,token.Start+token.Length-open.Start);
      var id=$"{streamIndex}:{ordinal++}";
      var errors=ImmutableArray.CreateBuilder<NativeRunDiagnostic>();
      if(!native.IsSupported) {
       errors.AddRange(native.Diagnostics.Where(d=>d.StreamIndex==streamIndex && d.ByteOffset>=range.Start && d.ByteOffset<range.End));
       if(errors.Count==0) errors.Add(new("PageAnalysisRejected",streamIndex,range.Start,
        "La página tiene estado no compatible; no se presentan fragmentos parciales como análisis completo."));
      }
      var runs=runsByBlock[id].ToImmutableArray();
      var adjustments=adjustmentsByBlock[id].ToImmutableArray();
      var compatible=legacyById.TryGetValue(id,out var oldBlock) && oldBlock.Fingerprint==streamHash;
      blocks.Add(new(id,streamIndex,range,streamHash,native.IsSupported,compatible,runs,adjustments,errors.ToImmutable()));
      open=null;
     }
    }
    if(open is not null) throw new InvalidOperationException("BT sin ET.");
   } catch(Exception ex) when(ex is InvalidOperationException or ArgumentException or FormatException or InvalidCastException) {
    pageErrors.Add(new("InventoryUnavailable",streamIndex,null,"No se pudo inventariar con seguridad el contenido de este stream."));
   }
  }
  return new(context,blocks.ToImmutable(),pageErrors.ToImmutable());
 }
}
