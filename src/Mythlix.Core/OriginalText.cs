namespace Mythlix.Core;

public enum TextAlignment { Left, Center, Right, Justify }

public sealed record OriginalTextBlock(
    string Id,
    string Text,
    string FontName,
    double FontSize,
    ObjectBounds Bounds,
    string Fingerprint,
    bool CanChangeFontSize = true,
    string Color = "#000000",
    bool Bold = false,
    bool Italic = false,
    TextAlignment Alignment = TextAlignment.Left,
    int LineCount = 1) {
    public string Label => Text.Length == 0 ? "Texto vacío" : Text[..Math.Min(Text.Length, 38)];
}

public sealed record TextEditOptions(
    string Text,
    double FontSize,
    string Color = "#000000",
    bool Bold = false,
    bool Italic = false,
    TextAlignment Alignment = TextAlignment.Left,
    string? FontFamily = null,
    double? MaxWidth = null,
    bool Strict = false
);

public sealed record TextAnalysis(IReadOnlyList<OriginalTextBlock> Blocks, int UnsupportedBlocks, string Explanation);
