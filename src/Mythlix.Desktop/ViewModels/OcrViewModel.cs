using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Mythlix.Core;
using Mythlix.Infrastructure;
using Mythlix.Infrastructure.Ocr;
namespace Mythlix.Desktop.ViewModels;
public sealed partial class OcrWordViewModel(OcrWord word):ObservableObject {
 public OcrWord Original {get;}=word;
 [ObservableProperty] private string text=word.Text;
 public bool LowConfidence=>Original.Confidence<70;
 public string Confidence=>$"{Original.Confidence:0}%";
 public OcrWord Corrected=>Original with{Text=Text};
}
public sealed class OcrReviewPage(int page,OcrPageResult result) {
 public int Number {get;}=page;
 public string Label=>$"Página {Number} · {Words.Count} palabras";
 public OcrPageResult Result {get;}=result;
 public ObservableCollection<OcrWordViewModel> Words {get;}=new(result.Words.Select(w=>new OcrWordViewModel(w)));
 public OcrPageResult Corrected=>Result with{Words=Words.Select(w=>w.Corrected).ToArray()};
}
public sealed record OcrModeChoice(OcrPageMode Mode,string Name);
public sealed record OcrLanguage(string Code,string Name);
public sealed partial class WorkspaceViewModel {
 public IReadOnlyList<OcrModeChoice> OcrModes {get;}=[new(OcrPageMode.Automatic,"Automático"),new(OcrPageMode.SingleColumn,"Una columna"),new(OcrPageMode.UniformBlock,"Bloque uniforme"),new(OcrPageMode.SparseText,"Texto disperso")];
 [ObservableProperty] private OcrModeChoice? selectedOcrMode;
 [ObservableProperty] private string ocrPageRange="";
 readonly IOcrEngine ocr=new TesseractEngine();
 CancellationTokenSource? ocrCancellation;
 DocumentViewModel? ocrOwner;
 long ocrRevision;
 public ObservableCollection<OcrLanguage> OcrLanguages {get;}=new();
 public ObservableCollection<OcrReviewPage> OcrPages {get;}=new();
 [ObservableProperty] private OcrLanguage? ocrLanguage;
 [ObservableProperty] private OcrReviewPage? selectedOcrPage;
 [ObservableProperty] private bool ocrVisible;
 [ObservableProperty] private bool ocrRunning;
 [ObservableProperty] private bool skipExistingText=true;
 [ObservableProperty] private double ocrProgress;
 [ObservableProperty] private string ocrCompletedText="";
 [ObservableProperty] private string ocrStatus="OCR local · revise el reconocimiento antes de aplicarlo.";
 public async Task PrepareOcrAsync(){OcrVisible=true;await RunAsync(async()=>{var languages=await ocr.LanguagesAsync(CancellationToken.None);OcrLanguages.Clear();if(languages.Contains("spa"))OcrLanguages.Add(new("spa","Español"));if(languages.Contains("eng"))OcrLanguages.Add(new("eng","Inglés"));if(languages.Contains("spa")&&languages.Contains("eng"))OcrLanguages.Add(new("spa+eng","Español + inglés"));OcrLanguage=OcrLanguages.FirstOrDefault();OcrStatus=OcrLanguage is null?"No hay modelos compatibles instalados.":"Motor disponible · reconocimiento sin conexión.";});}
 void ClearOcr(){OcrPages.Clear();SelectedOcrPage=null;ocrOwner=null;OcrCompletedText="";OcrProgress=0;OcrStatus="OCR local · seleccione páginas para reconocer.";OnPropertyChanged(nameof(OcrExportText));OnPropertyChanged(nameof(OcrReviewSummary));}
 [RelayCommand] private Task RecognizePageAsync()=>RunAsync(()=>RecognizeAsync(false));
 [RelayCommand] private Task RecognizeDocumentAsync()=>RunAsync(()=>RecognizeAsync(true));
 [RelayCommand] private Task RecognizeRangeAsync()=>RunAsync(()=>RecognizeAsync(true,true));
 [RelayCommand] private void CancelOcr()=>ocrCancellation?.Cancel();
 async Task RecognizeAsync(bool all,bool range=false) {
  var doc=Selected;if(doc is null || OcrLanguage is null){Status="Abra un PDF y elija un idioma OCR.";return;}
  var pages=range?PageSelection.Parse(OcrPageRange,doc.Document.PageCount).ToArray():all?Enumerable.Range(1,doc.Document.PageCount).ToArray():[CurrentPage];
  var language=OcrLanguage.Code;var options=new OcrOptions(SelectedOcrMode?.Mode??OcrPageMode.Automatic);
  if(pages.Length>500)throw new InvalidOperationException("OCR por lote: máximo de 500 páginas por operación.");
  ClearOcr();ocrOwner=doc;ocrRevision=doc.Revision;
  using var cancellation=new CancellationTokenSource();ocrCancellation=cancellation;OcrRunning=true;
  try {
   await PrepareWorkingCopyAsync(doc,cancellation.Token);

   for(var i=0;i<pages.Length;i++) {
    cancellation.Token.ThrowIfCancellationRequested();var page=pages[i];OcrStatus=$"Reconociendo página {page} ({i+1}/{pages.Length})…";Status=OcrStatus;
    var geometry=doc.Document.GetPageGeometry(page);
    if(!geometry.CanEdit)throw new InvalidOperationException("OCR: use páginas editables.");
    var existingText=SkipExistingText ? await renderer.ExtractTextAsync(doc.WorkingPath,page,cancellation.Token) : "";
    if(!SkipExistingText || string.IsNullOrWhiteSpace(existingText) || existingText.Trim().Length < 50) {
     var size=(int)Math.Min(4000,Math.Max(geometry.DisplayWidth,geometry.DisplayHeight)*300/72);
     var image=await poppler.RenderOcrAsync(doc.WorkingPath,page,size,cancellation.Token);
     var result=await ocr.RecognizeAsync(image,language,options,cancellation.Token);
     if(OcrPages.Sum(p=>p.Words.Count)+result.Words.Count>100000)throw new InvalidOperationException("Límite de revisión: 100 000 palabras por operación OCR.");
     var newReview=new OcrReviewPage(page,result);
     foreach(var word in newReview.Words)word.PropertyChanged+=(_,e)=>{if(e.PropertyName==nameof(OcrWordViewModel.Text))OnPropertyChanged(nameof(OcrExportText));};
     OcrPages.Add(newReview);SelectedOcrPage??=newReview;
     OnPropertyChanged(nameof(OcrReviewSummary));
    }
    OcrProgress=(i+1)*100d/pages.Length;
   }
   SelectedOcrPage=OcrPages.FirstOrDefault(p=>p.Number==CurrentPage)??OcrPages.FirstOrDefault();
   if(SelectedOcrPage is {} review && review.Number!=CurrentPage){CurrentPage=review.Number;await RenderAsync();}
   OcrStatus=OcrPages.Count==0?"Sin páginas pendientes: ya contienen texto seleccionable.":$"{OcrPages.Count} {(OcrPages.Count==1?"página reconocida":"páginas reconocidas")}. Revise las palabras antes de crear la capa buscable.";Status=OcrStatus;
  }catch(OperationCanceledException){OcrStatus=$"OCR cancelado. Se conservan {OcrPages.Count} páginas para revisión. El PDF no se modificó.";Status=OcrStatus;}
  catch(Exception ex){OcrStatus=$"{ex.Message} Se conservan {OcrPages.Count} páginas para revisión.";Status=OcrStatus;}
  finally{ocrCancellation=null;OcrRunning=false;OnPropertyChanged(nameof(OcrExportText));OnPropertyChanged(nameof(OcrReviewSummary));}
 }
 [RelayCommand] private Task ApplyOcrAsync()=>RunAsync(async()=> {
  var doc=Selected;if(doc is null || OcrPages.Count==0)return;
  if(doc!=ocrOwner || doc.Revision!=ocrRevision)throw new InvalidOperationException("El documento cambió desde el reconocimiento. Repita OCR para alinear la capa.");
  var results=OcrPages.Select(p=>(p.Number,Result:p.Corrected with{Words=p.Corrected.Words.Where(w=>!string.IsNullOrWhiteSpace(w.Text)).ToArray()})).Where(p=>p.Result.Words.Count>0).ToArray();
  if(results.Length==0){OcrStatus="No se detectaron palabras para aplicar.";return;}
  using var cancellation=new CancellationTokenSource();ocrCancellation=cancellation;OcrRunning=true;OcrProgress=0;OcrStatus="Creando capa buscable…";
  var progress=new Progress<int>(n=>OcrProgress=n*100d/results.Length);
  try {
   await Task.Run(()=> {
    var before=doc.Document.Snapshot();
    ((ISearchablePdfEditor)doc.Document).SetOcrPages(results,cancellation.Token,progress);
    doc.Undo.Push(before);doc.Redo.Clear();LimitUndo(doc);
   });
   doc.Modified=true;doc.Revision++;await RenderAsync();var completed=OcrExportText;ClearOcr();OcrCompletedText=completed;OcrProgress=100;OcrStatus="Capa buscable creada. Guarde una copia del PDF.";Status=OcrStatus;
  }catch(OperationCanceledException){OcrStatus="Aplicación OCR cancelada. El PDF se restauró y la revisión sigue disponible.";Status=OcrStatus;}
  finally{ocrCancellation=null;OcrRunning=false;}
 });
 public Task ExportOcrReconstructionAsync(string path) {
  var doc=Selected;if(doc is null || doc!=ocrOwner || doc.Revision!=ocrRevision)throw new InvalidOperationException("Repita OCR después de cambiar el documento.");
  var pages=OcrPages.Select(p=>(doc.Document.GetPageGeometry(p.Number),p.Corrected)).ToArray();
  return Task.Run(()=>OcrReconstruction.Save(path,pages));
 }
 [RelayCommand] private void HideOcr()=>OcrVisible=false;
 public string OcrReviewSummary=>$"{OcrPages.Sum(p=>p.Words.Count)} palabras · {OcrPages.Sum(p=>p.Words.Count(w=>w.LowConfidence))} con confianza inferior al 70%.";
 public string OcrExportText=>OcrPages.Count==0?OcrCompletedText:string.Join("\n\n",OcrPages.Select(p=>$"Página {p.Number}\n{p.Corrected.Text}"));
}
