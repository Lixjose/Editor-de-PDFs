using Mythlix.Core.TextEditing;
using Mythlix.Infrastructure;
using PdfSharp.Pdf;
using System.Text;
using Xunit;

namespace Mythlix.Tests;

public sealed class ParagraphRunBufferTests : IDisposable {
 readonly string directory=Path.Combine(Path.GetTempPath(),"mythlix-run-buffer-"+Guid.NewGuid().ToString("N"));
 readonly PdfDocumentSession document;
 readonly IFontEncodingResolver resolver;
 readonly ParagraphRunBuffer buffer;
 public ParagraphRunBufferTests() {
  Directory.CreateDirectory(directory);
  var path=Path.Combine(directory,"source.pdf");
  using(var pdf=new PdfDocument()) {
   var page=pdf.AddPage();page.Width=PdfSharp.Drawing.XUnit.FromPoint(612);page.Height=PdfSharp.Drawing.XUnit.FromPoint(792);
   var resources=new PdfDictionary(pdf);var fonts=new PdfDictionary(pdf);
   foreach(var (key,family) in new[] {("/F1","/Courier"),("/F2","/Courier-Bold")}) {
    var font=new PdfDictionary(pdf);font.Elements.SetName("/Type","/Font");font.Elements.SetName("/Subtype","/Type1");
    font.Elements.SetName("/BaseFont",family);font.Elements.SetName("/Encoding","/WinAnsiEncoding");
    pdf.Internals.AddObject(font);fonts.Elements[key]=font.Reference!;
   }
   resources.Elements["/Font"]=fonts;page.Elements["/Resources"]=resources;
   page.Contents.AppendContent().CreateStream(Encoding.ASCII.GetBytes("BT /F1 12 Tf 50 700 Td (abc) Tj /F2 12 Tf (DEF) Tj /F1 12 Tf (ghi) Tj ET"));
   pdf.Save(path);
  }
  document=new(path);
  buffer=ParagraphRunBuffer.FromRuns(document.AnalyzeTextRuns(1,7).Runs.Select(r=>r.Run));
  resolver=document.CreateRunEncodingResolver(1,7);
 }

 [Fact] public void InsertionAtStyleBoundaryInheritsLeftStyleAndDoesNotModifyOriginal() {
  var result=buffer.InsertText(3,"ñ",resolver);
  Assert.True(result.Applied);Assert.True(result.IsEncodingValid);
  Assert.Equal("abcñDEFghi",result.Buffer.Text);Assert.Equal("abcDEFghi",buffer.Text);
  var inserted=Assert.Single(result.Buffer.Runs,r=>r.Text=="ñ");
  Assert.False(inserted.Style.Bold);Assert.Empty(inserted.Sources);Assert.NotNull(inserted.InsertionAnchor);
  var atStart=buffer.InsertText(0,"x",resolver);
  Assert.Equal(buffer.Runs[0].Style,atStart.Buffer.Runs[0].Style);
 }

 [Fact] public void DeletingMiddleStyleMergesOriginalSourcesWithoutDroppingDeletedInventory() {
  var result=buffer.DeleteRange(new(3,3),resolver);
  Assert.True(result.Applied);Assert.True(result.IsEncodingValid);
  var merged=Assert.Single(result.Buffer.Runs);
  Assert.Equal("abcghi",merged.Text);Assert.Equal(2,merged.Sources.Length);
  Assert.Equal(buffer.Runs[2].Sources[0].Source,merged.Sources[1].Source);
  Assert.Equal(buffer.OriginalSources,result.Buffer.OriginalSources);
  Assert.Equal(3,result.Buffer.OriginalSources.Length);
 }

 [Fact] public void PartialDeletionNarrowsTextRangesWithoutInventingTokenByteOffsets() {
  var source=buffer.Runs[0].Sources[0].Source;
  var result=buffer.DeleteRange(new(1,1),resolver);
  Assert.Equal("acDEFghi",result.Buffer.Text);
  var first=result.Buffer.Runs[0];Assert.Equal("ac",first.Text);Assert.Equal(2,first.Sources.Length);
  Assert.Equal(source.ByteRange,first.Sources[1].Source.ByteRange);
  Assert.Equal(2,first.Sources[1].Source.SourceTextRange.Start);
  Assert.Equal(1,first.Sources[1].DraftRange.Start);
 }

