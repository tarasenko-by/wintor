# Manual UI checks

Things the automated UIA run (`tests/ui/ui-tests.ps1`) cannot judge. Claude adds items here
while testing; tick them off after a look by hand. Newest on top.

## Open

- [ ] **Column widths** — hover a header's right edge: the resize cursor and a thin line appear; dragging changes
      the width (the name column gives way), double-click resets; widths are back after a restart.
- [ ] **Columns menu** — right-click the header and the ⚙ button at its end both open it; Name, Progress and Status
      are ticked and greyed; "Reset columns" brings back №, Name, Size, Progress, Status, Down, Up.

- [ ] **Table** — hover highlights a row; click selects and fills the pane below; Ctrl/Shift-click selects several;
      column headers sort (arrow shows the direction, click again flips); narrow the window — Seeds/Peers, Time left,
      Up, Status, Size, № disappear in that order and come back.
- [ ] **Progress colors** — downloading green, seeding blue, paused / stopped grey, error red.
- [ ] **Details pane** — drag the splitter (the line lights up), double-click resets; the height is remembered.
      General: "Downloaded" fills while downloading, "Availability" shows parts peers have; Speed graph moves.
- [ ] **Labels** — right-click → Label → New label…: the label appears in the side pane with a count and as a pill
      on the row; picking it in the side pane filters; right-click the label in the side pane → Delete label.
- [ ] **Order (№)** — right-click → Order → Move up / down / top / bottom and Alt+↑/↓ move the row and switch the
      table to the № order.

- [ ] **Tray** — close the window: Wintor stays in the notification area, a one-time "still running"
      notification appears; left-click the icon brings the window back; right-click menu: Open, Resume all,
      Pause all, Exit (Exit really quits). Settings → "Keep running when the window is closed" off: closing quits.
- [ ] **"Download finished" notification** — when a torrent completes, the notification shows its name;
      "Open folder" opens Explorer on it; clicking the body brings the window back (also when Wintor was hidden).
- [ ] **.torrent double-click / magnet in a browser** — pick Wintor as the default app for `.torrent` and
      `magnet:`; both open the add dialog (folder + file choice), also when Wintor runs in the tray.
- [ ] **Add dialog for a multi-file .torrent** — "All files" ticks/unticks everything, the summary updates,
      "Add" is disabled with no files; unticked files show "Don't download" on the details page.
- [ ] **Window background** — Settings → Window background: Mica, Mica Alt, Acrylic, Acrylic Thin apply at once;
      with Windows transparency effects off (or high contrast) it stays solid; back to "Solid" restores the palette.
- [ ] **Theme switch** — Settings → Theme: light / dark / as in Windows apply at once, caption buttons
      (minimize/maximize/close) follow, dialogs open in the same theme.
- [ ] **Context menu** — right-click a card: Resume/Pause, Open folder, Details, Copy magnet link, Remove…;
      with several cards selected the menu acts on all of them.
- [ ] **Multi-select** — Ctrl/Shift-click, Ctrl+A selects all, Esc clears; Remove asks once for all.
- [ ] **Speed limits and port** — limits change speed while downloading; a new port works after change.
- [ ] **Start with Windows** — turn it on, sign out and in: Wintor starts hidden in the tray.
- [ ] **Speed while actually downloading** — add a torrent with seeders: ↓ speed and ETA update
      smoothly (rates count payload only, averaged over 5 s).
- [ ] **Hover and press states** — command bar buttons, list cards, the ⓘ button, Settings cards:
      hover/pressed visuals look right in light and dark theme.
- [ ] **Drag and drop** — drop a `.torrent` file and a magnet text onto the Torrents page; the
      "Drop" caption appears and the add dialog opens.
- [ ] **Keyboard shortcuts** — Ctrl+N (magnet), Ctrl+O (open file), Ctrl+F (search), Esc (clear search /
      selection), Delete (remove selected), Enter (details).
- [ ] **Narrow window** — commands move into the "…" overflow; nothing is clipped.
- [ ] **High contrast** — switch Windows to a contrast theme with the app open: readable, no backdrop.
- [ ] **Icon-only Resume / Pause / Remove** — hover each: the tooltip shows its name.
- [ ] **Speed limit dialog** — right-click a torrent → "Speed limit…": the fields show its current limits; set
      e.g. 100 / 20 KB/s → General → Transfer shows them and the download rate stays under the limit; 0 clears.

## Done

- [x] Placeholder app icon — replaced by the Wintor logo (tools/Wintor.IconTool `logo`) (2026-10-23).
- [x] "Torrents" in the side pane did nothing on the details page — now goes back (2026-10-23).
- [x] Search typed through UI Automation did not filter — the app now follows the box's Text property (2026-10-23).
- [x] "Remove" hidden in "…" on a wide window — Resume/Pause/Remove are icon-only with tooltips (2026-10-23).
- [x] "Параметры" vs "Настройки" — ru-RU page title is now "Параметры", matching the nav item (2026-10-23).
- [x] Speed with zero peers — rates no longer include protocol overhead; a finished torrent shows 0 B/s (2026-10-23).
