# Microsoft Store listing (English)

Copy these into Partner Center → the app → Store listings → English. Field limits are Partner Center's.
Keep the wording about legal use: torrent clients get a closer look in certification.

## Product name

Wintor - Torrent Client (reserved in Partner Center; Start and the title bar say "Wintor")

## Short description (up to 100 characters, shown on some surfaces)

A modern, open-source BitTorrent client made for Windows 11. No ads, no telemetry.

## Description (up to 10,000 characters)

Wintor is a BitTorrent client that feels like it belongs on Windows 11: Fluent design, a calm torrent table and
sensible defaults, with no ads, no telemetry and no bundled offers. It is free and open source (GPL-3.0).

Add a magnet link or a .torrent file — or double-click one in File Explorer, or click a magnet link in your
browser — choose the folder and the files you want, and Wintor does the rest. Progress is colored by state:
blue while downloading, green when done and seeding, grey when paused.

Under the table, a details pane shows what is going on: which pieces you have and how available the rest are,
the files with their priorities, connected peers, trackers and a live speed graph.

Organize with status filters and colored labels in the side pane, search from the title bar, sort by any column,
keep your own order, and pick which columns to show. Set speed limits for everything or for a single torrent.
When the window is closed, Wintor keeps seeding quietly from the notification area and lets you know when a
download finishes.

When something goes wrong, Wintor says so in plain words — files that were moved or deleted, a full disk, no
network — and Resume puts it right.

Wintor works in light, dark and high-contrast themes, with Mica or Acrylic if you like, and speaks 12 languages.

Wintor does not include any torrent search or content catalog. Use it for content you have the right to download
and share, such as Linux distributions, open-source software and public-domain or Creative Commons works.

Source code, issues and ideas: https://github.com/tarasenko-by/wintor

## What's new in this version (release notes)

First release on the Microsoft Store:
- A calm torrent table: progress colored by state, sortable columns that shrink with the window, your own order.
- A details pane: pieces, files with priorities, peers, trackers and a speed graph.
- Colored labels and status filters, search from the title bar.
- Speed limits for everything and for each torrent.
- Problems explained in plain words; Resume fixes them.
- 12 interface languages.

## Product features (up to 20, up to 200 characters each)

1. Magnet links and .torrent files, from the app, File Explorer or your browser
2. Choose the folder and the files before a download starts
3. Progress colored by state: downloading, done and seeding, paused
4. Details pane with piece map, files and priorities, peers, trackers and a live speed graph
5. Status filters and colored labels; search from the title bar
6. Sortable, resizable columns; pick which ones to show; keep your own order
7. Speed limits for all torrents and for each one
8. Keeps seeding from the notification area; "download finished" notifications
9. Clear explanations for missing files, a full disk or no network
10. Light, dark and high-contrast themes; optional Mica or Acrylic
11. 12 interface languages
12. No ads, no telemetry, no bundled offers — free and open source (GPL-3.0)

## Search terms (up to 7, 30 characters each in total per term)

torrent, bittorrent, magnet, torrent client, p2p, download manager, open source

## Screenshots (1366×768 or larger, PNG)

From `docs/screenshots` (retake with `tools/screenshots.ps1`), in this order, each with its caption:

1. `details-light.png` — Your torrents at a glance, with details for the one you pick
2. `speed-dark.png` — A live speed graph for every torrent
3. `torrents-dark.png` — Labels and status filters keep things tidy, in light or dark
4. `peers-light.png` — See who you are connected to
5. `settings-light.png` — Sensible defaults, everything else one click away

## Other fields

- **Category:** Utilities & tools (subcategory: File managers, if offered; otherwise none)
- **Privacy policy URL:** https://github.com/tarasenko-by/wintor/blob/main/docs/PRIVACY.md
- **Website:** https://github.com/tarasenko-by/wintor
- **Support contact:** https://github.com/tarasenko-by/wintor/issues
- **Copyright:** © 2026 Siarhei Tarasenka and Wintor contributors
- **Developed by:** Siarhei Tarasenka
- **Pricing:** Free
- **Age rating questionnaire:** no violence, no user-generated content shared through the app, no in-app purchases,
  no ads. The app accesses the internet to download files the user adds. Expected rating: 3+ / Everyone.
- **Restricted capability `runFullTrust`** — justification for certification: "Wintor is a desktop app built with
  WinUI 3 and the Windows App SDK; runFullTrust is required for every packaged Win32 desktop app (it runs its own
  BitTorrent engine, listens on a network port and writes downloaded files to the folder the user chooses)."
- **Notes for certification:** "Wintor is a BitTorrent client without any content search or catalog. To test, add
  a legal torrent, e.g. the Ubuntu desktop image from https://ubuntu.com/download/alternative-downloads, or use the
  Blender Foundation's Big Buck Bunny magnet link. No account is needed."
