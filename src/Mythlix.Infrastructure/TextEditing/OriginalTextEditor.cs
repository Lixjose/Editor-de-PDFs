using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Mythlix.Core;
using PdfSharp.Pdf;

namespace Mythlix.Infrastructure.TextEditing;

internal static class OriginalTextEditor {
    sealed record Candidate(
        OriginalTextBlock Block,
        int Stream,
        Token TextToken,
        Token SizeToken,
        double[] Widths,
        bool WinAnsi,
        int BtByteStart,
        int BtByteLength,
        string FontResourceName,
        string BaseFont,
        double BaselineX,
        double BaselineY
    );

    static readonly Encoding WindowsEncoding;
    static OriginalTextEditor() {
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        WindowsEncoding = Encoding.GetEncoding(1252, EncoderFallback.ExceptionFallback, DecoderFallback.ExceptionFallback);
    }

    static readonly HashSet<string> Allowed = ["Tf","Tm","Td","TD","Tj","TJ","Tc","Tw","Tz","Ts","Tr","TL","rg","g","k","RG","G","K","[","]"];

    public static TextAnalysis Analyze(PdfPage page, PageGeometry geometry) {
        try {
            var result = Read(page, geometry);
            return new(result.Items.Select(c => c.Block).ToArray(), result.Rejected, result.Message);
        } catch(Exception ex) when(ex is InvalidOperationException or FormatException or DecoderFallbackException or InvalidCastException or ArgumentException) {
            return new([], 0, "No se pudo analizar esta página con seguridad: " + ex.Message);
        }
    }

    static (List<Candidate> Items, int Rejected, string Message) Read(PdfPage page, PageGeometry geometry) {
        var streams = new List<(int Index, byte[] Data, IReadOnlyList<Token> Tokens)>();
        long total = 0;
        for(var i = 0; i < page.Contents.Elements.Count; i++) {
            var dict = page.Contents.Elements.GetDictionary(i)!;
            if(dict.Elements.GetBoolean("/MythlixGraphicsV1") || dict.Elements.GetBoolean("/MythlixOcrV1")) continue;
            var bytes = dict.Stream!.UnfilteredValue;
            total += bytes.Length;
            if(total > 16 * 1024 * 1024) throw new InvalidOperationException("Contenido de página superior a 16 MB.");
            var tokens = ContentTokenizer.Read(bytes);
            streams.Add((i, bytes, tokens));
        }

        var count = streams.Sum(s => s.Tokens.Count(t => t.Value == "BT" && t.Bytes is null));
        var unsafePage = streams.Any(s => s.Tokens.Any(t => t.Bytes is null && t.Value is "cm" or "W" or "W*" or "gs" or "BI" or "BDC" or "BMC"));

        foreach(var stream in streams) {
            for(var n = 0; n < stream.Tokens.Count; n++) {
                var op = stream.Tokens[n];
                if(op.Bytes is not null || op.Value is not ("Tc" or "Tw" or "Ts" or "Tr" or "Tz")) continue;
                if(n == 0 || !stream.Tokens[n - 1].Number(out var value) || Math.Abs(value - (op.Value == "Tz" ? 100 : 0)) > .0001) unsafePage = true;
            }
        }

        if(!geometry.CanEdit || unsafePage) {
            return ([], count, "Edición directa no compatible: esta página usa recortes, transformaciones, marcado o estados gráficos no compatibles con la edición directa inicial.");
        }

        var candidates = new List<Candidate>();
        var rejected = 0;

        foreach(var stream in streams) {
            var ordinal = 0;
            for(var start = 0; start < stream.Tokens.Count; start++) {
                if(stream.Tokens[start].Value != "BT" || stream.Tokens[start].Bytes is not null) continue;
                var id = $"{stream.Index}:{ordinal++}";
                var end = start + 1;
                while(end < stream.Tokens.Count && stream.Tokens[end].Value != "ET") end++;
                if(end == stream.Tokens.Count) { rejected++; break; }

                var block = stream.Tokens.Skip(start + 1).Take(end - start - 1).ToArray();
                var btByteStart = stream.Tokens[start].Start;
                var etEndToken = stream.Tokens[end];
                var btByteLength = (etEndToken.Start + etEndToken.Length) - btByteStart;

                var candidate = TryRead(page, geometry, stream.Index, id, stream.Data, block, btByteStart, btByteLength);
                if(candidate is null) rejected++; else candidates.Add(candidate);
                start = end;
            }
        }

        var inheritedFont = streams.Any(s => s.Tokens.Any(t => t.Bytes is null && t.Value == "Do"));
        foreach(var stream in streams) {
            var inText = false;
            var hasFont = false;
            foreach(var token in stream.Tokens) {
                if(token.Bytes is not null) continue;
                if(token.Value == "BT") { inText = true; hasFont = false; }
                else if(token.Value == "ET") inText = false;
                else if(inText && token.Value == "Tf") hasFont = true;
                else if(inText && token.Value is "Tj" or "TJ" or "'" or "\"") { if(!hasFont) inheritedFont = true; }
            }
        }

        if(inheritedFont) candidates = candidates.Select(c => c with { Block = c.Block with { CanChangeFontSize = false } }).ToList();
        return (candidates, rejected, candidates.Count == 0 ? "Sin bloques compatibles. Fuentes CID/subconjuntos, párrafos fragmentados y texto en formularios XObject necesitan un analizador adicional." : $"{candidates.Count} bloques editables · {rejected} bloques no compatibles. Se conserva la fuente y la posición originales.");
    }

