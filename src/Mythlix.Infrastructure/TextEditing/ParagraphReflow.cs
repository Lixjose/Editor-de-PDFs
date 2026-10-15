using System.Text;
using Mythlix.Core;

namespace Mythlix.Infrastructure.TextEditing;

public sealed record ReflowLine(string Text, byte[] EncodedBytes, double Width, double XOffset);

public static class ParagraphReflow {
    public static IReadOnlyList<ReflowLine> Reflow(
        string text,
        double fontSize,
        double[] widths,
        Encoding encoding,
        double maxWidth,
        TextAlignment alignment) {
        if(string.IsNullOrEmpty(text)) return [];

        var effectiveMaxWidth = maxWidth > 0 ? maxWidth : 1000.0;
        var lines = new List<ReflowLine>();
        var paragraphs = text.Replace("\r\n", "\n").Split('\n');

        foreach(var paragraph in paragraphs) {
            if(paragraph.Length == 0) {
                lines.Add(new("", [], 0, 0));
                continue;
            }

            var words = SplitIntoWords(paragraph);
            var currentLineWords = new List<string>();
            double currentLineWidth = 0;

            foreach(var word in words) {
                var wordWidth = MeasureText(word, fontSize, widths, encoding);

                if(currentLineWords.Count > 0 && currentLineWidth + wordWidth > effectiveMaxWidth) {
                    var lineText = string.Concat(currentLineWords).TrimEnd();
                    var lineWidth = MeasureText(lineText, fontSize, widths, encoding);
                    var xOffset = CalculateOffset(lineWidth, effectiveMaxWidth, alignment);
                    lines.Add(new(lineText, encoding.GetBytes(lineText), lineWidth, xOffset));

                    currentLineWords.Clear();
                    currentLineWidth = 0;

                    var cleanWord = word.TrimStart();
                    if(cleanWord.Length > 0) {
                        currentLineWords.Add(cleanWord);
                        currentLineWidth = MeasureText(cleanWord, fontSize, widths, encoding);
                    }
                } else {
                    currentLineWords.Add(word);
                    currentLineWidth += wordWidth;
                }
            }

            if(currentLineWords.Count > 0) {
                var lineText = string.Concat(currentLineWords).TrimEnd();
                var lineWidth = MeasureText(lineText, fontSize, widths, encoding);
                var xOffset = CalculateOffset(lineWidth, effectiveMaxWidth, alignment);
                lines.Add(new(lineText, encoding.GetBytes(lineText), lineWidth, xOffset));
            }
        }

        return lines;
    }

    public static double MeasureText(string str, double fontSize, double[] widths, Encoding encoding) {
        try {
            var bytes = encoding.GetBytes(str);
            double total = 0;
            foreach(var b in bytes) {
                if(b < widths.Length && widths[b] > 0 && double.IsFinite(widths[b])) {
                    total += widths[b];
                } else {
                    total += 500;
                }
            }
            return total * fontSize / 1000.0;
        } catch {
            return str.Length * fontSize * 0.5;
        }
    }

    static double CalculateOffset(double lineWidth, double maxWidth, TextAlignment alignment) => alignment switch {
        TextAlignment.Center => Math.Max(0, (maxWidth - lineWidth) / 2.0),
        TextAlignment.Right => Math.Max(0, maxWidth - lineWidth),
        _ => 0
    };

    static List<string> SplitIntoWords(string text) {
        var result = new List<string>();
        var current = new StringBuilder();
        var inSpace = false;

        for(var i = 0; i < text.Length; i++) {
            var c = text[i];
            var isSpace = char.IsWhiteSpace(c);

            if(i == 0) {
                inSpace = isSpace;
                current.Append(c);
            } else if(isSpace == inSpace) {
                current.Append(c);
            } else {
                result.Add(current.ToString());
                current.Clear();
                current.Append(c);
                inSpace = isSpace;
            }
        }

        if(current.Length > 0) {
            result.Add(current.ToString());
        }

        return result;
    }
}
