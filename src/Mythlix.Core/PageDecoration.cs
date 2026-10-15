namespace Mythlix.Core;

public enum HeaderFooterPosition {
    TopLeft,
    TopCenter,
    TopRight,
    BottomLeft,
    BottomCenter,
    BottomRight
}

public sealed record HeaderFooterOptions(
    string Format = "Página {n} de {total}",
    HeaderFooterPosition Position = HeaderFooterPosition.BottomCenter,
    double FontSize = 10,
    string Color = "#4B5563",
    string FontFamily = "Liberation Sans",
    double MarginX = 36,
    double MarginY = 36,
    bool SkipFirstPage = false,
    IReadOnlyList<int>? Pages = null
) {
    public void Validate() {
        if(string.IsNullOrWhiteSpace(Format) || Format.Length > 200)
            throw new ArgumentException("El formato de encabezado/pie debe tener entre 1 y 200 caracteres.");
        if(FontSize is < 4 or > 72 || !double.IsFinite(FontSize))
            throw new ArgumentException("Tamaño de fuente: 4 a 72 pt.");
        if(Color.Length != 7 || Color[0] != '#' || !uint.TryParse(Color.AsSpan(1), System.Globalization.NumberStyles.HexNumber, null, out _))
            throw new ArgumentException("Color inválido. Use formato hexadecimal #RRGGBB.");
        if(!Enum.IsDefined(Position))
            throw new ArgumentException("Posición inválida.");
    }
}

public sealed record WatermarkOptions(
    string Text = "CONFIDENCIAL",
    double Rotation = 45,
    double FontSize = 54,
    string Color = "#DC2626",
    double Opacity = 0.25,
    bool OnTop = false,
    IReadOnlyList<int>? Pages = null
) {
    public void Validate() {
        if(string.IsNullOrWhiteSpace(Text) || Text.Length > 200)
            throw new ArgumentException("El texto de la marca de agua debe tener entre 1 y 200 caracteres.");
        if(FontSize is < 8 or > 200 || !double.IsFinite(FontSize))
            throw new ArgumentException("Tamaño de fuente de marca de agua: 8 a 200 pt.");
        if(Opacity is < 0.01 or > 1.0 || !double.IsFinite(Opacity))
            throw new ArgumentException("Opacidad: 1% a 100% (0.01 a 1.0).");
        if(Color.Length != 7 || Color[0] != '#' || !uint.TryParse(Color.AsSpan(1), System.Globalization.NumberStyles.HexNumber, null, out _))
            throw new ArgumentException("Color inválido. Use formato hexadecimal #RRGGBB.");
    }
}

public interface IPdfMerger {
    Task MergeAsync(string destination, IReadOnlyList<string> sourcePdfs, CancellationToken token = default, IProgress<int>? progress = null);
}

public interface IPageDecorator {
    void ApplyHeaderFooter(HeaderFooterOptions options);
    void ApplyWatermark(WatermarkOptions options);
}
