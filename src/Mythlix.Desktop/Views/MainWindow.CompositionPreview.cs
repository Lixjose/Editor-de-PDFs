using Avalonia.Interactivity;
using Mythlix.Core;
using Mythlix.Core.TextEditing;
using Mythlix.Infrastructure;

namespace Mythlix.Desktop.Views;

public partial class MainWindow {
 async void OnCompositionPreview(object? sender,RoutedEventArgs args) {
  await Execute(async()=>await CreateCompositionPreview().ShowDialog(this));
 }
 internal CompositionPreviewWindow CreateCompositionPreview() {
   var doc=vm.Selected;
   var block=vm.SelectedInspectedBlock;
   if(doc?.Document is not PdfDocumentSession session || block is null || !block.NativeAnalysisSupported || block.Runs.IsEmpty)
    throw new InvalidOperationException("Analice y seleccione un bloque nativo compatible primero.");
   var page=vm.CurrentPage;var revision=doc.Revision;
   var inspected=session.InspectParagraphDraft(page,revision,block.BlockId);
   if(!inspected.IsSupported)throw new InvalidOperationException(string.Join(Environment.NewLine,inspected.Diagnostics.Select(d=>d.Message)));
   var buffer=inspected.Draft!;var metrics=inspected.Metrics!;
   if(buffer.OriginalSources.Any(s=>s.StreamFingerprint!=block.StreamFingerprint))
    throw new InvalidOperationException("El bloque cambió. Vuelva a analizar los fragmentos.");
   var geometry=session.GetPageGeometry(page);
   ParagraphCompositionOptions options;
   if(inspected.SavedOptions is {} saved)options=saved;
   else {
    var baseline=block.Runs[0].BaselineY;
    var left=Math.Max(0,block.Runs.Min(r=>r.BaselineX));
    var right=block.Runs.Max(r=>r.BaselineX+r.AdvancePt);
    var ascent=block.Runs.Max(r=>metrics.Measure(r.Run.Style,"").AscentPt+r.Run.Style.BaselineRisePt);
    var top=Math.Max(0,baseline-ascent);
    var size=block.Runs.Max(r=>r.Run.Style.FontSizePt+Math.Abs(r.Run.Style.BaselineRisePt));
    options=new(new(left,top,Math.Min(Math.Max(40,right-left),geometry.Width-left),Math.Min(Math.Max(120,size*2),geometry.Height-top)),baseline,size*1.5);
   }
   var window=new CompositionPreviewWindow(buffer,metrics,()=>vm.Selected==doc && vm.CurrentPage==page && doc.Revision==revision && vm.SelectedInspectedBlock==block,
    options,geometry,(draft,plan)=>vm.ApplyParagraphCompositionAsync(doc,draft,plan));
   void Invalidate(object? _,System.ComponentModel.PropertyChangedEventArgs e) {
    if(!window.IsCommitting && (vm.Selected!=doc || vm.CurrentPage!=page || doc.Revision!=revision || vm.SelectedInspectedBlock!=block)) window.Close();
   }
   vm.PropertyChanged+=Invalidate;doc.PropertyChanged+=Invalidate;
   window.Closed+=(_,_)=> {vm.PropertyChanged-=Invalidate;doc.PropertyChanged-=Invalidate;};
   return window;
 }
}
