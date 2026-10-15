using System.Collections.Immutable;

namespace Mythlix.Core.TextEditing;

/// <summary>Optional inspection capability. This interface exposes no text-writing operation.</summary>
public interface ITextRunInspector {
 TextRunInspectionReport InspectTextRuns(int page, long revision);
}

public sealed record TextBlockInspection(string BlockId, int StreamIndex, TextRange ContentRange,
 string StreamFingerprint, bool NativeAnalysisSupported, bool LegacyEditable,
 ImmutableArray<NativeTextRun> Runs, ImmutableArray<NativeTextAdjustment> Adjustments,
 ImmutableArray<NativeRunDiagnostic> Diagnostics) {
 public bool IsReadOnly => true;
 public bool HasMixedStyles => Runs.Select(r=>r.Run.Style).Distinct().Skip(1).Any();
 public string Text => string.Concat(Runs.Select(r=>r.Run.Text));
 public string Label => $"Bloque {BlockId} · {(NativeAnalysisSupported ? $"{Runs.Length} fragmentos" : "análisis no compatible")}";
 public string Summary => NativeAnalysisSupported
  ? $"{Runs.Length} fragmentos · {(HasMixedStyles ? "estilos mixtos" : "estilo uniforme")} · solo lectura"
  : "El análisis nativo de la página no está disponible; se conserva el flujo de edición anterior.";
}

public enum InspectionMatchKind { VerifiedNative, ExtractedFallback, StaleSelection, NotFound }
public sealed record TextInspectionMatch(InspectionMatchKind Kind, TextBlockInspection? Block);

public sealed record TextRunInspectionReport(TextDocumentContext Context,
 ImmutableArray<TextBlockInspection> Blocks, ImmutableArray<NativeRunDiagnostic> PageDiagnostics) {
 public bool IsReadOnly => true;
 public bool NativeAnalysisSupported => PageDiagnostics.IsEmpty;
 /// <summary>No geometric matching: fallback extraction does not identify original stream ranges.</summary>
 public TextInspectionMatch MatchOriginal(OriginalTextBlock block, TextDocumentContext selectionContext) {
  ArgumentNullException.ThrowIfNull(block);ArgumentNullException.ThrowIfNull(selectionContext);
  if(selectionContext!=Context) return new(InspectionMatchKind.StaleSelection,null);
  if(block.Id.StartsWith("v:",StringComparison.Ordinal)) return new(InspectionMatchKind.ExtractedFallback,null);
  var found=Blocks.FirstOrDefault(b=>b.BlockId==block.Id);
  if(found is null) return new(InspectionMatchKind.NotFound,null);
  return found.StreamFingerprint==block.Fingerprint ? new(InspectionMatchKind.VerifiedNative,found)
   : new(InspectionMatchKind.StaleSelection,null);
 }
}
