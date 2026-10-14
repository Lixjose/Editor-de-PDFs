using Mythlix.Core;
using Mythlix.Infrastructure;
namespace Mythlix.Desktop.ViewModels;
public sealed partial class WorkspaceViewModel {
 public async Task<IReadOnlyList<string>> ExportImagesAsync(string selection, int longestSide, string destination,
  CancellationToken token = default, IProgress<int>? progress = null) {
  var doc = Selected ?? throw new InvalidOperationException("Abra un PDF antes de exportar.");
  var pages = PageSelection.Parse(selection, doc.Document.PageCount);
  if(longestSide < 128 || longestSide > 2400) throw new ArgumentOutOfRangeException(nameof(longestSide));
  await PrepareWorkingCopyAsync(doc, token);
  // Export sequentially without filling the interactive viewer's cache.
  return await new PdfImageExporter(poppler).ExportAsync(doc.WorkingPath, doc.Document.PageCount, pages, destination, longestSide, token, progress);
 }
}
