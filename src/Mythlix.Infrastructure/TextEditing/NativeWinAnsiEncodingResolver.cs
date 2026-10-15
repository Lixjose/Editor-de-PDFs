using System.Collections.Immutable;
using System.Globalization;
using System.Text;
using Mythlix.Core.TextEditing;
using PdfSharp.Pdf;

namespace Mythlix.Infrastructure.TextEditing;

/// <summary>Immutable encoding capability snapshot of resources used by supported native runs.
/// Not a commit validator: the owning session must recheck revision/fingerprints before future writes.</summary>
internal sealed class NativeWinAnsiEncodingResolver : IFontEncodingResolver {
 readonly ImmutableDictionary<PdfFontReference,ImmutableArray<double>> fonts;
 static readonly Encoding WinAnsi;
 static NativeWinAnsiEncodingResolver() {
  Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
  WinAnsi=Encoding.GetEncoding(1252,EncoderFallback.ExceptionFallback,DecoderFallback.ExceptionFallback);
 }
 NativeWinAnsiEncodingResolver(ImmutableDictionary<PdfFontReference,ImmutableArray<double>> fonts) => this.fonts=fonts;
 internal static NativeWinAnsiEncodingResolver Create(PdfPage page,NativeRunAnalysis analysis) {
  var (resources,scope)=NativeRunExtractor.Resources(page);
  var dictionary=resources?.Elements.GetDictionary("/Font");
  var builder=ImmutableDictionary.CreateBuilder<PdfFontReference,ImmutableArray<double>>();
  foreach(var reference in analysis.Runs.Select(r=>r.Run.Style.Font).Distinct()) {
   if(reference.ResourceScopeId!=scope) throw new InvalidOperationException("El ámbito de recursos cambió.");
   var font=NativeRunExtractor.ReadFont(dictionary?.Elements.GetDictionary(reference.ResourceName));
   builder.Add(reference,font.Widths.ToImmutableArray());
  }
  return new(builder.ToImmutable());
 }
 public FontEncodingResult Encode(PdfFontReference font,string text) {
  ArgumentNullException.ThrowIfNull(font);ArgumentNullException.ThrowIfNull(text);
  if(!fonts.TryGetValue(font,out var widths))
   return new([],[new("FontUnavailable",new(0,text.Length),"La fuente, página o revisión no pertenece a esta instantánea de recursos.")]);
  var bytes=ImmutableArray.CreateBuilder<byte>();
  var errors=ImmutableArray.CreateBuilder<RunEditDiagnostic>();
  var starts=StringInfo.ParseCombiningCharacters(text);
  for(var i=0;i<starts.Length;i++) {
   var start=starts[i];var length=(i+1<starts.Length?starts[i+1]:text.Length)-start;
   byte[] encoded;
   try { encoded=WinAnsi.GetBytes(text.Substring(start,length)); }
   catch(EncoderFallbackException) {errors.Add(new("UnencodableText",new(start,length),"El texto no puede codificarse en la fuente WinAnsi original."));continue;}
   if(encoded.Any(b=>b<32 || b==127 || b==173 || b is 129 or 141 or 143 or 144 or 157 || widths[b]<=0 || !double.IsFinite(widths[b])))
    errors.Add(new("MissingGlyph",new(start,length),"Código o ancho ausente en la fuente original; no se sustituirá automáticamente."));
   else bytes.AddRange(encoded);
  }
  return errors.Count>0 ? new([],errors.ToImmutable()) : new(bytes.ToImmutable(),[]);
 }
}
