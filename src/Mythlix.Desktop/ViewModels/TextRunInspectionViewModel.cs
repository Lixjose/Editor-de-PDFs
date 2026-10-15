using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Mythlix.Core;
using Mythlix.Core.TextEditing;

namespace Mythlix.Desktop.ViewModels;

public sealed partial class WorkspaceViewModel {
 public ObservableCollection<TextBlockInspection> InspectedTextBlocks { get; } = new();
 [ObservableProperty] private TextBlockInspection? selectedInspectedBlock;
 [ObservableProperty] private string textInspectionStatus = "Análisis de fragmentos disponible a petición.";
 [ObservableProperty] private string textInspectionDiagnostics = "";
 [ObservableProperty] private string textInspectionMatch = "";
 TextRunInspectionReport? inspectionReport;
 DocumentViewModel? inspectionOwner;
 public string SelectedInspectionDiagnostics => SelectedInspectedBlock is {} block
  ? string.Join(Environment.NewLine,block.Diagnostics.Select(d=>d.Message)) : "";
 partial void OnSelectedInspectedBlockChanged(TextBlockInspection? value) => OnPropertyChanged(nameof(SelectedInspectionDiagnostics));

 [RelayCommand]
 public Task InspectTextRunsAsync() => RunAsync(async()=> {
  if(Selected is not {} doc || doc.Document is not ITextRunInspector inspector) {
   ClearTextInspection();TextInspectionStatus="Este documento no ofrece inspección de fragmentos.";return;
  }
  var page=CurrentPage;var revision=doc.Revision;
  TextInspectionStatus="Analizando fragmentos…";
  TextRunInspectionReport report;
  try {report=await Task.Run(()=>inspector.InspectTextRuns(page,revision));}
  catch {ClearTextInspection();TextInspectionStatus="No se pudo completar el análisis de fragmentos.";throw;}
  if(Selected!=doc || CurrentPage!=page || doc.Revision!=revision) {ClearTextInspection();return;}
  if(inspectionOwner is not null) inspectionOwner.PropertyChanged-=InspectionOwnerChanged;
  inspectionOwner=doc;inspectionOwner.PropertyChanged+=InspectionOwnerChanged;
  inspectionReport=report;
  InspectedTextBlocks.Clear();
  foreach(var block in report.Blocks) InspectedTextBlocks.Add(block);
  TextInspectionDiagnostics=string.Join(Environment.NewLine,report.PageDiagnostics.Select(d=>d.Message));
  TextInspectionStatus=$"{report.Blocks.Length} bloques · solo lectura. " +
   (report.NativeAnalysisSupported ? "Seleccione un bloque de una línea para editar su borrador." : "Hay contenido fuera del alcance del análisis nativo.");
  if(SelectedOriginal is {} original) SyncInspectionSelection(original);
  else {SelectedInspectedBlock=InspectedTextBlocks.FirstOrDefault(b=>b.NativeAnalysisSupported && !b.Runs.IsEmpty)??InspectedTextBlocks.FirstOrDefault();TextInspectionMatch="";}
 });

 void SyncInspectionSelection(OriginalTextBlock? original) {
  if(inspectionReport is null) return;
  if(original is null) {TextInspectionMatch="";return;}
  var match=inspectionReport.MatchOriginal(original,new(inspectionReport.Context.DocumentId,CurrentPage,Selected?.Revision ?? 0));
  SelectedInspectedBlock=match.Block;
  TextInspectionMatch=match.Kind switch {
   InspectionMatchKind.VerifiedNative => "Correspondencia nativa verificada con la selección actual.",
   InspectionMatchKind.ExtractedFallback => "La selección procede de extracción de respaldo; no hay una correspondencia nativa verificada.",
   InspectionMatchKind.StaleSelection => "La selección cambió; vuelva a analizar los fragmentos.",
   _ => "La selección no tiene un objeto nativo identificado en este informe."
  };
 }
 void InspectionOwnerChanged(object? sender,System.ComponentModel.PropertyChangedEventArgs args) {
  if(args.PropertyName==nameof(DocumentViewModel.Revision)) ClearTextInspection();
 }
 void ClearTextInspection() {
  if(inspectionOwner is not null) inspectionOwner.PropertyChanged-=InspectionOwnerChanged;
  inspectionOwner=null;
  inspectionReport=null;InspectedTextBlocks.Clear();SelectedInspectedBlock=null;
  TextInspectionStatus="Análisis de fragmentos disponible a petición.";
  TextInspectionDiagnostics="";TextInspectionMatch="";
 }
 partial void OnCurrentPageChanged(int value) => ClearTextInspection();
}
