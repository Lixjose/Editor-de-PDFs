using System.Text;
using System.Text.Json;
using Mythlix.Core;
using PdfSharp.Drawing;
using PdfSharp.Pdf;

namespace Mythlix.Infrastructure.Ocr;

internal static class SearchableLayerWriter {
    static readonly string[] Groups = ["/Font","/XObject","/ExtGState","/ColorSpace","/Pattern","/Shading","/Properties"];

    public static void Write(PdfPage page, PageGeometry geometry, OcrPageResult result) {
        result.Validate();
        if(!geometry.CanEdit)
            throw new InvalidOperationException("OCR buscable: use páginas editables.");

        var resources = GraphicLayerWriter.IsolateResources(page);
        var previous = page.Elements.GetString("/MythlixOcrResourcesV1");
        if(previous.Length > 16384) throw new InvalidOperationException("Recursos OCR inválidos.");

        var owned = string.IsNullOrEmpty(previous) ? new Dictionary<string, string[]>() : JsonSerializer.Deserialize<Dictionary<string, string[]>>(previous)!;
        foreach(var (group, names) in owned) {
            if(Groups.Contains(group)) {
                var dictionary = resources.Elements.GetDictionary(group);
                if(dictionary is not null) foreach(var name in names) dictionary.Elements.Remove(name);
            }
        }

        for(var i = page.Contents.Elements.Count - 1; i >= 0; i--) {
            if(page.Contents.Elements.GetDictionary(i)!.Elements.GetBoolean("/MythlixOcrV1"))
                page.Contents.Elements.RemoveAt(i);
        }
        page.Elements.Remove("/MythlixOcrResourcesV1");

        var before = Groups.ToDictionary(g => g, g => resources.Elements.GetDictionary(g)?.Elements.Keys.ToHashSet() ?? []);
        if(result.Words.Count == 0) return;

        EmbeddedFontResolver.EnsureInitialized();
        using(var graphics = XGraphics.FromPdfPage(page, XGraphicsPdfPageOptions.Append)) {
            var scaleX = geometry.DisplayWidth / result.ImageWidth;
            var scaleY = geometry.DisplayHeight / result.ImageHeight;

            foreach(var word in result.Words.Where(w => !string.IsNullOrWhiteSpace(w.Text))) {
                var displayBox = new ObjectBounds(word.X * scaleX, word.Y * scaleY, word.Width * scaleX, word.Height * scaleY);
                var rawBox = geometry.FromDisplay(displayBox);

                var x = rawBox.X;
                var y = rawBox.Y;
                var w = rawBox.Width;
                var h = rawBox.Height;

                var font = new XFont("Liberation Sans", Math.Max(1, h), XFontStyleEx.Regular, new XPdfFontOptions(PdfFontEncoding.Unicode));
                var measured = graphics.MeasureString(word.Text, font);
                var state = graphics.Save();

                graphics.TranslateTransform(x, y);
                if(geometry.Rotation != 0) {
                    graphics.RotateTransform(-geometry.Rotation);
                }
                graphics.ScaleTransform(w / Math.Max(.001, measured.Width), 1);

                double baselineOffset;
                if(word.BaselineY > word.Y && word.BaselineY <= word.Y + word.Height * 1.5) {
                    baselineOffset = (word.BaselineY - word.Y) * scaleY;
                } else {
                    baselineOffset = h * 0.82;
                }

                graphics.DrawString(word.Text, font, XBrushes.Black, new XPoint(0, Math.Clamp(baselineOffset, h * 0.6, h * 0.95)));
                graphics.Restore(state);
            }
        }

        var stream = page.Contents.Elements.GetDictionary(page.Contents.Elements.Count - 1)!;
        // PDF rendering mode 3 makes text invisible yet selectable; the scan remains untouched.
        var data = Encoding.Latin1.GetString(stream.Stream!.UnfilteredValue).Replace("BT\n", "BT\n3 Tr\n", StringComparison.Ordinal);
        if(!data.Contains("3 Tr")) throw new InvalidOperationException("No se pudo crear la capa invisible OCR.");

        stream.Elements.Remove("/Filter");
        stream.Elements.Remove("/DecodeParms");
        stream.Stream!.Value = Encoding.Latin1.GetBytes(data);
        stream.Elements.SetInteger("/Length", stream.Stream.Value.Length);
        stream.Elements.SetBoolean("/MythlixOcrV1", true);

        var added = Groups.ToDictionary(g => g, g => (page.Resources.Elements.GetDictionary(g)?.Elements.Keys ?? []).Where(n => !before[g].Contains(n)).ToArray());
        page.Elements.SetString("/MythlixOcrResourcesV1", JsonSerializer.Serialize(added));
    }
}
