using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using OfflineDAoCLauncher.Models;
using OfflineDAoCLauncher.Services;

namespace OfflineDAoCLauncher;

public partial class MainWindow : Window
{
    private const string WelcomeText = "Welcome to Offline DAoC 1.65!";
    private static readonly Uri Homepage = new("https://github.com/shadowofze/OfflineDAoC-True165");
    private static readonly Uri LauncherHomepage = new("https://github.com/rsandersjr/OfflineDAoC_GodotClient_Launcher");

    private const string DefaultNews =
        """
        ================================

                Offline DAoC 1.65
                  Launcher Notes

        ================================

        ==========================
        Getting Started
        ==========================

        Welcome to Offline DAoC 1.65!

        1. Click "Server Accounts" and add an entry: a server name, the Godot Mono folder (the engine), the Godot client folder (the game), the server IP, and the account to log in with. If you leave the account blank, the one the game generated (account.txt) is filled in for you.

        2. Pick the entry in the list on the left and hit Play. Double-clicking an entry works too.

        Add as many entries as you like, one for each server or account you play on.

        ==========================
        Before You Play
        ==========================

        The game server has to be running before the client can log in. For a local server the IP is 127.0.0.1. Click "Server Control" to start it; Play turns on once the selected server's dot is green.

        ==========================
        Homepage
        ==========================

        Downloads, guides and updates:
        https://github.com/shadowofze/OfflineDAoC-True165

        Tip: put a file named news.txt next to the launcher to show your own text here.
        """;

    private readonly AccountStore _store;
    private readonly DispatcherTimer _launchCooldown = new() { Interval = TimeSpan.FromSeconds(4) };

    public MainWindow()
    {
        InitializeComponent();

        _store = AccountStore.Load();
        ServerList.ItemsSource = _store.Accounts;
        _store.Accounts.CollectionChanged += (_, _) =>
        {
            UpdateEmptyHint();
            _ = CheckServers(); // an added or edited entry may point at a different server
        };
        Select(_store.LastSelectedId);
        UpdateEmptyHint();
        ServerList.SelectionChanged += (_, _) => UpdatePlayButton();
        UpdatePlayButton();

        NewsText.Text = LoadNews();
        LoadCustomLogo();

        _launchCooldown.Tick += (_, _) => EndLaunchCooldown();
        _statusTimer.Tick += (_, _) => _ = CheckServers();
        Opened += OnOpened;
        Closing += (_, _) =>
        {
            _statusTimer.Stop();
            _statusCancel.Cancel();
            RememberSelection();
        };
    }

    private readonly DispatcherTimer _statusTimer = new() { Interval = TimeSpan.FromSeconds(5) };
    private readonly CancellationTokenSource _statusCancel = new();
    private bool _checking;
    private bool _checkAgain;

    /// <summary>
    /// Updates the status dot of every entry by connecting to the TCP login port named in its install's
    /// server config. Entries for the same server are checked once.
    /// </summary>
    private async Task CheckServers()
    {
        if (_checking)
        {
            _checkAgain = true;
            return;
        }

        _checking = true;
        try
        {
            do
            {
                _checkAgain = false;
                var accounts = _store.Accounts.ToList();
                var endpoints = await Task.Run(() => accounts.Select(ServerStatus.Resolve).ToList());
                var results = await Task.WhenAll(endpoints
                    .Select(ep => (ep.Host, ep.Port)).Distinct()
                    .Select(async key => (key, online: await ServerStatus.IsOnline(
                        new ServerEndpoint(key.Host, key.Port, null), _statusCancel.Token))));
                if (_statusCancel.IsCancellationRequested)
                    return;

                var online = results.ToDictionary(r => r.key, r => r.online);
                for (var i = 0; i < accounts.Count; i++)
                {
                    var ep = endpoints[i];
                    var up = online[(ep.Host, ep.Port)];
                    accounts[i].IsOnline = up;
                    accounts[i].StatusText = $"{(up ? "Online" : "Offline")} ({ep.Host}:{ep.Port})" +
                        (ep.ConfigFile is null ? "\nNo server config found; using the Server IP and port 10300." : $"\nFrom {ep.ConfigFile}");
                }
                UpdatePlayButton();
            } while (_checkAgain);
        }
        finally
        {
            _checking = false;
        }
    }

    private async void OnOpened(object? sender, EventArgs e)
    {
        ServerList.Focus();
        _ = CheckServers();
        _statusTimer.Start();
        if (_store.LoadWarning is { } warning)
            await Dialogs.ShowMessage(this, "Server Accounts", warning);
    }

    private async void OnPlayClick(object? sender, RoutedEventArgs e) => await Play();

    private async void OnServerListDoubleTapped(object? sender, TappedEventArgs e)
    {
        if (Dialogs.IsOnListRow(e))
            await Play();
    }

