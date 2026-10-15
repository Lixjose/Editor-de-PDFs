using System.Text;
using Mythlix.Core;
using Mythlix.Infrastructure;
using Mythlix.Infrastructure.TextEditing;
using PdfSharp.Pdf;
using PdfSharp.Pdf.IO;
using Xunit;

namespace Mythlix.Tests;

public sealed class TextReflowEngineTests : IDisposable {
    readonly string directory = Path.Combine(Path.GetTempPath(), "mythlix-reflow-" + Guid.NewGuid().ToString("N"));
    string Source => Path.Combine(directory, "source.pdf");
    string Output => Path.Combine(directory, "edited.pdf");

    public TextReflowEngineTests() {
        Directory.CreateDirectory(directory);
        CreateSamplePdf("BT /F1 14 Tf 1 0 0 1 50 700 Tm (Small original text) Tj ET");
    }

    void CreateSamplePdf(string content, string baseFont = "/Helvetica", string encoding = "/WinAnsiEncoding") {
        using var doc = new PdfDocument();
        var page = doc.AddPage();
        page.Width = PdfSharp.Drawing.XUnit.FromPoint(612);
        page.Height = PdfSharp.Drawing.XUnit.FromPoint(792);

        var font = new PdfDictionary(doc);
        font.Elements.SetName("/Type", "/Font");
        font.Elements.SetName("/Subtype", "/Type1");
        font.Elements.SetName("/BaseFont", baseFont);
        if(!string.IsNullOrEmpty(encoding)) font.Elements.SetName("/Encoding", encoding);
        doc.Internals.AddObject(font);

        var resources = new PdfDictionary(doc);
        var fonts = new PdfDictionary(doc);
        fonts.Elements["/F1"] = font.Reference!;
        resources.Elements["/Font"] = fonts;
        page.Elements["/Resources"] = resources;

        var stream = page.Contents.AppendContent();
        stream.CreateStream(Encoding.ASCII.GetBytes(content));
        doc.Save(Source);
    }

    [Fact]
    public void ReflowWrapsWordsIntoMultipleLinesBasedOnMaxWidth() {
        var metrics = StandardFontMetrics.Fonts["Helvetica"];
        var widths = metrics.Widths.Select(w => (double)w).ToArray();
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        var winAnsi = Encoding.GetEncoding(1252);

        var text = "El motor de edicion de texto solido debe permitir reflujo de parrafos completo";
        var lines = ParagraphReflow.Reflow(text, 14, widths, winAnsi, 200, TextAlignment.Left);

        Assert.True(lines.Count >= 2, $"Debería dividir en múltiples líneas pero produjo {lines.Count}");
        foreach(var line in lines) {
            Assert.True(line.Width <= 210, $"Línea '{line.Text}' excede el ancho máximo: {line.Width}");
        }
        Assert.Equal(string.Join(" ", lines.Select(l => l.Text)), text);
    }

    [Fact]
    public void ReflowSupportsAlignments() {
        var metrics = StandardFontMetrics.Fonts["Helvetica"];
        var widths = metrics.Widths.Select(w => (double)w).ToArray();
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        var winAnsi = Encoding.GetEncoding(1252);

        var text = "Titulo";
        var left = ParagraphReflow.Reflow(text, 14, widths, winAnsi, 200, TextAlignment.Left);
        var center = ParagraphReflow.Reflow(text, 14, widths, winAnsi, 200, TextAlignment.Center);
        var right = ParagraphReflow.Reflow(text, 14, widths, winAnsi, 200, TextAlignment.Right);

        Assert.Equal(0, left[0].XOffset);
        Assert.True(center[0].XOffset > 0, "Centro debe tener offset > 0");
        Assert.True(right[0].XOffset > center[0].XOffset, "Derecha debe tener mayor offset que Centro");
    }

    [Fact]
    public async Task ReplaceWithReflowProducesMultipleLinesInPdf() {
        using var doc = new PdfDocumentSession(Source);
        var block = doc.AnalyzeText(1).Blocks[0];

        var longText = "Este es un texto considerablemente mas largo que el titulo original y debe generar reflujo automatico";
        doc.ReplaceText(1, block, new TextEditOptions(longText, 14, MaxWidth: 180));
        await doc.SaveCopyAsync(Output);

        using var parsed = PdfReader.Open(Output, PdfDocumentOpenMode.Modify);
        var streamBytes = parsed.Pages[0].Contents.Elements.GetDictionary(0)!.Stream!.UnfilteredValue;
        var content = Encoding.ASCII.GetString(streamBytes);

        // Ensure multiple Tj operators were generated for the reflowed lines
        var tjCount = content.Split("Tj").Length - 1;
        Assert.True(tjCount >= 2, $"Se esperaban múltiples operadores Tj pero se encontraron {tjCount}");

        var popplerText = await new PopplerRenderer().ExtractTextAsync(Output, 1, CancellationToken.None);
        Assert.Contains("reflujo", popplerText);
        Assert.Contains("automatico", popplerText);
    }

