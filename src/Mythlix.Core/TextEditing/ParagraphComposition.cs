using System.Collections.Immutable;

namespace Mythlix.Core.TextEditing;

/// <summary>Metrics for validated single-byte WinAnsi characters. Not shaped glyph indices.</summary>
public sealed record RunMetricsResult(ImmutableArray<byte> EncodedBytes, ImmutableArray<double> AdvancesPt,
 double AscentPt, double DescentPt, ImmutableArray<RunEditDiagnostic> Diagnostics) {
 public bool IsSupported => Diagnostics.IsEmpty;
}
public interface IRunMetricsProvider : IFontEncodingResolver {
 RunMetricsResult Measure(PdfRunStyle style,string text);
 string GetResourceFingerprint(PdfFontReference font);
}

/// <summary>Page coordinates are points from top-left. First baseline excludes text rise Ts.</summary>
public sealed record ParagraphCompositionOptions {
 public ObjectBounds Bounds { get; }
 public double FirstBaselineY { get; }
 public double LineHeightPt { get; }
 public TextAlignment Alignment { get; }
 public ParagraphCompositionOptions(ObjectBounds bounds,double firstBaselineY,double lineHeightPt,TextAlignment alignment=TextAlignment.Left) {
  if(new[] {bounds.X,bounds.Y,bounds.Width,bounds.Height,firstBaselineY,lineHeightPt}.Any(v=>!double.IsFinite(v)) ||
   bounds.X<0 || bounds.Y<0 || bounds.Width<=0 || bounds.Height<=0 || firstBaselineY<bounds.Y || lineHeightPt<=0 || !Enum.IsDefined(alignment))
   throw new ArgumentOutOfRangeException(nameof(bounds),"Finite page bounds, baseline and positive leading are required.");
  if(!double.IsFinite(bounds.X+bounds.Width) || !double.IsFinite(bounds.Y+bounds.Height)) throw new ArgumentOutOfRangeException(nameof(bounds));
  Bounds=bounds;FirstBaselineY=firstBaselineY;LineHeightPt=lineHeightPt;Alignment=alignment;
 }
}
public enum CompositionBreakKind { Soft, Hard, End }

/// <summary>The owner retains all source slices/anchors. Ranges address its draft and the whole paragraph.
/// ExtraSpacingPt is per character, in already-scaled page points. No inferred glyph IDs are exposed.</summary>
public sealed record ComposedRunSegment(PdfTextRun Owner, TextRange RunTextRange, TextRange ParagraphTextRange,
 double StartX,double BaselineY, ImmutableArray<byte> EncodedBytes,
 ImmutableArray<double> AdvancesPt,ImmutableArray<double> ExtraSpacingPt) {
 public string Text => Owner.Text.Substring(RunTextRange.Start,RunTextRange.Length);
 public double AdvancePt => AdvancesPt.Sum();
}
public sealed record ComposedTextLine(TextRange TextRange,TextRange? HardBreakRange,CompositionBreakKind BreakKind,
 double BaselineY,double AscentPt,double DescentPt,ObjectBounds AdvanceBounds,
 ImmutableArray<ComposedRunSegment> Segments) {
 public bool IsLastLineOfParagraph => BreakKind!=CompositionBreakKind.Soft;
 public string Text => string.Concat(Segments.Select(s=>s.Text));
}

/// <summary>Immutable composition proposal, not a write authorization or raster-equivalence guarantee.</summary>
public sealed record ParagraphCompositionSnapshot(TextDocumentContext Context,string Text,
 ImmutableArray<TextSourceSpan> OriginalSources,ParagraphCompositionOptions Options,
 ImmutableArray<ComposedTextLine> Lines,ObjectBounds? ComposedBounds,
 ImmutableArray<RunEditDiagnostic> Diagnostics) {
 public ImmutableDictionary<PdfFontReference,string> FontResourceFingerprints { get; init; } = ImmutableDictionary<PdfFontReference,string>.Empty;
 public bool HasHorizontalOverflow => Diagnostics.Any(d=>d.Code=="HorizontalOverflow");
 public bool HasVerticalOverflow => Diagnostics.Any(d=>d.Code=="VerticalOverflow");
 public bool IsValid => Diagnostics.IsEmpty;
}
