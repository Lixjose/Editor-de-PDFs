using System.Collections.ObjectModel;
using Avalonia.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Mythlix.Core;

namespace Mythlix.Desktop.ViewModels;

public sealed partial class PageThumbnailViewModel(int number) : ObservableObject, IDisposable {
 public int Number { get; } = number;
 public string Label => "Página " + Number;
 [ObservableProperty, NotifyPropertyChangedFor(nameof(HasImage))] private Bitmap? image;
 public bool HasImage => Image is not null;
 public void Dispose() { Image?.Dispose(); Image = null; }
}

public sealed partial class WorkspaceViewModel {
 public ObservableCollection<OriginalTextBlock> OriginalBlocks { get; } = new();
 public ObservableCollection<OriginalTextBlock> SelectedOriginals { get; } = new();
 public ObservableCollection<PageThumbnailViewModel> PageThumbnails { get; } = new();

 [ObservableProperty] private OriginalTextBlock? selectedOriginal;
 [ObservableProperty] private OriginalTextBlock? activeEditingOriginal;
 [ObservableProperty] private Avalonia.Media.IBrush editingBackground = Avalonia.Media.Brushes.White;
 [ObservableProperty] private string originalDraftText = "";
 [ObservableProperty] private double originalDraftSize = 14;
 [ObservableProperty] private string originalDraftColor = "#000000";
 [ObservableProperty] private bool originalDraftBold;
 [ObservableProperty] private bool originalDraftItalic;
 [ObservableProperty] private int originalDraftAlignmentIndex;
 [ObservableProperty] private string originalDraftFontFamily = "Conservar fuente";
 [ObservableProperty] private string originalStatus = "Seleccione un documento para analizar su texto.";
 [ObservableProperty] private double originalDraftX;
 [ObservableProperty] private double originalDraftY;
 [ObservableProperty] private double originalDraftBaseline;

 public bool HasOriginal => SelectedOriginal is not null || SelectedOriginals.Count > 0;
 public bool IsMultiOriginalSelected => SelectedOriginals.Count > 1;
 public string MultiSelectionLabel => $"{SelectedOriginals.Count} filas de texto seleccionadas";
 public PageThumbnailViewModel? ActiveThumbnail => PageThumbnails.FirstOrDefault(c => c.Number == CurrentPage);
 public string PageCountLabel => Selected is null ? "Sin documento" : Selected.Document.PageCount == 1 ? "1 página" : $"{Selected.Document.PageCount} páginas";
 public string OriginalFontLabel => SelectedOriginal?.FontName ?? (IsMultiOriginalSelected ? "Varias fuentes" : "Fuente original");
 public string OriginalGeometryLabel => SelectedOriginal is {} b ? $"X {b.Bounds.X:0.#} · Y {b.Bounds.Y:0.#} · {b.Bounds.Width:0.#} × {b.Bounds.Height:0.#} pt" : (IsMultiOriginalSelected ? $"{SelectedOriginals.Count} filas seleccionadas" : "");
 public string SelectionLabel => HasOriginal ? (IsMultiOriginalSelected ? $"{SelectedOriginals.Count} filas · alineación y posición" : "Texto original · edición y alineación") : HasGraphic ? "Contenido añadido" : "Seleccione un elemento";

 partial void OnSelectedOriginalChanged(OriginalTextBlock? value) {
  SyncInspectionSelection(value);
  if(value is not null) {
   SelectedGraphic = null;
   if(!SelectedOriginals.Contains(value)) {
    SelectedOriginals.Clear();
    SelectedOriginals.Add(value);
   }
   OriginalDraftText = value.Text;
   OriginalDraftSize = value.FontSize;
   OriginalDraftColor = value.Color;
   OriginalDraftBold = value.Bold;
   OriginalDraftItalic = value.Italic;
   OriginalDraftAlignmentIndex = value.Alignment switch { TextAlignment.Center => 1, TextAlignment.Right => 2, TextAlignment.Justify => 3, _ => 0 };
   OriginalDraftFontFamily = "Conservar fuente";
   OriginalDraftX = Math.Round(value.Bounds.X, 1);
   OriginalDraftY = Math.Round(value.Bounds.Y, 1);
   OriginalDraftBaseline = Math.Round(value.EffectiveBaseline, 1);
  } else if(SelectedOriginals.Count == 0) {
   OriginalDraftText = "";
  }
  NotifySelectionProperties();
 }

