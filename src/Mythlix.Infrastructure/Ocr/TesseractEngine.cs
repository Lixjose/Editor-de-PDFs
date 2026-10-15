using System.Diagnostics;
using System.Buffers.Binary;
using System.Text;
using System.Globalization;
using Mythlix.Core;
namespace Mythlix.Infrastructure.Ocr;
public sealed class TesseractEngine : IOcrEngine {
 public static string? LocalRoot {
  get {
   if(!OperatingSystem.IsLinux())return null;
   var path=Path.GetFullPath(".tools/ocr");
   if(Directory.Exists(path))return path;
   for(var dir=new DirectoryInfo(AppContext.BaseDirectory);dir is not null;dir=dir.Parent){var local=Path.Combine(dir.FullName,".tools/ocr");if(Directory.Exists(local))return local;}
   return null;
  }
 }
 static async Task<string> Run(IEnumerable<string> args,CancellationToken token) {
  token.ThrowIfCancellationRequested();var root=LocalRoot;
  var executable=Environment.GetEnvironmentVariable("MYTHLIX_TESSERACT")??(root is null?"tesseract":Path.Combine(root,"usr/bin/tesseract"));
  var start=new ProcessStartInfo(executable){UseShellExecute=false,CreateNoWindow=true,RedirectStandardOutput=true,RedirectStandardError=true};
  start.Environment["OMP_THREAD_LIMIT"]="2";
  foreach(var arg in args)start.ArgumentList.Add(arg);
  var data=Environment.GetEnvironmentVariable("MYTHLIX_TESSDATA")??(root is null?null:Path.Combine(root,"usr/share/tesseract/tessdata"));
  if(data is not null){start.ArgumentList.Add("--tessdata-dir");start.ArgumentList.Add(data);}
  if(root is not null && OperatingSystem.IsLinux())start.Environment["LD_LIBRARY_PATH"]=Path.Combine(root,"usr/lib64")+":"+Environment.GetEnvironmentVariable("LD_LIBRARY_PATH");
  using var timeout=CancellationTokenSource.CreateLinkedTokenSource(token);timeout.CancelAfter(TimeSpan.FromMinutes(3));
  using var process=new Process{StartInfo=start};
  try{process.Start();}catch(System.ComponentModel.Win32Exception){throw new InvalidOperationException("Instale Tesseract y los modelos español/inglés, o configure MYTHLIX_TESSERACT y MYTHLIX_TESSDATA.");}
  using var registration=timeout.Token.Register(()=>{try{if(!process.HasExited)process.Kill(true);}catch(InvalidOperationException){}});
  async Task<string> ReadBounded(StreamReader reader,int limit) {
   var result=new StringBuilder();var buffer=new char[4096];int count;
   while((count=await reader.ReadAsync(buffer.AsMemory(),timeout.Token))>0) {
    if(result.Length+count>limit) {
     try{if(!process.HasExited)process.Kill(true);}catch(InvalidOperationException){}
     throw new InvalidOperationException("Salida de Tesseract fuera del límite de recursos.");
    }
    result.Append(buffer,0,count);
   }
   return result.ToString();
  }
  var output=ReadBounded(process.StandardOutput,16*1024*1024);var error=ReadBounded(process.StandardError,256*1024);
  await Task.WhenAll(output,error,process.WaitForExitAsync(timeout.Token));var result=await output;
  if(process.ExitCode!=0)throw new InvalidOperationException("Tesseract no pudo reconocer la página. Compruebe los idiomas instalados y la imagen.");
  if(result.Length>16*1024*1024)throw new InvalidOperationException("Resultado OCR superior a 16 MB.");return result;
 }
 static IReadOnlyList<string>? _cachedLanguages;
 public static void ClearLanguageCache() => _cachedLanguages = null;
 public async Task<IReadOnlyList<string>> LanguagesAsync(CancellationToken token) {
  if(_cachedLanguages is not null) return _cachedLanguages;
  var langs = (await Run(["--list-langs"], token))
   .Split('\n', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
   .Where(s => System.Text.RegularExpressions.Regex.IsMatch(s, "^[a-z]{3}$"))
   .ToArray();
  _cachedLanguages = langs;
  return langs;
 }
 public Task<OcrPageResult> RecognizeAsync(byte[] image,string language,CancellationToken token)=>RecognizeAsync(image,language,new OcrOptions(),token);
 public async Task<OcrPageResult> RecognizeAsync(byte[] image,string language,OcrOptions options,CancellationToken token) {
  token.ThrowIfCancellationRequested();options.Validate();
  if(language is not ("eng" or "spa" or "spa+eng"))throw new InvalidOperationException("Idiomas OCR compatibles: inglés, español o ambos.");
  ValidateImage(image);
  var installed=await LanguagesAsync(token);if(language.Split('+').Any(l=>!installed.Contains(l)))throw new InvalidOperationException("Faltan los modelos del idioma OCR seleccionado.");
  if(image.Length>64*1024*1024)throw new InvalidOperationException("Imagen OCR superior a 64 MB.");
  var folder=Path.Combine(Path.GetTempPath(),"mythlix-ocr-"+Guid.NewGuid().ToString("N"));
  if(OperatingSystem.IsWindows())Directory.CreateDirectory(folder);else Directory.CreateDirectory(folder,UnixFileMode.UserRead|UnixFileMode.UserWrite|UnixFileMode.UserExecute);
  try {
   var path=Path.Combine(folder,"page.png");await File.WriteAllBytesAsync(path,image,token);
   var text=await Run([path,"stdout","-l",language,"--psm",((int)options.Mode).ToString(CultureInfo.InvariantCulture),"-c","tessedit_create_tsv=1"],token);
   return ParseTsv(text);
  } finally {Directory.Delete(folder,true);}
 }
 public static void ValidateImage(byte[] image) {
  if(image.Length>64*1024*1024)throw new InvalidOperationException("Imagen OCR superior a 64 MB.");
  if(image.Length<33 || !image.AsSpan(0,8).SequenceEqual(new byte[]{137,80,78,71,13,10,26,10}) ||
   BinaryPrimitives.ReadUInt32BigEndian(image.AsSpan(8,4))!=13 || !image.AsSpan(12,4).SequenceEqual("IHDR"u8))
   throw new InvalidOperationException("OCR requiere una imagen PNG válida.");
  var width=BinaryPrimitives.ReadUInt32BigEndian(image.AsSpan(16,4));var height=BinaryPrimitives.ReadUInt32BigEndian(image.AsSpan(20,4));
  if(width is <1 or >6000 || height is <1 or >6000 || (long)width*height>24_000_000)throw new InvalidOperationException("Imagen OCR fuera del límite de dimensiones.");
 }
 public static OcrPageResult ParseTsv(string tsv) {
  if(tsv.Length>16*1024*1024)throw new InvalidOperationException("Resultado OCR superior a 16 MB.");
  var words=new List<OcrWord>();int width=0,height=0;
  foreach(var line in tsv.Split('\n')) {
   var fields=line.TrimEnd('\r').Split('\t',12);if(fields.Length<11 || !int.TryParse(fields[0],out var level))continue;
   if(level==1){width=int.Parse(fields[8],CultureInfo.InvariantCulture);height=int.Parse(fields[9],CultureInfo.InvariantCulture);}
   if(level!=5 || fields.Length<12 || string.IsNullOrWhiteSpace(fields[11]))continue;
   double N(int i)=>double.Parse(fields[i],CultureInfo.InvariantCulture);
   var confidence=N(10);if(confidence<0)continue;
   if(words.Count>=20000)throw new InvalidOperationException("Resultado OCR superior a 20 000 palabras por página.");
   words.Add(new(fields[11],confidence,N(6),N(7),N(8),N(9)){Block=int.Parse(fields[2],CultureInfo.InvariantCulture),Paragraph=int.Parse(fields[3],CultureInfo.InvariantCulture),Line=int.Parse(fields[4],CultureInfo.InvariantCulture)});
  }
  var wordsWithBaseline = new List<OcrWord>();
  var lineGroups = words.GroupBy(w => (w.Block, w.Paragraph, w.Line));
  foreach(var group in lineGroups) {
   var list = group.ToList();
   var estimatedLineBaseline = list.Average(w => {
    bool hasDescender = w.Text.Any(c => c is 'g' or 'j' or 'p' or 'q' or 'y' or ',' or ';');
    return hasDescender ? (w.Y + w.Height * 0.82) : (w.Y + w.Height);
   });
   var avgHeight = list.Average(w => w.Height);
   foreach(var w in list) {
    wordsWithBaseline.Add(w with {
     BaselineY = estimatedLineBaseline,
     EstimatedFontSize = Math.Max(4, avgHeight * 0.9)
    });
   }
  }
  var result=new OcrPageResult(width,height,wordsWithBaseline);result.Validate();return result;
 }
}
