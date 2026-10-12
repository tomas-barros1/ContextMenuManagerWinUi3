using Microsoft.Windows.Globalization;
using Microsoft.UI.Xaml;

namespace ContextMenuManager;

public partial class App : Application
{
    public const string LanguageOverrideKey = "LanguageOverride";

    public static Window? MainWindow { get; private set; }

    private Window? _window;

    public App()
    {
        InitializeComponent();
        UnhandledException += OnUnhandledException;
    }

    protected override void OnLaunched(LaunchActivatedEventArgs args)
    {
        try
        {
            string? lang = Services.AppConfig.Get(LanguageOverrideKey);
            if (!string.IsNullOrEmpty(lang))
            {
                ApplicationLanguages.PrimaryLanguageOverride = lang;
            }

            _window = new MainWindow();
            MainWindow = _window;
            _window.Activate();
        }
        catch (Exception ex)
        {
            WriteCrashLog(ex);
            throw;
        }
    }

    private void OnUnhandledException(object sender, Microsoft.UI.Xaml.UnhandledExceptionEventArgs e)
    {
        WriteCrashLog(e.Exception);
        e.Handled = true;
    }

    private static void WriteCrashLog(Exception ex)
    {
        try
        {
            string path = Path.Combine(Path.GetTempPath(), "ContextMenuManager-crash.log");
            File.AppendAllText(path,
                $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}]\n{ex}\n\n");
        }
        catch
        {
            // Logging must never throw.
        }
    }
}
