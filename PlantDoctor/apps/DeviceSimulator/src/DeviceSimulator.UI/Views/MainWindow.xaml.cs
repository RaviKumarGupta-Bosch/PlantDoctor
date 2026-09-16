using System.ComponentModel;
using System.Windows;
using System.Windows.Threading;
using DeviceSimulator.UI.ViewModels;
using Microsoft.Extensions.DependencyInjection;
using PlantDoctor.Contracts;

namespace DeviceSimulator.UI.Views;

public partial class MainWindow : Window
{
    private readonly MainViewModel _vm;
    private readonly DispatcherTimer _timer = new();
    private bool _ticking;

    public MainWindow()
    {
        InitializeComponent();
        _vm = App.Services!.GetRequiredService<MainViewModel>();
        DataContext = _vm;

        _vm.PropertyChanged += OnViewModelPropertyChanged;
        _timer.Interval = TimeSpan.FromMilliseconds(_vm.EmitIntervalMs);
        _timer.Tick += OnTimerTick;

        Loaded += async (_, _) =>
        {
            await _vm.OpenAllCommand.ExecuteAsync(null);
            _timer.Start();
        };
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(MainViewModel.EmitIntervalMs) && _vm.EmitIntervalMs >= 100)
            _timer.Interval = TimeSpan.FromMilliseconds(_vm.EmitIntervalMs);
    }

    private async void OnTimerTick(object? sender, EventArgs e)
    {
        if (_ticking) return; // a slow transport must not queue up overlapping emits
        _ticking = true;
        try { await _vm.TickAsync(); }
        finally { _ticking = false; }
    }

    protected override void OnClosed(EventArgs e)
    {
        _timer.Stop();
        _vm.CloseAllCommand.Execute(null);
        base.OnClosed(e);
    }

    private void Exit_Click(object sender, RoutedEventArgs e) => Application.Current.Shutdown();

    private void About_Click(object sender, RoutedEventArgs e) =>
        MessageBox.Show(
            "Device Simulator v1.0\n\n" +
            "Simulates the plant's field devices: three sensors and a barcode scanner.\n" +
            "Each device streams to the Plant Monitor main application over its own transport:\n\n" +
            $"  Temperature  {TransportEndpoints.DescribeFor(TransportChannel.NamedPipe)}\n" +
            $"  Vibration    {TransportEndpoints.DescribeFor(TransportChannel.Tcp)}\n" +
            $"  Pressure     {TransportEndpoints.DescribeFor(TransportChannel.Bluetooth)}\n" +
            $"  Scanner      {TransportEndpoints.DescribeFor(TransportChannel.ComPort)}\n\n" +
            "Start Plant Monitor first — it owns the listeners and the JSONL log.",
            "About Device Simulator", MessageBoxButton.OK, MessageBoxImage.Information);
}
