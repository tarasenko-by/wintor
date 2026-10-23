# AGENTS.md — rules for contributors and AI coding agents

This file is the single source of truth for how Wintor is built. It applies to every
change, whether written by a person or by an AI tool (Claude Code, Codex, Copilot, Cursor, …).
Human-oriented onboarding is in [CONTRIBUTING.md](CONTRIBUTING.md); design rules are in
[docs/DESIGN.md](docs/DESIGN.md) and are part of these rules.

Wintor is a modern, open-source BitTorrent client for Windows 11 built with WinUI 3,
licensed GPL-3.0-or-later. Its reason to exist is **design and ease of use** — a change
that works but makes the app feel like a 2010 torrent client is not done.

## Project layout and boundaries

| Project | Responsibility | Must not |
|---|---|---|
| `src/Wintor.Core` | Models, `ITorrentEngine`, formatting/validation helpers | Reference WinUI or MonoTorrent |
| `src/Wintor.Engine.MonoTorrent` | The only `ITorrentEngine` implementation | Leak MonoTorrent types outside the project |
| `src/Wintor.App` | WinUI 3 UI, view models, resources | Call MonoTorrent directly |
| `tests/*` | xUnit tests | Download anything except legal test torrents |

- MVVM with `CommunityToolkit.Mvvm`: `[ObservableProperty]` on partial properties, `[RelayCommand]`.
- Dependency injection via `Microsoft.Extensions.DependencyInjection`, registered in `App.ConfigureServices`.
- Dialogs and pickers live in page code-behind; state and logic live in view models.
- The app is **single-instance** (`Program.cs`). Two instances would overwrite each other's
  torrent state — do not remove this. A second launch (a `.torrent` double-click, a `magnet:` link,
  the Start menu) is redirected to the running instance and routed in `App.Route`.
- Closing the window keeps Wintor running in the notification area by default. To quit from a
  terminal or a script — saving the torrent state — run `wintor --quit` (the app execution alias).
  Do not kill the process: resume data is written on a graceful stop.

## UI

Read [docs/DESIGN.md](docs/DESIGN.md) before changing any XAML. The short version:

- Native Windows 11 / Fluent look; cards over dense tables; sensible defaults; advanced options hidden but reachable.
- Colors only via `{ThemeResource ...}`; no hard-coded colors. Works in light, dark and high-contrast themes.
- `x:Bind` with an explicit `Mode` for changing data. The build must have **zero** WinUI analyzer warnings.
- Dialogs: `Dialogs.Prepare` + `ShowQueuedAsync()`, primary button on the right in the footer band, destructive ones
  `MakeDestructive()` and naming what will be removed. Full rules: docs/DESIGN.md, "Dialogs".
- Every interactive element has visible text or `AutomationProperties.Name` and is keyboard-reachable.

## Icons

- **All icons come from [Hugeicons](https://hugeicons.com), free set only (MIT license, Stroke Rounded style).**
- **Never add Hugeicons Pro icons** — their license forbids redistribution, and this repository is public.
- Do not use Segoe Fluent Icons glyphs (`FontIcon Glyph="&#x…;"`) or other icon sets. Exception: glyphs that
  built-in controls draw themselves (window buttons, overflow "…", chevrons).
- Each icon is the original SVG in `src/Wintor.App/Assets/Icons/<hugeicons-name>.svg`. Hugeicons are stroke
  drawings and `PathIcon` only fills, so `tools/Wintor.IconTool` outlines them into
  `src/Wintor.App/Icons/HugeIcons.g.cs` (generated — never edit by hand, never paste path data inline):
  ```powershell
  # extract the npm package @hugeicons/core-free-icons somewhere first (npm pack + tar)
  dotnet run --project tools/Wintor.IconTool -- import <package-dir> search-01 folder-open
  dotnet run --project tools/Wintor.IconTool -- preview icons.png   # eyeball the outlines
  ```
- Use icons as `Icon="{icons:Huge Kind=FolderOpen}"` (optional `Size`, default 16) in XAML, or
  `HugeIcons.Create(HugeIconKind.FolderOpen)` in code. They follow `Foreground` and the theme.
- The app logo and every MSIX tile/`.ico` are rendered by `dotnet run --project tools/Wintor.IconTool -- logo`;
  change the logo there, not in an image editor.

## Releases

- A new version (`Package.appxmanifest` `Version`) adds its entry on top of `Wintor.Core/WhatsNew.cs` with its
  points as `WhatsNew_<version>_<name>` strings in every language. Wintor shows it once after the update and from
  Settings → About.

## Localization

- No user-visible strings in code or XAML. XAML uses `x:Uid`; code uses `Strings.Get` / `Strings.Format`.
- Every new string is added to **all** `src/Wintor.App/Strings/<lang>/Resources.resw` files
  (12 languages; the list and their review status are in CONTRIBUTING.md, "Translations"). `en-US` is the
  default; if you cannot translate, copy the English text and mention it in the PR. Every file keeps exactly
  the keys and placeholders of `en-US`.

## Dependencies and licensing

- Pin exact NuGet versions; never use `*`.
- New dependencies must be GPL-3.0-compatible (MIT, BSD, Apache-2.0, LGPL, GPL) and listed in
  [THIRD-PARTY-NOTICES.md](THIRD-PARTY-NOTICES.md).
- No telemetry, no ads, no built-in torrent search or content catalog.

## Build, run, test

```powershell
dotnet build Wintor.sln -p:Platform=x64
dotnet test Wintor.sln -p:Platform=x64
winapp run src\Wintor.App
```

- Platforms are `x64` and `ARM64` only — never `AnyCPU`.
- Logic in `Core` and the engine adapter is covered by xUnit tests; add tests with behavior changes.
- Engine tests use only legal torrents (e.g. Big Buck Bunny, Linux ISOs) and temporary folders,
  never the user's Downloads folder.
- For UI changes, run the app and look at it (light and dark theme, narrow and wide window) —
  a green build is not proof that the UI works.
- AI agents verify UI in the background (UI Automation, logs, tests) and never take over the
  developer's mouse and keyboard. `tests/ui/ui-tests.ps1` does that (`-Target local -AppPid <pid>
  -AppScope local -NoBuild -ArtifactDirectory <dir>` against a running app, or `-Target sandbox`);
  what UIA cannot judge goes to `tests/ui/manual-checks.md`.
- Give every interactive element an `AutomationProperties.AutomationId`; the UI tests rely on them.

## Code style

- Code, comments, commit messages and docs in English.
- Match the surrounding code: naming, file layout, comment density. Comments explain *why*, not *what*.
- Nullable reference types are on; do not silence warnings with `!` without a reason.

## Pull requests

- One logical change per PR, with a short description of what and why, and screenshots for UI changes.
- Every commit is signed off (`git commit -s`, the DCO — see CONTRIBUTING.md); contributions are GPL-3.0-or-later
  with the app store permission in `LICENSE-STORE-EXCEPTION.md`.
- Where things live and how data flows: [docs/ARCHITECTURE.md](docs/ARCHITECTURE.md).
- Fill in the checklist from the PR template.
- No CI yet: run the build and tests locally before opening a PR.
