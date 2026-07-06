using Microsoft.UI.Xaml;
using XunxianDpkViewer.Core;

namespace XunxianDpkViewer;

public partial class App : Application
{
    private Window? _window;

    public App()
    {
        InitializeComponent();
        UnhandledException += (_, args) =>
        {
            System.Diagnostics.Debug.WriteLine(args.Exception);
            WriteCrashLog(args.Exception);
        };
    }

    protected override void OnLaunched(LaunchActivatedEventArgs args)
    {
        try
        {
            if (Environment.GetCommandLineArgs().Any(argument => argument.Equals("--self-test", StringComparison.OrdinalIgnoreCase)))
            {
                string folder = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "XunxianDpkViewer");
                Directory.CreateDirectory(folder);
                File.WriteAllText(Path.Combine(folder, "self-test.log"), SelfTest.Run());
                Exit();
                return;
            }
            _window = new MainWindow();
            _window.Activate();
        }
        catch (Exception exception)
        {
            WriteCrashLog(exception);
            throw;
        }
    }

    private static void WriteCrashLog(Exception exception)
    {
        try
        {
            string folder = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "XunxianDpkViewer");
            Directory.CreateDirectory(folder);
            string properties = string.Join("\n", exception.GetType().GetProperties()
                .Select(property =>
                {
                    try { return $"{property.Name}: {property.GetValue(exception)}"; }
                    catch { return $"{property.Name}: <unavailable>"; }
                }));
            File.WriteAllText(
                Path.Combine(folder, "crash.log"),
                $"{DateTimeOffset.Now:O}\nHRESULT: 0x{exception.HResult:X8}\n{properties}\n\n{exception}");
        }
        catch
        {
            // 不能让诊断日志覆盖原始异常。
        }
    }
}
