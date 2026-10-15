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

    [Fact]
    public void JustificationFillsWrappedLinesAndResetsAtParagraphEnd() {
        var widths=StandardFontMetrics.Fonts["Helvetica"].Widths.Select(w=>(double)w).ToArray();
        var lines=ParagraphReflow.Reflow("Uno dos tres cuatro cinco seis siete ocho nueve diez\nFinal",12,widths,Encoding.ASCII,100,TextAlignment.Justify);
        Assert.True(lines.Count>2);
        foreach(var line in lines.Take(lines.Count-2)) {
            Assert.True(line.WordSpacing>0);
            Assert.Equal(100,line.Width+line.Text.Count(c=>c==' ')*line.WordSpacing,6);
        }
        Assert.Equal(0,lines[^2].WordSpacing);
        Assert.Equal(0,lines[^1].WordSpacing);
    }

    [Fact]
    public void SupportedTextSpacingProducesAccurateBoundsAndBaseline() {
        CreateSamplePdf("BT /F1 14 Tf 2 Tc 3 Tw 95 Tz 2 Ts 1 0 0 1 50 700 Tm (Uno dos) Tj ET");
        using var doc=new PdfDocumentSession(Source);
        var row=Assert.Single(doc.AnalyzeText(1).Blocks);
        var widths=StandardFontMetrics.Fonts["Helvetica"].Widths.Select(w=>(double)w).ToArray();
        var expected=(ParagraphReflow.MeasureText("Uno dos",14,widths,Encoding.ASCII)+6*2+3)*0.95;
        Assert.Equal(expected,row.Bounds.Width,6);
        Assert.Equal(90,row.EffectiveBaseline,6);
        Assert.Equal(2,row.CharacterSpacing);Assert.Equal(3,row.WordSpacing);Assert.Equal(.95,row.HorizontalScale);
    }

    [Fact]
    public async Task ParagraphReplacementReflowsJustifiesAndRemainsEditableAfterReopen() {
        CreateSamplePdf("BT /F1 14 Tf 1 0 0 1 50 700 Tm (Una primera linea) Tj ET\nBT /F1 14 Tf 1 0 0 1 50 682.5 Tm (Una segunda linea) Tj ET\nBT /F1 14 Tf 1 0 0 1 50 665 Tm (Ultima) Tj ET");
        using var doc=new PdfDocumentSession(Source);
        var rows=doc.AnalyzeText(1).Blocks;
        var detected=PdfParagraph.Around(rows[1],rows);
        Assert.Equal(3,detected.Lines.Count);
        var paragraph=detected with {Alignment=TextAlignment.Justify};
        var before=doc.Snapshot();
        doc.ReplaceParagraph(1,paragraph,new TextEditOptions("Texto nuevo para comprobar que varias palabras quedan justificadas y se pueden volver a editar",14,Alignment:TextAlignment.Justify));
        await doc.SaveCopyAsync(Output);
        using(var reopened=new PdfDocumentSession(Output)) {
            var edited=reopened.AnalyzeText(1).Blocks;
            Assert.True(edited.Count>1);
            Assert.True(edited[0].WordSpacing>0);
            Assert.Equal(0,edited[^1].WordSpacing);
            Assert.DoesNotContain(edited,b=>b.Text.Contains("segunda"));
            var text=await new PopplerRenderer().ExtractTextAsync(Output,1,CancellationToken.None);
            Assert.Contains("justificadas",text);
            Assert.DoesNotContain("primera",text);
        }
        doc.Restore(before);
        Assert.Equal(rows.Select(b=>b.Text),doc.AnalyzeText(1).Blocks.Select(b=>b.Text));
    }

    [Fact]
    public void ParagraphReplacementRejectsStaleRowsWithoutChangingDocument() {
        using var doc=new PdfDocumentSession(Source);
        var row=Assert.Single(doc.AnalyzeText(1).Blocks);
        var paragraph=PdfParagraph.Around(row,[row]) with {Lines=[row with {Fingerprint="stale"}]};
        Assert.Throws<InvalidOperationException>(()=>doc.ReplaceParagraph(1,paragraph,new("Cambio",14)));
        Assert.Equal(row.Text,Assert.Single(doc.AnalyzeText(1).Blocks).Text);
    }

    [Fact]
    public async Task ReflowPreservesHorizontalScaleAndCharacterSpacing() {
        CreateSamplePdf("BT /F1 14 Tf 1 Tc 2 Tw 95 Tz 1 0 0 1 50 700 Tm (Uno dos tres) Tj ET");
        using var doc=new PdfDocumentSession(Source);
        var row=Assert.Single(doc.AnalyzeText(1).Blocks);
        doc.ReplaceText(1,row,new TextEditOptions("Otro texto",14,MaxWidth:200));
        await doc.SaveCopyAsync(Output);
        using var reopened=new PdfDocumentSession(Output);
        var edited=Assert.Single(reopened.AnalyzeText(1).Blocks);
        Assert.Equal(.95,edited.HorizontalScale);Assert.Equal(1,edited.CharacterSpacing);Assert.Equal(2,edited.WordSpacing);
        Assert.Equal(row.EffectiveBaseline,edited.EffectiveBaseline,6);
    }

    [Fact]
    public void ParagraphDetectionDoesNotCombineColumnsOrDifferentFonts() {
        var first=new OriginalTextBlock("0:0","Columna A","Helvetica",12,new(30,40,150,12),"fp",Baseline:50);
        var column=first with {Id="0:1",Bounds=new(300,55,150,12),Baseline=65};
        var otherFont=first with {Id="0:2",FontName="Courier",Bounds=new(30,55,150,12),Baseline=65};
        Assert.Single(PdfParagraph.Around(first,[first,column,otherFont]).Lines);
    }

    [Theory]
    [InlineData("Uno dos",-17.5)]
    [InlineData("Uno dos\nTres cuatro\nCinco seis\nSiete ocho",35)]
    public void ParagraphResizeShiftsOnlyFollowingColumnAndPreservesStyle(string replacement,double delta) {
        CreateSamplePdf("BT /F1 14 Tf 1 0 0 1 50 700 Tm (Una primera linea) Tj ET\nBT /F1 14 Tf 1 0 0 1 50 682.5 Tm (Segunda linea) Tj ET\nBT /F1 14 Tf 1 0 0 1 50 600 Tm (Bloque inferior que conserva formato) Tj ET\nBT /F1 14 Tf 1 0 0 1 50 582.5 Tm (Cola) Tj ET\nBT /F1 14 Tf 1 0 0 1 400 600 Tm (Lateral) Tj ET");
        using var doc=new PdfDocumentSession(Source);
        var rows=doc.AnalyzeText(1).Blocks;
        var paragraph=PdfParagraph.Around(rows[0],rows);
        Assert.Equal(2,paragraph.Lines.Count);
        var lower=rows.Single(b=>b.Text.StartsWith("Bloque"));
        var lateral=rows.Single(b=>b.Text=="Lateral");
        doc.ReplaceParagraph(1,paragraph,new TextEditOptions(replacement,14));
        var after=doc.AnalyzeText(1).Blocks;
        var moved=after.Single(b=>b.Text==lower.Text);
        Assert.Equal(lower.EffectiveBaseline+delta,moved.EffectiveBaseline,6);
        Assert.Equal(rows.Single(b=>b.Text=="Cola").EffectiveBaseline+delta,after.Single(b=>b.Text=="Cola").EffectiveBaseline,6);
        Assert.Equal(lower.Bounds.X,moved.Bounds.X);Assert.Equal(lower.FontName,moved.FontName);
        Assert.Equal(lateral.Bounds,after.Single(b=>b.Text=="Lateral").Bounds);
        // A second resize must work despite the empty placeholders from the first edit.
        var edited=after.First();
        var second=PdfParagraph.Around(edited,after);
        doc.ReplaceParagraph(1,second,new TextEditOptions("Uno dos",14));
        Assert.Contains(doc.AnalyzeText(1).Blocks,b=>b.Text=="Uno dos");
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void VerticalReflowRejectsOverflowOrFixedGraphicsAtomically(bool graphics) {
        var tail=graphics?"50 300 100 40 re S":"BT /F1 14 Tf 1 0 0 1 50 40 Tm (Bloque inferior del documento) Tj ET";
        CreateSamplePdf("BT /F1 14 Tf 1 0 0 1 50 700 Tm (Una primera linea) Tj ET\nBT /F1 14 Tf 1 0 0 1 50 682.5 Tm (Segunda linea) Tj ET\n"+tail);
        using var doc=new PdfDocumentSession(Source);
        var before=doc.AnalyzeText(1).Blocks;
        var paragraph=PdfParagraph.Around(before[0],before);
        var ex=Assert.Throws<InvalidOperationException>(()=>doc.ReplaceParagraph(1,paragraph,new TextEditOptions("Uno dos\nTres cuatro\nCinco seis\nSiete ocho",14)));
        Assert.Contains("Desbordamiento",ex.Message);
        Assert.Equal(before.Select(b=>(b.Text,b.Bounds)),doc.AnalyzeText(1).Blocks.Select(b=>(b.Text,b.Bounds)));
    }

    [Fact]
    public void TwoLineJustificationUsesExplicitWordSpacing() {
        var first=new OriginalTextBlock("0:0","Primera linea","Helvetica",12,new(30,40,200,12),"fp",Baseline:50,WordSpacing:2);
        var last=first with {Id="0:1",Text="Final",Bounds=new(30,55,60,12),Baseline=65,WordSpacing=0};
        Assert.Equal(TextAlignment.Justify,PdfParagraph.Around(first,[first,last]).Alignment);
        Assert.Equal(TextAlignment.Left,PdfParagraph.Around(first with {WordSpacing=0},[first with {WordSpacing=0},last]).Alignment);
    }

    [Fact]
    public void TwoLineJustificationCanUseMatchingConfirmedColumn() {
        OriginalTextBlock Row(string id,double y,double width)=>new(id,"Texto de ejemplo","Helvetica",12,new(30,y,width,12),"fp",Baseline:y+10);
        var rows=new[]{Row("0:0",40,200),Row("0:1",55,200),Row("0:2",70,70),Row("0:3",140,200),Row("0:4",155,60)};
        Assert.Equal(TextAlignment.Justify,PdfParagraph.Around(rows[3],rows).Alignment);
        var otherColumn=rows.Take(3).Select(b=>b with {Bounds=b.Bounds with {X=300}}).Concat(rows.Skip(3)).ToArray();
        Assert.Equal(TextAlignment.Left,PdfParagraph.Around(rows[3],otherColumn).Alignment);
    }

    [Fact]
    public void BackgroundSamplingAcceptsSolidColorAndRejectsGradients() {
        const uint cream=0xFFF4E9D2;
        Assert.Equal(cream,BackgroundColorSampler.UniformColor(Enumerable.Repeat(cream,100)));
        Assert.Null(BackgroundColorSampler.UniformColor(Enumerable.Range(0,100).Select(i=>0xFF000000u+(uint)i*0x010101u)));
        Assert.Null(BackgroundColorSampler.UniformColor([cream]));
    }

    [Theory]
    [InlineData("q 0.5 w 50 760 m 300 760 l S Q",true)]
    [InlineData("q 450 500 30 20 re f Q",true)]
    [InlineData("q 50 300 100 40 re S Q",false)]
    [InlineData("q 30 w 50 730 m 300 730 l S Q",false)]
    [InlineData("q /Unknown Do Q",false)]
    public void DistantSimpleGraphicsPermitReflowButCorridorAndUnknownGraphicsBlock(string graphic,bool allowed) {
        CreateSamplePdf(graphic+"\nBT /F1 14 Tf 1 0 0 1 50 700 Tm (Una primera linea) Tj ET\nBT /F1 14 Tf 1 0 0 1 50 682.5 Tm (Segunda linea) Tj ET");
        using var doc=new PdfDocumentSession(Source);
        var rows=doc.AnalyzeText(1).Blocks;
        var paragraph=PdfParagraph.Around(rows[0],rows);
        var edit=new TextEditOptions("Uno dos\nTres cuatro\nCinco seis",14);
        if(allowed) {doc.ReplaceParagraph(1,paragraph,edit);Assert.Contains(doc.AnalyzeText(1).Blocks,b=>b.Text=="Cinco seis");}
        else {Assert.Throws<InvalidOperationException>(()=>doc.ReplaceParagraph(1,paragraph,edit));Assert.Equal(rows.Select(b=>b.Text),doc.AnalyzeText(1).Blocks.Select(b=>b.Text));}
    }

    [Fact]
    public void InteriorColorCheckAcceptsAntialiasingAndRejectsColoredWatermark() {
        var grayscale=Enumerable.Range(0,256).Select(i=>0xFF000000u+(uint)i*0x010101u).ToArray();
        Assert.True(BackgroundColorSampler.InteriorMatches(grayscale,0xFFFFFFFF,0xFF000000));
        Assert.False(BackgroundColorSampler.InteriorMatches(grayscale.Concat(Enumerable.Repeat(0xFFFF0000u,20)),0xFFFFFFFF,0xFF000000));
        Assert.False(BackgroundColorSampler.InteriorMatches([0x00FFFFFF],0xFFFFFFFF,0xFF000000));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task LayoutMetadataSurvivesCompressionButRejectsExternalChanges(bool changePosition) {
        using(var doc=new PdfDocumentSession(Source)) {
            var row=doc.AnalyzeText(1).Blocks[0];
            doc.ReplaceText(1,row,new TextEditOptions("Otro texto",14,MaxWidth:240,LineHeight:18));
            await doc.SaveCopyAsync(Output);
        }
        var compressed=Path.Combine(directory,"compressed.pdf");
        using(var pdf=PdfReader.Open(Output,PdfDocumentOpenMode.Modify)) {
            var page=pdf.Pages[0];var raw=page.Contents.Elements.GetDictionary(0)!.Stream!.UnfilteredValue;
            using var buffer=new MemoryStream();
            using(var zlib=new System.IO.Compression.ZLibStream(buffer,System.IO.Compression.CompressionLevel.Optimal,true))zlib.Write(raw);
            var stream=new PdfDictionary(pdf);stream.CreateStream(buffer.ToArray());stream.Elements.SetName("/Filter","/FlateDecode");
            pdf.Internals.AddObject(stream);page.Contents.Elements[0]=stream.Reference!;pdf.Save(compressed);
        }
        using(var reopened=new PdfDocumentSession(compressed)) {
            var row=Assert.Single(reopened.AnalyzeText(1).Blocks);
            Assert.Equal(240,row.LayoutWidth);Assert.Equal(18,row.LayoutLineHeight);
        }
        var changed=Path.Combine(directory,"external.pdf");
        using(var pdf=PdfReader.Open(compressed,PdfDocumentOpenMode.Modify)) {
            pdf.Options.CompressContentStreams=true;
            var page=pdf.Pages[0];var old=page.Contents.Elements.GetDictionary(0)!;
            var content=Encoding.ASCII.GetString(old.Stream!.UnfilteredValue);
            content=changePosition?content.Replace("1 0 0 1 50 700 Tm","1 0 0 1 70 700 Tm"):content.Replace("(Otro texto)","(Otro nuevo)");
            var stream=new PdfDictionary(pdf);stream.CreateStream(Encoding.ASCII.GetBytes(content));
            pdf.Internals.AddObject(stream);page.Contents.Elements[0]=stream.Reference!;pdf.Save(changed);
        }
        using var modified=new PdfDocumentSession(changed);
        Assert.Null(Assert.Single(modified.AnalyzeText(1).Blocks).LayoutWidth);
    }

    public void Dispose() {
        if(Directory.Exists(directory)) Directory.Delete(directory, true);
    }
}
