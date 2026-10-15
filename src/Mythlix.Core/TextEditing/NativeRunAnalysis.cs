using System.Collections.Immutable;

namespace Mythlix.Core.TextEditing;

/// <summary>Single-byte source string with known horizontal placement, not a shaped glyph run.
/// Baseline coordinates are page points from the top-left; BaselineY excludes Ts.</summary>
public sealed record NativeTextRun(PdfTextRun Run, double BaselineX, double BaselineY,
 ImmutableArray<byte> EncodedBytes, ImmutableArray<double> CharacterAdvancesPt, TextRange ShowOperatorRange) {
 public double AdvancePt => CharacterAdvancesPt.Sum();
}

/// <summary>Numeric TJ operand retained independently of source strings.</summary>
public sealed record NativeTextAdjustment(int StreamIndex, string BlockId, TextRange ByteRange,
 double PdfAdjustment, double HorizontalAdvancePt);

public sealed record NativeRunDiagnostic(string Code, int? StreamIndex, int? ByteOffset, string Message);

/// <summary>Additional read-only analysis. Any unsupported state rejects the page, with no partial results.</summary>
public sealed record NativeRunAnalysis(TextDocumentContext Context, ImmutableArray<NativeTextRun> Runs,
 ImmutableArray<NativeTextAdjustment> Adjustments, ImmutableArray<NativeRunDiagnostic> Diagnostics) {
 public bool IsReadOnly => true;
 public bool IsSupported => Diagnostics.IsEmpty;
}
