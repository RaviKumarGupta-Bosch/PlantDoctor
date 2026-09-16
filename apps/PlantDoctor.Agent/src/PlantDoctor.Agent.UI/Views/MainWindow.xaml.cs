using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
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

    private void ChatTextBox_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter && (Keyboard.Modifiers & ModifierKeys.Control) == ModifierKeys.Control)
        {
            var viewModel = (MainViewModel)DataContext!;
            if (viewModel.SendChatCommand.CanExecute(null))
            {
                viewModel.SendChatCommand.Execute(null);
                e.Handled = true;
            }
        }
    }

    private void FilterTextBox_GotFocus(object sender, RoutedEventArgs e)
    {
        var textBox = (TextBox)sender;
        if (textBox.Text == "Search logs...")
        {
            textBox.Text = string.Empty;
            textBox.Foreground = System.Windows.Media.Brushes.Black;
        }
    }

    private void FilterTextBox_LostFocus(object sender, RoutedEventArgs e)
    {
        var textBox = (TextBox)sender;
        if (string.IsNullOrWhiteSpace(textBox.Text))
        {
            textBox.Text = "Search logs...";
            textBox.Foreground = System.Windows.Media.Brushes.Gray;
        }
    }

    private void ChatInputTextBox_GotFocus(object sender, RoutedEventArgs e)
    {
        var textBox = (TextBox)sender;
        if (textBox.Text == "Type your message... (Ctrl+Enter to send)")
        {
            textBox.Text = string.Empty;
            textBox.Foreground = System.Windows.Media.Brushes.Black;
        }
    }

    private void ChatInputTextBox_LostFocus(object sender, RoutedEventArgs e)
    {
        var textBox = (TextBox)sender;
        if (string.IsNullOrWhiteSpace(textBox.Text))
        {
            textBox.Text = "Type your message... (Ctrl+Enter to send)";
            textBox.Foreground = System.Windows.Media.Brushes.Gray;
        }
    }
}
