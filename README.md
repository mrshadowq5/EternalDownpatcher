# EternalDownpatcher
A Downpatcher for DOOM Eternal.

***downpatch it quick!***

![Preview](https://github.com/mrshadowq5/EternalDownpatcher/blob/main/assets/preview.png?raw=true)

## Why this project exists

I created EternalDownpatcher as a separate implementation after running into issues using [Xiae's Downpatcher](https://github.com/mcdalcin/DoomEternalDownpatcher).
Also, EternalDownpatcher downloads [DepotDownloader](https://github.com/SteamRE/DepotDownloader) when it is missing and checks for updates after startup.

***EternalDownpatcher is not meant to disrespect or replace any existing projects. It was made to solve issues I personally encountered and to make the downpatching process more reliable for my own use and for others who run into the same problems.***

## Important

***You must own [DOOM Eternal](https://store.steampowered.com/app/782330/DOOM_Eternal/) on Steam.***

## Requirements

* Windows 10/11
* [.NET 10 Desktop Runtime](https://dotnet.microsoft.com/en-us/download/dotnet/10.0) for framework-dependent builds. Self-contained builds include the runtime needed by EternalDownpatcher.

## User guide

Read the full guide: **[English](docs/DOCUMENTATION.md)** · **[Русский](docs/ru/DOCUMENTATION-RU.md)** · **[Español](docs/es/DOCUMENTATION-ES.md)**.

- **[Separate copy](docs/DOCUMENTATION.md#separate):** select **Download All Files** (the default mode) to download a complete version into an empty folder. An existing installation is not required.
- **[Existing installation](docs/DOCUMENTATION.md#existing):** select **Downpatch** to downpatch a recognized installation. Patch files are applied automatically after downloading, with a backup created first.

The guide also covers [Steam sign-in and options](docs/DOCUMENTATION.md#steam), [backups and troubleshooting](docs/DOCUMENTATION.md#support), and choosing an output folder.

## DepotDownloader startup and updates

EternalDownpatcher creates `DOWNPATCH_FILES` at startup without clearing existing contents. If DepotDownloader is missing, a progress window downloads and installs it from its official GitHub release before the main window opens. Cancelling stops startup; a failed installation can be retried.

Once the main window opens, a background check looks for DepotDownloader updates. The update window shows the installed and available versions, publication date, release notes, and a GitHub link. **OK** downloads and installs the update; **Ignore** or closing the window defers it until the next launch. The reminder continues until the installed version is current. Updates wait until game operations finish, and a failed check keeps the installed downloader usable. Installation uses direct GitHub release links rather than the REST API.

## Game version updates

At startup, EternalDownpatcher immediately uses its cached or bundled `data` and checks this repository's `main` branch in the background without locking the window. It downloads and validates `versions.json` and the filelists together, then caches them under `%LOCALAPPDATA%\EternalDownpatcher\version-data`. If GitHub is unavailable or the data is incomplete, the application keeps its last working data; bundled `data` is the fallback when there is no valid cache. Starting a download or restoration cancels the background check so the active version data cannot change during the operation. Closing the window also cancels the check.

To add a game version, append an entry to `data/versions.json` with its name, executable size in bytes, and eight manifest IDs in depot order (`782332`–`782339`). Add `data/<version>.txt` listing the files changed from the preceding version. Keep entries in chronological order and preserve existing filelists, including the special `1.1.0.txt`. Commit both files together to `main`. Applications with this updater will receive the new data on their next launch; no new application release is needed for data-only changes. Existing application builds without the updater need a one-time upgrade.

## Building from source

Install the .NET SDK, then run:

```powershell
dotnet build
```

## Source layout

`Program.cs` is the application entry point. Application code is grouped in `downpatcher/`:

- `MainForm.cs`: shared state, lifecycle, logging, saved credentials, and Steam Guard requests.
- `MainForm.UI.cs`: main window layout, themes, icons, and translations.
- `MainForm.Downpatching.cs`: version detection, file lists, download/patch workflow, cancellation, restoration, and filesystem helpers.
- `DepotDownloader.cs`: downloader discovery and installation, command arguments, process I/O, and authentication prompts.
- `PatchBackup.cs`: backups, rollback, restoration, and path checks.
- `UserSettings.cs`: saved preferences, Steam credential settings, and Windows DPAPI password protection.
- `SettingsForm.cs`: settings window, translations, social links, and logos.
- `SteamGuardForm.cs`: Steam Guard code entry dialog.
- `docs/`: the user guide in English, Russian, and Spanish.

Each combined file uses collapsible `#region` sections for navigation in VS Code.

To publish a Windows executable:

```powershell
dotnet publish .\DEDownpatcher.WinForms.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:DebugType=None -o .\bin\publish-single
```

Distribute the complete `bin/publish-single` folder, including `assets` and `data`. The executable contains the .NET runtime libraries.
