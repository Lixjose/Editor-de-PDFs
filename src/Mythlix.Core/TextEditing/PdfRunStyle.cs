namespace Mythlix.Core.TextEditing;

/// <summary>Normalized RGB components, independent of UI colors. Other color spaces are not supported yet.</summary>
public sealed record RunRgbColor {
 public double Red { get; }
 public double Green { get; }
 public double Blue { get; }
 public RunRgbColor(double red, double green, double blue) {
  if(new[] {red,green,blue}.Any(v => !double.IsFinite(v) || v < 0 || v > 1))
   throw new ArgumentOutOfRangeException(nameof(red), "RGB components must be finite values in [0,1].");
  Red=red; Green=green; Blue=blue;
 }
}

/// <summary>Phase 1 fill-only horizontal text state. Distances are unscaled text-space points;
/// horizontal scale is a factor (1 = 100%). Encoding capabilities are resolved separately.</summary>
public sealed record PdfRunStyle {
 public PdfFontReference Font { get; }
 public string FamilyName { get; }
 public string EncodingName { get; }
 public double FontSizePt { get; }
 public double CharacterSpacingPt { get; }
 public double WordSpacingPt { get; }
 public double HorizontalScale { get; }
 public double BaselineRisePt { get; }
 public RunRgbColor FillColor { get; }
 public bool Bold { get; }
 public bool Italic { get; }
 public PdfRunStyle(PdfFontReference font, string familyName, string encodingName,
  double fontSizePt, RunRgbColor fillColor, bool bold = false, bool italic = false,
  double characterSpacingPt = 0, double wordSpacingPt = 0, double horizontalScale = 1, double baselineRisePt = 0) {
  ArgumentNullException.ThrowIfNull(font); ArgumentNullException.ThrowIfNull(fillColor);
  ArgumentException.ThrowIfNullOrWhiteSpace(familyName); ArgumentException.ThrowIfNullOrWhiteSpace(encodingName);
  if(!double.IsFinite(fontSizePt) || fontSizePt <= 0) throw new ArgumentOutOfRangeException(nameof(fontSizePt));
  if(!double.IsFinite(horizontalScale) || horizontalScale <= 0) throw new ArgumentOutOfRangeException(nameof(horizontalScale));
  if(new[] {characterSpacingPt,wordSpacingPt,baselineRisePt}.Any(v => !double.IsFinite(v)))
   throw new ArgumentOutOfRangeException(nameof(characterSpacingPt));
  Font=font; FamilyName=familyName; EncodingName=encodingName; FontSizePt=fontSizePt;
  FillColor=fillColor; Bold=bold; Italic=italic; CharacterSpacingPt=characterSpacingPt;
  WordSpacingPt=wordSpacingPt; HorizontalScale=horizontalScale; BaselineRisePt=baselineRisePt;
 }
}