 void NotifySelectionProperties() {
  OnPropertyChanged(nameof(HasOriginal));
  OnPropertyChanged(nameof(IsMultiOriginalSelected));
  OnPropertyChanged(nameof(MultiSelectionLabel));
  OnPropertyChanged(nameof(OriginalFontLabel));
  OnPropertyChanged(nameof(OriginalGeometryLabel));
  OnPropertyChanged(nameof(SelectionLabel));
 }

 public void SelectOriginals(IEnumerable<OriginalTextBlock> blocks, bool additive = false) {
  SelectedGraphic=null;
  if(!additive) SelectedOriginals.Clear();
  foreach(var block in blocks)
   if(!SelectedOriginals.Any(b=>b.Id==block.Id)) SelectedOriginals.Add(block);
  SelectedOriginal=SelectedOriginals.LastOrDefault();
  NotifySelectionProperties();
 }

 public void ToggleSelectOriginal(OriginalTextBlock block, bool multiSelect) {
  SelectedGraphic = null;
  if(!multiSelect) {
   SelectedOriginals.Clear();
   SelectedOriginals.Add(block);
   SelectedOriginal = block;
  } else {
   if(SelectedOriginals.Contains(block)) {
    SelectedOriginals.Remove(block);
    SelectedOriginal = SelectedOriginals.LastOrDefault();
   } else {
    SelectedOriginals.Add(block);
    SelectedOriginal = block;
   }
  }
  NotifySelectionProperties();
 }

 [RelayCommand]
 public void ClearOriginalSelection() {
  SelectedOriginals.Clear();
  SelectedOriginal = null;
  NotifySelectionProperties();
 }

 [RelayCommand]
 public void SelectAllOriginals() {
  SelectedOriginals.Clear();
  foreach(var b in OriginalBlocks) SelectedOriginals.Add(b);
  SelectedOriginal = SelectedOriginals.FirstOrDefault();
  NotifySelectionProperties();
 }

 void RefreshOriginal(TextAnalysis analysis) {
  ClearTextInspection();
  var ids = SelectedOriginals.Select(b => b.Id).ToHashSet();
  var primaryId = SelectedOriginal?.Id;
  OriginalBlocks.Clear();
  foreach(var b in analysis.Blocks) OriginalBlocks.Add(b);
  SelectedOriginals.Clear();
  foreach(var b in OriginalBlocks.Where(b => ids.Contains(b.Id))) SelectedOriginals.Add(b);
  SelectedOriginal = OriginalBlocks.FirstOrDefault(b => b.Id == primaryId) ?? SelectedOriginals.FirstOrDefault();
  OriginalStatus = analysis.Explanation;
  NotifySelectionProperties();
 }

 string OriginalBackgroundColor(OriginalTextBlock block) {
  if(!block.Id.StartsWith("v:")) return "#FFFFFF";
  if(PageImage is not {} bitmap) throw new InvalidOperationException("Vuelva a renderizar la página antes de editar el texto.");
  var box = Geometry.ToDisplay(block.Bounds);
  var brush = Controls.EditingBackground.Sample(bitmap,
   new Avalonia.Rect(box.X, box.Y, box.Width, box.Height),
   new Avalonia.Size(Geometry.DisplayWidth, Geometry.DisplayHeight),
   Avalonia.Media.Color.TryParse(block.Color, out var color) ? color : Avalonia.Media.Colors.Black);
  if(brush is not Avalonia.Media.SolidColorBrush solid)
   throw new InvalidOperationException("El fondo del texto no es uniforme; no se puede cubrir sin alterar el diseño.");
  return $"#{solid.Color.R:X2}{solid.Color.G:X2}{solid.Color.B:X2}";
 }

 async Task ReplaceOriginalWithOptionsInternalAsync(TextEditOptions options) {
  if(SelectedOriginal is not {} block || Selected is not {} doc || doc.Document is not IContentEditor editor) return;
  var id = block.Id;
  options = options with { BackgroundColor = OriginalBackgroundColor(block) };
  await Task.Run(() => {
   var before = doc.Document.Snapshot();
   editor.ReplaceText(CurrentPage, block, options);
   doc.Undo.Push(before);
   doc.Redo.Clear();
   LimitUndo(doc);
  });
  doc.Modified = true;
  doc.Revision++;
  await RenderAsync();
  SelectedOriginal = OriginalBlocks.FirstOrDefault(b => b.Id == id);
 }

