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

- Shell: `NavigationView` (Left) with the custom `TitleBar`. Content pages use `Padding="24,12,24,0"`.
- Page header: title (`TitleTextBlockStyle`) on the left, `CommandBar` on the right with
  `IsDynamicOverflowEnabled="True"` so commands move to "…" on narrow windows.
- Lists of torrents are **cards**: `CardBackgroundFillColorDefaultBrush`, `CardStrokeColorDefaultBrush`,
  `OverlayCornerRadius`, 4 px between cards. No `DataGrid`, no dozens of columns.
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

- Only `{ThemeResource}` brushes, named by purpose. No hex colors in XAML or code.
- Accent color is the user's Windows accent — do not override it.
- Status is shown with the standard `ProgressBar` states (`ShowPaused`, `ShowError`) and `InfoBar` severities,
  not custom colors.
- Check light, dark and a high-contrast theme for every UI change.

## Icons

- [Hugeicons](https://hugeicons.com) **free set (MIT), Stroke Rounded style only** — never Pro icons.
- One style and stroke width across the whole app; 16 px in command bars and lists, 20 px in navigation.
- Icons follow `Foreground` so they adapt to the theme. Store SVGs in `src/Wintor.App/Assets/Icons/`.
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
