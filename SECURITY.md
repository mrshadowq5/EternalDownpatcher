# Security Policy

## Reporting a Vulnerability

If you find a security vulnerability in EternalDownpatcher, please report it privately through GitHub Security Advisories / Private Vulnerability Reporting if available.

Please do not create a public issue for security-related problems.

When reporting a vulnerability, include as much detail as possible:

- What the issue is
- How it can be reproduced
- What version of EternalDownpatcher you used
- Your Windows version
- Any relevant error messages or logs

## Sensitive Information

Steam passwords are passed to DepotDownloader through its redirected standard input,
only after it requests a password. They are not placed in command-line arguments.
Steam Guard codes are requested in a separate dialog and are not saved.

EternalDownpatcher does not save or restore Steam logins or passwords. They stay
in the current window only and must be entered again after restarting. The settings
file stores only language, theme and backup preferences. On startup, legacy saved
login/password fields and their remember flags are removed without decrypting them.
If that cleanup cannot be written, startup stops with an error.

DepotDownloader's -remember-password option remains enabled for its own Steam
session storage. Removing WinForms credential saving does not change or purge
DepotDownloader's account/session storage or revoke existing Steam sessions. After
credentials have been exposed, change the Steam password and sign out everywhere
through Steam Guard. Removing a release does not invalidate downloaded copies.

Please do not send or post any sensitive information, including:

- Steam passwords
- Steam Guard codes
- Steam tokens
- Cookies
- Full DepotDownloader login logs
- Personal account information

If you need to share logs, please remove or censor anything related to your Steam account before sending them.

## Project Scope

- EternalDownpatcher is intended for legitimate Steam copies of DOOM Eternal.

- EternalDownpatcher requires a legitimate Steam copy of DOOM Eternal.

## Supported Versions

Security reports are accepted for the latest public version of EternalDownpatcher.

Older versions may not receive fixes unless the issue also affects the current version.

## Response

I will try to review valid security reports as soon as possible.

If the issue is confirmed, I will work on a fix and publish an update when possible.