 public Task ReplaceOriginalWithOptionsAsync(TextEditOptions options) => RunAsync(() => ReplaceOriginalWithOptionsInternalAsync(options));

 public Task MoveBlocksAsync(IReadOnlyList<TextBlockMove> moves) => RunAsync(async () => {
  if(Selected is not {} doc || doc.Document is not IContentEditor editor || moves.Count == 0) return;
  var selectedIds = SelectedOriginals.Select(b => b.Id).ToHashSet();
  await Task.Run(() => {
   var before = doc.Document.Snapshot();
   editor.MoveTextBlocks(CurrentPage, moves);
   doc.Undo.Push(before);
   doc.Redo.Clear();
   LimitUndo(doc);
  });
  doc.Modified = true;
  doc.Revision++;
  await RenderAsync();
  SelectedOriginals.Clear();
  foreach(var b in OriginalBlocks.Where(b => selectedIds.Contains(b.Id))) {
   SelectedOriginals.Add(b);
  }
  SelectedOriginal = SelectedOriginals.FirstOrDefault();
  NotifySelectionProperties();
 });

 public Task CommitParagraphAsync(PdfParagraph paragraph, string text) => RunAsync(async()=> {
  if(Selected is not {} doc || doc.Document is not IContentEditor editor)return;
  var first=paragraph.Lines[0];
  await Task.Run(()=> {
   var before=doc.Document.Snapshot();
   editor.ReplaceParagraph(CurrentPage,paragraph,new TextEditOptions(text,first.FontSize,first.Color,first.Bold,first.Italic,paragraph.Alignment,first.FontName));
   doc.Undo.Push(before);doc.Redo.Clear();LimitUndo(doc);
  });
  doc.Modified=true;doc.Revision++;await RenderAsync();
 });

 public Task CommitOriginalTextDirectAsync(OriginalTextBlock block, string newText, string? fontFamily = null, double? fontSize = null, string? color = null, bool? bold = null, bool? italic = null, double? x = null, double? baseline = null) => RunAsync(async () => {
  if(Selected is not {} doc || doc.Document is not IContentEditor editor) return;
  var options = new TextEditOptions(
   Text: newText,
   FontSize: fontSize ?? block.FontSize,
   Color: string.IsNullOrWhiteSpace(color) ? block.Color : color,
   Bold: bold ?? block.Bold,
   Italic: italic ?? block.Italic,
   Alignment: block.Alignment,
   FontFamily: string.IsNullOrWhiteSpace(fontFamily) || fontFamily == "Conservar fuente" ? block.FontName : fontFamily,
   Strict: false,
   X: x ?? block.Bounds.X,
   Baseline: baseline ?? block.EffectiveBaseline,
   MaxWidth: block.LayoutWidth ?? block.Bounds.Width,
   BackgroundColor: OriginalBackgroundColor(block)
  );
  await Task.Run(() => {
   var before = doc.Document.Snapshot();
   editor.ReplaceText(CurrentPage, block, options);
   doc.Undo.Push(before);
   doc.Redo.Clear();
   LimitUndo(doc);
  });
  doc.Modified = true;
  doc.Revision++;
  await RenderAsync();
 });

 public Task ReplaceOriginalAsync(string text, double size) => ReplaceOriginalWithOptionsAsync(new TextEditOptions(text, size, Strict: false));

