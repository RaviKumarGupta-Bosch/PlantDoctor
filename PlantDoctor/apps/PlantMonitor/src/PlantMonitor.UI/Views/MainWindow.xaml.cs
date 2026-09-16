using System.Windows;
using Microsoft.Extensions.DependencyInjection;
using PlantDoctor.Contracts;
using PlantMonitor.UI.ViewModels;

namespace PlantMonitor.UI.Views;

public partial class MainWindow : Window
{
    private readonly MainViewModel _vm;

    public MainWindow()
    {
        InitializeComponent();
        _vm = App.Host!.Services.GetRequiredService<MainViewModel>();
        DataContext = _vm;
        Loaded += (_, _) => _vm.StartAllCommand.Execute(null);
    }

    protected override void OnClosed(EventArgs e)
    {
        _vm.DisconnectAllCommand.Execute(null);
        base.OnClosed(e);
    }

    private void Exit_Click(object sender, RoutedEventArgs e) => Application.Current.Shutdown();

    private void About_Click(object sender, RoutedEventArgs e) =>
        MessageBox.Show(
            "Plant Monitor v1.0 — main application\n\n" +
            "Listens for the Device Simulator's sensors and scanner, shows their connection\n" +
            "state and emitted data, and writes the JSONL log consumed by PlantDoctor.Agent.\n\n" +
            $"  Temperature  {TransportEndpoints.DescribeFor(TransportChannel.NamedPipe)}\n" +
            $"  Vibration    {TransportEndpoints.DescribeFor(TransportChannel.Tcp)}\n" +
            $"  Pressure     {TransportEndpoints.DescribeFor(TransportChannel.Bluetooth)}\n" +
            $"  Scanner      {TransportEndpoints.DescribeFor(TransportChannel.ComPort)}\n\n" +
            $"Log file: {_vm.LogFilePath}",
            "About Plant Monitor", MessageBoxButton.OK, MessageBoxImage.Information);
}
