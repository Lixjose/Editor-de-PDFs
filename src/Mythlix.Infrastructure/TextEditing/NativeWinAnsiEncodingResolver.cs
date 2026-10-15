using System.Collections.Immutable;
using System.Globalization;
using System.Text;
using Mythlix.Core.TextEditing;
using PdfSharp.Pdf;

namespace Mythlix.Infrastructure.TextEditing;

/// <summary>Immutable encoding capability snapshot of resources used by supported native runs.
/// Not a commit validator: the owning session must recheck revision/fingerprints before future writes.</summary>
internal sealed class NativeWinAnsiEncodingResolver : IRunMetricsProvider {
 sealed record Metrics(ImmutableArray<double> Widths,double AscentEm,double DescentEm,string Fingerprint);
 readonly ImmutableDictionary<PdfFontReference,Metrics> fonts;
 static readonly Encoding WinAnsi;
 static NativeWinAnsiEncodingResolver() {
  Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
  WinAnsi=Encoding.GetEncoding(1252,EncoderFallback.ExceptionFallback,DecoderFallback.ExceptionFallback);
 }
 NativeWinAnsiEncodingResolver(ImmutableDictionary<PdfFontReference,Metrics> fonts) => this.fonts=fonts;
 internal static NativeWinAnsiEncodingResolver Create(PdfPage page,NativeRunAnalysis analysis,IEnumerable<PdfFontReference>? additionalFonts=null) {
  var (resources,scope)=NativeRunExtractor.Resources(page);
  var dictionary=resources?.Elements.GetDictionary("/Font");
  var builder=ImmutableDictionary.CreateBuilder<PdfFontReference,Metrics>();
  foreach(var reference in analysis.Runs.Select(r=>r.Run.Style.Font).Concat(additionalFonts??[]).Distinct()) {
   if(reference.Context!=analysis.Context || reference.ResourceScopeId!=scope) throw new InvalidOperationException("El ámbito de recursos cambió.");
   var resource=dictionary?.Elements.GetDictionary(reference.ResourceName);
   var font=NativeRunExtractor.ReadFont(resource);
   var vertical=StandardFontMetrics.Fonts[font.Family];
   var signature=font.Family+"|"+string.Join(",",font.Widths.Select(w=>w.ToString("R",CultureInfo.InvariantCulture)))+"|"+resource;
   var fingerprint=Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(Encoding.UTF8.GetBytes(signature)));
   builder.Add(reference,new(font.Widths.ToImmutableArray(),vertical.Ascent/1000d,-vertical.Descent/1000d,fingerprint));
  }
  return new(builder.ToImmutable());
 }
 public string GetResourceFingerprint(PdfFontReference font) => fonts.TryGetValue(font,out var value)?value.Fingerprint:"unavailable";
 public RunMetricsResult Measure(PdfRunStyle style,string text) {
  ArgumentNullException.ThrowIfNull(style);
  var encoded=Encode(style.Font,text);
  if(!encoded.IsSupported) return new([],[],0,0,encoded.Diagnostics);
  var metrics=fonts[style.Font];
  var advances=encoded.Bytes.Select(b=>(metrics.Widths[b]*style.FontSizePt/1000+style.CharacterSpacingPt+
   (b==32?style.WordSpacingPt:0))*style.HorizontalScale).ToImmutableArray();
  var ascent=metrics.AscentEm*style.FontSizePt;var descent=metrics.DescentEm*style.FontSizePt;
  if(advances.Any(v=>!double.IsFinite(v) || v<=0) || !double.IsFinite(ascent) || !double.IsFinite(descent))
   return new([],[],0,0,[new("UnsupportedMetrics",new(0,text.Length),"Avances no positivos o métricas no finitas fuera del alcance de composición.")]);
  return new(encoded.Bytes,advances,ascent,descent,[]);
 }
 public FontEncodingResult Encode(PdfFontReference font,string text) {
  ArgumentNullException.ThrowIfNull(font);ArgumentNullException.ThrowIfNull(text);
  if(!fonts.TryGetValue(font,out var metrics))
   return new([],[new("FontUnavailable",new(0,text.Length),"La fuente, página o revisión no pertenece a esta instantánea de recursos.")]);
  var bytes=ImmutableArray.CreateBuilder<byte>();
  var errors=ImmutableArray.CreateBuilder<RunEditDiagnostic>();
  var starts=StringInfo.ParseCombiningCharacters(text);
  for(var i=0;i<starts.Length;i++) {
   var start=starts[i];var length=(i+1<starts.Length?starts[i+1]:text.Length)-start;
   byte[] encoded;
   try { encoded=WinAnsi.GetBytes(text.Substring(start,length)); }
   catch(EncoderFallbackException) {errors.Add(new("UnencodableText",new(start,length),"El texto no puede codificarse en la fuente WinAnsi original."));continue;}
   if(encoded.Any(b=>b<32 || b==127 || b==173 || b is 129 or 141 or 143 or 144 or 157 || metrics.Widths[b]<=0 || !double.IsFinite(metrics.Widths[b])))
    errors.Add(new("MissingGlyph",new(start,length),"Código o ancho ausente en la fuente original; no se sustituirá automáticamente."));
   else bytes.AddRange(encoded);
  }
  return errors.Count>0 ? new([],errors.ToImmutable()) : new(bytes.ToImmutable(),[]);
 }
}
