using System.Collections.Immutable;

namespace Mythlix.Core.TextEditing;

/// <summary>The owner must serialize session access, pass its live revision, and adopt NextRevision
/// together with the undo entry only after success. Preview substitutes never enter this contract.</summary>
public interface IParagraphCompositionWriter {
 ParagraphCommitReceipt CommitParagraphComposition(ParagraphRunBuffer draft,ParagraphCompositionSnapshot plan,
  long currentRevision,bool allowExplicitReflow);
}
public sealed record ParagraphCommitReceipt(ImmutableArray<byte> UndoSnapshot,long PreviousRevision,long NextRevision);
