using Mythlix.Core;
using PdfSharp.Drawing;
using PdfSharp.Pdf;

namespace Mythlix.Infrastructure;

public static class PageDecorator {
    public static void ApplyHeaderFooter(PdfDocument document, HeaderFooterOptions options) {
        options.Validate();
        EmbeddedFontResolver.EnsureInitialized();

        var totalPages = document.PageCount;
        var pagesToProcess = options.Pages ?? Enumerable.Range(1, totalPages).ToArray();

        var rgb = Convert.ToUInt32(options.Color[1..], 16);
        var color = XColor.FromArgb((int)((rgb >> 16) & 255), (int)((rgb >> 8) & 255), (int)(rgb & 255));
        var brush = new XSolidBrush(color);

        var fontName = string.IsNullOrWhiteSpace(options.FontFamily) ? "Liberation Sans" : options.FontFamily;
        var font = new XFont(fontName, options.FontSize, XFontStyleEx.Regular, new XPdfFontOptions(PdfFontEncoding.Unicode));

        foreach(var pageNumber in pagesToProcess) {
            if(pageNumber < 1 || pageNumber > totalPages) continue;
            if(options.SkipFirstPage && pageNumber == 1) continue;

            var page = document.Pages[pageNumber - 1];
            var text = options.Format
                .Replace("{n}", pageNumber.ToString())
                .Replace("{total}", totalPages.ToString());

            using var graphics = XGraphics.FromPdfPage(page, XGraphicsPdfPageOptions.Append);

            var measured = graphics.MeasureString(text, font);
            var (x, y, format) = CalculatePosition(page.Width.Point, page.Height.Point, measured, options);

            graphics.DrawString(text, font, brush, x, y, format);
        }
    }

    public static void ApplyWatermark(PdfDocument document, WatermarkOptions options) {
        options.Validate();
        EmbeddedFontResolver.EnsureInitialized();

        var totalPages = document.PageCount;
        var pagesToProcess = options.Pages ?? Enumerable.Range(1, totalPages).ToArray();

        var rgb = Convert.ToUInt32(options.Color[1..], 16);
        var alpha = Math.Clamp((int)(options.Opacity * 255), 1, 255);
        var color = XColor.FromArgb(alpha, (int)((rgb >> 16) & 255), (int)((rgb >> 8) & 255), (int)(rgb & 255));
        var brush = new XSolidBrush(color);

        var font = new XFont("Liberation Sans", options.FontSize, XFontStyleEx.Bold, new XPdfFontOptions(PdfFontEncoding.Unicode));

        foreach(var pageNumber in pagesToProcess) {
            if(pageNumber < 1 || pageNumber > totalPages) continue;

            var page = document.Pages[pageNumber - 1];
            using var graphics = XGraphics.FromPdfPage(page, XGraphicsPdfPageOptions.Append);

            var centerX = page.Width.Point / 2.0;
            var centerY = page.Height.Point / 2.0;

            var state = graphics.Save();
            graphics.TranslateTransform(centerX, centerY);
            graphics.RotateTransform(-options.Rotation);

            var measured = graphics.MeasureString(options.Text, font);
            var x = -measured.Width / 2.0;
            var y = measured.Height / 4.0;

            graphics.DrawString(options.Text, font, brush, x, y);
            graphics.Restore(state);
        }
    }

    static (double X, double Y, XStringFormat Format) CalculatePosition(
        double width, double height, XSize measured, HeaderFooterOptions options) {
        return options.Position switch {
            HeaderFooterPosition.TopLeft => (options.MarginX, options.MarginY + measured.Height, XStringFormats.TopLeft),
            HeaderFooterPosition.TopCenter => (width / 2.0, options.MarginY + measured.Height, XStringFormats.TopCenter),
            HeaderFooterPosition.TopRight => (width - options.MarginX, options.MarginY + measured.Height, XStringFormats.TopRight),
            HeaderFooterPosition.BottomLeft => (options.MarginX, height - options.MarginY, XStringFormats.TopLeft),
            HeaderFooterPosition.BottomCenter => (width / 2.0, height - options.MarginY, XStringFormats.TopCenter),
            HeaderFooterPosition.BottomRight => (width - options.MarginX, height - options.MarginY, XStringFormats.TopRight),
            _ => (width / 2.0, height - options.MarginY, XStringFormats.TopCenter)
        };
    }
}
