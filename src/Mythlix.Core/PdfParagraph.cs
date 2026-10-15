namespace Mythlix.Core;

public sealed record PdfParagraph(IReadOnlyList<OriginalTextBlock> Lines, ObjectBounds Bounds, TextAlignment Alignment, double LineHeight) {
 public string Text => string.Join(" ",Lines.Select(b=>b.Text.Trim()));
 public static PdfParagraph Around(OriginalTextBlock selected, IEnumerable<OriginalTextBlock> blocks) {
  var candidates=blocks.Where(b=>!b.Id.StartsWith("v:") && b.FontName==selected.FontName && b.Bold==selected.Bold && b.Italic==selected.Italic
   && b.CharacterSpacing==selected.CharacterSpacing && b.HorizontalScale==selected.HorizontalScale && b.Color==selected.Color && Math.Abs(b.FontSize-selected.FontSize)<0.25 && b.LineCount==1)
   .OrderBy(b=>b.EffectiveBaseline).ToArray();
  var lines=new List<OriginalTextBlock>{selected};
  var index=Array.FindIndex(candidates,b=>b.Id==selected.Id);
  bool Compatible(OriginalTextBlock a, OriginalTextBlock b) {
   var gap=b.EffectiveBaseline-a.EffectiveBaseline;
   return gap>=selected.FontSize && gap<=selected.FontSize*1.6
    && Math.Min(a.Bounds.X+a.Bounds.Width,b.Bounds.X+b.Bounds.Width)>Math.Max(a.Bounds.X,b.Bounds.X)
    && Math.Abs(a.Bounds.X-b.Bounds.X)<=1.5;
  }
  if(index>=0) {
   for(var i=index-1;i>=0 && Compatible(candidates[i],lines[0]);i--) lines.Insert(0,candidates[i]);
   for(var i=index+1;i<candidates.Length && Compatible(lines[^1],candidates[i]);i++) lines.Add(candidates[i]);
  }
  var leading=lines.Count>1?lines[1].EffectiveBaseline-lines[0].EffectiveBaseline:selected.FontSize*1.25;
  // Stop before a change of leading; avoid combining unrelated nearby paragraphs.
  var consistent=1;
  while(consistent<lines.Count && Math.Abs(lines[consistent].EffectiveBaseline-lines[consistent-1].EffectiveBaseline-leading)<=1.5)consistent++;
  if(consistent<lines.Count && lines.Take(consistent).Any(b=>b.Id==selected.Id)) lines=lines.Take(consistent).ToList();
  else if(consistent<lines.Count) lines=[selected];
  var left=lines.Min(b=>b.Bounds.X);var top=lines.Min(b=>b.Bounds.Y);
  var right=lines.Max(b=>b.Bounds.X+b.Bounds.Width);var bottom=lines.Max(b=>b.Bounds.Y+b.Bounds.Height);
  var justify=lines.Count>=3 && lines.Take(lines.Count-1).All(b=>Math.Abs(b.Bounds.X-left)<=1.5 && Math.Abs(b.Bounds.X+b.Bounds.Width-right)<=1.5)
   && lines.Take(lines.Count-1).Any(b=>b.WordSpacing>0 || b.Text.Contains(' '));
  return new(lines,new(left,top,right-left,bottom-top),justify?TextAlignment.Justify:selected.Alignment,leading);
 }
}