    [Fact]
    public async Task ReplaceWithExplicitLineBreaksProducesMultiLinePdf() {
        using var doc = new PdfDocumentSession(Source);
        var block = doc.AnalyzeText(1).Blocks[0];

        doc.ReplaceText(1, block, new TextEditOptions("Primera linea\nSegunda linea\nTercera linea", 12));
        await doc.SaveCopyAsync(Output);

        var popplerText = await new PopplerRenderer().ExtractTextAsync(Output, 1, CancellationToken.None);
        Assert.Contains("Primera linea", popplerText);
        Assert.Contains("Segunda linea", popplerText);
        Assert.Contains("Tercera linea", popplerText);
    }

    [Fact]
    public async Task ReplaceWithColorAppliesRgOperator() {
        using var doc = new PdfDocumentSession(Source);
        var block = doc.AnalyzeText(1).Blocks[0];

        doc.ReplaceText(1, block, new TextEditOptions("Texto en color", 14, Color: "#FF0000"));
        await doc.SaveCopyAsync(Output);

        using var parsed = PdfReader.Open(Output, PdfDocumentOpenMode.Modify);
        var streamBytes = parsed.Pages[0].Contents.Elements.GetDictionary(0)!.Stream!.UnfilteredValue;
        var content = Encoding.ASCII.GetString(streamBytes);

        Assert.Contains("1 0 0 rg", content);
    }

    [Fact]
    public async Task ReplaceWithBoldAndItalicSubstitutesFontAndRegistersResource() {
        using var doc = new PdfDocumentSession(Source);
        var block = doc.AnalyzeText(1).Blocks[0];

        doc.ReplaceText(1, block, new TextEditOptions("Texto en negrita cursiva", 14, Bold: true, Italic: true));
        await doc.SaveCopyAsync(Output);

        using var parsed = PdfReader.Open(Output, PdfDocumentOpenMode.Modify);
        var fontDict = parsed.Pages[0].Resources.Elements.GetDictionary("/Font")!;

        var hasBoldOblique = fontDict.Elements.Keys
            .Select(k => fontDict.Elements.GetDictionary(k))
            .Any(d => d?.Elements.GetName("/BaseFont") == "/Helvetica-BoldOblique");

        Assert.True(hasBoldOblique, "El diccionario de fuentes debe contener /Helvetica-BoldOblique");
    }

    [Fact]
    public async Task ReplaceWithFontFamilyChangeToTimesAndCourier() {
        using var doc = new PdfDocumentSession(Source);
        var block = doc.AnalyzeText(1).Blocks[0];

        doc.ReplaceText(1, block, new TextEditOptions("Cambiado a Times", 14, FontFamily: "Times-Roman"));
        await doc.SaveCopyAsync(Output);

        using var parsed = PdfReader.Open(Output, PdfDocumentOpenMode.Modify);
        var fontDict = parsed.Pages[0].Resources.Elements.GetDictionary("/Font")!;

        var hasTimes = fontDict.Elements.Keys
            .Select(k => fontDict.Elements.GetDictionary(k))
            .Any(d => d?.Elements.GetName("/BaseFont") == "/Times-Roman");

        Assert.True(hasTimes, "El diccionario de fuentes debe contener /Times-Roman");
    }

    [Fact]
    public async Task ReplaceWithLargerFontSizeScalesAndReflows() {
        using var doc = new PdfDocumentSession(Source);
        var block = doc.AnalyzeText(1).Blocks[0];

        // Original was 14 pt, now scale up to 28 pt
        doc.ReplaceText(1, block, new TextEditOptions("Titulo Gigante", 28));
        await doc.SaveCopyAsync(Output);

        using var parsed = PdfReader.Open(Output, PdfDocumentOpenMode.Modify);
        var streamBytes = parsed.Pages[0].Contents.Elements.GetDictionary(0)!.Stream!.UnfilteredValue;
        var content = Encoding.ASCII.GetString(streamBytes);

        Assert.Contains("28 Tf", content);
    }

    [Fact]
    public void GraphicTextSupportsAlignmentAndCustomFont() {
        using var doc = new PdfDocumentSession(Source);
        var graphic = new GraphicObject {
            Kind = GraphicKind.Text,
            Text = "Texto centrado",
            Alignment = TextAlignment.Center,
            FontFamily = "Liberation Sans",
            X = 100,
            Y = 100,
            Width = 300,
            Height = 50
        };

        doc.SetGraphics(1, [graphic]);
        var savedGraphics = doc.GetGraphics(1);

        Assert.Single(savedGraphics);
        Assert.Equal(TextAlignment.Center, savedGraphics[0].Alignment);
        Assert.Equal("Liberation Sans", savedGraphics[0].FontFamily);
    }

    public void Dispose() {
        if(Directory.Exists(directory)) Directory.Delete(directory, true);
    }
}
