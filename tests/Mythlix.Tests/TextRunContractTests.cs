using Mythlix.Core.TextEditing;
using Xunit;

namespace Mythlix.Tests;

public sealed class TextRunContractTests {
 readonly TextDocumentContext context = new(Guid.NewGuid(), 1, (long)int.MaxValue + 100);
 TextSourceSpan Source(string block = "0:0", int length = 3) => new(context,0,new(15,20),"stream-sha",block,"block-sha",new(0,length));
 PdfRunStyle Style(TextDocumentContext? sourceContext = null) => new(new(sourceContext ?? context,"page:1/resources","/F1"),
  "Helvetica","WinAnsiEncoding",11.25,new(0.1,0.2,0.3),wordSpacingPt:2.5,horizontalScale:0.9);

 [Theory]
 [InlineData(-1,1)]
 [InlineData(0,-1)]
 [InlineData(int.MaxValue,1)]
 public void RangesRejectNegativeValuesAndOverflow(int start,int length) =>
  Assert.Throws<ArgumentOutOfRangeException>(() => new TextRange(start,length));

 [Fact] public void EqualStylesCompareByValuesAndIncludeOriginalSpacingAndFontScope() {
  Assert.Equal(Style(),Style());
  var different = new PdfRunStyle(new(context,"other/resources","/F1"),"Helvetica","WinAnsiEncoding",11.25,new(0.1,0.2,0.3),wordSpacingPt:2.5,horizontalScale:0.9);
  Assert.NotEqual(Style(),different);
  Assert.Equal(2.5,Style().WordSpacingPt);
  Assert.Equal(0.9,Style().HorizontalScale);
  Assert.True(context.Revision > int.MaxValue);
 }

 [Theory]
 [InlineData(double.NaN)]
 [InlineData(double.PositiveInfinity)]
 [InlineData(0)]
 [InlineData(-1)]
 public void InvalidFontSizesAndScalesAreRejected(double value) {
  Assert.Throws<ArgumentOutOfRangeException>(() => new PdfRunStyle(Style().Font,"Helvetica","WinAnsiEncoding",value,new(0,0,0)));
  Assert.Throws<ArgumentOutOfRangeException>(() => new PdfRunStyle(Style().Font,"Helvetica","WinAnsiEncoding",12,new(0,0,0),horizontalScale:value));
 }

 [Fact] public void MergedRunRetainsBothSourcesAndCopiesMutableInput() {
  var first=Source("0:0"); var second=Source("0:1");
  var slices=new List<RunSourceSlice> {new(new(0,3),first),new(new(3,3),second)};
  var run=new PdfTextRun(Guid.NewGuid(),"abcdef",Style(),slices);
  slices.Clear();
  Assert.Equal(2,run.Sources.Length);
  Assert.Equal("0:1",run.Sources[1].Source.BlockId);
  var changed=run.Sources.RemoveAt(0);
  Assert.Single(changed);
  Assert.Equal(2,run.Sources.Length);
 }

 [Fact] public void StaleOrDifferentPageSourcesCannotBeMixedWithFontContext() {
  var stale=new TextDocumentContext(context.DocumentId,1,context.Revision-1);
  Assert.Throws<ArgumentException>(() => new PdfTextRun(Guid.NewGuid(),"abc",Style(stale),[new(new(0,3),Source())]));
  var otherPage=new TextDocumentContext(context.DocumentId,2,context.Revision);
  Assert.Throws<ArgumentException>(() => new PdfTextRun(Guid.NewGuid(),"abc",Style(otherPage),[new(new(0,3),Source())]));
 }

 [Fact] public void InsertedTextHasAnchorWithoutInventedSourceBytes() {
  var source=Source();
  var run=new PdfTextRun(Guid.NewGuid(),"ñ",Style(),[],true,new(source,3));
  Assert.Empty(run.Sources);
  Assert.Equal(source,run.InsertionAnchor!.Source);
  Assert.Throws<ArgumentException>(() => new PdfTextRun(Guid.NewGuid(),"ñ",Style(),[],true));
  Assert.Throws<ArgumentOutOfRangeException>(() => new RunInsertionAnchor(source,4));
 }

 [Fact] public void UneditedRunsRejectMissingOrOverlappingProvenance() {
  Assert.Throws<ArgumentException>(() => new PdfTextRun(Guid.NewGuid(),"abcdef",Style(),[new(new(0,3),Source())]));
  Assert.Throws<ArgumentException>(() => new PdfTextRun(Guid.NewGuid(),"abcdef",Style(),[new(new(0,3),Source()),new(new(2,3),Source("0:1"))]));
 }

 [Fact] public void SourceSlicesCannotSplitSurrogatePairsOrExceedText() {
  Assert.Throws<ArgumentException>(() => new PdfTextRun(Guid.NewGuid(),"😀",Style(),[new(new(0,1),Source(length:1)),new(new(1,1),Source(length:1))]));
  Assert.Throws<ArgumentException>(() => new PdfTextRun(Guid.NewGuid(),"abc",Style(),[new(new(0,4),Source(length:4))]));
 }

 [Fact] public void DraftCanRetainUnencodableTextWithoutClaimingGlyphOrByteMapping() {
  var run=new PdfTextRun(Guid.NewGuid(),"😀",Style(),[],true,new(Source(),0));
  Assert.Equal("😀",run.Text);
  Assert.True(run.IsEdited);
  // Encoding and shaping are future validated services, not inferred from the font name.
  Assert.Empty(run.Sources);
 }
}