    static Candidate? TryRead(PdfPage page, PageGeometry geometry, int stream, string id, byte[] bytes, Token[] tokens, int btByteStart, int btByteLength) {
        if(tokens.Any(t => t.Bytes is null && !t.Value.StartsWith('/') && !t.Number(out _) && !Allowed.Contains(t.Value))) return null;

        var shows = tokens.Select((t, i) => (t, i)).Where(p => p.t.Bytes is null && p.t.Value is "Tj" or "TJ").ToArray();
        var fonts = tokens.Select((t, i) => (t, i)).Where(p => p.t.Value == "Tf" && p.t.Bytes is null).ToArray();
        var positions = tokens.Select((t, i) => (t, i)).Where(p => p.t.Bytes is null && p.t.Value is "Tm" or "Td" or "TD").ToArray();

        if(shows.Length != 1 || fonts.Length != 1 || positions.Length != 1) return null;
        var show = shows[0];
        var font = fonts[0];
        var position = positions[0];

        if(font.i < 2 || position.i >= show.i || font.i >= show.i || !tokens[font.i - 1].Number(out var size) || size is < 4 or > 144) return null;

        var name = tokens[font.i - 2].Value;
        if(!name.StartsWith('/')) return null;

        var resource = page.Resources.Elements.GetDictionary("/Font")?.Elements.GetDictionary(name);
        if(resource is null || resource.Elements.GetName("/Subtype") != "/Type1" || resource.Elements.ContainsKey("/ToUnicode")) return null;

        var family = resource.Elements.GetName("/BaseFont").TrimStart('/');
        if(!StandardFontMetrics.Fonts.TryGetValue(family, out var metrics)) return null;

        var descriptor = resource.Elements.GetDictionary("/FontDescriptor");
        if(descriptor is not null && new[] { "/FontFile", "/FontFile2", "/FontFile3" }.Any(descriptor.Elements.ContainsKey)) return null;

        var encoding = resource.Elements.GetName("/Encoding");
        if(encoding is not ("" or "/StandardEncoding" or "/WinAnsiEncoding")) return null;
        var winAnsi = encoding == "/WinAnsiEncoding";

        Token text;
        if(show.t.Value == "Tj") {
            if(show.i == 0 || tokens[show.i - 1].Bytes is null) return null;
            text = tokens[show.i - 1];
        } else {
            if(show.i < 3 || tokens[show.i - 1].Value != "]" || tokens[show.i - 2].Bytes is null || tokens[show.i - 3].Value != "[") return null;
            text = tokens[show.i - 2];
        }

        if(text.Bytes!.Any(b => b < 32 || b == 127 || !winAnsi && (b > 126 || b is 39 or 96) || metrics.Widths[b] == 0)) return null;

        double x, y;
        if(position.t.Value == "Tm") {
            if(position.i < 6) return null;
            var values = new double[6];
            for(var i = 0; i < 6; i++) if(!tokens[position.i - 6 + i].Number(out values[i])) return null;
            if(Math.Abs(values[0] - 1) > .0001 || Math.Abs(values[3] - 1) > .0001 || Math.Abs(values[1]) + Math.Abs(values[2]) > .0001) return null;
            x = values[4]; y = values[5];
        } else {
            if(position.i < 2 || !tokens[position.i - 2].Number(out x) || !tokens[position.i - 1].Number(out y)) return null;
        }

        var widths = metrics.Widths.Select(w => (double)w).ToArray();
        if(resource.Elements.GetArray("/Widths") is { } declared) {
            var first = resource.Elements.GetInteger("/FirstChar");
            for(var i = 0; i < widths.Length; i++) widths[i] = i >= first && i - first < declared.Elements.Count ? declared.Elements.GetReal(i - first) : 0;
        }

        if(text.Bytes!.Any(b => !double.IsFinite(widths[b]) || widths[b] <= 0)) return null;

        var width = text.Bytes!.Sum(b => widths[b]) * size / 1000;
        var top = geometry.Height - y - metrics.Ascent * size / 1000;
        var height = (metrics.Ascent - metrics.Descent) * size / 1000;
        if(width <= 0 || x < 0 || top < 0 || x + width > geometry.Width || top + height > geometry.Height) return null;

        // Detect color in block if rg operator exists
        var hexColor = "#000000";
        for(var i = 0; i < tokens.Length; i++) {
            if(tokens[i].Bytes is null && tokens[i].Value == "rg" && i >= 3) {
                if(tokens[i - 3].Number(out var r) && tokens[i - 2].Number(out var g) && tokens[i - 1].Number(out var b)) {
                    var ri = Math.Clamp((int)Math.Round(r * 255), 0, 255);
                    var gi = Math.Clamp((int)Math.Round(g * 255), 0, 255);
                    var bi = Math.Clamp((int)Math.Round(b * 255), 0, 255);
                    hexColor = $"#{ri:X2}{gi:X2}{bi:X2}";
                }
            }
        }

        var bold = family.Contains("Bold", StringComparison.OrdinalIgnoreCase) || family.Contains("Black", StringComparison.OrdinalIgnoreCase);
        var italic = family.Contains("Italic", StringComparison.OrdinalIgnoreCase) || family.Contains("Oblique", StringComparison.OrdinalIgnoreCase);

        var fingerprint = Convert.ToHexString(SHA256.HashData(bytes));
        var value = winAnsi ? WindowsEncoding.GetString(text.Bytes!) : Encoding.ASCII.GetString(text.Bytes!);
        if(value.Any(char.IsControl) || text.Bytes!.Contains((byte)173)) return null;

        var blockObj = new OriginalTextBlock(
            id, value, family, size, new(x, top, width, height), fingerprint,
            CanChangeFontSize: true,
            Color: hexColor,
            Bold: bold,
            Italic: italic,
            Alignment: TextAlignment.Left,
            LineCount: 1
        );

        return new(blockObj, stream, text, tokens[font.i - 1], widths, winAnsi, btByteStart, btByteLength, name.TrimStart('/'), family, x, y);
    }

