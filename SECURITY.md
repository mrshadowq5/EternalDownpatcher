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

Settings → Steam contains independent **Remember Steam login** and
**Remember Steam password** options. Login saving is enabled by default;
password saving is disabled by default and requires confirmation of a warning. Saved passwords use Windows DPAPI with CurrentUser.
This protects the saved file; it does not protect against malicious software
running as the same Windows user. Turning either saving option off and
confirming OK removes its corresponding value from EternalDownpatcher's settings. Older saved passwords without explicit opt-in are
discarded when settings are loaded.

DepotDownloader's -remember-password option is used only when this checkbox is
enabled. DepotDownloader may maintain its own Steam session data; unchecking the
box does not revoke existing Steam sessions.

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