 [Fact] public void PlainPastePreservesRepeatedSpacesAndHardBreaks() {
  const string paste="  ñ\r\nX\n\nY\rZ";
  var result=buffer.PasteText(4,paste,resolver);
  Assert.True(result.IsEncodingValid);Assert.Equal("abcD"+paste+"EFghi",result.Buffer.Text);
  Assert.True(Assert.Single(result.Buffer.Runs,r=>r.Text==paste).Style.Bold);
 }

 [Fact] public void EmptyParagraphKeepsExplicitStyleAndAnchorForNewTyping() {
  var empty=buffer.DeleteRange(new(0,buffer.Text.Length),resolver).Buffer;
  Assert.Empty(empty.Runs);Assert.Equal("",empty.Text);
  Assert.Equal(buffer.Runs[0].Style,empty.EmptyStyle);
  var result=empty.InsertText(0,"Nuevo",resolver);
  Assert.True(result.IsEncodingValid);Assert.Equal("Nuevo",result.Buffer.Text);
  Assert.Equal(buffer.Runs[0].Style,Assert.Single(result.Buffer.Runs).Style);
  Assert.Equal(3,result.Buffer.OriginalSources.Length);
 }

 [Fact] public void UnencodableTextRemainsInDraftWithBlockingEncodingDiagnosticsAndCanBeRemoved() {
  var result=buffer.InsertText(3,"😀",resolver);
  Assert.True(result.Applied);Assert.False(result.IsEncodingValid);Assert.Equal("abc😀DEFghi",result.Buffer.Text);
  var diagnostic=Assert.Single(result.Diagnostics);Assert.Equal(new TextRange(3,2),diagnostic.Range);
  var corrected=result.Buffer.DeleteRange(new(3,2),resolver);
  Assert.True(corrected.IsEncodingValid);Assert.Equal(buffer.Text,corrected.Buffer.Text);
 }

 [Theory]
 [InlineData("a\u0301",1)]
 [InlineData("😀",1)]
 [InlineData("\r\n",1)]
 [InlineData("👩‍💻",2)]
 public void EditsCannotSplitGraphemesOrCrLf(string text,int invalidOffset) {
  var draft=buffer.InsertText(0,text,resolver).Buffer;
  var result=draft.DeleteRange(new(invalidOffset,1),resolver);
  Assert.False(result.Applied);Assert.Same(draft,result.Buffer);
  var split=draft.SplitRunAt(invalidOffset,resolver);
  Assert.False(split.Applied);Assert.Same(draft,split.Buffer);
 }

 [Fact] public void SplittingOriginalRunPreservesEverySourceAndDoesNotInsertLineBreak() {
  var result=buffer.SplitRunAt(1,resolver);
  Assert.True(result.Applied);Assert.Equal(buffer.Text,result.Buffer.Text);
  Assert.Equal("a",result.Buffer.Runs[0].Text);Assert.Equal("bc",result.Buffer.Runs[1].Text);
  Assert.Equal(1,result.Buffer.Runs[1].Sources[0].Source.SourceTextRange.Start);
  Assert.Equal(buffer.Runs[0].Sources[0].Source.ByteRange,result.Buffer.Runs[1].Sources[0].Source.ByteRange);
 }

 [Fact] public void InvalidRangeAndInvalidUtf16DoNotApplyPartialMutation() {
  var result=buffer.ReplaceRange(new(0,100),"x",resolver);
  Assert.False(result.Applied);Assert.Same(buffer,result.Buffer);
  var invalid=buffer.InsertText(2,"\uD800",resolver);
  Assert.False(invalid.Applied);Assert.Same(buffer,invalid.Buffer);
  var noOp=buffer.ReplaceRange(new(0,buffer.Text.Length),buffer.Text,resolver);
  Assert.Same(buffer,noOp.Buffer);
 }

 [Fact] public void SeveralInsertionsRetainTheirDistinctAnchors() {
  var first=buffer.InsertText(1,"X",resolver).Buffer;
  var second=first.InsertText(4,"Y",resolver).Buffer;
  var inserted=second.Runs.Where(r=>r.InsertionAnchor is not null).ToArray();
  Assert.Equal(2,inserted.Length);
  Assert.Equal(1,inserted[0].InsertionAnchor!.SourceUtf16Offset);
  Assert.Equal(3,inserted[1].InsertionAnchor!.SourceUtf16Offset);
 }

