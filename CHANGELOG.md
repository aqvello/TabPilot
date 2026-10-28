# Changelog

## v0.2.1 — 2026-10-29

- Added the MIT license to the repository and portable package.
- Kept the actual current shortcut in a dedicated localized footer row. Refresh progress, scan warnings and errors no longer replace it; unavailable registration is explicitly shown.
- Unified assembly, file, product, manifest, README and release versions. Builds and packaging reject version mismatches and packaging generates the archive checksum.
- Added ten shortcut regression checks and ten release/package checks.
- Added separate Chinese and English workflow animations and before/after images, including localized captions, screen labels, native popup UI and fictional page descriptions. Each README uses matching media; Japanese and Spanish currently use English media.
- Reframed the workflow as a three-monitor overview: search in front of all screens, then highlight the right-screen target and enlarge it into the foreground. The focus zoom is editorial; actual three-display selection and unchanged target-window bounds are verified.
- Replaced the list-only animation with an actual cross-window Chrome tab selection, resulting page and native orange highlight, using dedicated fictional local pages.
- Added before/after comparison images, a sanitized verification summary, and a reproducible window-scoped recorder. Browser profile UI and desktop content are excluded from the published media.

## v0.2.0 — 2026-10-29

- Simplified Chinese / English application UI with live switching from the popup or tray menu. The preference survives restarting.
- Localized navigation, search hints, monitor and window context, shortcut settings, and application-generated warnings. Original page titles and native diagnostics keep their source language.
- Redesigned READMEs in Simplified Chinese, English, Japanese, and Spanish.
- Native-control screenshots and an animated demo using fixed fictional data only; no real browser or desktop capture.
- Added language regression checks, a reproducible media generator, and an English quick-start file in the portable package.

## v0.1.0 — 2026-10-29

Initial private preview.

- Native Windows Chrome tab enumeration and exact-identity selection through UI Automation / Win32.
- Global shortcut, search popup, monitor grouping, and app-recorded recent switches.
- Two-second target-window highlight, mixed-DPI layout, and saved popup position.
- Atomic settings replacement, backup recovery, write retries, and user-data directory fallback.
- Bounded scan concurrency and a shared UIA scan budget with incomplete-read diagnostics.
- Focus changes after verified selection no longer automatically reopen the popup.

Known limits: incomplete UIA inventories, complex collapsed groups, Windows foreground restrictions, and environments outside the documented test matrix.
