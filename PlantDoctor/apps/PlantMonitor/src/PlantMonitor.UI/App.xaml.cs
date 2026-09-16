using System.IO;
using System.Windows;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using PlantMonitor.Core.Artifacts;
using PlantMonitor.Core.Ingest;
using PlantMonitor.Core.Logging;
using PlantMonitor.ServiceHost;
using PlantMonitor.UI.ViewModels;
using PlantMonitor.UI.Views;

namespace PlantMonitor.UI;

public partial class App : Application
{
    public static IHost? Host { get; private set; }

    public App()
    {
        DispatcherUnhandledException += (_, e) =>
        {
            MessageBox.Show($"UI Error: {e.Exception.Message}\n\n{e.Exception.StackTrace}",
                "Plant Monitor", MessageBoxButton.OK, MessageBoxImage.Error);
            e.Handled = true;
        };
    }

    private void App_OnStartup(object sender, StartupEventArgs e)
    {
        try
        {
            var logFolder = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "PlantDoctor", "logs");

            Host = Microsoft.Extensions.Hosting.Host.CreateDefaultBuilder()
                .ConfigureServices(services =>
                {
                    services.AddSingleton<IPlantLogger>(_ => new JsonlPlantLogger(logFolder));
                    services.AddSingleton<ICommunicationHub, CommunicationHub>();
                    services.AddSingleton<IArtifactWriter, ArtifactWriter>();
                    services.AddSingleton<MainViewModel>();
                })
                .AddPlantMonitorHost()
                .Build();

            Host.Start();
            new MainWindow().Show();
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Fatal error: {ex.Message}\n\n{ex.StackTrace}",
                "Plant Monitor", MessageBoxButton.OK, MessageBoxImage.Error);
            Shutdown();
        }
    }

    private void App_OnExit(object sender, ExitEventArgs e)
    {
        Host?.Services.GetService<ICommunicationHub>()?.Stop();
        Host?.StopAsync().GetAwaiter().GetResult();
        Host?.Dispose();
    }
}