 [RelayCommand]
 private Task ApplyOriginalAsync() => RunAsync(async () => {
  if(Selected is not {} doc || doc.Document is not IContentEditor editor) return;
  var alignment = OriginalDraftAlignmentIndex switch {
   1 => TextAlignment.Center,
   2 => TextAlignment.Right,
   3 => TextAlignment.Justify,
   _ => TextAlignment.Left
  };
  var font = OriginalDraftFontFamily is "Conservar fuente" or "" ? (SelectedOriginal?.FontName) : OriginalDraftFontFamily;
  var color = string.IsNullOrWhiteSpace(OriginalDraftColor) ? (SelectedOriginal?.Color ?? "#000000") : OriginalDraftColor;

  if(IsMultiOriginalSelected) {
   var selectedIds = SelectedOriginals.Select(b => b.Id).ToHashSet();
   var backgrounds = SelectedOriginals.ToDictionary(b => b.Id, OriginalBackgroundColor);
   await Task.Run(() => {
    var before = doc.Document.Snapshot();
    foreach(var block in SelectedOriginals) {
     var opts = new TextEditOptions(
      Text: block.Text,
      FontSize: OriginalDraftSize,
      Color: color,
      Bold: OriginalDraftBold,
      Italic: OriginalDraftItalic,
      Alignment: alignment,
      FontFamily: font ?? block.FontName,
      BackgroundColor: backgrounds[block.Id],
      Strict: false
     );
     editor.ReplaceText(CurrentPage, block, opts);
    }
    doc.Undo.Push(before);
    doc.Redo.Clear();
    LimitUndo(doc);
   });
   doc.Modified = true;
   doc.Revision++;
   await RenderAsync();
   SelectedOriginals.Clear();
   foreach(var b in OriginalBlocks.Where(b => selectedIds.Contains(b.Id))) SelectedOriginals.Add(b);
   SelectedOriginal = SelectedOriginals.FirstOrDefault();
   NotifySelectionProperties();
  } else if(SelectedOriginal is {} orig) {
   var opts = new TextEditOptions(
    Text: OriginalDraftText,
    FontSize: OriginalDraftSize,
    Color: color,
    Bold: OriginalDraftBold,
    Italic: OriginalDraftItalic,
    Alignment: alignment,
    FontFamily: font ?? orig.FontName,
    Strict: false,
    X: OriginalDraftX,
    Baseline: OriginalDraftBaseline
   );
   await ReplaceOriginalWithOptionsInternalAsync(opts);
  }
 });

 [RelayCommand]
 private async Task AlignSelectedLeftAsync() {
  if(IsMultiOriginalSelected) {
   var minX = SelectedOriginals.Min(b => b.Bounds.X);
   var moves = SelectedOriginals.Select(b => new TextBlockMove(b, minX, b.EffectiveBaseline)).ToArray();
   await MoveBlocksAsync(moves);
  } else if(SelectedOriginal is {} current) {
   var preceding = OriginalBlocks
    .Where(b => b.Id != current.Id && b.Bounds.Y < current.Bounds.Y - 1)
    .OrderByDescending(b => b.Bounds.Y)
    .FirstOrDefault();
   if(preceding is not null) {
    OriginalDraftX = Math.Round(preceding.Bounds.X, 1);
    await ApplyOriginalAsync();
   }
  }
 }

 [RelayCommand]
 private async Task AlignSelectedCenterAsync() {
  if(IsMultiOriginalSelected) {
   var minX = SelectedOriginals.Min(b => b.Bounds.X);
   var maxX = SelectedOriginals.Max(b => b.Bounds.X + b.Bounds.Width);
   var centerX = (minX + maxX) / 2.0;
   var moves = SelectedOriginals.Select(b => new TextBlockMove(b, Math.Max(0, centerX - b.Bounds.Width / 2.0), b.EffectiveBaseline)).ToArray();
   await MoveBlocksAsync(moves);
  } else if(SelectedOriginal is {} current) {
   OriginalDraftX = Math.Max(0, Math.Round((Geometry.Width - current.Bounds.Width) / 2.0, 1));
   await ApplyOriginalAsync();
  }
 }

 [RelayCommand]
 private async Task AlignSelectedRightAsync() {
  if(IsMultiOriginalSelected) {
   var maxRight = SelectedOriginals.Max(b => b.Bounds.X + b.Bounds.Width);
   var moves = SelectedOriginals.Select(b => new TextBlockMove(b, Math.Max(0, maxRight - b.Bounds.Width), b.EffectiveBaseline)).ToArray();
   await MoveBlocksAsync(moves);
  } else if(SelectedOriginal is {} current) {
   var preceding = OriginalBlocks
    .Where(b => b.Id != current.Id && b.Bounds.Y < current.Bounds.Y - 1)
    .OrderByDescending(b => b.Bounds.Y)
    .FirstOrDefault();
   if(preceding is not null) {
    var precedingRight = preceding.Bounds.X + preceding.Bounds.Width;
    OriginalDraftX = Math.Max(0, Math.Round(precedingRight - current.Bounds.Width, 1));
    await ApplyOriginalAsync();
   }
  }
 }

 [RelayCommand]
 private async Task AlignWithPreviousRowAsync() {
  if(SelectedOriginal is not {} current) return;
  var preceding = OriginalBlocks
   .Where(b => b.Id != current.Id && b.Bounds.Y < current.Bounds.Y - 0.5)
   .OrderByDescending(b => b.Bounds.Y)
   .FirstOrDefault();
  if(preceding is not null) {
   OriginalDraftX = Math.Round(preceding.Bounds.X, 1);
   await ApplyOriginalAsync();
  }
 }

