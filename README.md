# EternalDownpatcher

A Downpatcher for DOOM Eternal.

The project is inspired by Xiae's Downpatcher, but is made as a separate implementation.

## Requirements

* Windows 10/11
* .NET Desktop Runtime

## Important

You must own DOOM Eternal on Steam.

## Building from source

Install the .NET SDK, then run:

```powershell
dotnet build
```

To publish a Windows executable:

```powershell
dotnet publish -c Release -r win-x64 --self-contained false
```