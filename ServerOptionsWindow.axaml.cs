using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Layout;
using OfflineDAoCLauncher.Services;

namespace OfflineDAoCLauncher;

/// <summary>Edits the settings section of true165-rules.json. Closes with true when it saved.</summary>
public partial class ServerOptionsWindow : Window
{
    private const string OtherGroup = "Other";

    private readonly ServerRules _rules;
    private readonly List<Field> _fields = [];
    private bool _dirty;

    /// <summary>One editable setting: either a check box or a text box, plus the hint beside it.</summary>
    private sealed class Field(string key, SettingInfo info, CheckBox? check, TextBox? box, TextBlock hint)
    {
        public string Key { get; } = key;
        public SettingInfo Info { get; } = info;
        public CheckBox? Check { get; } = check;
        public TextBox? Box { get; } = box;
        public TextBlock Hint { get; } = hint;

        public string Text
        {
            get => Check is not null ? (Check.IsChecked == true ? "True" : "False") : Box!.Text ?? "";
            set
            {
                if (Check is not null)
                    Check.IsChecked = bool.TryParse(value, out var b) && b;
                else
                    Box!.Text = value;
            }
        }

        public Control Input => (Control?)Check ?? Box!;
    }

    public ServerOptionsWindow(ServerRules rules)
    {
        InitializeComponent();

        _rules = rules;
        PathRun.Text = rules.FilePath;

        // Group the settings the way the server does; within a group keep the file's order.
        var groupOrder = ServerRules.Known.Select(k => k.Group).Distinct().Append(OtherGroup).ToList();
        var fields = rules.Entries
            .Select(e => (Entry: e, Info: ServerRules.Describe(e.Key) ?? Unknown(e.Key, e.IsString)))
            .GroupBy(x => x.Info.Group)
            .OrderBy(g => groupOrder.IndexOf(g.Key));

        foreach (var group in fields)
        {
            Rows.Children.Add(new TextBlock { Classes = { "group" }, Text = group.Key });
            foreach (var (entry, info) in group)
                AddRow(entry.Key, entry.Value, info);
        }

        DefaultsButton.IsEnabled = _fields.Any(f => f.Info.Kind != SettingKind.Text);
        _dirty = false;
        Opened += (_, _) => _fields.FirstOrDefault()?.Input.Focus();
        Closing += OnClosing;
    }

    /// <summary>A setting the launcher has no description for: still editable, as text.</summary>
    private static SettingInfo Unknown(string key, bool isString) =>
        new(key, OtherGroup, key, SettingKind.Text, "",
            isString ? "No checks are made on this value." : "Not a text value in the file; it is kept as it is.");

    private void AddRow(string key, string value, SettingInfo info)
    {
        var row = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("210,150,*"),
            MinHeight = 24,
            Margin = new Thickness(0, 1),
        };
        ToolTip.SetTip(row, key);

        var label = new TextBlock { Classes = { "label" }, Text = info.Label + ":" };
        var hint = new TextBlock { Classes = { "hint" } };
        Grid.SetColumn(hint, 2);

        CheckBox? check = null;
        TextBox? box = null;
        Control input;
        if (info.Kind == SettingKind.Bool)
        {
            check = new CheckBox { VerticalAlignment = VerticalAlignment.Center };
            check.IsCheckedChanged += (_, _) => Changed();
            input = check;
        }
        else
        {
            box = new TextBox();
            box.TextChanged += (_, _) => Changed();
            input = new ContentControl { Theme = (Avalonia.Styling.ControlTheme)this.FindResource("SunkenFrame")!, Content = box, VerticalAlignment = VerticalAlignment.Center };

            // A value that is not text (a number or list in the JSON) is shown but not edited.
            if (info.Kind == SettingKind.Text && !_rules.Entries.First(e => e.Key == key).IsString)
                box.IsReadOnly = true;
        }

        Grid.SetColumn(input, 1);
        row.Children.Add(label);
        row.Children.Add(input);
        row.Children.Add(hint);
        Rows.Children.Add(row);

        var field = new Field(key, info, check, box, hint);
        _fields.Add(field);
        field.Text = value;
        UpdateHint(field);
        if (box is not null)
            box.TextChanged += (_, _) => UpdateHint(field);
    }

    private void Changed()
    {
        _dirty = true;
        ErrorText.IsVisible = false;
    }

    /// <summary>Shows the setting's help, or what is wrong with the value typed in.</summary>
    private static void UpdateHint(Field field)
    {
        var (value, error) = field.Info.Normalize(field.Text);
        field.Hint.Classes.Set("bad", error is not null);
        if (error is not null)
            field.Hint.Text = "Value " + error;
        else if (field.Info.Kind == SettingKind.Long && field.Info.Group.StartsWith("Housing"))
            field.Hint.Text = $"= {ServerRules.FormatCopper(long.Parse(value!))}   (default {ServerRules.FormatCopper(long.Parse(field.Info.Default))})";
        else
            field.Hint.Text = field.Info.Help;
    }

    private void OnDefaultsClick(object? sender, RoutedEventArgs e)
    {
        foreach (var field in _fields.Where(f => f.Info.Kind != SettingKind.Text))
            field.Text = field.Info.Default;

        ErrorText.Text = "Default values filled in. Click Save to write them to the file.";
        ErrorText.Foreground = Avalonia.Media.Brushes.DarkGreen;
        ErrorText.IsVisible = true;
    }

    private async void OnSaveClick(object? sender, RoutedEventArgs e)
    {
        var values = new Dictionary<string, string>();
        foreach (var field in _fields)
        {
            if (field.Box?.IsReadOnly == true)
                continue;

            var (value, error) = field.Info.Normalize(field.Text);
            if (error is not null)
            {
                ErrorText.Text = $"{field.Info.Label}: {error}";
                ErrorText.Foreground = Avalonia.Media.Brushes.DarkRed;
                ErrorText.IsVisible = true;
                field.Input.BringIntoView();
                field.Input.Focus();
                return;
            }

            values[field.Key] = value!;
        }

        try
        {
            _rules.Save(values);
        }
        catch (RulesFileException ex)
        {
            await Dialogs.ShowMessage(this, "Server Options", ex.Message);
            return;
        }

        _dirty = false;
        Close(true);
    }

    private void OnCancelClick(object? sender, RoutedEventArgs e) => Close(false);

    private bool _confirmedDiscard;

    private async void OnClosing(object? sender, WindowClosingEventArgs e)
    {
        if (!_dirty || _confirmedDiscard)
            return;

        e.Cancel = true;
        if (await Dialogs.Confirm(this, "Server Options", "Discard your changes to the server options?"))
        {
            _confirmedDiscard = true;
            Close(false);
        }
    }
}
