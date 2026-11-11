using System.ComponentModel;

namespace OfflineDAoCLauncher.Models;

/// <summary>One row in the server list: a server plus the account used to log in to it.</summary>
public sealed class ServerAccount : INotifyPropertyChanged
{
    public const string DefaultServerName = "Local Host";
    public const string DefaultServerIp = "127.0.0.1";
    public const string DefaultWorldMode = "world-mode=1.65";

    public Guid Id { get; init; } = Guid.NewGuid();
    public string ServerName { get; set; } = DefaultServerName;

    /// <summary>Folder the user picked for the Godot Mono engine.</summary>
    public string GodotFolder { get; set; } = "";

    /// <summary>
    /// The game's Godot project folder (the one holding project.godot). Empty only on entries
    /// saved before this field existed; those fall back to searching near <see cref="GodotFolder"/>.
    /// </summary>
    public string ClientFolder { get; set; } = "";

    public string ServerIp { get; set; } = DefaultServerIp;
    public string AccountName { get; set; } = "";
    public string Password { get; set; } = "";

    /// <summary>Passed to the client as <c>--&lt;WorldMode&gt;</c>, e.g. <c>world-mode=1.65</c>.</summary>
    public string WorldMode { get; set; } = DefaultWorldMode;

    private bool? _isOnline;
    private string _statusText = "Checking...";

    /// <summary>Whether the server answered the last check; null until it has been checked. Not saved.</summary>
    public bool? IsOnline
    {
        get => _isOnline;
        set
        {
            if (_isOnline == value)
                return;
            _isOnline = value;
            OnPropertyChanged(nameof(IsOnline));
            OnPropertyChanged(nameof(IsOnlineKnown));
            OnPropertyChanged(nameof(IsOfflineKnown));
        }
    }

    public bool IsOnlineKnown => _isOnline == true;
    public bool IsOfflineKnown => _isOnline == false;

    /// <summary>Tooltip for the status dot, e.g. "Online (127.0.0.1:10300)". Not saved.</summary>
    public string StatusText
    {
        get => _statusText;
        set
        {
            if (_statusText == value)
                return;
            _statusText = value;
            OnPropertyChanged(nameof(StatusText));
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    private void OnPropertyChanged(string name) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

    public ServerAccount Clone()
    {
        var copy = (ServerAccount)MemberwiseClone();
        copy.PropertyChanged = null; // the copy must not notify the original's list row
        return copy;
    }

    /// <summary>What screen readers announce for the list row.</summary>
    public override string ToString() => $"{ServerName}, account {AccountName}";
}
