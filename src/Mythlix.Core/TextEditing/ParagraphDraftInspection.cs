using System.Collections.Immutable;
namespace Mythlix.Core.TextEditing;

/// <summary>Read-only recovery of one complete, verified paragraph. No grouping by visual proximity.</summary>
public sealed record ParagraphDraftInspection(ParagraphRunBuffer? Draft,IRunMetricsProvider? Metrics,
 ParagraphCompositionOptions? SavedOptions,bool IsManaged,ImmutableArray<RunEditDiagnostic> Diagnostics) {
 public bool IsSupported=>Draft is not null && Metrics is not null && Diagnostics.IsEmpty;
}
public interface IParagraphDraftInspector {
 ParagraphDraftInspection InspectParagraphDraft(int page,long revision,string blockId);
}
