using Avalonia;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Mythlix.Core;
using Mythlix.Core.TextEditing;
using Mythlix.Desktop.TextEditing;
using Xunit;

namespace Mythlix.Tests;

public sealed class ParagraphGlyphPreviewTests {
 static ParagraphGlyphPreviewTests() {
  AvaloniaTestRuntime.Ensure();
 }
 [Fact] public void PhysicalGlyphsConsumePlanAndRejectForeignResources() {
  var path=Path.Combine(AppContext.BaseDirectory,"../../../../../src/Mythlix.Infrastructure/Fonts/LiberationSans-Regular.ttf");
  var ctx=new TextDocumentContext(Guid.NewGuid(),1,2);
  var font=new PdfFontReference(ctx,"page","/F1");
  using var binding=new PreviewFontBinding(font,File.ReadAllBytes(path),"Explicit Liberation Sans preview");
  var style=new PdfRunStyle(font,"Helvetica","WinAnsiEncoding",24,new(1,1,1),horizontalScale:0.8,baselineRisePt:3);
  var source=new TextSourceSpan(ctx,0,new(0,10),"stream","block","hash",new(0,3));
  var run=new PdfTextRun(Guid.NewGuid(),"A ñ",style,[new(new(0,3),source)]);
  var segment=new ComposedRunSegment(run,new(0,3),new(0,3),20,50,[65,32,241],[20,15,22],[0,5,0]);
  var plan=new ParagraphCompositionSnapshot(ctx,run.Text,[source],new(new(0,0,300,100),50,30),
   [new(new(0,3),null,CompositionBreakKind.End,50,24,5,new(20,23,57,29),[segment])],new(20,23,57,29),[]);
  using var preview=ParagraphGlyphPreview.Create(plan,[binding]);
  Assert.Single(preview.Items);
  Assert.Same(segment,preview.Items[0].Segment);
  Assert.Equal(64,binding.Sha256.Length);
  Assert.NotEqual((ushort)241,preview.Items[0].Glyphs.GlyphInfos[2].GlyphIndex);
  Assert.Equal(57/0.8,preview.Items[0].Glyphs.GlyphInfos.Sum(g=>g.GlyphAdvance),8);
  Assert.Throws<InvalidOperationException>(()=>ParagraphGlyphPreview.Create(plan,[]));
  using var foreign=new PreviewFontBinding(new(new(ctx.DocumentId,1,3),"page","/F1"),File.ReadAllBytes(path),"Other revision");
  Assert.Throws<InvalidOperationException>(()=>ParagraphGlyphPreview.Create(plan,[foreign]));
  var unsupported=new PdfTextRun(Guid.NewGuid(),"A \uFFFF",style,[new(new(0,3),source)]);
  var missing=segment with {Owner=unsupported};
  var missingPlan=plan with {Lines=[plan.Lines[0] with {Segments=[missing]}]};
  Assert.Throws<InvalidOperationException>(()=>ParagraphGlyphPreview.Create(missingPlan,[binding]));
  Assert.Throws<ArgumentException>(()=>ParagraphGlyphPreview.Create(plan with {Diagnostics=[new("Blocked",new(0,3),"test")]},[binding]));
  using var bitmap=new RenderTargetBitmap(new PixelSize(320,100),new Vector(72,72));
  using(var drawing=bitmap.CreateDrawingContext()) {
   drawing.DrawRectangle(Brushes.DarkBlue,null,new Rect(0,0,320,100));
   preview.Draw(drawing);
  }
  var output=Path.Combine(Path.GetTempPath(),"mythlix-phase2b-preview.png");
  bitmap.Save(output,PngBitmapEncoderOptions.Default);
  Assert.True(new FileInfo(output).Length>500);
 }
 [Theory]
 [InlineData(Mythlix.Core.TextAlignment.Left)]
 [InlineData(Mythlix.Core.TextAlignment.Center)]
 [InlineData(Mythlix.Core.TextAlignment.Right)]
 [InlineData(Mythlix.Core.TextAlignment.Justify)]
 public void RealMixedCompositionRendersAllStylesAndPreservesDocument(Mythlix.Core.TextAlignment alignment) {
  var directory=Path.Combine(Path.GetTempPath(),"mythlix-mixed-preview-"+Guid.NewGuid().ToString("N"));
  Directory.CreateDirectory(directory);
  var path=Path.Combine(directory,"mixed.pdf");
  try {
   using(var pdf=new PdfSharp.Pdf.PdfDocument()) {
    var page=pdf.AddPage();
    var resources=new PdfSharp.Pdf.PdfDictionary(pdf);var fontDict=new PdfSharp.Pdf.PdfDictionary(pdf);
    foreach(var (name,family) in new[]{("/F1","/Helvetica"),("/F2","/Helvetica-Bold"),("/F3","/Helvetica-Oblique"),("/F4","/Helvetica-BoldOblique")}) {
     var font=new PdfSharp.Pdf.PdfDictionary(pdf);
     font.Elements.SetName("/Type","/Font");font.Elements.SetName("/Subtype","/Type1");
     font.Elements.SetName("/BaseFont",family);font.Elements.SetName("/Encoding","/WinAnsiEncoding");
     pdf.Internals.AddObject(font);fontDict.Elements[name]=font.Reference!;
    }
    resources.Elements["/Font"]=fontDict;page.Elements["/Resources"]=resources;
    page.Contents.AppendContent().CreateStream(System.Text.Encoding.ASCII.GetBytes(
     "BT 20 700 Td /F1 18 Tf 0.5 Tc 2 Tw 80 Tz (Normal cafe ) Tj /F2 22 Tf 0.8 0 0 rg (negrita con ) Tj /F3 18 Tf 0 0.4 0 rg 3 Ts (cursiva y ) Tj /F4 20 Tf 0 0 0.8 rg 0 Ts (mixta final) Tj ET"));
    pdf.Save(path);
   }
   var before=File.ReadAllBytes(path);
   using var document=new Mythlix.Infrastructure.PdfDocumentSession(path);
   var analysis=document.AnalyzeTextRuns(1,4);Assert.True(analysis.IsSupported);
   var draft=ParagraphRunBuffer.FromRuns(analysis.Runs.Select(r=>r.Run));
   var metrics=document.CreateRunMetricsProvider(1,4);
   var plan=ParagraphComposer.Compose(draft,new(new(20,20,200,300),50,38,alignment),metrics);
   Assert.True(plan.IsValid);Assert.True(plan.Lines.Length>1);
   var bindings=new List<PreviewFontBinding>();
   try {
    foreach(var style in draft.Runs.Select(r=>r.Style)) {
     var variant=style.Bold?(style.Italic?"BoldItalic":"Bold"):(style.Italic?"Italic":"Regular");
     var physical=Path.Combine(AppContext.BaseDirectory,$"../../../../../src/Mythlix.Infrastructure/Fonts/LiberationSans-{variant}.ttf");
     bindings.Add(new(style.Font,File.ReadAllBytes(physical),variant));
    }
    using var preview=ParagraphGlyphPreview.Create(plan,bindings);
    Assert.Equal(plan.Lines.Sum(l=>l.Segments.Length),preview.Items.Length);
    Assert.Equal(4,preview.Items.Select(i=>i.Font.Sha256).Distinct().Count());
    Assert.Equal(draft.Text,string.Concat(preview.Items.Select(i=>i.Segment.Text)));
    foreach(var item in preview.Items) {
     Assert.Equal(item.Segment.AdvancePt,item.Glyphs.GlyphInfos.Sum(g=>g.GlyphAdvance)*item.Segment.Owner.Style.HorizontalScale,7);
     Assert.Same(item.Segment.Owner, draft.Runs.Single(r=>r.DraftId==item.Segment.Owner.DraftId));
    }
    using var bitmap=new RenderTargetBitmap(new PixelSize(260,230),new Vector(96,96));
    using(var drawing=bitmap.CreateDrawingContext()) {drawing.DrawRectangle(Brushes.White,null,new Rect(0,0,260,230));preview.Draw(drawing);}
    var image=Path.Combine(Path.GetTempPath(),$"mythlix-phase2b-mixed-{alignment}.png");
    bitmap.Save(image,PngBitmapEncoderOptions.Default);
    using var raster=SkiaSharp.SKBitmap.Decode(image);
    Assert.True(Enumerable.Range(0,raster.Width*raster.Height).Count(i=>raster.GetPixel(i%raster.Width,i/raster.Width)!=SkiaSharp.SKColors.White)>200);
   } finally {foreach(var binding in bindings) binding.Dispose();}
   Assert.Equal(before,File.ReadAllBytes(path));
   var after=document.AnalyzeTextRuns(1,4);
   Assert.Equal(analysis.Runs.Select(r=>r.Run.Text),after.Runs.Select(r=>r.Run.Text));
  } finally {Directory.Delete(directory,true);}
 }

}
