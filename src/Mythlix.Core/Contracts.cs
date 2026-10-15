namespace Mythlix.Core;
public interface IPdfDocument : IDisposable {
 int PageCount { get; }
 byte[] Snapshot();
 void Restore(byte[] data);
 void Rotate(int page);
 void Delete(int page);
 void AddBlank();
 void DuplicatePage(int page);
 void MovePage(int page, int destination);
 void InsertPages(string path, int afterPage);
 Task ExtractPagesAsync(string destination, IReadOnlyList<int> pages, CancellationToken token = default);
 Task<IReadOnlyList<string>> SplitAsync(string destinationFolder, int pagesPerPart, CancellationToken token = default, IProgress<int>? progress = null);
 PageGeometry GetPageGeometry(int page);
 IReadOnlyList<GraphicObject> GetGraphics(int page);
 void SetGraphics(int page, IReadOnlyList<GraphicObject> objects);
 Task SaveCopyAsync(string destination, CancellationToken token = default);
}
public interface IPdfRenderer {
 Task<byte[]> RenderAsync(string path, int page, int width, CancellationToken token);
 Task<string> ExtractTextAsync(string path, int page, CancellationToken token);
}
public interface IContentEditor {
 TextAnalysis AnalyzeText(int page);
 void ReplaceText(int page, OriginalTextBlock block, string replacement, double fontSize);
 void ReplaceText(int page, OriginalTextBlock block, TextEditOptions options);
 /// <summary>Moves several text lines as one atomic operation (all or nothing).</summary>
 void MoveTextBlocks(int page, IReadOnlyList<TextBlockMove> moves);
 /// <summary>Ascent/descent of the page fonts keyed by base font name (subset prefix removed).</summary>
 IReadOnlyDictionary<string, FontVerticalMetrics> GetFontMetrics(int page);
}
public interface IOcrEngine {
 Task<IReadOnlyList<string>> LanguagesAsync(CancellationToken token);
 Task<OcrPageResult> RecognizeAsync(byte[] image, string language, CancellationToken token);
 Task<OcrPageResult> RecognizeAsync(byte[] image, string language, OcrOptions options, CancellationToken token);
}
public interface ISearchablePdfEditor {
 void SetOcr(int page, OcrPageResult result);
 void SetOcrPages(IReadOnlyList<(int Page,OcrPageResult Result)> pages,CancellationToken token,IProgress<int>? progress=null);
}
