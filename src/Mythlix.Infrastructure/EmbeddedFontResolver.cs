using PdfSharp.Fonts;
namespace Mythlix.Infrastructure;

/// <summary>Identical font bytes on Windows/Linux; no dependency on installed system fonts.</summary>
internal sealed class EmbeddedFontResolver : IFontResolver {
 internal static void EnsureInitialized() {
  lock(typeof(EmbeddedFontResolver)) {
   if(GlobalFontSettings.FontResolver is null) GlobalFontSettings.FontResolver = new EmbeddedFontResolver();
  }
 }
 public FontResolverInfo ResolveTypeface(string familyName, bool bold, bool italic) =>
  new("LiberationSans-" + (bold && italic ? "BoldItalic" : bold ? "Bold" : italic ? "Italic" : "Regular"));
 public byte[] GetFont(string faceName) {
  using var stream = typeof(EmbeddedFontResolver).Assembly.GetManifestResourceStream($"Mythlix.Infrastructure.Fonts.{faceName}.ttf")
   ?? throw new InvalidOperationException("No se encontró la fuente incorporada.");
  using var memory = new MemoryStream(); stream.CopyTo(memory); return memory.ToArray();
 }
}
