using Avalonia;
namespace Mythlix.Tests;
internal static class AvaloniaTestRuntime {
 static readonly Lazy<bool> Ready=new(()=> {
  var previous=SynchronizationContext.Current;
  try {AppBuilder.Configure<Application>().UseStandardRuntimePlatformSubsystem().UseWindowingSubsystem(()=>{},"Offscreen").UseSkia().UseHarfBuzz().SetupWithoutStarting();}
  finally {SynchronizationContext.SetSynchronizationContext(previous);}
  return true;
 });
 internal static void Ensure()=>_=Ready.Value;
}
