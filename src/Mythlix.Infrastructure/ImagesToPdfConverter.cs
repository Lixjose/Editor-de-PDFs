using PdfSharp.Drawing;
using PdfSharp.Pdf;
namespace Mythlix.Infrastructure;
public enum ImagePdfPaper { A4, Letter }
public sealed class ImagesToPdfConverter {
 public async Task ConvertAsync(IReadOnlyList<string> sources, string destination, ImagePdfPaper paper = ImagePdfPaper.A4,
  double margin = 24, CancellationToken token = default, IProgress<int>? progress = null) {
  token.ThrowIfCancellationRequested();
  var paths = sources.ToArray();
  if(paths.Length is < 1 or > 100) throw new ArgumentException("Seleccione entre 1 y 100 imágenes.", nameof(sources));
  if(!Enum.IsDefined(paper) || !double.IsFinite(margin) || margin < 0 || margin > 100) throw new ArgumentException("Papel o margen inválido (0–100 pt).");
  var outputPath = Path.GetFullPath(destination);
  if(File.Exists(outputPath) || Directory.Exists(outputPath)) throw new IOException("El destino ya existe. Elija un nombre nuevo.");
  var temporary = outputPath + "." + Guid.NewGuid().ToString("N") + ".tmp";
  using var pdf = new PdfDocument();
  try {
   foreach(var path in paths) {
    token.ThrowIfCancellationRequested();
    var extension = Path.GetExtension(path).ToLowerInvariant();
    if(extension is not (".png" or ".jpg" or ".jpeg" or ".bmp")) throw new ArgumentException("Use imágenes PNG, JPEG o BMP.");
    if(new FileInfo(path).Length is 0 or > 10 * 1024 * 1024) throw new InvalidOperationException("Límite por imagen: 10 MB.");
    var bytes = await File.ReadAllBytesAsync(path, token);
    if(bytes.Length is 0 or > 10 * 1024 * 1024) throw new InvalidOperationException("Límite por imagen: 10 MB.");
    using var memory = new MemoryStream(bytes, 0, bytes.Length, false, true);
    using var image = XImage.FromStream(memory);
    if(image.PixelWidth < 1 || image.PixelHeight < 1 || (long)image.PixelWidth * image.PixelHeight > 25_000_000)
     throw new InvalidOperationException("Límite por imagen: 25 megapíxeles.");
    var page = pdf.AddPage();page.Size = paper == ImagePdfPaper.A4 ? PdfSharp.PageSize.A4 : PdfSharp.PageSize.Letter;
    if(image.PixelWidth > image.PixelHeight) page.Orientation = PdfSharp.PageOrientation.Landscape;
    var scale = Math.Min((page.Width.Point - margin * 2) / image.PixelWidth, (page.Height.Point - margin * 2) / image.PixelHeight);
    var width = image.PixelWidth * scale;var height = image.PixelHeight * scale;
    using(var graphics = XGraphics.FromPdfPage(page)) graphics.DrawImage(image, (page.Width.Point-width)/2, (page.Height.Point-height)/2, width, height);
    progress?.Report(pdf.PageCount);
   }
   token.ThrowIfCancellationRequested();
   using(var stream = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None)) pdf.Save(stream, false);
   token.ThrowIfCancellationRequested();
   File.Move(temporary, outputPath, false);
  } finally { if(File.Exists(temporary)) File.Delete(temporary); }
 }
}
