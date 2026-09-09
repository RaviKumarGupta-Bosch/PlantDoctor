using CoreWCF;
using CoreWCF.Configuration;
using CoreWCF.Description;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using PlantSimulator.Contracts;

namespace PlantSimulator.ServiceHost;

/// <summary>Self-hosts <see cref="PlantMonitorService"/> over net.pipe for in-process UI/service separation.
/// Future extension point: expose net.tcp so PlantDoctor.Agent can subscribe cross-process.</summary>
public static class ServiceHostBuilder
{
    public const string PipeBaseAddress = "net.pipe://localhost/PlantDoctor";

    public static IHostBuilder AddPlantMonitorHost(this IHostBuilder builder) =>
        builder.ConfigureServices(services =>
        {
            services.AddServiceModelServices();
            services.AddServiceModelNetNamedPipe();
            services.AddSingleton<PlantMonitorService>();
            services.AddSingleton<IPlantMonitorService>(sp => sp.GetRequiredService<PlantMonitorService>());
        });

    public static void ConfigurePlantMonitorEndpoints(IServiceBuilder serviceBuilder) =>
        serviceBuilder
            .AddService<PlantMonitorService>(o => o.BaseAddresses.Add(new Uri(PipeBaseAddress)))
            .AddServiceEndpoint<PlantMonitorService, IPlantMonitorService>(
                new NetNamedPipeBinding(), "monitor");
}
