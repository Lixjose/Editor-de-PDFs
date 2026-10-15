namespace Mythlix.Core;

public sealed record OcrWord(string Text, double Confidence, double X, double Y, double Width, double Height) {
    public int Block { get; init; }
    public int Paragraph { get; init; }
    public int Line { get; init; }
    public double BaselineY { get; init; }
    public double EstimatedFontSize { get; init; }
    public bool Bold { get; init; }
    public bool Italic { get; init; }
    public string? FontFamily { get; init; }
}

public sealed record OcrPageResult(int ImageWidth, int ImageHeight, IReadOnlyList<OcrWord> Words) {
    public string Text {
        get {
            var text = new System.Text.StringBuilder();
            OcrWord? previous = null;
            foreach(var word in Words.Where(w => !string.IsNullOrWhiteSpace(w.Text))) {
                if(previous is not null) {
                    var structured = word.Line > 0 && previous.Line > 0;
                    var newLine = structured
                        ? (word.Block, word.Paragraph, word.Line) != (previous.Block, previous.Paragraph, previous.Line)
                        : Math.Abs(word.Y - previous.Y) > Math.Max(word.Height, previous.Height) * .6 || word.X < previous.X;
                    text.Append(newLine ? "\n" : " ");
                }
                text.Append(word.Text);
                previous = word;
            }
            return text.ToString();
        }
    }

    public void Validate() {
        if(ImageWidth is < 1 or > 6000 || ImageHeight is < 1 or > 6000 || (long)ImageWidth * ImageHeight > 24_000_000 || Words.Count > 20000)
            throw new InvalidOperationException("Resultado OCR fuera del límite de recursos.");
        foreach(var w in Words) {
            if(w.Text.Length > 500 || w.Text.Any(char.IsControl) ||
               new[] { w.X, w.Y, w.Width, w.Height, w.Confidence, w.BaselineY, w.EstimatedFontSize }.Any(v => !double.IsFinite(v)) ||
               w.X < 0 || w.Y < 0 || w.Width <= 0 || w.Height <= 0 ||
               w.X + w.Width > ImageWidth + .01 || w.Y + w.Height > ImageHeight + .01 ||
               w.Confidence is < 0 or > 100)
                throw new InvalidOperationException("El resultado OCR contiene palabras o coordenadas inválidas.");
        }
    }
}

public enum OcrPageMode { Automatic = 3, SingleColumn = 4, UniformBlock = 6, SparseText = 11 }

public sealed record OcrOptions(OcrPageMode Mode = OcrPageMode.Automatic) {
    public void Validate() {
        if(!Enum.IsDefined(Mode)) throw new ArgumentException("Modo OCR inválido.");
    }
}
