using System.Globalization;
using Mythlix.Core.TextEditing;

namespace Mythlix.Desktop.ViewModels;

/// <summary>Plain-text control adapter: preserve untouched runs by applying only the changed range.
/// Grapheme boundaries are widened in both old and new text before delegating to the buffer.</summary>
public sealed class ParagraphDraftEditor {
 public ParagraphRunBuffer Buffer { get; private set; }
 readonly IFontEncodingResolver resolver;
 readonly Stack<ParagraphRunBuffer> undo=new(),redo=new();
 public ParagraphDraftEditor(ParagraphRunBuffer buffer,IFontEncodingResolver resolver) {Buffer=buffer;this.resolver=resolver;}
 public RunEditResult UpdateText(string text,TextRange? replacedSelection=null) {
  ArgumentNullException.ThrowIfNull(text);
  var old=Buffer.Text;
  if(old==text) return new(Buffer,false,Buffer.ValidateEncoding(resolver));
  if(replacedSelection is {} selection && selection.End<=old.Length) {
   var insertedLength=text.Length-(old.Length-selection.Length);
   if(insertedLength>=0 && text.StartsWith(old[..selection.Start],StringComparison.Ordinal) && text.EndsWith(old[selection.End..],StringComparison.Ordinal))
    return Apply(selection,text.Substring(selection.Start,insertedLength));
  }
  var prefix=0;
  while(prefix<old.Length && prefix<text.Length && old[prefix]==text[prefix]) prefix++;
  var suffix=0;
  while(suffix<old.Length-prefix && suffix<text.Length-prefix && old[old.Length-1-suffix]==text[text.Length-1-suffix]) suffix++;
  var oldBoundaries=StringInfo.ParseCombiningCharacters(old).Append(old.Length).ToHashSet();
  var newBoundaries=StringInfo.ParseCombiningCharacters(text).Append(text.Length).ToHashSet();
  while(prefix>0 && (!oldBoundaries.Contains(prefix) || !newBoundaries.Contains(prefix))) prefix--;
  while(suffix>0 && (!oldBoundaries.Contains(old.Length-suffix) || !newBoundaries.Contains(text.Length-suffix))) suffix--;
  return Apply(new(prefix,old.Length-prefix-suffix),text.Substring(prefix,text.Length-prefix-suffix));
 }
 RunEditResult Apply(TextRange range,string text) {
  var result=Buffer.ReplaceRange(range,text,resolver);
  if(result.Applied) {undo.Push(Buffer);redo.Clear();Buffer=result.Buffer;}
  return result;
 }
 public bool Undo() {if(!undo.TryPop(out var previous))return false;redo.Push(Buffer);Buffer=previous;return true;}
 public bool Redo() {if(!redo.TryPop(out var next))return false;undo.Push(Buffer);Buffer=next;return true;}
}
