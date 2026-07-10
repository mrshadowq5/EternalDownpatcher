# EternalDownpatcher
A Downpatcher for DOOM Eternal.

downpatch it quick!

![Preview](https://github.com/mrshadowq5/EternalDownpatcher/blob/main/assets/preview.png?raw=true)

## Requirements

* Windows 10/11
* [.NET Desktop Runtime](https://dotnet.microsoft.com/en-us/download/dotnet/10.0?utm)

## Important

You must own [DOOM Eternal](https://store.steampowered.com/app/782330/DOOM_Eternal/) on Steam.

## INSTRUCTIONS

Instructions can be found in the application in the top right.

![Preview](https://github.com/mrshadowq5/EternalDownpatcher/blob/main/assets/instructions.png?raw=true)

## Why this project exists

I created EternalDownpatcher as a separate implementation after running into issues using [Xiae's Downpatcher](https://github.com/mcdalcin/DoomEternalDownpatcher).

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
