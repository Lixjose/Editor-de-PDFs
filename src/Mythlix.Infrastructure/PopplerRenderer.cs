using System.Diagnostics;
using Mythlix.Core;
namespace Mythlix.Infrastructure;
public sealed class PopplerRenderer : IPdfRenderer {
 static async Task<byte[]> Run(string executable, IEnumerable<string> args, CancellationToken token) {
  var root = Environment.GetEnvironmentVariable("MYTHLIX_POPPLER_BIN");
  var systemExecutable=OperatingSystem.IsLinux() && File.Exists("/usr/bin/"+executable) ? "/usr/bin/"+executable : executable;
  var start = new ProcessStartInfo(root is null ? systemExecutable : Path.Combine(root, executable + (OperatingSystem.IsWindows() ? ".exe" : ""))) {
   RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false, CreateNoWindow = true
  };
  foreach(var arg in args) start.ArgumentList.Add(arg);
  using var timeout = CancellationTokenSource.CreateLinkedTokenSource(token); timeout.CancelAfter(TimeSpan.FromSeconds(45));
  using var process = new Process { StartInfo = start };
  try { process.Start(); } catch(System.ComponentModel.Win32Exception) { throw new InvalidOperationException("Instale Poppler (pdftoppm y pdftotext) o configure MYTHLIX_POPPLER_BIN."); }
  using var registration = timeout.Token.Register(() => { try { if(!process.HasExited) process.Kill(true); } catch(InvalidOperationException) {} });
  using var output = new MemoryStream();
  var error = process.StandardError.ReadToEndAsync(timeout.Token);
  await process.StandardOutput.BaseStream.CopyToAsync(output, timeout.Token);
  await process.WaitForExitAsync(timeout.Token); await error;
  if(process.ExitCode != 0) throw new InvalidOperationException("Poppler no pudo procesar esta página; el documento puede estar dañado o protegido.");
  return output.ToArray();
 }
 public Task<byte[]> RenderAsync(string path, int page, int width, CancellationToken token) => Run("pdftoppm", ["-f",page.ToString(),"-l",page.ToString(),"-singlefile","-scale-to",Math.Clamp(width, 128, 2400).ToString(),"-png",path],token);
 public Task<byte[]> RenderOcrAsync(string path,int page,int size,CancellationToken token)=>Run("pdftoppm",["-f",page.ToString(),"-l",page.ToString(),"-singlefile","-r","300","-png",path],token);
 public async Task<string> ExtractTextAsync(string path,int page,CancellationToken token) => System.Text.Encoding.UTF8.GetString(await Run("pdftotext",["-f",page.ToString(),"-l",page.ToString(),"-layout",path,"-"],token));
 public async Task<string> ExtractTsvAsync(string path,int page,CancellationToken token) => System.Text.Encoding.UTF8.GetString(await Run("pdftotext",["-f",page.ToString(),"-l",page.ToString(),"-tsv",path,"-"],token));
 public async Task<string> ExtractXmlAsync(string path,int page,CancellationToken token) => System.Text.Encoding.UTF8.GetString(await Run("pdftohtml",["-xml","-zoom","1.0","-i","-fontfullname","-nodrm","-f",page.ToString(),"-l",page.ToString(),"-stdout",path],token));
}
