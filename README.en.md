<div align="center">

<img src="docs/assets/banner.svg" width="1120" alt="TabPilot — Your tabs. One shortcut away." />

### Find the tab. Keep your focus.

[简体中文](README.md) · [English](README.en.md) · [日本語](README.ja.md) · [Español](README.es.md)

**[↓ Download for Windows](https://github.com/aqvello/TabPilot/releases/tag/v0.2.1)** · [Build from source](#build) · [Compatibility & limits](#limits)

Windows x64 · .NET Framework 4.8 · UI: 简体中文 / English · v0.2.1 Preview

</div>

A **native Windows tab switcher for Chrome across windows and monitors**. Open the popup, search a title, and return to the tab you need. No Chrome extension required.

| | |
| :--- | :--- |
| **⌨ One shortcut away** | Search titles, select with ↑↓, switch with Enter. |
| **▦ Across monitors** | Monitor grouping, mixed DPI and saved popup placement. |
| **◉ Native & local** | UI Automation / Win32. No Chrome restart or configuration changes. |

## See the workflow

![TabPilot: three screens → search → select → right-screen target brought into focus](docs/assets/demo-en.gif)

Find a tab without checking every window. Open TabPilot, search for Design System and press Enter to switch to the tab on the right-hand screen. An orange border shows which window you switched to.

The demo uses sample pages. The zoom makes the result easier to see. [About the demo](docs/assets/README.md)

| Before: three screens, target tab in the background on the right | After: right-screen target activated and enlarged for clarity |
| :---: | :---: |
| ![Before: three screens, target tab in the background on the right](docs/assets/workflow-before-en.png) | ![After: right-screen target activated and enlarged for clarity](docs/assets/workflow-after-en.png) |

## Chinese and English UI

| Simplified Chinese | English |
| :---: | :---: |
| ![Simplified Chinese](docs/assets/tabs-zh.png) | ![English](docs/assets/tabs-en.png) |

<details>
<summary>More screenshots: search and monitor grouping</summary>

| Title search | By monitor |
| :---: | :---: |
| ![Title search](docs/assets/search-en.png) | ![By monitor](docs/assets/monitors-en.png) |

</details>

## Get started in a minute

1. Download and extract `TabPilot-Windows-x64.zip` from the preview Release.
2. Run `TabPilot.exe`, keeping its `.config` file alongside it.
3. Use the registered global shortcut. The default is **Ctrl + Alt + Space**; if occupied, alternatives are tried and the actual shortcut is shown in the popup.
4. Search by title and click a result, or use ↑↓ and Enter. The target window activates and is highlighted for about two seconds. Esc or clicking outside hides the popup in the tray.

Requires Windows 10 1703+ x64 and .NET Framework 4.8. **v0.2.1 is a preview**, validated locally on Windows 10 / Chrome 156; compatibility with every version is not guaranteed.

## Choose your language

The application supports **Simplified Chinese and English**. Use **中文 / EN** in the popup header or **Language / 语言** in the tray menu. Changes apply immediately and survive restarting. Page titles keep their original language; low-level Windows or Chrome diagnostics may also remain untranslated. Japanese and Spanish are README translations, not additional application UI languages.

## Keyboard first

| Action | Shortcut / control |
| :--- | :--- |
| Open / hide | Registered global shortcut |
| Select / switch | ↑↓ / Enter |
| Hide / quit | Esc / Ctrl + Q |
| Refresh | F5 / Ctrl + R |
| All / by monitor / recent | Ctrl + 1 / 2 / 3 |
| Save popup position | Drag the top heading |

<a id="limits"></a>

## Compatibility & limits

- **Accessible tabs are not necessarily all tabs.** Collapsed groups, permissions and provider behavior may hide entries. Counts are labeled as found, with completeness unverified.
- **Windows controls foreground activation.** Dialogs, integrity levels and focus competition can prevent activation. Results are verified and failures reported.
- Complex collapsed groups, elevated Chrome, virtual desktops and other environments are not fully tested.
- Background-tab URLs are not reliably available. Subtitles show window context, and Recent only records successful switches made through TabPilot.

## Data & privacy

TabPilot itself does not upload tab data or read Chrome profiles, history or session files. Its own settings and diagnostics use a writable portable `data` folder, otherwise `%LOCALAPPDATA%\TabPilot`. Settings have atomic replacement, backup recovery and retries. Diagnostics may contain titles; review them before sharing. Documentation media uses dedicated fictional pages and native application controls only; see [how assets are generated](docs/assets/README.md).

<a id="build"></a>

## Build from source

Uses the system .NET Framework compiler, without the .NET SDK or NuGet downloads:

```powershell
git clone https://github.com/aqvello/TabPilot.git
Set-Location TabPilot
powershell -NoProfile -ExecutionPolicy Bypass -File .\build.ps1
.\bin\TabPilot.exe
```

## Tests & implementation

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\tests\build-gui-checks.ps1
.\bin\GuiChecks.exe
.\bin\TabPilot.Cli.exe self-test
powershell -NoProfile -ExecutionPolicy Bypass -File .\package.ps1
powershell -NoProfile -ExecutionPolicy Bypass -File .\tests\release-checks.ps1
```

8 core checks and 77 GUI/model regression checks, including localized controls, warnings, search, original titles and settings compatibility. Real multi-monitor checks and observed failures are documented separately; synthetic demonstrations do not establish Chrome compatibility.

[GUI validation](docs/GUI_VALIDATION.md) · [Core validation](docs/VALIDATION.md) · [CLI reference](docs/CONSOLE_POC.md) · [Changelog](CHANGELOG.md)

Native GUI integration scripts manipulate real windows and may require machine-specific monitor coordinates and private pre-test state files. Original screenshots, settings, inventories and logs are excluded from the repository and release package. Detailed validation notes currently use Chinese.

[MIT License](LICENSE) · Copyright © 2026 aqvello
