# Contributing to Wintor

Thanks for helping! Wintor is a modern BitTorrent client for Windows 11, and contributions of all
sizes are welcome: bug reports, translations, design feedback and code.

## Before you start

- Read [AGENTS.md](AGENTS.md) — project rules for everyone (people and AI tools alike).
- Read [docs/DESIGN.md](docs/DESIGN.md) if you touch the UI.
- For anything bigger than a small fix, open an issue first so we can agree on the approach.

## Getting it running

See [README.md](README.md#building). In short: Windows with Developer Mode, the .NET 10 SDK,
then `winapp run src\Wintor.App` or F5 in Visual Studio.

## Translations

Copy `src/Wintor.App/Strings/en-US/Resources.resw` to a new folder named after your language
(e.g. `de-DE`) and translate the `<value>` elements. Keep `{0}`-style placeholders intact.

## Pull requests

1. Fork, create a branch, make one logical change.
2. Run `dotnet build Wintor.sln -p:Platform=x64` (zero warnings) and `dotnet test Wintor.sln -p:Platform=x64`.
3. For UI changes, attach screenshots in light and dark theme.
4. Fill in the PR checklist.

## License

By contributing you agree that your contribution is licensed under GPL-3.0-or-later, the project's license.
