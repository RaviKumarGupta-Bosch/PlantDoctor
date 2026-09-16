using System.Windows;
using System.Windows.Threading;
using Microsoft.Extensions.Configuration;
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

    private void Exit_Click(object sender, RoutedEventArgs e) => Application.Current.Shutdown();

    private void About_Click(object sender, RoutedEventArgs e)
    {
        MessageBox.Show(
            "PlantSimulator v1.0\n\n" +
            "Industrial plant simulator for PlantDoctor hackathon.\n" +
            "Simulates sensors, COM port communication, and error injection.\n\n" +
            "Logs written to: %LOCALAPPDATA%\\PlantDoctor\\logs\\\n" +
            "This is the integration point with PlantDoctor.Agent (App 2).",
            "About PlantSimulator", MessageBoxButton.OK, MessageBoxImage.Information);
    }

    private void Settings_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new SettingsDialog();
        if (dlg.ShowDialog() == true)
        {
            // Note: In a full app, settings would be persisted and reloaded.
            // For this demo, the settings dialog demonstrates the UI pattern.
            MessageBox.Show("Settings applied (demo mode).", "Settings",
                MessageBoxButton.OK, MessageBoxImage.Information);
        }
    }
}
