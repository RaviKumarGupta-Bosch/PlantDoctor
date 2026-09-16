using System.Windows;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using PlantDoctor.Agent.Core.Ai;
using PlantDoctor.Agent.Core.Artifact;
using PlantDoctor.Agent.Core.Logs;
using PlantDoctor.Agent.UI.ViewModels;

namespace PlantDoctor.Agent.UI;

public partial class App : Application
{
    public static IHost? Host { get; private set; }

    protected override void OnStartup(StartupEventArgs e)
    {
        Host = Microsoft.Extensions.Hosting.Host.CreateDefaultBuilder()
            .ConfigureServices((ctx, services) =>
            {
                services.Configure<OllamaOptions>(ctx.Configuration.GetSection("Ollama"));
                services.AddHttpClient<IOllamaClient, OllamaClient>();
                var logFolder = Environment.ExpandEnvironmentVariables(
                    ctx.Configuration["Agent:LogFolder"] ?? @"%LOCALAPPDATA%\PlantDoctor\logs");
                services.AddSingleton<ILogTailer>(_ => new JsonlLogTailer(logFolder));
                services.AddSingleton<IArtifactWriter, ArtifactWriter>();
                services.AddSingleton<MainViewModel>();
            })
            .Build();
        base.OnStartup(e);
    }
}
