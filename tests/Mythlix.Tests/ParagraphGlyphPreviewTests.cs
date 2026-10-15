using Avalonia;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Mythlix.Core;
using Mythlix.Core.TextEditing;
using Mythlix.Desktop.TextEditing;
using Xunit;

namespace Mythlix.Tests;

public sealed class ParagraphGlyphPreviewTests {
 [Fact] public void PhysicalGlyphsConsumePlanAndRejectForeignResources() {
  AppBuilder.Configure<Application>().UseStandardRuntimePlatformSubsystem().UseWindowingSubsystem(()=>{},"Offscreen").UseSkia().UseHarfBuzz().SetupWithoutStarting();
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
}
