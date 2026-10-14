using Avalonia;
namespace Mythlix.Desktop;
internal static class Program {
 [STAThread] public static void Main(string[] args) => AppBuilder.Configure<App>().UsePlatformDetect().WithInterFont().StartWithClassicDesktopLifetime(args);
}
