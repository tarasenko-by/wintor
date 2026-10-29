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
- Search lives in the title bar (`AutoSuggestBox`, Ctrl+F, Esc clears): magnifier on the left, the clear button (×)
  on the right only while there is text. Every page that implements `ISearchablePage` searches its own content with
  its own placeholder (torrents; settings cards). Matching: every word, any order, ignoring case and accents.
- Pages have no title: the selected side-pane item names the view. The torrent page's `CommandBar` is
  left-aligned, icon-only with tooltips, `IsDynamicOverflowEnabled="True"`; rare commands go to the overflow.
  Resume and Pause are one place, one button at a time: Pause while everything selected runs, Resume otherwise. The
  row menu follows the same rule. There is no separate Stop.
  Settings start straight with the first section. About shows the version and copyright, with "What's new", GitHub
  (and Microsoft Store once listed) as links in its header next to the chevron, as in the author's Mail app; inside
  it, the switch for showing "What's new" after updates.
- Side pane: status views (All / Downloading / Completed / Paused) and the user's **labels**, each with a count on
  the right in `TertiaryCaptionStyle`. Labels have a color (Services/LabelColors: 8 mid-tones that read on both
  themes) shown on their tag icon, the row's pill and menus. The "Labels" header and hint hide in the compact pane.
- **Torrent list = table + details pane, no drill-in** (as in µTorrent / BitTorrent, Windows 11 look):
  - The table sits in a card. Column headers are quiet buttons: click sorts, again flips, the active one shows an
    arrow. Numbers are right-aligned, text left-aligned, one line per row (40 px), names trimmed with a tooltip.
  - Columns: №, Name (+ label pill), Size, Progress, Status, Down, Up, Time left, Seeds, Peers. By default the
    first seven show. Right-click the header (or its ⚙ button) to pick columns; **Name, Progress and Status are
    locked on**. Dividers between headers are always drawn (stronger on hover). Dragging one trades width between
    the two columns next to it only, so it follows the pointer and nothing else moves; each column stops at its
    minimum (the name at 200 px). No divider after the last column. Double-click resets. Turning a column on gives
    back room from widened columns. Header hover is square, no rounding.
    Choices and widths are remembered. A narrower window never hides a chosen column: the name shrinks to 200 px,
    then every other column shrinks in proportion to its width down to its minimum (wide enough for its header and
    values), and below that the header and rows scroll sideways together (`TableColumns.Fit`, `ColumnFit.Shrink`).
  - Progress color says the state: **blue** downloading, **green** finished and seeding, **grey** paused / stopped (red on error). Piece bars use `PieceBarBrush` (green).
    Use the `Torrent*Brush` palette keys, never ad-hoc colors.
  - Click selects (Ctrl/Shift for several, Ctrl+A, Esc clears); the selected torrent shows in the pane below.
    Right-click menu acts on the whole selection when the row is part of it. № order: Alt+↑/↓, or the "Order" menu.
    "Speed limit…" sets per-torrent download/upload limits in KB/s (0 = none); the global limit from Settings still
    applies. The Transfer section shows them.
    "Download order" (a submenu, off by default) has "From start to end" and "First and last pieces first"; a tick
    means every chosen torrent has it. "Play" (single torrent with a video or audio file) opens its largest one; rows
    of the Files tab have a Play button. A finished file opens in its default app; one still downloading streams to
    a player that takes links (the default one if it does, else VLC, mpv, MPC or PotPlayer), or a dialog offers the
    link.
  - Problems are said in plain words with the way out, not as exception text: "Files not found — resume to
    download them again", "Not enough disk space — free some and resume" (`TorrentProblem`). Resume fixes each
    of them. No network shows one warning `InfoBar` above the table instead of every row looking stuck.
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
- One style and stroke width across the whole app: 20 px in the torrent command bar, 16 px on the details pane tabs,
  16 px in menus (including the "…" overflow), navigation and small buttons, 20 px for card and settings header
  icons, 40–48 px in empty states.
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
| Progress of a torrent or file | 16 px `ProgressBar` with 4 px corners on the grey `ProgressTrackBrush` track (App.xaml) |

### Dialogs (ContentDialog) — the same rules as the author's Mail app

Helpers: `src/Wintor.App/Services/Dialogs.cs`. Every dialog, new or changed:

- **Fluent style with a footer band.** The body is the card color (white in light theme) with rounded corners; the
  buttons sit in the dialog's own footer band (its background and the divider above it), never inside the content.
  `Dialogs.Prepare(dialog, XamlRoot)` sets it, plus the window's theme and XamlRoot. A `ContentDialog` **subclass**
  (like `AddTorrentDialog`) does not get the app's implicit style: set `Style="{StaticResource DefaultContentDialogStyle}"`.
- **Shown with `dialog.ShowQueuedAsync()`**, never `ShowAsync()`: WinUI keeps one dialog per window, the rest wait.
- **The primary (accent) button is on the right, Cancel on the left** (`ShowQueuedAsync` mirrors the footer).
  The primary button says what happens: "Add", "Remove", "Create" — never "OK".
- **Destructive actions:** `dialog.MakeDestructive()` — a red primary button with the focus, so Enter confirms.
  The dialog names what is affected (the torrent's name, or "3 torrents").
- **Long content scrolls edge to edge:** a `ScrollViewer` as the content scrolls right up to the footer band.
- Esc and the mouse's Back button close the dialog.

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
