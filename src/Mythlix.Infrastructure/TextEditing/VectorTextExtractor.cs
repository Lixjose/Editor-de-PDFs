using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Xml.Linq;
using Mythlix.Core;

namespace Mythlix.Infrastructure.TextEditing;

public static class VectorTextExtractor {
    sealed record FontSpec(string Id, double Size, string Family, string Color, bool Bold, bool Italic);

    public static TextAnalysis ExtractFromXml(string xml, int page, PageGeometry geometry) {
        if(string.IsNullOrWhiteSpace(xml)) return new([], 0, "Página sin texto.");

        try {
            var doc = XDocument.Parse(xml);
            var pageElem = doc.Descendants("page").FirstOrDefault(p => p.Attribute("number")?.Value == page.ToString())
                ?? doc.Descendants("page").FirstOrDefault();
            if(pageElem is null) return new([], 0, "No se encontró la página especificada en el XML.");

            var fontSpecs = new Dictionary<string, FontSpec>();
            foreach(var fs in pageElem.Elements("fontspec")) {
                var id = fs.Attribute("id")?.Value ?? "";
                var size = double.TryParse(fs.Attribute("size")?.Value, NumberStyles.Float, CultureInfo.InvariantCulture, out var sz) ? sz : 12.0;
                var rawFamily = fs.Attribute("family")?.Value ?? "Liberation Sans";
                var cleanFamily = EmbeddedFontResolver.CleanFamilyName(rawFamily);
                var color = fs.Attribute("color")?.Value ?? "#000000";
                var bold = rawFamily.Contains("Bold", StringComparison.OrdinalIgnoreCase) || rawFamily.Contains("Black", StringComparison.OrdinalIgnoreCase);
                var italic = rawFamily.Contains("Italic", StringComparison.OrdinalIgnoreCase) || rawFamily.Contains("Oblique", StringComparison.OrdinalIgnoreCase);
                fontSpecs[id] = new FontSpec(id, size, cleanFamily, color, bold, italic);
            }

            var textElements = new List<(double Left, double Top, double Width, double Height, string Text, FontSpec Font, bool Bold, bool Italic)>();
            foreach(var t in pageElem.Elements("text")) {
                var text = t.Value?.Trim();
                if(string.IsNullOrEmpty(text)) continue;

                if(!double.TryParse(t.Attribute("left")?.Value, NumberStyles.Float, CultureInfo.InvariantCulture, out var left) ||
                   !double.TryParse(t.Attribute("top")?.Value, NumberStyles.Float, CultureInfo.InvariantCulture, out var top) ||
                   !double.TryParse(t.Attribute("width")?.Value, NumberStyles.Float, CultureInfo.InvariantCulture, out var width) ||
                   !double.TryParse(t.Attribute("height")?.Value, NumberStyles.Float, CultureInfo.InvariantCulture, out var height))
                    continue;

                var fontId = t.Attribute("font")?.Value ?? "";
                var font = fontSpecs.GetValueOrDefault(fontId, new FontSpec(fontId, Math.Max(6, height), "Liberation Sans", "#000000", false, false));
                var bold = font.Bold || t.Descendants("b").Any();
                var italic = font.Italic || t.Descendants("i").Any();

                textElements.Add((left, top, width, height, text, font, bold, italic));
            }

            // Group horizontally adjacent text elements on the same line if needed
            var mergedLines = new List<(double Left, double Top, double Width, double Height, string Text, FontSpec Font, bool Bold, bool Italic)>();
            for(var i = 0; i < textElements.Count; i++) {
                var current = textElements[i];
                while(i + 1 < textElements.Count) {
                    var next = textElements[i + 1];
                    // Same baseline within 3pt and adjacent horizontally
                    if(Math.Abs(next.Top - current.Top) <= 3 &&
                       next.Left >= current.Left + current.Width - 1 &&
                       next.Left <= current.Left + current.Width + current.Font.Size * 1.0 &&
                       next.Font.Family.Equals(current.Font.Family, StringComparison.OrdinalIgnoreCase)) {
                        var sep = current.Text.EndsWith(' ') || next.Text.StartsWith(' ') ? "" : " ";
                        var combinedText = current.Text + sep + next.Text;
                        var newWidth = (next.Left + next.Width) - current.Left;
                        var newHeight = Math.Max(current.Height, next.Height);
                        current = current with { Width = newWidth, Height = newHeight, Text = combinedText };
                        i++;
                    } else {
                        break;
                    }
                }
                mergedLines.Add(current);
            }

            // Group consecutive lines into paragraph blocks
            var paragraphs = new List<(double Left, double Top, double Width, double Height, string Text, FontSpec Font, bool Bold, bool Italic, int LineCount)>();
            var currentGroup = new List<(double Left, double Top, double Width, double Height, string Text, FontSpec Font, bool Bold, bool Italic)>();

            void FlushCurrentGroup() {
                if(currentGroup.Count == 0) return;
                var first = currentGroup[0];
                var last = currentGroup[^1];
                var left = currentGroup.Min(l => l.Left);
                var top = first.Top;
                var right = currentGroup.Max(l => l.Left + l.Width);
                var bottom = last.Top + last.Height;
                var width = right - left;
                var height = bottom - top;
                var text = string.Join("\n", currentGroup.Select(l => l.Text));
                paragraphs.Add((left, top, width, height, text, first.Font, first.Bold, first.Italic, currentGroup.Count));
                currentGroup.Clear();
            }

            for(var i = 0; i < mergedLines.Count; i++) {
                var line = mergedLines[i];
                if(currentGroup.Count == 0) {
                    currentGroup.Add(line);
                    continue;
                }

                var prev = currentGroup[^1];
                var verticalGap = line.Top - (prev.Top + prev.Height);
                var maxGap = Math.Max(12, line.Font.Size * 1.5);
                var sameFont = line.Font.Family.Equals(prev.Font.Family, StringComparison.OrdinalIgnoreCase) &&
                               Math.Abs(line.Font.Size - prev.Font.Size) <= 1.0 &&
                               line.Bold == prev.Bold &&
                               line.Italic == prev.Italic;
                var isAligned = Math.Abs(line.Left - prev.Left) <= Math.Max(35, line.Font.Size * 3);
                var isNextLine = verticalGap >= -3 && verticalGap <= maxGap;

                if(sameFont && isAligned && isNextLine) {
                    currentGroup.Add(line);
                } else {
                    FlushCurrentGroup();
                    currentGroup.Add(line);
                }
            }
            FlushCurrentGroup();

            var blocks = new List<OriginalTextBlock>();
            for(var i = 0; i < paragraphs.Count; i++) {
                var item = paragraphs[i];
                var fontSize = Math.Max(4, Math.Round(item.Font.Size, 1));
                var bounds = new ObjectBounds(item.Left, item.Top, Math.Max(item.Width, 8), Math.Max(item.Height, 8));
                var fingerprint = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes($"{page}_{i}_{item.Text}_{item.Left}_{item.Top}")));

                blocks.Add(new OriginalTextBlock(
                    Id: $"v:{page}:{i}",
                    Text: item.Text,
                    FontName: item.Font.Family,
                    FontSize: fontSize,
                    Bounds: bounds,
                    Fingerprint: fingerprint,
                    CanChangeFontSize: true,
                    Color: item.Font.Color,
                    Bold: item.Bold,
                    Italic: item.Italic,
                    Alignment: TextAlignment.Left,
                    LineCount: item.LineCount
                ));
            }

