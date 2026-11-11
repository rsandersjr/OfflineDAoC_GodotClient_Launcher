# Offline DAoC 1.65 Launcher

A small retro-style launcher for [OfflineDAoC-True165](https://github.com/shadowofze/OfflineDAoC-True165).
It keeps a list of server accounts and starts the game client for the one you pick.

Used Claude to code in C# with [Avalonia](https://avaloniaui.net), so the same code runs on Windows, Linux and macOS.

<img width="646" height="481" alt="image" src="https://github.com/user-attachments/assets/2f110bcd-f00e-470e-b480-c792c7e0df13" />
<img width="583" height="322" alt="image" src="https://github.com/user-attachments/assets/b44f7f24-3d72-44aa-a7b2-59003444aa32" />
<img width="503" height="341" alt="image" src="https://github.com/user-attachments/assets/9089de8c-5c2d-4681-9f63-5df0cd7e5176" />


## Running it

Windows: run `dist\win-x64\OfflineDAoCLauncher.exe`. It is a single file with .NET built in, so it can be
copied anywhere and needs nothing installed.

## Using it

| Button | What it does |
| --- | --- |
| **Play** | Starts the client for the entry selected in the list. Double-clicking an entry or pressing Enter does the same. |
| **Server Accounts** | Add, edit and delete entries. |
| **Server Options** | Edits the server settings for the selected entry's install (see below). |
| **Homepage** | Opens the OfflineDAoC-True165 page in your browser. |
| **Exit** | Closes the launcher. |

The dot to the left of each server name shows whether that server is running: green is online, red is
offline, grey is not checked yet. Hover over it to see the address checked. Every 5 seconds the launcher
opens a TCP connection to the server's login port. The address and port come from the install's server config,
`runtime\server\config`, two folders up from the entry's Godot client folder. The file used is the one
`runtime\data\world-mode.txt` names (`serverconfig-165.xml` for 1.65 Mode), else `serverconfig.xml`.
Without a config the entry's Server IP and port 10300 are used.

Each server account has:

| Field | Default | Notes |
| --- | --- | --- |
| Server Name | `Local Host` | Any label you like. |
| Godot Mono Folder | | The engine: the folder holding the Godot Mono executable. |
| Godot Client Folder | | The game: the `godot` folder holding `project.godot`. |
| Server IP | `127.0.0.1` | |
| Account Name | | |
| Account Password | | |
| World Mode | `world-mode=1.65` | Passed to the client as `--world-mode=1.65`. |

### Which folders to pick

- **Godot Mono Folder**: the folder that contains `Godot_v4.7.2-stable_mono_win64.exe`.
- **Godot Client Folder**: the game's `godot` folder, the one that contains `project.godot`. Picking the
  folder one level above it works too; the launcher saves the `godot` folder inside it.

The two can be anywhere; they do not have to sit near each other. The form shows under each box whether
it found the engine and the game before you save. If a `godot` folder sits next to or above the engine
folder you pick, the client box is filled in for you, and you can change it.

A new entry starts with the folders of the entry added last, so both are usually picked only once.

### The generated account

The game writes the account it generates for you to `account.txt`:

```
Account: username_here
Password: password_here
```

When the Account Name box is empty and a client folder is picked, the launcher fills in Account Name and
Account Password from that file. It reads the install's `runtime` folder: for the client folder
`C:\OfflineDAoC-True165\new-client\godot` that is `C:\OfflineDAoC-True165\runtime\account.txt`. An account you
typed in yourself is never replaced. To pull the generated account into an existing entry, clear its
Account Name and pick the client folder again.

### What Play runs

From the folder above the client folder:

```
Godot_v4.7.2-stable_mono_win64.exe --path godot -- --server=<Server IP> --account=<Account Name> --password=<Account Password> --<World Mode>
```

## Server Options

Edits the `settings` section of `true165-rules.json`, found in `runtime\true165`, two folders up from the
selected entry's Godot client folder. For the client folder `C:\OfflineDAoC-True165\new-client\godot` that is
`C:\OfflineDAoC-True165\runtime\true165\true165-rules.json`. Nothing else in the file is changed.

Each value is checked against the type the server reads it as (on/off, a number, a whole number in range,
or a list of region IDs such as `27;165;233-242;244`) before it is saved. Housing rents are in copper; the
form shows the amount in platinum, gold, silver and copper next to each one.

**Restore Defaults** fills in the values the 1.65 install ships with; click **Save** to write them. Changes
take effect the next time the server starts.

## Where accounts are saved

| Platform | File |
| --- | --- |
| Windows | `%APPDATA%\OfflineDAoCLauncher\accounts.json` |
| Linux | `~/.config/OfflineDAoCLauncher/accounts.json` |
| macOS | `~/Library/Application Support/OfflineDAoCLauncher/accounts.json` |

On Windows, passwords in that file are encrypted for your Windows user (DPAPI). Linux and macOS have no
built-in equivalent, so there the password is only encoded and the file is readable by your user alone.
On every platform the password is handed to the client on its command line, because that is how the client
takes it.

To keep the accounts somewhere else, set the `OFFLINEDAOC_LAUNCHER_ACCOUNTS` environment variable to the
full path of the file to use.

## Customising

Put either of these next to the launcher executable:

- `news.txt` replaces the text in the News / Patch Messages panel.
- `logo.png` replaces the title mark in the top-left corner (about 190 x 80).

## Building

Needs the [.NET 10 SDK](https://dotnet.microsoft.com/download).

Run from source:

```
dotnet run
```

Publish a standalone single-file build:

```
dotnet publish -c Release -r win-x64 -o dist/win-x64
dotnet publish -c Release -r linux-x64 -o dist/linux-x64
dotnet publish -c Release -r osx-arm64 -o dist/osx-arm64
```

All three build from any OS. Only the Windows build has been run and tested so far. On Linux and macOS the
launcher looks for `Godot_v*_mono_linux.x86_64` and `Godot_mono.app` in the folder you pick; mark the
published launcher executable with `chmod +x` before running it.
