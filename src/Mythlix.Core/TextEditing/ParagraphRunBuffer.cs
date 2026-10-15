using System.Collections.Immutable;
using System.Globalization;

namespace Mythlix.Core.TextEditing;

/// <summary>Immutable, PDF-independent draft. Offsets are UTF-16; edits must respect grapheme boundaries.</summary>
public sealed class ParagraphRunBuffer {
 public ImmutableArray<PdfTextRun> Runs { get; }
 /// <summary>Original source inventory, including deleted fragments, for later stale-selection validation.</summary>
 public ImmutableArray<TextSourceSpan> OriginalSources { get; }
 public PdfRunStyle EmptyStyle { get; }
 public RunInsertionAnchor EmptyAnchor { get; }
 public TextDocumentContext Context => EmptyStyle.Font.Context;
 public string Text { get; }

 public ParagraphRunBuffer(IEnumerable<PdfTextRun> runs, PdfRunStyle emptyStyle, RunInsertionAnchor emptyAnchor)
  : this(runs.ToImmutableArray(),emptyStyle,emptyAnchor,default) { }

 ParagraphRunBuffer(ImmutableArray<PdfTextRun> runs, PdfRunStyle style, RunInsertionAnchor anchor,
  ImmutableArray<TextSourceSpan> originals) {
  ArgumentNullException.ThrowIfNull(style);ArgumentNullException.ThrowIfNull(anchor);
  if(style.Font.Context!=anchor.Source.Context) throw new ArgumentException("Empty style and anchor contexts differ.");
  if(runs.Any(r=>r is null || r.Style.Font.Context!=style.Font.Context)) throw new ArgumentException("Run contexts differ.");
  // Phase 1 accepts exact source slices or inserted runs with one anchor. Ambiguous
  // many-to-many edited mappings must not be silently cropped as if they were 1:1.
  foreach(var run in runs) {
   if(run.Sources.Length>0) {
    if(run.InsertionAnchor is not null) throw new ArgumentException("Mixed source/anchor mapping is not supported yet.");
    var end=0;
    foreach(var source in run.Sources) {
     if(source.DraftRange.Start!=end || source.DraftRange.Length!=source.Source.SourceTextRange.Length)
      throw new ArgumentException("Exact, ordered source mapping is required.");
     end=source.DraftRange.End;
    }
    if(end!=run.Text.Length) throw new ArgumentException("Source mapping must cover the run.");
   }
  }
  Text=string.Concat(runs.Select(r=>r.Text));
  // Reject invalid UTF-16, rather than accepting replacement characters later.
  for(var i=0;i<Text.Length;i++) {
   if(char.IsHighSurrogate(Text[i])) {if(i+1>=Text.Length || !char.IsLowSurrogate(Text[++i])) throw new ArgumentException("Invalid UTF-16.");}
   else if(char.IsLowSurrogate(Text[i])) throw new ArgumentException("Invalid UTF-16.");
  }
  Runs=runs;EmptyStyle=style;EmptyAnchor=anchor;
  OriginalSources=originals.IsDefault ? runs.SelectMany(r=>r.Sources.Select(s=>s.Source))
   .Concat(runs.Where(r=>r.InsertionAnchor is not null).Select(r=>r.InsertionAnchor!.Source))
   .Append(anchor.Source).Distinct().ToImmutableArray() : originals;
 }

 public static ParagraphRunBuffer FromRuns(IEnumerable<PdfTextRun> runs) {
  ArgumentNullException.ThrowIfNull(runs);
  var copy=runs.ToImmutableArray();
  if(copy.Length==0) throw new ArgumentException("An empty paragraph requires an explicit writing style and anchor.",nameof(runs));
  if(copy.Any(r=>r is null)) throw new ArgumentException("Null run.",nameof(runs));
  var first=copy[0];
  var anchor=first.InsertionAnchor ?? new RunInsertionAnchor(first.Sources[0].Source,first.Sources[0].Source.SourceTextRange.Start);
  return new(copy,first.Style,anchor,default);
 }

 /// <summary>Restore an explicit empty-line writing style without discarding the complete source inventory.</summary>
 public ParagraphRunBuffer WithEmptyStyle(PdfRunStyle style)=>new(Runs,style,EmptyAnchor,OriginalSources);

 public RunEditResult InsertText(int offset,string text,IFontEncodingResolver resolver) => ReplaceRange(new(offset,0),text,resolver);
 public RunEditResult PasteText(int offset,string text,IFontEncodingResolver resolver) => InsertText(offset,text,resolver);
 public RunEditResult DeleteRange(TextRange range,IFontEncodingResolver resolver) => ReplaceRange(range,"",resolver);

 public RunEditResult ReplaceRange(TextRange range,string text,IFontEncodingResolver resolver) {
  ArgumentNullException.ThrowIfNull(range);ArgumentNullException.ThrowIfNull(text);ArgumentNullException.ThrowIfNull(resolver);
  if(range.End>Text.Length || !Boundary(range.Start) || !Boundary(range.End))
   return Reject("InvalidBoundary",range,"El rango debe respetar los límites del texto y sus grafemas.");
  if(Text.AsSpan(range.Start,range.Length).SequenceEqual(text.AsSpan())) return new(this,true,ValidateEncoding(resolver));
  // Inherit the style before deletion, so replacing a whole paragraph keeps its first style.
  var (style,anchor)=InsertionPoint(range.Start);
  var next=ImmutableArray.CreateBuilder<PdfTextRun>();
  next.AddRange(Slice(0,range.Start));
  if(text.Length>0) next.Add(new(Guid.NewGuid(),text,style,[],true,anchor));
  next.AddRange(Slice(range.End,Text.Length-range.End));
  ParagraphRunBuffer buffer;
  try {buffer=new(Merge(next.ToImmutable()),style,anchor,OriginalSources);}
  catch(ArgumentException) {return Reject("InvalidUnicode",range,"El texto insertado contiene UTF-16 inválido.");}
  return new(buffer,true,buffer.ValidateEncoding(resolver));
 }