            var explanation = blocks.Count == 0
                ? "No se detectaron bloques de texto vectorial en esta página."
                : $"{blocks.Count} bloques de texto detectados (edición directa activa).";

            return new TextAnalysis(blocks, 0, explanation);
        } catch {
            return new([], 0, "Error analizando estructura XML vectorial.");
        }
    }

    public static TextAnalysis Extract(string tsv, int page, PageGeometry geometry) {
        if(string.IsNullOrWhiteSpace(tsv)) return new([], 0, "Página sin texto.");

        var lines = new List<(double Left, double Top, double Width, double Height, List<string> Words)>();
        (double Left, double Top, double Width, double Height, List<string> Words)? currentLine = null;

        var rows = tsv.Split('\n');
        foreach(var row in rows) {
            var parts = row.TrimEnd('\r').Split('\t');
            if(parts.Length < 12 || !int.TryParse(parts[0], out var level)) continue;

            if(level == 4) {
                // New line marker from pdftotext
                if(currentLine is { Words.Count: > 0 }) {
                    lines.Add(currentLine.Value);
                }
                if(double.TryParse(parts[6], NumberStyles.Float, CultureInfo.InvariantCulture, out var left) &&
                   double.TryParse(parts[7], NumberStyles.Float, CultureInfo.InvariantCulture, out var top) &&
                   double.TryParse(parts[8], NumberStyles.Float, CultureInfo.InvariantCulture, out var width) &&
                   double.TryParse(parts[9], NumberStyles.Float, CultureInfo.InvariantCulture, out var height)) {
                    currentLine = (left, top, width, height, new List<string>());
                } else {
                    currentLine = null;
                }
            } else if(level == 5 && currentLine is not null) {
                var word = parts[11].Trim();
                if(!string.IsNullOrEmpty(word)) {
                    currentLine.Value.Words.Add(word);
                }
            }
        }

        if(currentLine is { Words.Count: > 0 }) {
            lines.Add(currentLine.Value);
        }

        var blocks = new List<OriginalTextBlock>();
        for(var i = 0; i < lines.Count; i++) {
            var line = lines[i];
            var text = string.Join(" ", line.Words);
            if(string.IsNullOrWhiteSpace(text)) continue;

            var fontSize = Math.Max(6, Math.Round(line.Height * 0.75, 1));
            var bounds = new ObjectBounds(line.Left, line.Top, Math.Max(line.Width, 8), Math.Max(line.Height, 8));
            var fingerprint = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes($"{page}_{i}_{text}_{line.Left}_{line.Top}")));

            blocks.Add(new OriginalTextBlock(
                Id: $"v:{page}:{i}",
                Text: text,
                FontName: "Liberation Sans",
                FontSize: fontSize,
                Bounds: bounds,
                Fingerprint: fingerprint,
                CanChangeFontSize: true,
                Color: "#000000",
                Bold: false,
                Italic: false,
                Alignment: TextAlignment.Left,
                LineCount: 1
            ));
        }

        var explanation = blocks.Count == 0
            ? "No se detectaron bloques de texto vectorial en esta página."
            : $"{blocks.Count} bloques de texto detectados (edición directa activa).";

        return new TextAnalysis(blocks, 0, explanation);
    }
}
