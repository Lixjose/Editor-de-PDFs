using System.Collections.Immutable;

namespace Mythlix.Core.TextEditing;

/// <summary>A draft range retaining its original source; lengths may differ after editing.</summary>
public sealed record RunSourceSlice {
 public TextRange DraftRange { get; }
 public TextSourceSpan Source { get; }
 public RunSourceSlice(TextRange draftRange, TextSourceSpan source) {
  ArgumentNullException.ThrowIfNull(draftRange); ArgumentNullException.ThrowIfNull(source);
  DraftRange=draftRange; Source=source;
 }
}

/// <summary>Inserted text has an anchor, never a fabricated original byte range.</summary>
public sealed record RunInsertionAnchor {
 public TextSourceSpan Source { get; }
 public int SourceUtf16Offset { get; }
 public RunInsertionAnchor(TextSourceSpan source, int sourceUtf16Offset) {
  ArgumentNullException.ThrowIfNull(source);
  if(sourceUtf16Offset < source.SourceTextRange.Start || sourceUtf16Offset > source.SourceTextRange.End)
   throw new ArgumentOutOfRangeException(nameof(sourceUtf16Offset));
  Source=source; SourceUtf16Offset=sourceUtf16Offset;
 }
}

/// <summary>Immutable text draft, not an editable PDF object. No glyph metrics or byte mapping are invented.</summary>
public sealed record PdfTextRun {
 public Guid DraftId { get; }
 public string Text { get; }
 public PdfRunStyle Style { get; }
 public ImmutableArray<RunSourceSlice> Sources { get; }
 public RunInsertionAnchor? InsertionAnchor { get; }
 public bool IsEdited { get; }
 public PdfTextRun(Guid draftId, string text, PdfRunStyle style, IEnumerable<RunSourceSlice> sources,
  bool isEdited = false, RunInsertionAnchor? insertionAnchor = null) {
  if(draftId == Guid.Empty) throw new ArgumentException("Draft identity is required.", nameof(draftId));
  ArgumentNullException.ThrowIfNull(text); ArgumentNullException.ThrowIfNull(style);
  ArgumentNullException.ThrowIfNull(sources);
  var copy=sources.ToImmutableArray();
  if(copy.Any(s => s is null)) throw new ArgumentException("Null source slice.", nameof(sources));
  foreach(var slice in copy) {
   if(slice.DraftRange.End > text.Length) throw new ArgumentException("Source slice exceeds draft text.", nameof(sources));
   if(slice.Source.Context != style.Font.Context) throw new ArgumentException("Source and font contexts differ.", nameof(sources));
   if(!IsUtf16Boundary(text,slice.DraftRange.Start) || !IsUtf16Boundary(text,slice.DraftRange.End))
    throw new ArgumentException("Source slice splits a surrogate pair.", nameof(sources));
  }
  if(insertionAnchor is not null && insertionAnchor.Source.Context != style.Font.Context)
   throw new ArgumentException("Insertion anchor and font contexts differ.", nameof(insertionAnchor));
  if(!isEdited && (copy.Length == 0 || insertionAnchor is not null))
   throw new ArgumentException("Unedited runs require original sources and no insertion anchor.");
  if(isEdited && copy.Length == 0 && insertionAnchor is null)
   throw new ArgumentException("Inserted runs require an explicit source anchor.");
  if(!isEdited) {
   var position=0;
   foreach(var slice in copy) {
    if(slice.DraftRange.Start != position || slice.DraftRange.Length != slice.Source.SourceTextRange.Length)
     throw new ArgumentException("Unedited source slices must cover the draft in order without gaps or overlap.", nameof(sources));
    position=slice.DraftRange.End;
   }
   if(position != text.Length) throw new ArgumentException("Unedited sources must cover all text.", nameof(sources));
  }
  DraftId=draftId; Text=text; Style=style; Sources=copy; IsEdited=isEdited; InsertionAnchor=insertionAnchor;
 }
 static bool IsUtf16Boundary(string text,int index) => index==0 || index==text.Length ||
  !char.IsHighSurrogate(text[index-1]) || !char.IsLowSurrogate(text[index]);
}
