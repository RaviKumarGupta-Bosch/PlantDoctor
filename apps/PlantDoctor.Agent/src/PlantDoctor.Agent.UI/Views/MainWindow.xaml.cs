using System.Windows;
using Microsoft.Extensions.DependencyInjection;
using PlantDoctor.Agent.UI.ViewModels;

namespace PlantDoctor.Agent.UI.Views;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        DataContext = App.Host!.Services.GetRequiredService<MainViewModel>();
    }
}