 [RelayCommand]
 private async Task AlignWithNextRowAsync() {
  if(SelectedOriginal is not {} current) return;
  var following = OriginalBlocks
   .Where(b => b.Id != current.Id && b.Bounds.Y > current.Bounds.Y + 0.5)
   .OrderBy(b => b.Bounds.Y)
   .FirstOrDefault();
  if(following is not null) {
   OriginalDraftX = Math.Round(following.Bounds.X, 1);
   await ApplyOriginalAsync();
  }
 }

 [RelayCommand]
 private async Task DistributeVerticalAsync() {
  if(SelectedOriginals.Count < 3) return;
  var sorted = SelectedOriginals.OrderBy(b => b.Bounds.Y).ToList();
  var firstBaseline = sorted[0].EffectiveBaseline;
  var lastBaseline = sorted[^1].EffectiveBaseline;
  var step = (lastBaseline - firstBaseline) / (sorted.Count - 1);

  var moves = new List<TextBlockMove>();
  for(var i = 0; i < sorted.Count; i++) {
   var targetBaseline = firstBaseline + i * step;
   moves.Add(new TextBlockMove(sorted[i], sorted[i].Bounds.X, Math.Round(targetBaseline, 2)));
  }
  await MoveBlocksAsync(moves);
 }

 [RelayCommand]
 private async Task NudgeAsync(string direction) {
  double dx = 0, dy = 0;
  switch(direction) {
   case "left": dx = -1; break;
   case "right": dx = 1; break;
   case "up": dy = -1; break;
   case "down": dy = 1; break;
   case "left5": dx = -5; break;
   case "right5": dx = 5; break;
   case "up5": dy = -5; break;
   case "down5": dy = 5; break;
  }
  if(IsMultiOriginalSelected) {
   var moves = SelectedOriginals.Select(b => new TextBlockMove(b, Math.Max(0, b.Bounds.X + dx), Math.Max(0, b.EffectiveBaseline + dy))).ToArray();
   await MoveBlocksAsync(moves);
  } else if(SelectedOriginal is not null) {
   OriginalDraftX = Math.Max(0, Math.Round(OriginalDraftX + dx, 1));
   OriginalDraftY = Math.Max(0, Math.Round(OriginalDraftY + dy, 1));
   OriginalDraftBaseline = Math.Max(0, Math.Round(OriginalDraftBaseline + dy, 1));
   await ApplyOriginalAsync();
  }
 }

 [RelayCommand]
 private async Task DeleteOriginalAsync() {
  if(IsMultiOriginalSelected) {
   if(Selected is not {} doc || doc.Document is not IContentEditor editor) return;
   await Task.Run(() => {
    var before = doc.Document.Snapshot();
    foreach(var b in SelectedOriginals) {
     editor.ReplaceText(CurrentPage, b, new TextEditOptions("", b.FontSize, Strict: false));
    }
    doc.Undo.Push(before);
    doc.Redo.Clear();
    LimitUndo(doc);
   });
   doc.Modified = true;
   doc.Revision++;
   await RenderAsync();
   ClearOriginalSelection();
  } else {
   await ReplaceOriginalAsync("", OriginalDraftSize);
  }
 }

 async Task RenderThumbnailsAsync(DocumentViewModel doc) {
  var visible = Enumerable.Range(Math.Max(1, CurrentPage - 2), Math.Min(doc.Document.PageCount, CurrentPage + 2) - Math.Max(1, CurrentPage - 2) + 1).ToHashSet();
  foreach(var card in PageThumbnails.Where(c => !visible.Contains(c.Number))) card.Dispose();
  foreach(var card in PageThumbnails.Where(c => visible.Contains(c.Number))) {
   if(card.Image is not null && card.Number != CurrentPage) continue;
   var data = await renderer.RenderAsync(doc.WorkingPath, card.Number, 260, CancellationToken.None);
   var old = card.Image;
   card.Image = new Bitmap(new MemoryStream(data));
   old?.Dispose();
  }
 }

 void ResetThumbnails() {
  foreach(var card in PageThumbnails) card.Dispose();
  PageThumbnails.Clear();
  if(Selected is not null) for(var n = 1; n <= Selected.Document.PageCount; n++) PageThumbnails.Add(new(n));
  OnPropertyChanged(nameof(PageCountLabel));
  OnPropertyChanged(nameof(ActiveThumbnail));
 }
}
