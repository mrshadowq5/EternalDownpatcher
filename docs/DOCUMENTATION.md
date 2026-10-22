# EternalDownpatcher — User guide

[English](DOCUMENTATION.md) · [Русский](ru/DOCUMENTATION.md) · [Español](es/DOCUMENTATION.md)

[EternalDownpatcher](../README.md) · Steam

## Contents

- [Choose your installation](#start)
- [Downpatch an installation](#existing)
- [Download a separate copy](#separate)
- [Sign-in and download options](#steam)
- [Restore files or get help](#support)

<a id="start"></a>

## Choose your installation

At startup, `DOWNPATCH_FILES` is created if missing; existing contents are preserved. If DepotDownloader is missing, a progress window installs it from GitHub before the main window opens. You can cancel startup or retry a failed installation. After the main window opens, it checks for DepotDownloader updates in the background. The update window shows versions, the publication date, release notes, and a release link. **OK** downloads and installs the update. **Ignore** or closing the window defers the reminder until the next launch; it repeats until updated. Updates wait for active game operations to finish. If a check fails, the installed downloader remains usable.

Select either Download All Files or Downpatch directly. Download All Files is selected by default; Validate files is a separate option for Downpatch.

### Separate copy · Download All Files

Downloads every file for the selected version into a separate folder. Your selected installation stays unchanged, and an existing game folder is not required. Use this when you want to keep the current version or its version cannot be detected.

### Existing installation · Downpatch

Downloads the files needed to downgrade a recognized installation, then applies them automatically. Select the folder containing `DOOMEternalx64vk.exe` and an older target version. Replaced files are backed up first.

> **Choose an empty output folder**
>
> Use a dedicated folder for downloaded files, such as `DOWNPATCH_FILES`. If it is not empty, the program asks to delete its contents before continuing. Do not keep personal files there or select the game folder, the application folder itself, or a drive root.

### Before you start

Close DOOM Eternal and let Steam finish any updates. Have enough space for downloaded files and, when patching an installation, the backup. You need an internet connection and a Steam account that owns the game.

<a id="existing"></a>

## Downpatch an installation

Select Downpatch. This mode changes the game folder you select.

### 1 · Select the game and target version

Click Select DOOM Eternal Folder and choose the folder containing `DOOMEternalx64vk.exe`. Wait for version detection, then choose an older Game Version. If the version is not recognized, use Download All Files to obtain a separate copy instead.

### 2 · Prepare the download

Enter your Steam login and password. Choose an empty Patch Files Folder outside the game folder; neither folder may be inside the other. The default output is `DOWNPATCH_FILES`. Steam credential saving is configured under Settings → Steam; Validate files is available in this mode.

### 3 · Start and follow Steam prompts

Click Downpatch and review the confirmations. DepotDownloader is prepared during startup. Enter an email or authenticator code when the Steam Guard window appears, or approve a mobile sign-in if Steam requests it. All eight depots use one downloader process and one shared Steam session.

### 4 · Wait for automatic patching

After all downloads finish, the program checks the downloaded EXE and confirms that the source version has not changed. It creates a backup, then copies the patch files into the selected installation. No manual file copying is needed. Wait for the success message.

> **Keep the complete backup**
>
> The backup is created inside the game folder as `EternalDownpatcher_Backup_…`. Keep the entire folder for Restore Backup. It contains the replaced game files and a record of files added by the patch; it is not a backup of your saves or Steam profile.

<a id="separate"></a>

## Download a separate copy

Select Download All Files. You can select any version listed by the program.

### 1 · Select Download All Files

The start button changes to Download. Selecting an installed game folder is disabled because it is not needed. Choose the Game Version you want to download.

### 2 · Choose a folder and sign in

Choose a separate, empty Download Folder with enough free space for the full game. Enter your Steam login and password. Click Download, review the confirmations, and complete any Steam Guard prompts.

### 3 · Wait for the success message

The program downloads the game files for the selected version, checks the downloaded EXE, and creates `steam_appid.txt` automatically. The files stay in your Download Folder and are not copied into the selected installation.

### 4 · Launch your copy

The game executable is `DOOMEternalx64vk.exe` in the Download Folder. You can add it to Steam as a Non-Steam Game. Keep Steam running and signed in to the account that owns DOOM Eternal.

> **A separate folder is still required**
>
> Do not select an existing game installation as the Download Folder. If the folder contains files, confirming the clear-folder prompt deletes those contents. Cancel stops the current operation; a partial download is not a ready-to-play copy.

<a id="steam"></a>

## Sign-in and download options

Both Steam Login and Steam Password are required before starting either mode.

### Steam Login and Steam Guard

Use your Steam account login, not your public profile display name. DepotDownloader performs the sign-in. When it asks for an email or authenticator code, the program opens a Steam Guard window. Mobile approval may need to be completed in the Steam app.

### Saved Steam sign-in · Settings → Steam

Open Settings → Steam to configure Remember Steam login and Remember Steam password independently. Login saving is enabled by default; password saving is disabled by default and requires accepting a warning. Click OK to apply your choices. Credentials are saved when you confirm Settings or start a download. Turning an option off and clicking OK removes its saved copy; the fields already entered in the main window remain usable for the current session. Saved passwords are encrypted for your Windows account; programs running under that same account may still access them. DepotDownloader may retain Steam session data separately.

### Validate files

Enables additional file validation by DepotDownloader when downpatching an existing installation. It can increase the time required. The checkbox is cleared and disabled in Download All Files mode.

### Log and cancellation

The log shows the current download and any errors. Passwords and entered Steam Guard codes are redacted, but logs can still contain your Steam login and local folder paths. Review the text before sharing it. Cancel requests cancellation of the current download or patch operation.

<a id="support"></a>

## Restore files or get help

Keep the error text and the complete backup folder when troubleshooting.

### Restore Backup

Close the game, select the installation you want to restore, and click Restore Backup. Choose the matching `EternalDownpatcher_Backup_…` folder and confirm the destination. Choose the outer backup folder, not its files subfolder. Current backups restore replaced files and remove files added by the patch. Older backups without a manifest cannot automatically identify added files.

Keep both `eternaldownpatcher-backup.json` and the `files` subfolder. Restoration overwrites changes made to those files since the patch. If you applied several patches, restore their backups in reverse order, starting with the most recent.

If applying a patch fails or is cancelled, the application attempts to restore the files already replaced. If that recovery also fails, keep the backup folder; its location is included in the error.

### Version not detected or changed

Detection matches the size of `DOOMEternalx64vk.exe` to the version metadata. A new or modified executable may not match. Use Download All Files for a separate copy. If Steam changes the source installation during a download, patching stops; select the game folder again before retrying.

### Download failed or sign-in is waiting

Read the last messages in the log. Check your connection, account details, free disk space, and Steam Guard requests. If you retry, use a dedicated output folder and read the clear-folder prompt before confirming.

### The game downloaded but has a problem

A successful download confirms the files and executable checks performed by the program; it does not guarantee that an old build works with every driver, mod, or save. Include the target game version, your GPU and driver, and the exact symptoms when requesting help.

### Contact

Click Copy Log in the main window and review it before sharing. Ask in the `DE Downpatcher Support thread` on [Modern DOOM Speedrunning Discord](https://discord.gg/dtDa9VZ), or contact **mr.shadowq5** on Discord.
