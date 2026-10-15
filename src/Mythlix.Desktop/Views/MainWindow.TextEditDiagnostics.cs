using System.Diagnostics;
using Avalonia;
using Avalonia.Threading;

namespace Mythlix.Desktop.Views;

public partial class MainWindow {
 async Task CheckTextEditLatencyAsync() {
  await Dispatcher.UIThread.InvokeAsync(UpdateLayout, DispatcherPriority.Background);
  // Exercise the commit transition independently of background detection.
  var block = vm.OriginalBlocks.First(b => !b.Id.StartsWith("v:") && b.Text.StartsWith("Seleccione"));
  var bounds = vm.Geometry.ToDisplay(block.Bounds);
  var scale = PdfCanvas.Bounds.Width / vm.Geometry.DisplayWidth;
  activeEditingOriginal = block;
  vm.ActiveEditingOriginal = block;
  vm.EditingBackground = Avalonia.Media.Brushes.White;
  FitEditorToBox(block.Text, block.FontSize*scale, new Rect(bounds.X*scale,bounds.Y*scale,bounds.Width*scale,bounds.Height*scale));
  DirectEditor.IsVisible = true;
  DirectEditor.Text = "Texto editado";
  var oldImage = vm.PageImage;
  var watch = Stopwatch.StartNew();
  long imageMilliseconds = -1;
  bool flickered = false;
  void CheckOverlay(object? sender, EventArgs args) {
   if(ReferenceEquals(oldImage, vm.PageImage) && (!DirectEditor.IsVisible || vm.ActiveEditingOriginal is null)) flickered = true;
  }
  var timer = new DispatcherTimer {Interval=TimeSpan.FromMilliseconds(10)};
  timer.Tick += CheckOverlay;
  void Changed(object? sender, System.ComponentModel.PropertyChangedEventArgs args) {
   if(args.PropertyName == nameof(vm.PageImage)) imageMilliseconds = watch.ElapsedMilliseconds;
  }
  vm.PropertyChanged += Changed;
  timer.Start();
  try {
   var commit = CommitDirectEditAsync();
   CheckOverlay(null, EventArgs.Empty);
   await commit;
   if(flickered || imageMilliseconds<0 || DirectEditor.IsVisible || !vm.PageText.Contains("Texto editado"))
    throw new InvalidOperationException($"Transición inválida: flicker={flickered}, image_ms={imageMilliseconds}, status={vm.Status}");
   Console.WriteLine($"MYTHLIX_TEXT_EDIT_SMOKE_OK image_ms={imageMilliseconds} total_ms={watch.ElapsedMilliseconds} flicker=false");
  } finally {timer.Stop();vm.PropertyChanged-=Changed;}
 }
}
