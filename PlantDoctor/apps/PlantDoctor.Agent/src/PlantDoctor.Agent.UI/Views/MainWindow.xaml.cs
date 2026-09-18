using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Microsoft.Extensions.DependencyInjection;
using PlantDoctor.Agent.UI.ViewModels;

namespace PlantDoctor.Agent.UI.Views;

public partial class MainWindow : Window
{
    private ListBox? _chatListBox;

    public MainWindow()
    {
        InitializeComponent();
        DataContext = App.Host!.Services.GetRequiredService<MainViewModel>();

        // Get reference to chat ListBox for auto-scroll
        _chatListBox = FindName("ChatListBox") as ListBox;
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

    private void ChatScrollViewer_ScrollChanged(object sender, ScrollChangedEventArgs e)
    {
        // Auto-scroll to bottom when new messages are added
        if (e.ExtentHeightChange != 0)
        {
            var scrollViewer = e.Source as ScrollViewer;
            scrollViewer?.ScrollToBottom();
        }
    }
}
