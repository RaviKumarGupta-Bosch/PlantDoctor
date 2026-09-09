using System.Windows;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using PlantSimulator.Core.Com;
using PlantSimulator.Core.Errors;
using PlantSimulator.Core.Logging;
using PlantSimulator.Core.Sensors;
using PlantSimulator.UI.ViewModels;

namespace PlantSimulator.UI;

public partial class App : Application
{
    public static IHost? Host { get; private set; }

    protected override void OnStartup(StartupEventArgs e)
    {
        Host = Microsoft.Extensions.Hosting.Host.CreateDefaultBuilder()
            .ConfigureServices(services =>
            {
                var logFolder = Environment.ExpandEnvironmentVariables(@"%LOCALAPPDATA%\PlantDoctor\logs");
                services.AddSingleton<IPlantLogger>(_ => new JsonlPlantLogger(logFolder));
                services.AddSingleton<IEnumerable<SensorDefinition>>(_ => DefaultSensors.Build());
                services.AddSingleton<ISensorSimulationService, SensorSimulationService>();
                services.AddSingleton<IComPortSimulator, ComPortSimulator>();
                services.AddSingleton<IErrorInjector, ErrorInjector>();
                services.AddSingleton<MainViewModel>();
            })
            .Build();
        base.OnStartup(e);
    }

    protected override void OnExit(ExitEventArgs e)
    {
        Host?.Dispose();
        base.OnExit(e);
    }
}
