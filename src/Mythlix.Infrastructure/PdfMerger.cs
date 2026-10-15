using Mythlix.Core;
using PdfSharp.Pdf;
using PdfSharp.Pdf.IO;

namespace Mythlix.Infrastructure;

public sealed class PdfMerger : IPdfMerger {
    public async Task MergeAsync(string destination, IReadOnlyList<string> sourcePdfs, CancellationToken token = default, IProgress<int>? progress = null) {
        token.ThrowIfCancellationRequested();
        if(sourcePdfs.Count < 2)
            throw new ArgumentException("Seleccione al menos dos documentos para combinar.", nameof(sourcePdfs));

        foreach(var path in sourcePdfs) {
            if(!File.Exists(path))
                throw new FileNotFoundException("No se encontró el documento: " + path, path);
            if(new FileInfo(path).Length > 256L * 1024 * 1024)
                throw new InvalidOperationException("Uno de los documentos supera el límite de 256 MB.");
        }

        var destinationFull = Path.GetFullPath(destination);
        var parent = Path.GetDirectoryName(destinationFull) ?? throw new ArgumentException("Destino inválido.", nameof(destination));
        if(!Directory.Exists(parent))
            throw new DirectoryNotFoundException("La carpeta contenedora no existe.");

        var temporary = destinationFull + "." + Guid.NewGuid().ToString("N") + ".tmp";
        using var output = new PdfDocument();

        try {
            for(var i = 0; i < sourcePdfs.Count; i++) {
                token.ThrowIfCancellationRequested();
                var sourcePath = sourcePdfs[i];
                using var input = PdfReader.Open(sourcePath, PdfDocumentOpenMode.Import);

                // Check signed
                if(input.Internals.GetAllObjects().OfType<PdfDictionary>().Any(d => d.Elements.ContainsKey("/ByteRange"))) {
                    throw new InvalidOperationException("No se puede combinar el PDF firmado: " + Path.GetFileName(sourcePath));
                }

                for(var p = 0; p < input.PageCount; p++) {
                    token.ThrowIfCancellationRequested();
                    output.AddPage(input.Pages[p]);
                }

                progress?.Report(i + 1);
            }

            token.ThrowIfCancellationRequested();
            using var buffer = new MemoryStream();
            output.Save(buffer, false);
            await File.WriteAllBytesAsync(temporary, buffer.ToArray(), token);
            token.ThrowIfCancellationRequested();
            File.Move(temporary, destinationFull, true);
        } finally {
            if(File.Exists(temporary)) File.Delete(temporary);
        }
    }
}
