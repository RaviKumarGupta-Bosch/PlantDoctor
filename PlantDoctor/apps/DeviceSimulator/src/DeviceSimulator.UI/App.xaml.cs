using System.Windows;
using DeviceSimulator.Core.Com;
using DeviceSimulator.Core.Errors;
using DeviceSimulator.Core.Scanner;
using DeviceSimulator.Core.Sensors;
using DeviceSimulator.Core.Transports;
using DeviceSimulator.UI.ViewModels;
using DeviceSimulator.UI.Views;
using Microsoft.Extensions.DependencyInjection;

namespace DeviceSimulator.UI;

public partial class App : Application
{
    public static IServiceProvider? Services { get; private set; }

    public App()
    {
        DispatcherUnhandledException += (_, e) =>
        {
            MessageBox.Show($"UI Error: {e.Exception.Message}\n\n{e.Exception.StackTrace}",
                "Device Simulator", MessageBoxButton.OK, MessageBoxImage.Error);
            e.Handled = true;
        };
    }

    private void App_OnStartup(object sender, StartupEventArgs e)
    {
        try
        {
            var services = new ServiceCollection();
            services.AddSingleton<IEnumerable<SensorDefinition>>(_ => DefaultSensors.Build());
            services.AddSingleton<ISensorSimulationService, SensorSimulationService>();
            services.AddSingleton<IScannerSimulator, ScannerSimulator>();
            services.AddSingleton<IComPortSimulator, ComPortSimulator>();
            services.AddSingleton<IErrorInjector, ErrorInjector>();
            services.AddSingleton<DeviceTransportSet>();
            services.AddSingleton<MainViewModel>();
            Services = services.BuildServiceProvider();

            new MainWindow().Show();
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Fatal error: {ex.Message}\n\n{ex.StackTrace}",
                "Device Simulator", MessageBoxButton.OK, MessageBoxImage.Error);
            Shutdown();
        }
    }

    private void App_OnExit(object sender, ExitEventArgs e) =>
        (Services as IDisposable)?.Dispose();
}
