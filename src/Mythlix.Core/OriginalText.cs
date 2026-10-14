namespace Mythlix.Core;
public sealed record OriginalTextBlock(string Id, string Text, string FontName, double FontSize, ObjectBounds Bounds, string Fingerprint, bool CanChangeFontSize = true) {
 public string Label => Text.Length == 0 ? "Texto vacío" : Text[..Math.Min(Text.Length,38)];
}
public sealed record TextAnalysis(IReadOnlyList<OriginalTextBlock> Blocks, int UnsupportedBlocks, string Explanation);
