namespace Mythlix.Core.TextEditing;

/// <summary>Half-open range. Units are defined by its owner: bytes or UTF-16 code units.</summary>
public sealed record TextRange {
 public int Start { get; }
 public int Length { get; }
 public int End => Start + Length;
 public TextRange(int start, int length) {
  if(start < 0 || length < 0 || (long)start + length > int.MaxValue)
   throw new ArgumentOutOfRangeException(nameof(length), "Invalid range or integer overflow.");
  Start = start; Length = length;
 }
}

public sealed record TextDocumentContext {
 public Guid DocumentId { get; }
 public int PageNumber { get; }
 public long Revision { get; }
 public TextDocumentContext(Guid documentId, int pageNumber, long revision) {
  if(documentId == Guid.Empty) throw new ArgumentException("Document identity is required.", nameof(documentId));
  if(pageNumber < 1) throw new ArgumentOutOfRangeException(nameof(pageNumber));
  if(revision < 0) throw new ArgumentOutOfRangeException(nameof(revision));
  DocumentId = documentId; PageNumber = pageNumber; Revision = revision;
 }
}

/// <summary>Range in the unfiltered bytes of a specific content stream at a specific revision.</summary>
public sealed record TextSourceSpan {
 public TextDocumentContext Context { get; }
 public int StreamIndex { get; }
 public TextRange ByteRange { get; }
 public string StreamFingerprint { get; }
 public string BlockId { get; }
 public string BlockFingerprint { get; }
 public TextRange SourceTextRange { get; }
 public TextSourceSpan(TextDocumentContext context, int streamIndex, TextRange byteRange,
  string streamFingerprint, string blockId, string blockFingerprint, TextRange sourceTextRange) {
  ArgumentNullException.ThrowIfNull(context); ArgumentNullException.ThrowIfNull(byteRange);
  ArgumentNullException.ThrowIfNull(sourceTextRange);
  if(streamIndex < 0) throw new ArgumentOutOfRangeException(nameof(streamIndex));
  if(byteRange.Length == 0 || sourceTextRange.Length == 0) throw new ArgumentException("Source spans must contain original content.");
  ArgumentException.ThrowIfNullOrWhiteSpace(streamFingerprint);
  ArgumentException.ThrowIfNullOrWhiteSpace(blockId);
  ArgumentException.ThrowIfNullOrWhiteSpace(blockFingerprint);
  Context=context; StreamIndex=streamIndex; ByteRange=byteRange; StreamFingerprint=streamFingerprint;
  BlockId=blockId; BlockFingerprint=blockFingerprint; SourceTextRange=sourceTextRange;
 }
}

/// <summary>Font resource identity; a family name alone does not identify a PDF font.</summary>
public sealed record PdfFontReference {
 public TextDocumentContext Context { get; }
 public string ResourceScopeId { get; }
 public string ResourceName { get; }
 public PdfFontReference(TextDocumentContext context, string resourceScopeId, string resourceName) {
  ArgumentNullException.ThrowIfNull(context);
  ArgumentException.ThrowIfNullOrWhiteSpace(resourceScopeId);
  ArgumentException.ThrowIfNullOrWhiteSpace(resourceName);
  if(!resourceName.StartsWith('/') || resourceName.Length == 1 || resourceName.Any(char.IsWhiteSpace))
   throw new ArgumentException("Expected a PDF resource name such as /F1.", nameof(resourceName));
  Context=context; ResourceScopeId=resourceScopeId; ResourceName=resourceName;
 }
}
