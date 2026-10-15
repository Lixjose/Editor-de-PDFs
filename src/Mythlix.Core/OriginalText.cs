namespace Mythlix.Core;

public enum TextAlignment { Left, Center, Right, Justify }

/// <param name="Baseline">Baseline of the first line in PDF points from the page top (null when unknown).</param>
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
    int LineCount = 1,
    double? Baseline = null) {
    public string Label => Text.Length == 0 ? "Texto vacío" : Text[..Math.Min(Text.Length, 38)];
    /// <summary>Known baseline or an estimate from the box (ascent ≈ 78 % of the line box).</summary>
    public double EffectiveBaseline => Baseline ?? Bounds.Y + Bounds.Height * 0.78;
}

/// <param name="X">New left edge of the text in points from the page left (null keeps the original).</param>
/// <param name="Baseline">New baseline in points from the page top (null keeps the original).</param>
public sealed record TextEditOptions(
    string Text,
    double FontSize,
    string Color = "#000000",
    bool Bold = false,
    bool Italic = false,
    TextAlignment Alignment = TextAlignment.Left,
    string? FontFamily = null,
    double? MaxWidth = null,
    bool Strict = false,
    double? X = null,
    double? Baseline = null
);

/// <summary>Moves an original text line so that its left edge and baseline land on the given coordinates.</summary>
public sealed record TextBlockMove(OriginalTextBlock Block, double NewX, double NewBaseline);

/// <summary>Vertical font metrics in em units (ascent &gt; 0, descent &lt; 0).</summary>
public readonly record struct FontVerticalMetrics(double Ascent, double Descent);

public sealed record TextAnalysis(IReadOnlyList<OriginalTextBlock> Blocks, int UnsupportedBlocks, string Explanation);
