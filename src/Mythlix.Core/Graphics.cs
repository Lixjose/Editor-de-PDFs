namespace Mythlix.Core;

public enum GraphicKind { Text, Image, Rectangle, Ellipse, Line }

/// <summary>New page content owned by Mythlix, not an original PDF object or annotation.</summary>
public sealed record GraphicObject {
 public Guid Id { get; init; } = Guid.NewGuid();
 public GraphicKind Kind { get; init; }
 public double X { get; init; } = 72;
 public double Y { get; init; } = 144;
 public double Width { get; init; } = 240;
 public double Height { get; init; } = 72;
 public string Text { get; init; } = "Texto nuevo";
 public double FontSize { get; init; } = 18;
 public bool Bold { get; init; }
 public bool Italic { get; init; }
 public string Color { get; init; } = "#4F46E5";
 public string? FillColor { get; init; }
 public double StrokeWidth { get; init; } = 2;
 public byte[]? ImageData { get; init; }
 public TextAlignment Alignment { get; init; } = TextAlignment.Left;
 public string FontFamily { get; init; } = "Liberation Sans";
 /// <summary>Distance from <see cref="Y"/> to the first text baseline. When set, text is drawn on exact baselines.</summary>
 public double? Baseline { get; init; }
 public string Label => Kind switch {
  GraphicKind.Text => string.IsNullOrWhiteSpace(Text) ? "Texto" : Text[..Math.Min(Text.Length, 28)],
  GraphicKind.Image => "Imagen", GraphicKind.Rectangle => "Rectángulo",
  GraphicKind.Ellipse => "Elipse", _ => "Línea"
 };
 public void Validate(PageGeometry page) {
  if(!Enum.IsDefined(Kind) || !Enum.IsDefined(Alignment) || Id == Guid.Empty || Text is null || Color is null || FontFamily is null)
   throw new InvalidOperationException("El objeto contiene propiedades inválidas.");
  var numbers = new[] { X, Y, Width, Height, FontSize, StrokeWidth };
  if(numbers.Any(v => !double.IsFinite(v)) || X < 0 || Y < 0 || Width < 4 || Height < 4 || X + Width > page.Width + .01 || Y + Height > page.Height + .01)
   throw new InvalidOperationException("El objeto debe quedar dentro de la página, con dimensiones de al menos 4 pt.");
  if(FontSize is < 4 or > 144 || StrokeWidth is < 0 or > 20)
   throw new InvalidOperationException("Tamaño de fuente: 4–144 pt. Trazo: 0–20 pt.");
  if(Color.Length != 7 || Color[0] != '#' || !uint.TryParse(Color.AsSpan(1), System.Globalization.NumberStyles.HexNumber, null, out _))
   throw new InvalidOperationException("Use un color hexadecimal #RRGGBB.");
  if(FillColor is not null && (FillColor.Length != 7 || FillColor[0] != '#' || !uint.TryParse(FillColor.AsSpan(1), System.Globalization.NumberStyles.HexNumber, null, out _)))
   throw new InvalidOperationException("Use un color de relleno hexadecimal #RRGGBB.");
  if(Text.Length > 10000) throw new InvalidOperationException("Límite de texto: 10 000 caracteres por objeto.");
  if(Kind == GraphicKind.Image && (ImageData is null || ImageData.Length is 0 or > 10 * 1024 * 1024))
   throw new InvalidOperationException("La imagen debe tener un máximo de 10 MB.");
 }
}

public readonly record struct ObjectBounds(double X, double Y, double Width, double Height) {
 public bool Contains(double x, double y) => x >= X && x <= X + Width && y >= Y && y <= Y + Height;
}

/// <summary>Coordinates in PDF points, measured from the unrotated page's top-left.</summary>
public readonly record struct PageGeometry(double Width, double Height, int Rotation, bool CanEdit = true) {
 public double DisplayWidth => Rotation is 90 or 270 ? Height : Width;
 public double DisplayHeight => Rotation is 90 or 270 ? Width : Height;
 public ObjectBounds ToDisplay(ObjectBounds b) => Rotation switch {
  90 => new(Height - b.Y - b.Height, b.X, b.Height, b.Width),
  180 => new(Width - b.X - b.Width, Height - b.Y - b.Height, b.Width, b.Height),
  270 => new(b.Y, Width - b.X - b.Width, b.Height, b.Width), _ => b
 };
 public ObjectBounds FromDisplay(ObjectBounds b) => Rotation switch {
  90 => new(b.Y, Height - b.X - b.Width, b.Height, b.Width),
  180 => new(Width - b.X - b.Width, Height - b.Y - b.Height, b.Width, b.Height),
  270 => new(Width - b.Y - b.Height, b.X, b.Height, b.Width), _ => b
 };
 public GraphicObject Clamp(GraphicObject obj) {
  var w = Math.Clamp(obj.Width, 4, Width); var h = Math.Clamp(obj.Height, 4, Height);
  return obj with { X = Math.Clamp(obj.X, 0, Width - w), Y = Math.Clamp(obj.Y, 0, Height - h), Width = w, Height = h };
 }
}
