# Wintor

A modern, open-source BitTorrent client for Windows 11, built with WinUI 3.

Most torrent clients still look like it is 2010: dense tables, dozens of columns,
settings you get lost in. Wintor aims to feel like a native Windows 11 app —
Mica, Fluent design, sensible defaults — with no ads and no telemetry.

> **Status:** early development (v0.1). See [docs/ROADMAP.md](docs/ROADMAP.md).

## Features (so far)

- Add torrents from magnet links (prefilled from the clipboard), `.torrent` files or drag & drop
- Live progress, speed, peers and time left
- Pause, resume, remove (optionally with downloaded files)
- Torrents survive restarts; paused ones stay paused
- Light/dark theme, English and Russian UI

## Building

Requirements:

- Windows 10 1809+ or Windows 11, with **Developer Mode** on
  (Settings → System → For developers)
- [.NET 10 SDK](https://dotnet.microsoft.com/download)
- Optional: Visual Studio 2022/2026 with the *WinUI application development* workload,
  or [WinApp CLI](https://github.com/microsoft/winappcli)

From Visual Studio: open `Wintor.sln`, pick `x64` (or `ARM64`), set **Wintor.App (Package)** as startup and press F5.

From the command line:

```powershell
winapp run src\Wintor.App
```

Tests:

```powershell
dotnet test Wintor.sln -p:Platform=x64
```

## Project layout

| Project | What it is |
|---|---|
| `src/Wintor.App` | WinUI 3 app: windows, pages, view models, localized strings |
| `src/Wintor.Core` | Engine-independent models, `ITorrentEngine`, formatting helpers |
| `src/Wintor.Engine.MonoTorrent` | `ITorrentEngine` implementation on [MonoTorrent](https://github.com/alanmcgovern/monotorrent) |
| `tests/*` | xUnit tests |

The UI only talks to `ITorrentEngine`, so the BitTorrent engine can be replaced
without touching the app.

## Translations

UI strings live in `src/Wintor.App/Strings/<language>/Resources.resw`.
To add a language, copy `en-US` to a new folder (e.g. `de-DE`) and translate the values.

## Contributing

Contributions are welcome — see [CONTRIBUTING.md](CONTRIBUTING.md). Project rules for people and
AI coding agents are in [AGENTS.md](AGENTS.md); UI rules in [docs/DESIGN.md](docs/DESIGN.md).

## License

Wintor is free software: you can redistribute it and/or modify it under the terms of the
GNU General Public License as published by the Free Software Foundation, either version 3
of the License, or (at your option) any later version. See [LICENSE](LICENSE).

MonoTorrent is licensed under the MIT License.
