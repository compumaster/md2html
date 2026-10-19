namespace Md2Html;

internal static class Program
{
    private const string MutexName = @"Local\md2html.tray";

    /// <summary>
    /// No arguments: run the tray app (single instance, registers itself to start with Windows).
    /// <c>--once</c>: convert the clipboard once and exit (exit code 0 on success) — usable from shortcuts/scripts.
    /// </summary>
    [STAThread]
    private static int Main(string[] args)
    {
        if (args.Contains("--once", StringComparer.OrdinalIgnoreCase))
            return ClipboardConverter.ConvertClipboard().Success ? 0 : 1;

        using var mutex = new Mutex(initiallyOwned: true, MutexName, out bool createdNew);
        if (!createdNew)
            return 0;

        ApplicationConfiguration.Initialize();
        Settings.SyncAutoStart();
        Application.Run(new TrayApplicationContext());
        return 0;
    }
}
