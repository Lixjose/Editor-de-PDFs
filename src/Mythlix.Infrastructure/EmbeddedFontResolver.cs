using System.Collections.Concurrent;
using System.Diagnostics;
using System.Text.RegularExpressions;
using PdfSharp.Fonts;

namespace Mythlix.Infrastructure;

/// <summary>Resolves embedded Liberation Sans and installed system fonts (e.g. Montserrat).</summary>
internal sealed class EmbeddedFontResolver : IFontResolver {
    static readonly ConcurrentDictionary<string, byte[]> FontBytesCache = new(StringComparer.OrdinalIgnoreCase);
    static readonly ConcurrentDictionary<string, string?> SystemFontPathCache = new(StringComparer.OrdinalIgnoreCase);

    internal static void EnsureInitialized() {
        lock(typeof(EmbeddedFontResolver)) {
            if(GlobalFontSettings.FontResolver is null) GlobalFontSettings.FontResolver = new EmbeddedFontResolver();
        }
    }

    public static string CleanFamilyName(string familyName) {
        if(string.IsNullOrWhiteSpace(familyName)) return "Liberation Sans";
        var cleaned = Regex.Replace(familyName.TrimStart('/'), @"^[A-Z]{6}\+", "");
        // Remove common style suffixes if attached directly to family
        cleaned = Regex.Replace(cleaned, @"-(Regular|Bold|Italic|BoldItalic|SemiBold|Medium|Light|Black|ExtraBold)$", "", RegexOptions.IgnoreCase);
        cleaned = Regex.Replace(cleaned, @",(Bold|Italic|Regular)$", "", RegexOptions.IgnoreCase);
        return cleaned.Trim();
    }

    public FontResolverInfo ResolveTypeface(string familyName, bool bold, bool italic) {
        var cleanFamily = CleanFamilyName(familyName);
        var styleSuffix = bold && italic ? "BoldItalic" : bold ? "Bold" : italic ? "Italic" : "Regular";

        if(string.IsNullOrWhiteSpace(cleanFamily) || cleanFamily.Equals("Liberation Sans", StringComparison.OrdinalIgnoreCase) || cleanFamily.Equals("LiberationSans", StringComparison.OrdinalIgnoreCase)) {
            return new("LiberationSans-" + styleSuffix);
        }

        var path = FindSystemFont(cleanFamily, bold, italic);
        if(path is not null && File.Exists(path)) {
            var faceName = $"SysFont_{cleanFamily.Replace(" ", "")}_{styleSuffix}";
            return new(faceName);
        }

        return new("LiberationSans-" + styleSuffix);
    }

    public byte[] GetFont(string faceName) {
        if(FontBytesCache.TryGetValue(faceName, out var cached)) return cached;

        if(faceName.StartsWith("LiberationSans-", StringComparison.OrdinalIgnoreCase)) {
            using var stream = typeof(EmbeddedFontResolver).Assembly.GetManifestResourceStream($"Mythlix.Infrastructure.Fonts.{faceName}.ttf")
                ?? typeof(EmbeddedFontResolver).Assembly.GetManifestResourceStream("Mythlix.Infrastructure.Fonts.LiberationSans-Regular.ttf")
                ?? throw new InvalidOperationException($"No se encontró la fuente incorporada: {faceName}");
            using var memory = new MemoryStream();
            stream.CopyTo(memory);
            var bytes = memory.ToArray();
            FontBytesCache[faceName] = bytes;
            return bytes;
        }

        if(faceName.StartsWith("SysFont_", StringComparison.OrdinalIgnoreCase)) {
            var parts = faceName[8..].Split('_');
            if(parts.Length >= 2) {
                var family = parts[0];
                var style = parts[1];
                var bold = style.Contains("Bold", StringComparison.OrdinalIgnoreCase);
                var italic = style.Contains("Italic", StringComparison.OrdinalIgnoreCase);
                var path = FindSystemFont(family, bold, italic);
                if(path is not null && File.Exists(path)) {
                    var bytes = File.ReadAllBytes(path);
                    FontBytesCache[faceName] = bytes;
                    return bytes;
                }
            }
        }

        return GetFont("LiberationSans-Regular");
    }

    static string? FindSystemFont(string family, bool bold, bool italic) {
        var styleSuffix = bold && italic ? "BoldItalic" : bold ? "Bold" : italic ? "Italic" : "Regular";
        var key = $"{family}_{styleSuffix}";
        if(SystemFontPathCache.TryGetValue(key, out var cached)) return cached;

        string? path = null;

        if(OperatingSystem.IsLinux()) {
            if(family.Equals("Montserrat", StringComparison.OrdinalIgnoreCase)) {
                var direct = $"/usr/share/fonts/julietaula-montserrat-fonts/Montserrat-{styleSuffix}.otf";
                if(File.Exists(direct)) path = direct;
                else {
                    var regular = "/usr/share/fonts/julietaula-montserrat-fonts/Montserrat-Regular.otf";
                    if(File.Exists(regular)) path = regular;
                }
            }

            if(path is null) {
                try {
                    var styleArg = bold && italic ? ":style=Bold Italic" : bold ? ":style=Bold" : italic ? ":style=Italic" : ":style=Regular";
                    var psi = new ProcessStartInfo("fc-match", ["-f", "%{file}\n", family + styleArg]) {
                        RedirectStandardOutput = true,
                        UseShellExecute = false,
                        CreateNoWindow = true
                    };
                    using var proc = Process.Start(psi);
                    if(proc != null) {
                        var line = proc.StandardOutput.ReadLine()?.Trim();
                        proc.WaitForExit(1000);
                        if(!string.IsNullOrEmpty(line) && File.Exists(line) && (line.Contains(family, StringComparison.OrdinalIgnoreCase) || line.EndsWith(".ttf") || line.EndsWith(".otf"))) {
                            path = line;
                        }
                    }
                } catch {}
            }
        } else if(OperatingSystem.IsWindows()) {
            var fontsDir = Environment.GetFolderPath(Environment.SpecialFolder.Fonts);
            var direct = Path.Combine(fontsDir, $"{family}-{styleSuffix}.ttf");
            if(File.Exists(direct)) path = direct;
            else {
                var cand = Directory.EnumerateFiles(fontsDir, $"{family}*.ttf").FirstOrDefault();
                if(cand is not null) path = cand;
            }
        }

        SystemFontPathCache[key] = path;
        return path;
    }
}