    public static void Replace(PdfDocument document, PdfPage page, PageGeometry geometry, OriginalTextBlock selected, string replacement, double fontSize) {
        Replace(document, page, geometry, selected, new TextEditOptions(replacement, fontSize, Strict: true));
    }

    public static void Replace(PdfDocument document, PdfPage page, PageGeometry geometry, OriginalTextBlock selected, TextEditOptions options) {
        var candidate = Read(page, geometry).Items.SingleOrDefault(c => c.Block.Id == selected.Id)
            ?? throw new InvalidOperationException("El bloque ya no es compatible; vuelva a analizar la página.");

        if(candidate.Block.Fingerprint != selected.Fingerprint)
            throw new InvalidOperationException("El bloque cambió; vuelva a seleccionarlo antes de editar.");

        if(options.Strict) {
            // Strict Phase 3 backward-compatible single line mode
            if(options.Text.Any(char.IsControl))
                throw new InvalidOperationException("Este bloque admite una sola línea; no use caracteres de control.");
            if(options.Text.Length > 10000 || !double.IsFinite(options.FontSize) || options.FontSize < 4 || options.FontSize > candidate.Block.FontSize)
                throw new InvalidOperationException("Use hasta 10 000 caracteres y un tamaño de 4 pt hasta el tamaño original.");
            if(options.FontSize != candidate.Block.FontSize && !candidate.Block.CanChangeFontSize)
                throw new InvalidOperationException("Este documento hereda tamaños de fuente o utiliza XObjects. Puede reemplazar el texto, pero el cambio de tamaño se bloquea para preservar otros elementos.");

            byte[] encoded;
            try {
                if(!candidate.WinAnsi && options.Text.Any(c => c < 32 || c > 126 || c is (char)39 or (char)96)) throw new EncoderFallbackException();
                encoded = candidate.WinAnsi ? WindowsEncoding.GetBytes(options.Text) : Encoding.ASCII.GetBytes(options.Text);
            } catch(EncoderFallbackException) {
                throw new InvalidOperationException("La fuente original no permite esos caracteres. Use texto compatible con su codificación.");
            }

            if(encoded.Any(b => b < 32 || b == 127 || candidate.Widths[b] <= 0 || !double.IsFinite(candidate.Widths[b]) || b == 173))
                throw new InvalidOperationException("Texto multilínea o glifos sin métricas: operación bloqueada.");

            var width = encoded.Sum(b => candidate.Widths[b]) * options.FontSize / 1000;
            if(width > candidate.Block.Bounds.Width + .01)
                throw new InvalidOperationException("El texto excede el ancho original. Acórtelo o reduzca el tamaño para preservar la distribución.");

            var old = page.Contents.Elements.GetDictionary(candidate.Stream)!;
            var bytes = old.Stream!.UnfilteredValue;
            var edits = new List<(int Start, int Length, byte[] Value)> {
                (candidate.TextToken.Start, candidate.TextToken.Length, ContentTokenizer.Literal(encoded))
            };
            if(options.FontSize != candidate.Block.FontSize) {
                edits.Add((candidate.SizeToken.Start, candidate.SizeToken.Length,
                    Encoding.ASCII.GetBytes(options.FontSize.ToString("G17", CultureInfo.InvariantCulture))));
            }

            foreach(var (start, length, val) in edits.OrderByDescending(e => e.Start)) {
                bytes = bytes[..start].Concat(val).Concat(bytes[(start + length)..]).ToArray();
            }

            var updated = new PdfDictionary(document);
            foreach(var key in old.Elements.Keys) if(key is not ("/Filter" or "/DecodeParms" or "/Length" or "/DL")) updated.Elements[key] = old.Elements[key];
            updated.CreateStream(bytes);
            document.Internals.AddObject(updated);
            page.Contents.Elements[candidate.Stream] = updated.Reference!;
            return;
        }

        // Full Reflow & Formatting Mode
        if(options.Text.Length > 100000 || !double.IsFinite(options.FontSize) || options.FontSize < 4 || options.FontSize > 144)
            throw new InvalidOperationException("Tamaño de fuente: 4–144 pt.");

        var targetBaseFont = FontSubstitutionService.ResolveBaseFont(options.FontFamily ?? candidate.BaseFont, options.Bold, options.Italic);
        var targetMetrics = FontSubstitutionService.GetMetrics(targetBaseFont);
        var targetWidths = targetMetrics.Widths.Select(w => (double)w).ToArray();
        var fontRes = FontSubstitutionService.EnsureFontResource(page, document, targetBaseFont);

        double maxWidth = options.MaxWidth ?? Math.Max(candidate.Block.Bounds.Width, 100);
        var lines = ParagraphReflow.Reflow(options.Text, options.FontSize, targetWidths, WindowsEncoding, maxWidth, options.Alignment);

        var sb = new StringBuilder();
        sb.AppendLine("BT");
        sb.AppendLine($"/{fontRes} {options.FontSize.ToString("G17", CultureInfo.InvariantCulture)} Tf");

        if(!string.IsNullOrEmpty(options.Color) && options.Color.Length == 7 && options.Color[0] == '#') {
            if(uint.TryParse(options.Color.AsSpan(1), NumberStyles.HexNumber, null, out var rgb)) {
                double r = ((rgb >> 16) & 255) / 255.0;
                double g = ((rgb >> 8) & 255) / 255.0;
                double b = (rgb & 255) / 255.0;
                sb.AppendLine($"{r.ToString("0.###", CultureInfo.InvariantCulture)} {g.ToString("0.###", CultureInfo.InvariantCulture)} {b.ToString("0.###", CultureInfo.InvariantCulture)} rg");
            }
        }

        if(lines.Count == 0) {
            sb.AppendLine($"1 0 0 1 {candidate.BaselineX.ToString("G17", CultureInfo.InvariantCulture)} {candidate.BaselineY.ToString("G17", CultureInfo.InvariantCulture)} Tm () Tj");
        } else {
            double leading = options.FontSize * 1.25;
            for(var i = 0; i < lines.Count; i++) {
                var line = lines[i];
                double lineX = candidate.BaselineX + line.XOffset;
                double lineY = candidate.BaselineY - (i * leading);
                sb.AppendLine($"1 0 0 1 {lineX.ToString("G17", CultureInfo.InvariantCulture)} {lineY.ToString("G17", CultureInfo.InvariantCulture)} Tm");
                sb.Append(Encoding.ASCII.GetString(ContentTokenizer.Literal(line.EncodedBytes)));
                sb.AppendLine(" Tj");
            }
        }
        sb.Append("ET");

        var newBytes = Encoding.ASCII.GetBytes(sb.ToString());
        var oldDict = page.Contents.Elements.GetDictionary(candidate.Stream)!;
        var streamBytes = oldDict.Stream!.UnfilteredValue;
        var updatedBytes = streamBytes[..candidate.BtByteStart].Concat(newBytes).Concat(streamBytes[(candidate.BtByteStart + candidate.BtByteLength)..]).ToArray();

        var updatedDict = new PdfDictionary(document);
        foreach(var key in oldDict.Elements.Keys) if(key is not ("/Filter" or "/DecodeParms" or "/Length" or "/DL")) updatedDict.Elements[key] = oldDict.Elements[key];
        updatedDict.CreateStream(updatedBytes);
        document.Internals.AddObject(updatedDict);
        page.Contents.Elements[candidate.Stream] = updatedDict.Reference!;
    }
}
