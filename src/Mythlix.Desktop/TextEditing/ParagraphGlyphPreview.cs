using System.Collections.Immutable;
using System.Security.Cryptography;
using Avalonia;
using Avalonia.Media;
using Avalonia.Media.TextFormatting;
using Avalonia.Media.Fonts;
using Mythlix.Core.TextEditing;

namespace Mythlix.Desktop.TextEditing;

/// <summary>An explicit preview-only association. Standard PDF fonts do not identify physical outlines.
/// Owns the loaded face; keep it alive until all previews using it have been disposed.</summary>
public sealed class PreviewFontBinding : IDisposable {
 sealed class PhysicalFontCollection : FontCollectionBase {
  public override Uri Key { get; } = new("fonts:mythlix-preview-"+Guid.NewGuid().ToString("N"));
 }
 public PdfFontReference Resource { get; }
 public string Sha256 { get; }
 public string Description { get; }
 public GlyphTypeface Face { get; }
 public PreviewFontBinding(PdfFontReference resource, ReadOnlySpan<byte> fontBytes, string description) {
  ArgumentNullException.ThrowIfNull(resource);
  ArgumentException.ThrowIfNullOrWhiteSpace(description);
  var bytes=fontBytes.ToArray();
  using var stream=new MemoryStream(bytes,false);
  var collection=new PhysicalFontCollection();
  if(!collection.TryAddGlyphTypeface(stream,out var face))
   throw new ArgumentException("Archivo de fuente física no compatible.",nameof(fontBytes));
  Face=face;Resource=resource;Description=description;
  Sha256=Convert.ToHexString(SHA256.HashData(bytes));
 }
 public void Dispose()=>Face.Dispose();
}

/// <summary>Consumes existing line breaks and page-point advances. No shaping, reflow, PDF writes or fallback.
/// Physical outlines are explicitly supplied preview substitutes, not proof of original PDF fidelity.</summary>
public sealed class ParagraphGlyphPreview : IDisposable {
 public sealed record Item(ComposedRunSegment Segment, PreviewFontBinding Font, GlyphRun Glyphs);
 public ImmutableArray<Item> Items { get; }
 ParagraphGlyphPreview(ImmutableArray<Item> items)=>Items=items;
 public static ParagraphGlyphPreview Create(ParagraphCompositionSnapshot plan,IEnumerable<PreviewFontBinding> fonts) {
  ArgumentNullException.ThrowIfNull(plan);ArgumentNullException.ThrowIfNull(fonts);
  if(!plan.IsValid) throw new ArgumentException("La composición contiene diagnósticos.",nameof(plan));
  var bindings=fonts.ToDictionary(f=>f.Resource);
  var items=ImmutableArray.CreateBuilder<Item>();
  try {
   foreach(var segment in plan.Lines.SelectMany(l=>l.Segments)) {
    var style=segment.Owner.Style;
    if(style.Font.Context!=plan.Context || !bindings.TryGetValue(style.Font,out var binding))
     throw new InvalidOperationException("Falta asociación física explícita para el recurso y revisión.");
    var text=segment.Text;
    if(text.Length!=segment.AdvancesPt.Length || text.Length!=segment.EncodedBytes.Length ||
       text.Length!=segment.ExtraSpacingPt.Length)
     throw new InvalidOperationException("El segmento no tiene un mapeo WinAnsi 1:1.");
    var glyphs=new GlyphInfo[text.Length];
    for(var i=0;i<text.Length;i++) {
     if(char.IsControl(text[i]) || char.IsSurrogate(text[i]) ||
        !binding.Face.CharacterToGlyphMap.TryGetGlyph(text[i],out var gid) || gid==0)
      throw new InvalidOperationException($"Glifo físico ausente en {binding.Sha256}, U+{(int)text[i]:X4}.");
     // The drawing transform scales outlines and advances. Undo Tz here exactly once.
     var advance=segment.AdvancesPt[i]/style.HorizontalScale;
     if(!double.IsFinite(advance) || advance<=0) throw new InvalidOperationException("Avance visual inválido.");
     glyphs[i]=new GlyphInfo(gid,i,advance,default);
    }
    items.Add(new(segment,binding,new GlyphRun(binding.Face,style.FontSizePt,text.AsMemory(),glyphs,new Point(0,0))));
   }
   return new(items.ToImmutable());
  } catch {
   foreach(var item in items) item.Glyphs.Dispose();
   throw;
  }
 }
 /// <summary>Caller applies page zoom separately. All coordinates here are page points.</summary>
 public void Draw(DrawingContext context) {
  foreach(var item in Items) {
   var segment=item.Segment;var style=segment.Owner.Style;var rgb=style.FillColor;
   var brush=new SolidColorBrush(Color.FromRgb((byte)Math.Round(rgb.Red*255),(byte)Math.Round(rgb.Green*255),(byte)Math.Round(rgb.Blue*255)));
   using(context.PushTransform(new Matrix(style.HorizontalScale,0,0,1,segment.StartX,segment.BaselineY-style.BaselineRisePt)))
    context.DrawGlyphRun(brush,item.Glyphs);
  }
 }
 public void Dispose() {foreach(var item in Items) item.Glyphs.Dispose();}
}
