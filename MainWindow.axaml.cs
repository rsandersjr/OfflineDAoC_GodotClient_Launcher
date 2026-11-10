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

        The game server has to be running before the client can log in. For a local server the IP is 127.0.0.1.

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
        _store.Accounts.CollectionChanged += (_, _) => UpdateEmptyHint();
        Select(_store.LastSelectedId);
        UpdateEmptyHint();

        NewsText.Text = LoadNews();
        LoadCustomLogo();

        _launchCooldown.Tick += (_, _) => EndLaunchCooldown();
        Opened += OnOpened;
        Closing += (_, _) => RememberSelection();
    }

    private async void OnOpened(object? sender, EventArgs e)
    {
        ServerList.Focus();
        if (_store.LoadWarning is { } warning)
            await Dialogs.ShowMessage(this, "Server Accounts", warning);
    }

    private async void OnPlayClick(object? sender, RoutedEventArgs e) => await Play();

    private async void OnServerListDoubleTapped(object? sender, TappedEventArgs e)
    {
        if (Dialogs.IsOnListRow(e))
            await Play();
    }

    private async Task Play()
    {
        if (!PlayButton.IsEnabled)
            return;

        if (ServerList.SelectedItem is not ServerAccount account)
        {
            await Dialogs.ShowMessage(this, "Play",
                _store.Accounts.Count == 0
                    ? "There are no server accounts yet.\n\nClick \"Server Accounts\" to add one."
                    : "Choose a server in the list first.");
            return;
        }

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
        PlayButton.IsEnabled = true;
    }

    private async void OnAccountsClick(object? sender, RoutedEventArgs e)
    {
        var selectedId = (ServerList.SelectedItem as ServerAccount)?.Id;
        var accounts = new AccountsWindow(_store, selectedId);
        await accounts.ShowDialog(this);
        Select(accounts.SelectedId ?? selectedId);
        ServerList.Focus();
    }

    private async void OnHomepageClick(object? sender, RoutedEventArgs e)
    {
        var opened = false;
        try
        {
            opened = await Launcher.LaunchUriAsync(Homepage);
        }
        catch (Exception)
        {
            // Reported below.
        }

        if (!opened)
            await Dialogs.ShowMessage(this, "Homepage", $"Could not open your web browser. The homepage is:\n\n{Homepage}");
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
