using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Platform.Storage;
using OfflineDAoCLauncher.Models;
using OfflineDAoCLauncher.Services;

namespace OfflineDAoCLauncher;

/// <summary>The add/edit form. Closes with the filled-in account, or null when cancelled.</summary>
public partial class AccountEditWindow : Window
{
    private static readonly IBrush Good = new SolidColorBrush(Color.Parse("#005A00"));
    private static readonly IBrush Bad = new SolidColorBrush(Color.Parse("#A00000"));
    private static readonly IBrush Neutral = new SolidColorBrush(Color.Parse("#404040"));

    private readonly ServerAccount _account;
    private int _godotCheck;
    private int _clientCheck;

    public AccountEditWindow(ServerAccount account, bool isNew)
    {
        InitializeComponent();

        _account = account;
        Title = isNew ? "Add Server Account" : "Edit Server Account";

        ServerNameBox.Text = account.ServerName;
        GodotFolderBox.Text = account.GodotFolder;
        ClientFolderBox.Text = account.ClientFolder;
        ServerIpBox.Text = account.ServerIp;
        AccountNameBox.Text = account.AccountName;
        PasswordBox.Text = account.Password;
        WorldModeBox.Text = account.WorldMode;

        // A message from a failed OK goes away as soon as anything is corrected.
        foreach (var box in new[]
                 {
                     ServerNameBox, GodotFolderBox, ClientFolderBox, ServerIpBox,
                     AccountNameBox, PasswordBox, WorldModeBox,
                 })
            box.TextChanged += (_, _) => ErrorText.IsVisible = false;

        _ = CheckGodotFolder();
        _ = CheckClientFolder();
        Opened += (_, _) =>
        {
            ServerNameBox.Focus();
            ServerNameBox.SelectAll();
        };
    }

    private void OnGodotFolderChanged(object? sender, TextChangedEventArgs e) => _ = CheckGodotFolder();

    private void OnClientFolderChanged(object? sender, TextChangedEventArgs e) => _ = CheckClientFolder();

    /// <summary>Shows, under the Godot Mono folder box, whether the engine was found there.</summary>
    private async Task CheckGodotFolder()
    {
        var check = ++_godotCheck;
        var folder = (GodotFolderBox.Text ?? "").Trim();

        if (folder.Length == 0)
        {
            Show(FolderStatus, "Pick the folder that holds the Godot Mono executable.", Neutral);
            return;
        }

        // Wait for typing to pause, then look on a worker thread so a slow disk cannot freeze the form.
        await Task.Delay(250);
        if (check != _godotCheck)
            return;

        var (exists, executable, nearbyClient) = await Task.Run(() =>
            (Directory.Exists(folder), GameLauncher.FindExecutable(folder), GameLauncher.GuessClientFolder(folder)));
        if (check != _godotCheck)
            return;

        if (!exists)
            Show(FolderStatus, "This folder does not exist.", Bad);
        else if (executable is null)
            Show(FolderStatus, "No Godot Mono executable found in this folder.", Bad);
        else
            Show(FolderStatus, $"Found {Path.GetFileName(executable)}.", Good);

        // Save a trip to the second Browse button when the game sits right next to the engine.
        if (nearbyClient is not null && string.IsNullOrWhiteSpace(ClientFolderBox.Text))
            ClientFolderBox.Text = nearbyClient;
    }

    /// <summary>Shows, under the client folder box, whether the game was found there.</summary>
    private async Task CheckClientFolder()
    {
        var check = ++_clientCheck;
        var folder = (ClientFolderBox.Text ?? "").Trim();

        if (folder.Length == 0)
        {
            Show(ClientStatus,
                $"Pick the game's \"{GameLauncher.ProjectFolderName}\" folder, the one that holds {GameLauncher.ProjectFileName}.",
                Neutral);
            return;
        }

        await Task.Delay(250);
        if (check != _clientCheck)
            return;

        var (exists, client, generated) = await Task.Run(() =>
        {
            var found = GameLauncher.FindClientFolder(folder);
            return (Directory.Exists(folder), found, GeneratedAccount.Find(found));
        });
        if (check != _clientCheck)
            return;

        if (!exists)
        {
            Show(ClientStatus, "This folder does not exist.", Bad);
            return;
        }

        if (client is null)
        {
            Show(ClientStatus, $"No {GameLauncher.ProjectFileName} found in this folder.", Bad);
            return;
        }

        var status = SamePath(client, folder)
            ? $"Found {GameLauncher.ProjectFileName}."
            : $"Found {GameLauncher.ProjectFileName} in the \"{Path.GetFileName(client)}\" folder inside it.";

        // No account typed in yet: use the one the game generated for itself.
        if (generated is not null && string.IsNullOrWhiteSpace(AccountNameBox.Text))
        {
            AccountNameBox.Text = generated.AccountName;
            PasswordBox.Text = generated.Password;
            status += $" Account \"{generated.AccountName}\" filled in from {generated.SourceFile}";
        }

        Show(ClientStatus, status, Good);
    }

