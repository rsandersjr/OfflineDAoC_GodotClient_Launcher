using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using OfflineDAoCLauncher.Models;
using OfflineDAoCLauncher.Services;

namespace OfflineDAoCLauncher;

/// <summary>Add, edit and delete server accounts. Every change is saved straight away.</summary>
public partial class AccountsWindow : Window
{
    private readonly AccountStore _store;

    public AccountsWindow(AccountStore store, Guid? selectedId)
    {
        InitializeComponent();

        _store = store;
        AccountList.ItemsSource = store.Accounts;
        AccountList.SelectedItem =
            store.Accounts.FirstOrDefault(a => a.Id == selectedId) ?? store.Accounts.FirstOrDefault();
        OnSelectionChanged();

        Opened += (_, _) => AccountList.Focus();
    }

    /// <summary>The entry selected when the window closed, so the main list can select it too.</summary>
    public Guid? SelectedId { get; private set; }

    private ServerAccount? Selected => AccountList.SelectedItem as ServerAccount;

    private void OnSelectionChanged(object? sender, SelectionChangedEventArgs e) => OnSelectionChanged();

    private void OnSelectionChanged()
    {
        SelectedId = Selected?.Id;
        EditButton.IsEnabled = Selected is not null;
        DeleteButton.IsEnabled = Selected is not null;
    }

    private async void OnAddClick(object? sender, RoutedEventArgs e)
    {
        // Most people have one game install, so start from the folders used last time.
        var last = _store.Accounts.LastOrDefault();
        var draft = new ServerAccount
        {
            GodotFolder = last?.GodotFolder ?? "",
            ClientFolder = last?.ClientFolder ?? "",
        };

        var added = await new AccountEditWindow(draft, isNew: true).ShowDialog<ServerAccount?>(this);
        if (added is null)
            return;

        _store.Accounts.Add(added);
        AccountList.SelectedItem = added;
        await Save();
    }

    private async void OnEditClick(object? sender, RoutedEventArgs e) => await EditSelected();

    private async void OnListDoubleTapped(object? sender, TappedEventArgs e)
    {
        if (Dialogs.IsOnListRow(e))
            await EditSelected();
    }

    private async Task EditSelected()
    {
        if (Selected is not { } current)
            return;

        var edited = await new AccountEditWindow(current.Clone(), isNew: false).ShowDialog<ServerAccount?>(this);
        if (edited is null)
            return;

        var index = _store.Accounts.IndexOf(current);
        if (index < 0)
            return;

        _store.Accounts[index] = edited;
        AccountList.SelectedItem = edited;
        await Save();
    }

    private async void OnDeleteClick(object? sender, RoutedEventArgs e)
    {
        if (Selected is not { } current)
            return;

        var confirmed = await Dialogs.Confirm(this, "Delete Server Account",
            $"Delete \"{current.ServerName}\" (account \"{current.AccountName}\")?\n\n" +
            "This only removes it from the launcher. The account on the server is not touched.");
        if (!confirmed)
            return;

        var index = _store.Accounts.IndexOf(current);
        _store.Accounts.Remove(current);
        if (_store.Accounts.Count > 0)
            AccountList.SelectedItem = _store.Accounts[Math.Min(index, _store.Accounts.Count - 1)];
        OnSelectionChanged();
        await Save();
    }

    private void OnCloseClick(object? sender, RoutedEventArgs e) => Close();

    private async Task Save()
    {
        try
        {
            _store.Save();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            await Dialogs.ShowMessage(this, "Server Accounts",
                $"The change could not be saved to:\n{_store.FilePath}\n\n{ex.Message}");
        }
    }
}
