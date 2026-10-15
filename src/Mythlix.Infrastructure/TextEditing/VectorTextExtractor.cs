using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Mythlix.Core;

namespace Mythlix.Infrastructure.TextEditing;

public static class VectorTextExtractor {
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

            var fontSize = Math.Max(6, Math.Round(line.Height, 1));
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