    /// <summary>
    /// Play is enabled only while a server is selected and its last status check found it online
    /// (and no launch is in progress). The tooltip says why it is disabled.
    /// </summary>
    private void UpdatePlayButton()
    {
        var account = ServerList.SelectedItem as ServerAccount;
        string? reason = _store.Accounts.Count == 0 ? "There are no server accounts yet. Click \"Server Accounts\" to add one."
            : account is null ? "Choose a server in the list first."
            : account.IsOnline is null ? $"Checking whether {account.ServerName} is online..."
            : account.IsOnline == false ? $"{account.ServerName} is offline. Start it with \"Server Control\", then Play turns on."
            : null;

        PlayButton.IsEnabled = reason is null && !_launchCooldown.IsEnabled;
        ToolTip.SetTip(PlayButton, reason);
    }

    private async Task Play()
    {
        UpdatePlayButton();
        if (!PlayButton.IsEnabled || ServerList.SelectedItem is not ServerAccount account)
            return;

        try
        {
            GameLauncher.Launch(account);
        }
        catch (LaunchException ex)
        {
            await Dialogs.ShowMessage(this, "Play", ex.Message);
            return;
        }

        // The client takes a few seconds to show its window; hold Play so a second click
        // does not start a second copy.
        BannerText.Text = $"Launching {account.ServerName}...";
        PlayButton.IsEnabled = false;
        _launchCooldown.Start();
        RememberSelection();
    }

    private void EndLaunchCooldown()
    {
        _launchCooldown.Stop();
        BannerText.Text = WelcomeText;
        UpdatePlayButton();
    }

    private async void OnAccountsClick(object? sender, RoutedEventArgs e)
    {
        var selectedId = (ServerList.SelectedItem as ServerAccount)?.Id;
        var accounts = new AccountsWindow(_store, selectedId);
        await accounts.ShowDialog(this);
        Select(accounts.SelectedId ?? selectedId);
        ServerList.Focus();
    }

    private async void OnServerControlClick(object? sender, RoutedEventArgs e)
    {
        if (ServerList.SelectedItem is not ServerAccount account)
        {
            await Dialogs.ShowMessage(this, "Server Control",
                _store.Accounts.Count == 0
                    ? "There are no server accounts yet.\n\nClick \"Server Accounts\" to add one; its Godot client folder tells the launcher where the server is installed."
                    : "Choose a server in the list first.");
            return;
        }

        try
        {
            ServerControl.Launch(account);
        }
        catch (LaunchException ex)
        {
            await Dialogs.ShowMessage(this, "Server Control", ex.Message);
            return;
        }

        RememberSelection();
        _ = CheckServers(); // the status dot turns green once the server is up
    }

    private async void OnHomepageClick(object? sender, RoutedEventArgs e) =>
        await OpenInBrowser("OfflineDAoC Homepage", Homepage);

    private async void OnLauncherHomepageClick(object? sender, RoutedEventArgs e) =>
        await OpenInBrowser("Launcher Homepage", LauncherHomepage);

    private async Task OpenInBrowser(string title, Uri page)
    {
        var opened = false;
        try
        {
            opened = await Launcher.LaunchUriAsync(page);
        }
        catch (Exception)
        {
            // Reported below.
        }

        if (!opened)
            await Dialogs.ShowMessage(this, title, $"Could not open your web browser. The page is:\n\n{page}");
    }

    private void OnExitClick(object? sender, RoutedEventArgs e) => Close();

    private void Select(Guid? id) =>
        ServerList.SelectedItem =
            _store.Accounts.FirstOrDefault(a => a.Id == id) ?? _store.Accounts.FirstOrDefault();

    private void UpdateEmptyHint() => EmptyHint.IsVisible = _store.Accounts.Count == 0;

    private void RememberSelection()
    {
        var id = (ServerList.SelectedItem as ServerAccount)?.Id;
        if (id is null || id == _store.LastSelectedId)
            return;

        _store.LastSelectedId = id;
        try
        {
            _store.Save();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // Only the remembered selection is lost; not worth interrupting the player.
        }
    }

    private static string LoadNews()
    {
        try
        {
            var custom = Path.Combine(AppContext.BaseDirectory, "news.txt");
            if (File.Exists(custom))
                return File.ReadAllText(custom);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // Fall back to the built-in text.
        }

        return DefaultNews;
    }

    private void LoadCustomLogo()
    {
        try
        {
            var custom = Path.Combine(AppContext.BaseDirectory, "logo.png");
            if (!File.Exists(custom))
                return;

            LogoImage.Source = new Bitmap(custom);
            LogoImage.IsVisible = true;
            Logo.IsVisible = false;
        }
        catch (Exception)
        {
            // Not a readable image: keep the built-in title mark.
        }
    }
}
