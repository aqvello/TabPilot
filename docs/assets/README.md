# Documentation media

All webpage content is fictional and authored for this repository. No personal pages, accounts, bookmarks, browser profile UI or desktop content appear in the published media.

## Full workflow: real Chrome selection

`demo-zh.gif` and `demo-en.gif`, with matching `workflow-before-{zh,en}.png` and `workflow-after-{zh,en}.png`, frame a real, verified action as a three-monitor overview with a focus zoom. Chinese README uses the Chinese animation and comparison images; English README uses the English ones. Japanese and Spanish READMEs currently use English media.

Both versions localize the explanatory captions, monitor labels, native TabPilot controls and fictional page descriptions. Example tab titles stay in their original language so the search result matches the target page. Each language has its own verified recording in a fresh isolated browser profile: three dedicated Chrome windows contain four local demo tabs on three actual physical displays:

1. The left display shows window C, **Team Notes**. The center display shows window A, **Atlas Overview**.
2. On the right display, **Design System** is an unselected tab in window B, whose selected tab is **API Reference**.
3. The native TabPilot popup searches the real fixture inventory for `design`.
4. Its actual Enter handler invokes the production `Program.Select` worker. Window B on the right display becomes foreground, Design System becomes exclusively selected, and the native orange highlight appears for about two seconds.
5. An editorial zoom enlarges that window into the foreground of the animation while all three monitor outlines stay behind it. The actual window stays on the right display; unchanged window placement is verified locally. The animation remains on the resulting page after the highlight disappears.

`tools/WorkflowDemo.cs` creates local HTML and launches a separate Chrome process with a newly generated temporary `--user-data-dir`. It never reuses the daily browser profile. Only this documentation tool supplies startup flags to skip first-run/default-browser prompts and disable extensions and sync; the shipped TabPilot application still operates on existing Chrome windows without startup changes. Demo windows must belong to the launched process before any movement, input or document capture. Setup uses foreground-checked normal Chrome shortcuts; production selection uses UI Automation / Win32. On success or failure, cleanup closes the isolated browser and removes its temporary profile; the encoder refuses incomplete cleanup evidence.

### Capture boundaries

- Chrome is rendered with **PrintWindow**, never a desktop or screen-pixel capture. The temporary window bitmap stays in memory; only the UIA-verified demo document rectangle is retained before writing a frame. Browser toolbars, addresses, avatars and bookmarks are discarded.
- TabPilot uses `Control.DrawToBitmap` with an inventory containing only the dedicated demo windows. The recorder discards accidental keyboard, IME, mouse and paste input into its popup.
- Orange strips come from the production worker's actual highlight windows, verified by target geometry and process ownership. They are mapped onto neutral viewport framing used to omit browser chrome.
- The monitor rigs, neutral background, tab bars, connector, captions and focus zoom are illustrated by `tools/WorkflowScene.cs`. They show the actual left/center/right fixture relationship, without copying the user's desktop or browser chrome. The zoom is explanatory, not a feature that moves Chrome between displays.
- Observed highlight frames are resampled to 10 fps using the verified duration. This is an edited demonstration, not an uncut desktop recording.
- Each document capture requires a known fixture title, foreground HWND, unique accessible document and expected color markers. Unexpected or blank capture stops with no desktop-capture fallback.

The published workflow verification JSON includes only software/language labels and outcome flags. Runtime identifiers, device configuration, exact timings and private settings stay local.

### Reproduce and review

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\tools\build-workflow-demo.ps1
.\bin\WorkflowDemo.exe .\artifacts\private\workflow-demo-zh zh-CN
.\bin\WorkflowDemo.exe .\artifacts\private\workflow-demo-en en
```

Inspect generated before/after images and representative frames locally. With Python and FFmpeg available:

```text
python tools/encode-workflow-demo.py artifacts/private/workflow-demo-zh docs/assets
python tools/encode-workflow-demo.py artifacts/private/workflow-demo-en docs/assets
python tools/render-readmes.py
python tests/demo-privacy-checks.py
```

The encoder requires passed production selection, foreground and highlight checks, plus a confirmed isolated profile, browser exit, temporary-profile removal and unchanged existing browser windows. It rejects recordings made by the older shared-profile tool. Public verification files expose outcome flags only. Original frames and detailed evidence remain ignored under `artifacts/private/`.

The regression layouts in `tests/GuiChecks.cs` and `tests/ReliabilityChecks.cs` are independently invented examples; they are neither captured monitor coordinates nor proportional transformations of a test machine.

The recorder requires three physical displays with the primary display in the middle, sorted by horizontal coordinates. It stops if that layout is unavailable instead of substituting a simulated compatibility test. Monitor sizes are normalized in the presentation; physical coordinates and desktop pixels are not published.

## Other screenshots

`tabs-en.png`, `tabs-zh.png`, `search-en.png` and `monitors-en.png` are **native-control renders with fixed in-memory fixture data**. Their generator, `tools/ReadmeDemo.cs`, never starts `DesktopContext`, scans Chrome, loads personal settings, captures desktop pixels or injects input. Use `tools/build-readme-demo.ps1` and `bin/ReadmeDemo.exe` to regenerate those static screenshots. Do not overwrite the full-workflow GIF with that generator's earlier list-only sequence.

`banner.svg` is code-authored vector artwork without external resources.

Application UI languages are Simplified Chinese and English. Japanese and Spanish are README translations only.
