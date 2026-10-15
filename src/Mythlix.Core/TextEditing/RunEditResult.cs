using System.Collections.Immutable;

namespace Mythlix.Core.TextEditing;

public sealed record RunEditDiagnostic(string Code, TextRange Range, string Message);

/// <summary>Success contains exact bytes; failure never returns a partial encoding. Offsets are local UTF-16.</summary>
public sealed record FontEncodingResult(ImmutableArray<byte> Bytes, ImmutableArray<RunEditDiagnostic> Diagnostics) {
 public bool IsSupported => Diagnostics.IsEmpty;
}

/// <summary>Validates against a snapshot of actual PDF resources, not a family-name guess.
/// Hard line breaks are handled by the draft buffer, not encoded as visible PDF string bytes.</summary>
public interface IFontEncodingResolver {
 FontEncodingResult Encode(PdfFontReference font, string text);
}

/// <summary>Applied describes draft mutation only. Encoding validity does not imply layout/commit readiness.</summary>
public sealed record RunEditResult(ParagraphRunBuffer Buffer, bool Applied, ImmutableArray<RunEditDiagnostic> Diagnostics) {
 public bool IsEncodingValid => Diagnostics.IsEmpty;
}
