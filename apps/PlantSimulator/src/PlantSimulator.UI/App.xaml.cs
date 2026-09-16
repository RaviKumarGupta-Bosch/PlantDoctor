using System.IO;
using System.Windows;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using PlantSimulator.Core.Com;
using PlantSimulator.Core.Errors;
using PlantSimulator.Core.Logging;
using PlantSimulator.Core.Scanner;
using PlantSimulator.Core.Sensors;
using PlantSimulator.Core.Transports;
using PlantSimulator.ServiceHost;
using PlantSimulator.UI.ViewModels;
using PlantSimulator.UI.Views;

namespace PlantSimulator.UI;

public partial class App : Application
{
    public static IHost? Host { get; private set; }

    public App()
    {
        // Global exception handlers to catch any unhandled errors
        this.DispatcherUnhandledException += (s, e) =>
        {
            MessageBox.Show($"UI Error: {e.Exception.Message}\n\n{e.Exception.StackTrace}", "Application Error",
                MessageBoxButton.OK, MessageBoxImage.Error);
            e.Handled = true;
            Shutdown();
        };
        AppDomain.CurrentDomain.UnhandledException += (s, e) =>
        {
            if (e.ExceptionObject is Exception ex)
            {
                MessageBox.Show($"Fatal Error: {ex.Message}\n\n{ex.StackTrace}", "Application Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
            Shutdown();
        };
    }

    private void App_OnStartup(object sender, StartupEventArgs e)
    {
        try
        {
            InitializeApp();
            // Start the host (which includes the WCF service)
            Host!.Start();
            // Show MainWindow after DI container is built and service is running
            var mainWindow = new MainWindow();
            mainWindow.Show();
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Fatal error: {ex.Message}\n\n{ex.StackTrace}", "Application Error",
                MessageBoxButton.OK, MessageBoxImage.Error);
            Shutdown();
        }
    }

    private void InitializeApp()
    {
        var logFolder = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "PlantDoctor", "logs");

        Host = Microsoft.Extensions.Hosting.Host.CreateDefaultBuilder()
            .ConfigureServices(services =>
            {
                services.AddSingleton<IPlantLogger>(_ => new JsonlPlantLogger(logFolder));
                services.AddSingleton<IEnumerable<SensorDefinition>>(_ => DefaultSensors.Build());
                services.AddSingleton<ISensorSimulationService, SensorSimulationService>();
                services.AddSingleton<IComPortSimulator, ComPortSimulator>();
                services.AddSingleton<IErrorInjector, ErrorInjector>();
                services.AddSingleton<IScannerSimulator, ScannerSimulator>();
                services.AddSingleton<ICommunicationHub, CommunicationHub>();
                services.AddSingleton<NamedPipeSensorClient>();
                services.AddSingleton<TcpSensorClient>();
                services.AddSingleton<BluetoothSimSensorClient>();
                services.AddSingleton<MainViewModel>();
            })
            .AddPlantMonitorHost()
            .Build();
    }

    private void App_OnExit(object sender, ExitEventArgs e)
    {
        // Stop the host (which closes the WCF service) then dispose the DI container
        Host?.StopAsync().GetAwaiter().GetResult();
        Host?.Dispose();
    }
}