 [Fact] public void ResolverUsesRealFontIdentityAndRevisionAndNeverReturnsPartialBytes() {
  var font=buffer.Runs[0].Style.Font;
  var encoded=resolver.Encode(font,"Español áéíóú ñ €");
  Assert.True(encoded.IsSupported);Assert.Contains((byte)241,encoded.Bytes);Assert.Contains((byte)128,encoded.Bytes);
  var invalid=resolver.Encode(font,"A😀B");Assert.False(invalid.IsSupported);Assert.Empty(invalid.Bytes);
  var stale=new PdfFontReference(new(font.Context.DocumentId,1,8),font.ResourceScopeId,font.ResourceName);
  Assert.Equal("FontUnavailable",Assert.Single(resolver.Encode(stale,"A").Diagnostics).Code);
  var invented=new PdfFontReference(font.Context,font.ResourceScopeId,"/F99");
  Assert.False(resolver.Encode(invented,"A").IsSupported);
 }

 [Fact] public void ResolverRejectsControlCodesAndNoPdfMutationOccursDuringDraftEditing() {
  var before=document.AnalyzeTextRuns(1,7);
  Assert.False(buffer.InsertText(0,"\t",resolver).IsEncodingValid);
  buffer.InsertText(2,"XYZ",resolver).Buffer.DeleteRange(new(4,2),resolver);
  var after=document.AnalyzeTextRuns(1,7);
  Assert.Equal(string.Concat(before.Runs.Select(r=>r.Run.Text)),string.Concat(after.Runs.Select(r=>r.Run.Text)));
  Assert.Equal(before.Runs[0].Run.Sources[0].Source,after.Runs[0].Run.Sources[0].Source);
 }
 [Fact] public void EmptyAndLineBreakOnlyDraftsStillValidateFontIdentity() {
  var stale=document.CreateRunEncodingResolver(1,8);
  var empty=buffer.DeleteRange(new(0,buffer.Text.Length),resolver).Buffer;
  Assert.NotEmpty(empty.ValidateEncoding(stale));
  var breaks=empty.InsertText(0,"\r\n",resolver).Buffer;
  Assert.NotEmpty(breaks.ValidateEncoding(stale));
 }

 [Fact] public void ResolverChecksDeclaredWidthsRatherThanAssumingFamilyCoverage() {
  var path=Path.Combine(directory,"limited-font.pdf");
  using(var pdf=PdfSharp.Pdf.IO.PdfReader.Open(Path.Combine(directory,"source.pdf"),PdfSharp.Pdf.IO.PdfDocumentOpenMode.Modify)) {
   var font=pdf.Pages[0].Resources.Elements.GetDictionary("/Font")!.Elements.GetDictionary("/F1")!;
   font.Elements.SetInteger("/FirstChar",65);font.Elements.SetInteger("/LastChar",105);
   var widths=new PdfArray(pdf);
   for(var code=65;code<=105;code++) widths.Elements.Add(new PdfInteger(600));
   font.Elements["/Widths"]=widths;pdf.Save(path);
  }
  using var limited=new PdfDocumentSession(path);
  var analysis=limited.AnalyzeTextRuns(1,0);Assert.True(analysis.IsSupported);
  var limitedResolver=limited.CreateRunEncodingResolver(1,0);
  var fontReference=analysis.Runs[0].Run.Style.Font;
  Assert.True(limitedResolver.Encode(fontReference,"abc").IsSupported);
  var missing=limitedResolver.Encode(fontReference,"abcñ");
  Assert.False(missing.IsSupported);Assert.Empty(missing.Bytes);
  Assert.Equal("MissingGlyph",Assert.Single(missing.Diagnostics).Code);
 }

 [Fact] public void ReplacingAcrossStylesPreservesTheRemainingSourceOffsetsAndInheritedStyle() {
  var result=buffer.ReplaceRange(new(2,5),"XY",resolver);
  Assert.Equal("abXYhi",result.Buffer.Text);
  Assert.False(Assert.Single(result.Buffer.Runs,r=>r.Text=="XY").Style.Bold);
  var remaining=result.Buffer.Runs[^1];
  Assert.Equal("hi",remaining.Text);
  Assert.Equal(7,remaining.Sources[0].Source.SourceTextRange.Start);
  Assert.Equal(buffer.OriginalSources,result.Buffer.OriginalSources);
 }

 public void Dispose() {document.Dispose();Directory.Delete(directory,true);}
}
