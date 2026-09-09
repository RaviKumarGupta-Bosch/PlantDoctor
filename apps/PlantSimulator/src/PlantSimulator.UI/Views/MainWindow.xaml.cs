using System.Windows;
using System.Windows.Threading;
using Microsoft.Extensions.DependencyInjection;
using PlantSimulator.UI.ViewModels;

namespace PlantSimulator.UI.Views;

public partial class MainWindow : Window
{
    private readonly DispatcherTimer _timer = new() { Interval = TimeSpan.FromSeconds(1.5) };

    public MainWindow()
    {
        InitializeComponent();
        var vm = App.Host!.Services.GetRequiredService<MainViewModel>();
        DataContext = vm;
        _timer.Tick += (_, _) => vm.Tick();
        _timer.Start();
    }
}
