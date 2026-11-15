using Velopack;

namespace NexGrades.App;

/// <summary>
/// The entry point. It replaces the <c>Main</c> that WPF generates from <c>App.xaml</c>, because Velopack must run
/// before anything else: during install, update and uninstall the installer starts the app with hook arguments, and
/// <see cref="VelopackApp.Run"/> handles those and exits at once. On a normal start it returns and the app runs.
/// </summary>
internal static class Program
{
    [STAThread]
    private static int Main(string[] args)
    {
        VelopackApp.Build().SetArgs(args).Run();

        var app = new App();
        app.InitializeComponent();
        return app.Run();
    }
}