    private static void Show(TextBlock status, string text, IBrush brush)
    {
        status.Text = text;
        status.Foreground = brush;
    }

    private static bool SamePath(string a, string b) => string.Equals(
        Path.TrimEndingDirectorySeparator(Path.GetFullPath(a)),
        Path.TrimEndingDirectorySeparator(Path.GetFullPath(b)),
        OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal);

    private async void OnBrowseGodotClick(object? sender, RoutedEventArgs e) =>
        await BrowseInto(GodotFolderBox, "Select Godot Mono Folder");

    private async void OnBrowseClientClick(object? sender, RoutedEventArgs e) =>
        await BrowseInto(ClientFolderBox, "Select Godot Client Folder");

    private async Task BrowseInto(TextBox box, string title)
    {
        var current = (box.Text ?? "").Trim();
        var start = Directory.Exists(current)
            ? await StorageProvider.TryGetFolderFromPathAsync(current)
            : null;

        var picked = await StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
        {
            Title = title,
            AllowMultiple = false,
            SuggestedStartLocation = start,
        });

        if (picked.Count > 0 && picked[0].TryGetLocalPath() is { } path)
            box.Text = path;
    }

    private void OnOkClick(object? sender, RoutedEventArgs e)
    {
        var edited = _account.Clone();
        edited.ServerName = (ServerNameBox.Text ?? "").Trim();
        edited.GodotFolder = (GodotFolderBox.Text ?? "").Trim();
        edited.ClientFolder = (ClientFolderBox.Text ?? "").Trim();
        edited.ServerIp = (ServerIpBox.Text ?? "").Trim();
        edited.AccountName = (AccountNameBox.Text ?? "").Trim();
        edited.Password = PasswordBox.Text ?? "";
        edited.WorldMode = (WorldModeBox.Text ?? "").Trim().TrimStart('-');

        if (Validate(edited) is { } problem)
        {
            ErrorText.Text = problem.Message;
            ErrorText.IsVisible = true;
            problem.Box.Focus();
            return;
        }

        // Keep the folder that really holds project.godot, even if the one above it was picked.
        edited.ClientFolder = GameLauncher.FindClientFolder(edited.ClientFolder) ?? edited.ClientFolder;
        Close(edited);
    }

    private (string Message, TextBox Box)? Validate(ServerAccount account)
    {
        if (account.ServerName.Length == 0)
            return ("Enter a server name.", ServerNameBox);

        if (account.GodotFolder.Length == 0)
            return ("Select the Godot Mono folder.", GodotFolderBox);

        if (GameLauncher.FindExecutable(account.GodotFolder) is null)
            return (Directory.Exists(account.GodotFolder)
                ? "No Godot Mono executable was found in the Godot Mono folder."
                : "The Godot Mono folder does not exist.", GodotFolderBox);

        if (account.ClientFolder.Length == 0)
            return ("Select the Godot client folder.", ClientFolderBox);

        if (GameLauncher.FindClientFolder(account.ClientFolder) is null)
            return (Directory.Exists(account.ClientFolder)
                ? $"No {GameLauncher.ProjectFileName} was found in the Godot client folder. Pick the game's \"{GameLauncher.ProjectFolderName}\" folder."
                : "The Godot client folder does not exist.", ClientFolderBox);

        if (account.ServerIp.Length == 0 || account.ServerIp.Any(char.IsWhiteSpace))
            return ("Enter the server IP address, for example 127.0.0.1.", ServerIpBox);

        if (account.AccountName.Length == 0)
            return ("Enter the account name.", AccountNameBox);

        if (account.WorldMode.Length == 0 || account.WorldMode.Any(char.IsWhiteSpace))
            return ($"Enter the world mode, for example {ServerAccount.DefaultWorldMode}.", WorldModeBox);

        return null;
    }

    private void OnCancelClick(object? sender, RoutedEventArgs e) => Close(null);
}
