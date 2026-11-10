using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.VisualTree;

namespace OfflineDAoCLauncher;

/// <summary>Small message and confirm boxes (Avalonia has none built in).</summary>
public static class Dialogs
{
    public static Task ShowMessage(Window owner, string title, string message) =>
        Show(owner, title, message, "OK");

    public static async Task<bool> Confirm(Window owner, string title, string message) =>
        await Show(owner, title, message, "Yes", "No") == 1;

    /// <summary>Returns the 1-based index of the button pressed, or 0 when the box was dismissed.</summary>
    private static Task<int> Show(Window owner, string title, string message, params string[] buttons)
    {
        var window = new Window
        {
            Title = title,
            Icon = owner.Icon,
            SizeToContent = SizeToContent.WidthAndHeight,
            CanResize = false,
            CanMinimize = false,
            ShowInTaskbar = false,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
        };

        var buttonRow = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Center,
            Spacing = 8,
        };

        for (var i = 0; i < buttons.Length; i++)
        {
            var result = i + 1;
            var button = new Button
            {
                Content = buttons[i],
                IsDefault = i == 0,
                IsCancel = i == buttons.Length - 1,
            };
            button.Click += (_, _) => window.Close(result);
            buttonRow.Children.Add(button);
        }

        window.Content = new StackPanel
        {
            Margin = new Thickness(18, 16, 18, 12),
            Spacing = 16,
            Children =
            {
                new TextBlock
                {
                    Text = message,
                    TextWrapping = TextWrapping.Wrap,
                    MinWidth = 220,
                    MaxWidth = 430,
                },
                buttonRow,
            },
        };

        window.Opened += (_, _) => (buttonRow.Children[0] as Button)?.Focus();
        return window.ShowDialog<int>(owner);
    }

    /// <summary>True when a double-click landed on a list row rather than on empty space or the scroll bar.</summary>
    public static bool IsOnListRow(RoutedEventArgs e) =>
        (e.Source as Visual)?.FindAncestorOfType<ListBoxItem>(includeSelf: true) is not null;
}
