using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Media;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using Mythlix.Core;
using TextAlignment = Mythlix.Core.TextAlignment;
using Mythlix.Core.TextEditing;
using Mythlix.Desktop.TextEditing;
using Mythlix.Desktop.ViewModels;

namespace Mythlix.Desktop.Views;

/// <summary>Isolated draft editor. Only the explicit Apply action invokes the transactional writer.</summary>
public sealed class CompositionPreviewWindow : Window {
 sealed class PreviewSurface : Control {
  public ParagraphGlyphPreview? Preview { get; set; }
  public IBrush PreviewBackground { get; set; }=Brushes.White;
  public override void Render(DrawingContext context) {context.DrawRectangle(PreviewBackground,null,new Rect(Bounds.Size));Preview?.Draw(context);}
 }
 readonly Dictionary<PdfFontReference,PreviewFontBinding> fonts=new();
 readonly Dictionary<PdfFontReference,Button> fontButtons=new();
 readonly ParagraphDraftEditor draft;
 readonly IRunMetricsProvider metrics;
 readonly Func<bool> contextIsCurrent;
 readonly Func<ParagraphRunBuffer,ParagraphCompositionSnapshot,Task>? commit;
 readonly PreviewSurface surface=new();
 readonly TextBlock status=new(){TextWrapping=TextWrapping.Wrap};
 readonly TextBox editor=new(){AcceptsReturn=true,TextWrapping=TextWrapping.Wrap,IsUndoEnabled=false,MinHeight=75,MaxHeight=160};
 readonly NumericUpDown x=Number(),y=Number(),width=Number(1),height=Number(1),baseline=Number(),leading=Number(1);
 readonly ComboBox alignment=new(){ItemsSource=new[]{"Izquierda","Centro","Derecha","Justificada"},SelectedIndex=0};
 readonly CheckBox accept=new(){Content="Aplicar este borrador con reflujo y recursos PDF originales"};
 readonly Button apply=new(){Content="Aplicar al PDF",IsEnabled=false};
 readonly StackPanel controls=new(){Spacing=8,Margin=new Thickness(16)};
 readonly StackPanel footer=new(){Spacing=8,Margin=new Thickness(16,8)};
 ParagraphCompositionSnapshot? plan;
 TextRange? pendingSelection;
 bool released,syncing;
 internal bool IsCommitting {get;private set;}
 internal TextBox DraftTextBox=>editor;
 internal bool HasPreview=>surface.Preview is not null;
 internal bool CanApply=>apply.IsEnabled;
 static NumericUpDown Number(decimal minimum=0)=>new(){Minimum=minimum,Maximum=100000,Increment=1,FormatString="0.##"};
 public CompositionPreviewWindow(ParagraphRunBuffer buffer,IRunMetricsProvider metrics,Func<bool> contextIsCurrent,
  ParagraphCompositionOptions options,PageGeometry page,Func<ParagraphRunBuffer,ParagraphCompositionSnapshot,Task>? commit=null) {
  draft=new(buffer,metrics);this.metrics=metrics;this.contextIsCurrent=contextIsCurrent;this.commit=commit;
  Title="Texto mixto · borrador y composición";Width=1120;Height=900;
  surface.Width=page.Width;surface.Height=page.Height;
  controls.Children.Add(new TextBlock{Text="Edite el borrador y revise su composición. Los caracteres insertados heredan el estilo izquierdo; el resto conserva sus estilos. La vista usa sustitutos físicos explícitos; al aplicar se conservan las fuentes PDF originales. El reflujo reemplaza las posiciones y ajustes TJ originales.",TextWrapping=TextWrapping.Wrap});
  editor.Text=buffer.Text;editor.TextChanging+=(_,_)=> {if(!syncing && !released && !IsCommitting) {
   var start=Math.Min(editor.SelectionStart,editor.SelectionEnd);var length=Math.Abs(editor.SelectionEnd-editor.SelectionStart);
   var selection=pendingSelection??new(start,length);pendingSelection=null;
   EditText(editor.Text??"",selection);
  }};
  editor.AddHandler(KeyDownEvent,(_,e)=> {
   pendingSelection=null;
   if(editor.SelectionStart==editor.SelectionEnd && e.KeyModifiers==KeyModifiers.None && e.Key is Key.Back or Key.Delete) {
    var caret=editor.CaretIndex;var boundaries=System.Globalization.StringInfo.ParseCombiningCharacters(draft.Buffer.Text).Append(draft.Buffer.Text.Length).ToArray();
    if(e.Key==Key.Back && caret>0) {var start=boundaries.Last(b=>b<caret);pendingSelection=new(start,caret-start);}
    if(e.Key==Key.Delete && caret<draft.Buffer.Text.Length) {var end=boundaries.First(b=>b>caret);pendingSelection=new(caret,end-caret);}
   }
   if((e.KeyModifiers & (KeyModifiers.Control|KeyModifiers.Meta))==0)return;
   if(e.Key==Key.Z || e.Key==Key.Y) {ChangeDraftHistory(e.Key==Key.Y || e.KeyModifiers.HasFlag(KeyModifiers.Shift));e.Handled=true;}
  },RoutingStrategies.Tunnel);controls.Children.Add(editor);
  var history=new StackPanel{Orientation=Avalonia.Layout.Orientation.Horizontal,Spacing=8};
  var undo=new Button{Content="Deshacer borrador"};undo.Click+=(_,_)=>ChangeDraftHistory(false);history.Children.Add(undo);
  var redo=new Button{Content="Rehacer borrador"};redo.Click+=(_,_)=>ChangeDraftHistory(true);history.Children.Add(redo);controls.Children.Add(history);
  foreach(var resource in buffer.Runs.Select(r=>r.Style.Font).Append(buffer.EmptyStyle.Font).Distinct()) {
   var style=buffer.Runs.FirstOrDefault(r=>r.Style.Font==resource)?.Style??buffer.EmptyStyle;
   var button=new Button{HorizontalAlignment=Avalonia.Layout.HorizontalAlignment.Stretch,Content=new TextBlock{Text=$"Elegir fuente: {resource.ResourceName} · {style.FamilyName} · {(style.Bold?"negrita ":"")}{(style.Italic?"cursiva":"")}",TextWrapping=TextWrapping.Wrap}};
   fontButtons.Add(resource,button);button.Click+=async (_,_)=>await ChooseFont(resource);controls.Children.Add(button);
  }
  x.Value=(decimal)options.Bounds.X;y.Value=(decimal)options.Bounds.Y;width.Value=(decimal)options.Bounds.Width;
  height.Value=(decimal)options.Bounds.Height;baseline.Value=(decimal)options.FirstBaselineY;leading.Value=(decimal)options.LineHeightPt;
  var positions=new UniformGrid{Columns=2,Rows=3};
  foreach(var (label,input) in new[]{("X (pt)",x),("Y (pt)",y),("Ancho (pt)",width),("Alto (pt)",height),("Línea base Y (pt)",baseline),("Interlineado (pt)",leading)}) {
   var cell=new StackPanel{Margin=new Thickness(3)};cell.Children.Add(new TextBlock{Text=label});cell.Children.Add(input);positions.Children.Add(cell);
   input.ValueChanged+=(_,_)=>ClearPreview();
  }
  controls.Children.Add(positions);controls.Children.Add(alignment);alignment.SelectionChanged+=(_,_)=>ClearPreview();
  var background=new ComboBox{ItemsSource=new[]{"Vista: fondo blanco","Vista: fondo azul","Vista: fondo oscuro"},SelectedIndex=0};
  background.SelectionChanged+=(_,_)=> {surface.PreviewBackground=background.SelectedIndex switch {1=>Brushes.DarkBlue,2=>Brushes.Black,_=>Brushes.White};surface.InvalidateVisual();};controls.Children.Add(background);
  var compose=new Button{Content="Componer y revisar"};compose.Click+=(_,_)=>Compose();controls.Children.Add(compose);
  accept.IsCheckedChanged+=(_,_)=>UpdateApply();footer.Children.Add(accept);
  var actions=new StackPanel{Orientation=Avalonia.Layout.Orientation.Horizontal,Spacing=8};
  apply.Click+=async (_,_)=>await ApplyAsync();actions.Children.Add(apply);
  var cancel=new Button{Content="Cancelar"};cancel.Click+=(_,_)=>Close();actions.Children.Add(cancel);footer.Children.Add(actions);
  footer.Children.Add(status);
  var layout=new Grid{ColumnDefinitions=new ColumnDefinitions("380,*"),RowDefinitions=new RowDefinitions("*,Auto")};
  layout.Children.Add(new ScrollViewer{Content=controls});
  var viewport=new ScrollViewer{Content=surface,HorizontalScrollBarVisibility=ScrollBarVisibility.Auto,VerticalScrollBarVisibility=ScrollBarVisibility.Auto,Margin=new Thickness(8)};
  Grid.SetColumn(viewport,1);layout.Children.Add(viewport);
  Grid.SetRow(footer,1);Grid.SetColumnSpan(footer,2);layout.Children.Add(footer);Content=layout;
  Closing+=(_,e)=> {if(IsCommitting)e.Cancel=true;};
  Closed+=(_,_)=>Release();
 }
 internal RunEditResult EditText(string text,TextRange? replacedSelection=null) {
  if(released || IsCommitting || !contextIsCurrent()) throw new InvalidOperationException("El borrador ya no está disponible.");
  if(text==draft.Buffer.Text) return draft.UpdateText(text);
  ClearPreview();
  var result=draft.UpdateText(text,replacedSelection);
  syncing=true;editor.Text=draft.Buffer.Text;syncing=false;
  status.Text=result.Diagnostics.IsEmpty?"Borrador actualizado. Componga y revise antes de aplicar.":string.Join(Environment.NewLine,result.Diagnostics.Select(d=>d.Message));
  return result;
 }
 void ChangeDraftHistory(bool redo) {
  if(released || IsCommitting || !contextIsCurrent())return;
  if(!(redo?draft.Redo():draft.Undo()))return;
  ClearPreview();syncing=true;editor.Text=draft.Buffer.Text;syncing=false;
  status.Text="Borrador recuperado. Componga y revise antes de aplicar.";
 }
 async Task ChooseFont(PdfFontReference resource) {
  try {
   var files=await StorageProvider.OpenFilePickerAsync(new(){Title=$"Fuente física para {resource.ResourceName}",AllowMultiple=false,
    FileTypeFilter=[new FilePickerFileType("Fuente OpenType/TrueType"){Patterns=["*.ttf","*.otf"]}]});
   if(files.Count==0 || released) return;
   using var stream=await files[0].OpenReadAsync();using var memory=new MemoryStream();await stream.CopyToAsync(memory);
   if(released || !contextIsCurrent()) {InvalidateContext();return;}
   BindFont(resource,memory.ToArray(),files[0].Name);
   status.Text="Fuente asociada. Componga para generar una vista nueva.";
  } catch(Exception ex) {status.Text=ex.Message;}
 }
 internal PreviewFontBinding BindFont(PdfFontReference resource,byte[] bytes,string description) {
  ObjectDisposedException.ThrowIf(released,this);
  if(IsCommitting || !contextIsCurrent()) throw new InvalidOperationException("El contexto de la vista cambió.");
  var replacement=new PreviewFontBinding(resource,bytes,description);ClearPreview();
  if(fonts.Remove(resource,out var old)) old.Dispose();fonts.Add(resource,replacement);
  if(fontButtons.TryGetValue(resource,out var button)) {
   ToolTip.SetTip(button,replacement.Sha256);button.Content=new TextBlock{Text=$"{resource.ResourceName} → {replacement.Description} · SHA256 {replacement.Sha256[..12]}",TextWrapping=TextWrapping.Wrap};
  }
  return replacement;
 }
 internal void Compose() {
  ClearPreview();
  if(released || IsCommitting || !contextIsCurrent()) {InvalidateContext();return;}
  try {
   var options=new ParagraphCompositionOptions(new((double)(x.Value??0),(double)(y.Value??0),(double)(width.Value??0),(double)(height.Value??0)),
    (double)(baseline.Value??0),(double)(leading.Value??0),(TextAlignment)Math.Max(0,alignment.SelectedIndex));
   var proposal=ParagraphComposer.Compose(draft.Buffer,options,metrics);
   if(!proposal.IsValid) {status.Text=string.Join(Environment.NewLine,proposal.Diagnostics.Select(d=>d.Message));return;}
   surface.Preview=ParagraphGlyphPreview.Create(proposal,fonts.Values);plan=proposal;surface.InvalidateVisual();
   status.Text=$"{proposal.Lines.Length} líneas · vista aproximada · el PDF todavía no se ha modificado.";UpdateApply();
  } catch(Exception ex) {status.Text=ex.Message;}
 }
 internal void AcceptReflow(bool value) {accept.IsChecked=value;UpdateApply();}
 void UpdateApply()=>apply.IsEnabled=!released && !IsCommitting && commit is not null && plan is not null && HasPreview && accept.IsChecked==true && contextIsCurrent();
 internal async Task ApplyAsync() {
  if((editor.Text??"")!=draft.Buffer.Text) {EditText(editor.Text??"");return;}
  UpdateApply();if(!CanApply || commit is null || plan is not {} approved) return;
  IsCommitting=true;controls.IsEnabled=false;footer.IsEnabled=false;UpdateApply();
  try {
   await commit(draft.Buffer,approved);
   IsCommitting=false;Close();
  } catch(Exception ex) {ClearPreview();status.Text=ex.Message;}
  finally {IsCommitting=false;if(!released) {if(!contextIsCurrent())Close();else {controls.IsEnabled=true;footer.IsEnabled=true;UpdateApply();}}}
 }
 void InvalidateContext() {ClearPreview();status.Text="El documento, la página o la revisión cambió. Cierre y vuelva a analizar.";}
 void ClearPreview() {plan=null;surface.Preview?.Dispose();surface.Preview=null;accept.IsChecked=false;UpdateApply();surface.InvalidateVisual();}
 void Release() {released=true;ClearPreview();foreach(var font in fonts.Values) font.Dispose();fonts.Clear();}
}
