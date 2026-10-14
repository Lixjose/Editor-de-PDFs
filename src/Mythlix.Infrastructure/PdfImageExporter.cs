using Mythlix.Core;
namespace Mythlix.Infrastructure;

/// <summary>Sequential local PNG export, published only when every requested page succeeds.</summary>
public sealed class PdfImageExporter(IPdfRenderer renderer) {
 public async Task<IReadOnlyList<string>> ExportAsync(string source, int pageCount, IReadOnlyList<int> pages,
  string destinationFolder, int longestSide, CancellationToken token = default, IProgress<int>? progress = null) {
  token.ThrowIfCancellationRequested();
  var selection = pages.ToArray();
  if(selection.Length == 0 || selection.Distinct().Count() != selection.Length || selection.Any(p => p < 1 || p > pageCount))
   throw new ArgumentException("Seleccione páginas válidas sin repetir.", nameof(pages));
  if(longestSide < 128 || longestSide > 2400) throw new ArgumentOutOfRangeException(nameof(longestSide), "Use entre 128 y 2400 píxeles.");
  var destination = Path.GetFullPath(destinationFolder);
  if(Directory.Exists(destination) || File.Exists(destination)) throw new IOException("El destino ya existe. Elija una carpeta nueva.");
  var parent = Path.GetDirectoryName(destination) ?? throw new ArgumentException("Destino inválido.", nameof(destinationFolder));
  if(!Directory.Exists(parent)) throw new DirectoryNotFoundException("La carpeta contenedora no existe.");
  var staging = Path.Combine(parent, ".mythlix-images-" + Guid.NewGuid().ToString("N"));
  var names = new List<string>();
  Directory.CreateDirectory(staging);
  try {
   foreach(var page in selection) {
    token.ThrowIfCancellationRequested();
    var png = await renderer.RenderAsync(source, page, longestSide, token);
    var name = $"{names.Count + 1:D4}-pagina-{page:D4}.png";
    await File.WriteAllBytesAsync(Path.Combine(staging, name), png, token);
    names.Add(name);progress?.Report(names.Count);
   }
   token.ThrowIfCancellationRequested();
   Directory.Move(staging, destination);
   return names.Select(name => Path.Combine(destination, name)).ToArray();
  } finally { if(Directory.Exists(staging)) Directory.Delete(staging, true); }
 }
}
