# Manual UI checks

Things the automated UIA run (`tests/ui/ui-tests.ps1`) cannot judge. Claude adds items here
while testing; tick them off after a look by hand. Newest on top.

## Open

- [ ] **When done (Actions)** — side pane → Actions → When done: Off is ticked; on a PC without hibernation (or
      Modern Standby) Hibernate is not listed. Pick "Quit Wintor when downloads finish" with a small legal torrent
      downloading: Actions gets a dot and the tooltip names the choice; when it finishes the window comes back (also
      from the tray) with "Wintor will quit in 60 s", the number counts down, Cancel keeps Wintor running and the
      choice is Off again; "Quit now" / the time running out quits and the torrent resumes on the next start.
      Then once each, on a PC you can spare: Sleep (also on a Modern Standby laptop: the screen goes off and it
      sleeps), Hibernate, Restart, Shut down — after Restart / Shut down Wintor's torrents come back intact.
      Narrator reads the countdown when the dialog opens.

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
- [ ] **Deleted files** — finish a small legal torrent, quit (`wintor --quit`), delete its folder, start Wintor:
      the row says "Files not found — resume…", grey bar; Resume rechecks and downloads it again.
- [ ] **No network** — turn Wi-Fi/Ethernet off: the "No network connection" bar appears above the table and
      goes away when the network is back.
- [ ] **Not enough space** — add a .torrent bigger than the free space of the chosen drive (or pick a small USB
      stick as the folder): a caution line under the folder names free vs. needed space; unticking files updates it.
- [ ] **Close to tray** — close the window with ×: a "Wintor is still running" notification appears once per launch;
      clicking it brings the window back.
- [ ] **Language** — Settings → Language → pick another → Restart: Wintor closes and comes back in that language
      with the same torrents; "As in Windows" + Restart returns to the system language.
- [ ] **What's new after an update** — set `WhatsNewSeenVersion` in settings.json to an older version ("0.1.0"),
      start Wintor: "What's new" opens once; the next start it does not; with the switch in About off it never does.
- [ ] **GitHub link** — Settings → About → "Source code on GitHub" opens the repository in the browser.
- [ ] **Resume/Pause** — select a running torrent: the bar shows Pause; pause it: the same place shows Resume.
- [ ] **Window place** — move and resize the window, quit from the tray, start again: same place and size;
      maximize, quit, start: maximized, and restoring gives the old size back; unplug the monitor it was on: it opens
      on the main screen.
- [ ] **Long languages** — switch the Windows display language to German, then Polish (Settings → Time &
      language), restart Wintor: column headers, status cells, side pane, dialogs and Settings are not clipped.
      Also look at Japanese or Chinese once for font fallback.
- [ ] **Narrator** — Files, Peers and Trackers rows read as one line each (not a class name); the current label
      in the Label submenu is announced as "Current label".
- [ ] **Download order** — right-click a downloading torrent → Download order → From start to end: the menu shows it
      ticked next time, the Downloaded bar fills from the left; it survives a restart. Several selected with mixed
      settings: unticked, clicking turns it on for all.
- [ ] **Play, finished file** — right-click a finished torrent with a video → Play: it opens in the default player.
- [ ] **Play while downloading, VLC** — with VLC installed (default or not), Play on a torrent at a few percent: VLC
      opens a 127.0.0.1 link and starts once the first pieces arrive; seeking forward waits a moment, then plays.
      Closing VLC: after about two minutes the torrent goes back to its usual order.
- [ ] **Play, no player for links** — with only Media Player (no VLC/mpv): the "Play while downloading" dialog
      shows the link; Copy link puts it on the clipboard.
- [ ] **Files tab** — video and audio rows have a Play button (same column for every row), others leave it empty;
      Narrator reads it as "Play".

## Done

- [x] Placeholder app icon — replaced by the Wintor logo (tools/Wintor.IconTool `logo`) (2026-10-23).
- [x] "Torrents" in the side pane did nothing on the details page — now goes back (2026-10-23).
- [x] Search typed through UI Automation did not filter — the app now follows the box's Text property (2026-10-23).
- [x] "Remove" hidden in "…" on a wide window — Resume/Pause/Remove are icon-only with tooltips (2026-10-23).
- [x] "Параметры" vs "Настройки" — ru-RU page title is now "Параметры", matching the nav item (2026-10-23).
- [x] Speed with zero peers — rates no longer include protocol overhead; a finished torrent shows 0 B/s (2026-10-23).
