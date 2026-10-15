using PdfSharp.Pdf;

namespace Mythlix.Infrastructure.TextEditing;

internal static class FontSubstitutionService {
    public static string ResolveBaseFont(string? familyName, bool bold, bool italic) {
        var name = (familyName ?? "").ToLowerInvariant();

        if(name.Contains("times") || name.Contains("serif") || name.Contains("georgia") || name.Contains("minion")) {
            if(bold && italic) return "Times-BoldItalic";
            if(bold) return "Times-Bold";
            if(italic) return "Times-Italic";
            return "Times-Roman";
        }

        if(name.Contains("courier") || name.Contains("mono") || name.Contains("consolas")) {
            if(bold && italic) return "Courier-BoldOblique";
            if(bold) return "Courier-Bold";
            if(italic) return "Courier-Oblique";
            return "Courier";
        }

        // Default to Helvetica (Sans-Serif)
        if(bold && italic) return "Helvetica-BoldOblique";
        if(bold) return "Helvetica-Bold";
        if(italic) return "Helvetica-Oblique";
        return "Helvetica";
    }

    public static FontMetrics GetMetrics(string baseFontName) {
        if(StandardFontMetrics.Fonts.TryGetValue(baseFontName, out var metrics)) {
            return metrics;
        }
        return StandardFontMetrics.Fonts["Helvetica"];
    }

    public static string EnsureFontResource(PdfPage page, PdfDocument document, string targetBaseFont) {
        var resources = GraphicLayerWriter.IsolateResources(page);
        var fontDict = resources.Elements.GetDictionary("/Font");
        if(fontDict is null) {
            fontDict = new PdfDictionary(document);
            resources.Elements["/Font"] = fontDict;
        }

        var normalizedTarget = "/" + targetBaseFont.TrimStart('/');

        // Check if there is already a font with this BaseFont and WinAnsiEncoding
        foreach(var key in fontDict.Elements.Keys) {
            var existing = fontDict.Elements.GetDictionary(key);
            if(existing is not null) {
                var bf = existing.Elements.GetName("/BaseFont");
                var enc = existing.Elements.GetName("/Encoding");
                if(string.Equals(bf, normalizedTarget, StringComparison.OrdinalIgnoreCase) &&
                   (enc is "" or "/WinAnsiEncoding" or "/StandardEncoding")) {
                    return key.TrimStart('/');
                }
            }
        }

        // Add new font resource
        var newFont = new PdfDictionary(document);
        newFont.Elements.SetName("/Type", "/Font");
        newFont.Elements.SetName("/Subtype", "/Type1");
        newFont.Elements.SetName("/BaseFont", normalizedTarget);
        newFont.Elements.SetName("/Encoding", "/WinAnsiEncoding");
        document.Internals.AddObject(newFont);

        var newKey = $"/MtxF{fontDict.Elements.Count + 1}";
        while(fontDict.Elements.ContainsKey(newKey)) {
            newKey += "x";
        }

        fontDict.Elements[newKey] = newFont.Reference!;
        return newKey.TrimStart('/');
    }
}
