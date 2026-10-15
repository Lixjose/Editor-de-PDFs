using PdfSharp.Pdf;
using PdfSharp.Drawing;

namespace Mythlix.Infrastructure.TextEditing;

/// <summary>Uses the export font resolver for both original-width calibration and fit checks.</summary>
public static class TextFontMeasurement {
 public static string Family(string name) => EmbeddedFontResolver.CleanFamilyName(name);
 public static double Width(string text, string family, double size, bool bold, bool italic) {
  EmbeddedFontResolver.EnsureInitialized();
  using var graphics = XGraphics.CreateMeasureContext(new XSize(1, 1), XGraphicsUnit.Point, XPageDirection.Downwards);
  var style = (bold ? XFontStyleEx.Bold : XFontStyleEx.Regular) | (italic ? XFontStyleEx.Italic : XFontStyleEx.Regular);
  var font = new XFont(Family(family), size, style, new XPdfFontOptions(PdfFontEncoding.Unicode));
  return graphics.MeasureString(text, font).Width;
 }
 public static double Calibrate(string text, string family, double size, bool bold, bool italic, double width) {
  var measured = Width(text, family, size, bold, italic);
  // Do not interpret column gaps or corrupt geometry as extreme font stretching.
  return measured > 0 ? Math.Clamp(width / measured, 0.75, 1.25) : 1;
 }
}
