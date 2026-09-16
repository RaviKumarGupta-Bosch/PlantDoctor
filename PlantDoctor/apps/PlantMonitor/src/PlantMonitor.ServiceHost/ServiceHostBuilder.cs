using CoreWCF;
using CoreWCF.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using PlantDoctor.Contracts;
using System.Runtime.Versioning;

namespace PlantMonitor.ServiceHost;

/// <summary>Self-hosts <see cref="PlantMonitorService"/> over net.pipe for UI/service separation.
/// Future extension point: expose net.tcp so PlantDoctor.Agent can subscribe cross-process.</summary>
public static class ServiceHostBuilder
{
    public const string PipeBaseAddress = "net.pipe://localhost/PlantDoctor";

    public static IHostBuilder AddPlantMonitorHost(this IHostBuilder builder) =>
        builder.ConfigureServices(services =>
        {
            services.AddServiceModelServices();
            services.AddSingleton<PlantMonitorService>();
            services.AddSingleton<IPlantMonitorService>(sp => sp.GetRequiredService<PlantMonitorService>());
        });

    [SupportedOSPlatform("windows")]
    public static void ConfigurePlantMonitorEndpoints(IServiceBuilder serviceBuilder) =>
        serviceBuilder
            .AddService<PlantMonitorService>(o => o.BaseAddresses.Add(new Uri(PipeBaseAddress)))
            .AddServiceEndpoint<PlantMonitorService, IPlantMonitorService>(
                new NetNamedPipeBinding(), "monitor");
}
