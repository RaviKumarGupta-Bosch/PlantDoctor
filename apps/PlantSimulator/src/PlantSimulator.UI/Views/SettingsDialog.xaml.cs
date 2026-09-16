using System.IO;
using System.Windows;
using System.Windows.Controls;

namespace PlantSimulator.UI.Views;

/// <summary>Simple settings dialog for adjusting sensor interval, log folder, and active sensors.</summary>
public class SettingsDialog : Window
{
    private readonly TextBox _intervalBox = new();
    private readonly TextBox _logFolderBox = new();
    private readonly ListBox _sensorList = new();
    private readonly List<string> _allSensors = DefaultSensors.Build().Select(s => s.Name).ToList();
    private readonly List<string> _activeSensors = new();

    public SettingsDialog()
    {
        Title = "Settings";
        Width = 400;
        Height = 350;
        ResizeMode = ResizeMode.NoResize;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;

        var interval = 1500;
        var logFolder = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "PlantDoctor", "logs");

        _activeSensors.AddRange(_allSensors);

        var intervalLabel = new TextBlock
        {
            Text = "Sensor Update Interval (ms):",
            Margin = new Thickness(0, 0, 0, 4)
        };
        _intervalBox = new TextBox
        {
            Text = "1500",
            Margin = new Thickness(0, 0, 0, 12)
        };

        var logFolderLabel = new TextBlock
        {
            Text = "Log Folder:",
            Margin = new Thickness(0, 0, 0, 4)
        };
        _logFolderBox = new TextBox
        {
            Text = logFolder,
            Margin = new Thickness(0, 0, 0, 12)
        };

        var sensorLabel = new TextBlock
        {
            Text = "Active Sensors (select all that apply):",
            Margin = new Thickness(0, 0, 0, 4)
        };
        _sensorList = new ListBox
        {
            SelectionMode = SelectionMode.Multiple,
            Height = 120,
            Margin = new Thickness(0, 0, 0, 12)
        };

        var okButton = new Button
        {
            Content = "OK",
            Width = 75,
            IsDefault = true
        };
        okButton.Click += Ok_Click;

        var cancelButton = new Button
        {
            Content = "Cancel",
            Width = 75,
            Margin = new Thickness(8, 0, 0, 0),
            IsCancel = true
        };

        var buttonPanel = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right,
            Margin = new Thickness(0, 8, 0, 0)
        };
        buttonPanel.Children.Add(okButton);
        buttonPanel.Children.Add(cancelButton);

        var mainPanel = new StackPanel { Margin = new Thickness(12) };
        mainPanel.Children.Add(intervalLabel);
        mainPanel.Children.Add(_intervalBox);
        mainPanel.Children.Add(logFolderLabel);
        mainPanel.Children.Add(_logFolderBox);
        mainPanel.Children.Add(sensorLabel);
        mainPanel.Children.Add(_sensorList);
        mainPanel.Children.Add(buttonPanel);

        Content = mainPanel;

        foreach (var s in _allSensors)
        {
            _sensorList.Items.Add(s);
            if (_activeSensors.Contains(s))
                _sensorList.SelectedItems.Add(s);
        }
    }

    private void Ok_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = true;
        Close();
    }

    public int IntervalMs => int.TryParse(_intervalBox.Text, out var v) ? v : 1500;
    public string LogFolder => _logFolderBox.Text;
    public string[] ActiveSensors => _sensorList.SelectedItems.Cast<string>().ToArray();
}