 /// <summary>Splits a draft fragment at a legal boundary; does not insert a hard line break.</summary>
 public RunEditResult SplitRunAt(int offset,IFontEncodingResolver resolver) {
  ArgumentNullException.ThrowIfNull(resolver);
  if(offset<0 || offset>Text.Length || !Boundary(offset)) return Reject("InvalidBoundary",new(Math.Max(0,offset),0),"La división debe respetar un límite de grafema.");
  var next=Slice(0,offset).Concat(Slice(offset,Text.Length-offset)).ToImmutableArray();
  var buffer=new ParagraphRunBuffer(next,EmptyStyle,EmptyAnchor,OriginalSources);
  return new(buffer,true,buffer.ValidateEncoding(resolver));
 }

 /// <summary>Hard breaks are preserved in Text, but never emitted as PDF string control bytes.</summary>
 public ImmutableArray<RunEditDiagnostic> ValidateEncoding(IFontEncodingResolver resolver) {
  ArgumentNullException.ThrowIfNull(resolver);
  var diagnostics=ImmutableArray.CreateBuilder<RunEditDiagnostic>();
  int baseOffset=0;
  if(Runs.IsEmpty) return resolver.Encode(EmptyStyle.Font, "").Diagnostics;
  foreach(var run in Runs) {
   var fontCheck=resolver.Encode(run.Style.Font, "");
   if(!fontCheck.IsSupported) {
    foreach(var error in fontCheck.Diagnostics) diagnostics.Add(error with {Range=new(baseOffset,run.Text.Length)});
    baseOffset+=run.Text.Length;continue;
   }
   int start=0;
   for(var i=0;i<=run.Text.Length;i++) {
    if(i<run.Text.Length && run.Text[i] is not ('\r' or '\n')) continue;
    if(i>start) {
     var encoded=resolver.Encode(run.Style.Font,run.Text[start..i]);
     foreach(var error in encoded.Diagnostics)
      diagnostics.Add(error with {Range=new(baseOffset+start+error.Range.Start,error.Range.Length)});
    }
    start=i+1;
   }
   baseOffset+=run.Text.Length;
  }
  return diagnostics.ToImmutable();
 }

 bool Boundary(int index) => index>=0 && index<=Text.Length &&
  (index==Text.Length || Array.BinarySearch(StringInfo.ParseCombiningCharacters(Text),index)>=0);
 RunEditResult Reject(string code,TextRange range,string message) => new(this,false,[new(code,range,message)]);

 (PdfRunStyle Style,RunInsertionAnchor Anchor) InsertionPoint(int offset) {
  int start=0;
  foreach(var run in Runs) {
   if(offset<=start+run.Text.Length) {
    var local=offset-start;
    if(run.InsertionAnchor is {} inserted) return (run.Style,inserted);
    foreach(var source in run.Sources) {
     if(local<=source.DraftRange.End) return (run.Style,new(source.Source,
      source.Source.SourceTextRange.Start+Math.Max(0,local-source.DraftRange.Start)));
    }
   }
   start+=run.Text.Length;
  }
  return (EmptyStyle,EmptyAnchor);
 }

 IEnumerable<PdfTextRun> Slice(int start,int length) {
  int position=0,end=start+length;
  foreach(var run in Runs) {
   int left=Math.Max(start,position),right=Math.Min(end,position+run.Text.Length);
   if(right>left) {
    int local=left-position,count=right-left;
    if(local==0 && count==run.Text.Length) {yield return run;}
    else {
     var sources=ImmutableArray.CreateBuilder<RunSourceSlice>();
     foreach(var slice in run.Sources) {
      int a=Math.Max(local,slice.DraftRange.Start),b=Math.Min(local+count,slice.DraftRange.End);
      if(b<=a) continue;
      var old=slice.Source;
      // The original token bytes remain the containing range: UTF-16 offsets
      // are narrowed independently; escaped PDF literals do not permit byte slicing.
      var source=new TextSourceSpan(old.Context,old.StreamIndex,old.ByteRange,old.StreamFingerprint,old.BlockId,old.BlockFingerprint,
       new(old.SourceTextRange.Start+a-slice.DraftRange.Start,b-a));
      sources.Add(new(new(a-local,b-a),source));
     }
     yield return new(Guid.NewGuid(),run.Text.Substring(local,count),run.Style,sources.ToImmutable(),true,run.InsertionAnchor);
    }
   }
   position+=run.Text.Length;
  }
 }

 static ImmutableArray<PdfTextRun> Merge(ImmutableArray<PdfTextRun> runs) {
  var result=new List<PdfTextRun>();
  foreach(var run in runs) {
   if(result.Count>0 && result[^1].Style==run.Style && result[^1].InsertionAnchor==run.InsertionAnchor) {
    var previous=result[^1];
    var sources=previous.Sources.Concat(run.Sources.Select(s=>new RunSourceSlice(new(s.DraftRange.Start+previous.Text.Length,s.DraftRange.Length),s.Source)));
    result[^1]=new(Guid.NewGuid(),previous.Text+run.Text,run.Style,sources,true,run.InsertionAnchor);
   } else result.Add(run);
  }
  return result.ToImmutableArray();
 }
}
