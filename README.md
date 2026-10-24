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

## Bulding from source

To publish a Windows executable:

```powershell
dotnet publish .\DEDownpatcher.WinForms.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:DebugType=None -o .\bin\publish-single
```

Distribute the complete `bin/publish-single` folder, including `assets` and `data`. The executable contains the .NET runtime libraries.
