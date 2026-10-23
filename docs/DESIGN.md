# Wintor design guidelines

Wintor competes on design. These rules keep the app consistent no matter who contributes.
They are binding for all UI changes (see [AGENTS.md](../AGENTS.md)).

## Principles

1. **Feels like Windows 11.** Use platform controls and Fluent styles before anything custom.
   If an inbox app (Settings, File Explorer, Photos) solves a similar problem, look how it does it.
2. **Calm by default.** Show what most people need: name, progress, speed, time left.
   Peers, trackers, pieces and other details live one click away, not on the main screen.
3. **Sensible defaults.** A new user should never need settings to start downloading.
4. **No dead ends.** Every empty, loading and error state explains what is happening and what to do next.
5. **Fast to scan.** Clear hierarchy (one primary line, secondary caption lines), aligned columns,
   generous spacing.

## Layout

- Shell: `NavigationView` (Left) with the custom `TitleBar`. Content pages use `Padding="24,12,24,0"` and
  `Background="{ThemeResource ContentLayerBrush}"` (clear on the solid palette, translucent over a backdrop).
- Search lives in the title bar (`AutoSuggestBox`, Ctrl+F, Esc clears), shown only on pages that implement
  `ISearchablePage`. Matching: every word, any order, ignoring case and accents (`TorrentQuery`).
- Page header: title (`PageTitleStyle`) with a one-line summary caption under it, `CommandBar` on the right with
  `IsDynamicOverflowEnabled="True"` so commands move to "…" on narrow windows. Rare commands go to the overflow.
- Side pane: status views (All / Downloading / Completed / Paused) and the user's **labels**, each with a count on
  the right in `TertiaryCaptionStyle`. Settings in the footer. Everything else stays out of the pane.
- **Torrent list = table + details pane, no drill-in** (as in µTorrent / BitTorrent, Windows 11 look):
  - The table sits in a card. Column headers are quiet buttons: click sorts, again flips, the active one shows an
    arrow. Numbers are right-aligned, text left-aligned, one line per row (40 px), names trimmed with a tooltip.
  - Columns: №, Name (+ label pill), Size, Progress, Status, Down, Up, Time left, Seeds, Peers. `TableColumns.Fit`
    hides the less important ones, in that order from the right, until the name has 220 px.
  - Progress color says the state: **green** downloading, **blue** seeding, **grey** paused / stopped (red on error).
    Use the `Torrent*Brush` palette keys, never ad-hoc colors.
  - Click selects (Ctrl/Shift for several, Ctrl+A, Esc clears); the selected torrent shows in the pane below.
    Right-click menu acts on the whole selection when the row is part of it. № order: Alt+↑/↓, or the "Order" menu.
  - The pane (resizable with the splitter, double-click resets) has tabs in a `SelectorBar`: General (piece bars
    "Downloaded" and "Availability", Transfer, General info), Files, Peers, Trackers, Speed (graph).
  - When `Microsoft.UI.Xaml.Controls.Tabular.TableView` ships in a stable Windows App SDK (it is experimental-only
    in 2.5.x), replace the header + `ListView` pair with it.
- Every list has an empty state (icon, one line, the next action) and a separate "nothing found" state.
- Settings use `SettingsCard` / `SettingsExpander` from the Community Toolkit, grouped under
  `BodyStrongTextBlockStyle` section headers.
- Spacing on a 4 px grid: 4 / 8 / 12 / 16 / 24.
- The window must work from 640×480 up to full screen. Test narrow and wide.

## Typography

Use only the built-in type ramp: `TitleTextBlockStyle`, `SubtitleTextBlockStyle`, `BodyStrongTextBlockStyle`,
`BodyTextBlockStyle`, `CaptionTextBlockStyle`. No custom font sizes or font families.
Secondary text uses `TextFillColorSecondaryBrush`, tertiary `TextFillColorTertiaryBrush`.
Long names are trimmed with `TextTrimming="CharacterEllipsis"` and show the full text in a tooltip.

## Color and theme

- Only `{ThemeResource}` brushes, named by purpose. No hex colors in XAML or code — the only exception is
  `Themes/Palette.xaml`, which defines them.
- **Default look is solid:** the zinc palette in `Themes/Palette.xaml`. Mica, Mica Alt, Acrylic and Acrylic Thin
  are opt-in in Settings (`Services/Appearance.cs`) and only apply while Windows transparency effects are on,
  never in high contrast. Cards always stay on solid "paper".
- Theme (as in Windows / light / dark) is set on the window root; caption buttons follow the window's theme and
  dialogs get the window's theme (`Shell.Prepare`).
- Accent color is the user's Windows accent — do not override it.
- Torrent state colors come from the palette (`TorrentDownloadingBrush`, `TorrentSeedingBrush`,
  `TorrentInactiveBrush`); errors use the standard `ShowError` / `InfoBar` severities.
- Check light, dark and a high-contrast theme for every UI change.

## Icons

- [Hugeicons](https://hugeicons.com) **free set (MIT), Stroke Rounded style only** — never Pro icons.
- One style and stroke width across the whole app: 16 px in command bars, menus and navigation, 20 px for
  card and settings header icons, 40–48 px in empty states.
- Icons follow `Foreground` so they adapt to the theme. How to add one: see [AGENTS.md](../AGENTS.md#icons).
- Different settings get different icons; do not reuse one icon for two meanings.
- Every icon in a button has a text label or a tooltip; icon-only buttons need `AutomationProperties.Name`.

## Feedback and dialogs

| Situation | Use |
|---|---|
| Blocking choice or destructive action | `ContentDialog` with a verb on the primary button ("Remove", not "OK") |
| Error or warning on a page | `InfoBar` at the top of the page |
| Background event (download finished) | Windows app notification |
| Hint or onboarding | `TeachingTip` |
| Long operation | `ProgressRing` / indeterminate `ProgressBar` with text |

Destructive dialogs default to **Cancel** and name the item being affected.

## Motion

Use the built-in control animations and `NavigationThemeTransition`. No custom animations unless they
explain a change of state, and respect the system "Animation effects" setting.

## Accessibility

- Everything reachable and operable with the keyboard; common actions have shortcuts
  (Ctrl+N add magnet, Ctrl+O open file, Delete remove).
- Interactive elements have a name for screen readers; list items expose the torrent name.
- Do not convey information by color alone.

## Writing

- Short, plain, sentence case: "Add magnet link", not "Add Magnet Link".
- Talk about what the user wants, not how BitTorrent works: "Getting file list…" over "Fetching metadata".
- Same term everywhere: "torrent", "pause", "resume", "remove".
