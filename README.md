# EternalDownpatcher

A Downpatcher for DOOM Eternal.

## Requirements

* Windows 10/11
* [.NET Desktop Runtime](https://dotnet.microsoft.com/en-us/download/dotnet/10.0?utm)

## Important

You must own [DOOM Eternal](https://store.steampowered.com/app/782330/DOOM_Eternal/) on Steam.

## INSTRUCTIONS

Instructions can be found in the application in the top right.

## Why this project exists

I created EternalDownpatcher as a separate implementation after running into issues using [Xiae's Downpatcher](https://github.com/mcdalcin/DoomEternalDownpatcher/releases#release-1.0).

Xiae's Downpatcher [has not been updated since 2023](https://github.com/mcdalcin/DoomEternalDownpatcher/releases/tag/1.8) and uses an older version of DepotDownloader [(2.4.6)](https://github.com/SteamRE/DepotDownloader/releases/tag/DepotDownloader_2.4.7), this causes InvalidPassword error, which makes downpatching impossible.

EternalDownpatcher is not meant to disrespect or replace any existing projects. It was made to solve issues I personally encountered and to make the downpatching process more reliable for my own use and for others who run into the same problems.

## Building from source

Install the .NET SDK, then run:

```powershell
dotnet build
```

To publish a Windows executable:

```powershell
dotnet publish -c Release -r win-x64 --self-contained false
```
