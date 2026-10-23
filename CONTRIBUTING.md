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

The easiest way to help if you do not write C#. Strings live in
`src/Wintor.App/Strings/<language>/Resources.resw`; Wintor follows the Windows display language.

| Language | Folder | Status |
|---|---|---|
| English | `en-US` | Source |
| Russian | `ru-RU` | Reviewed |
| German, French, Spanish, Italian, Portuguese (Brazil), Polish, Ukrainian, Turkish, Chinese (Simplified), Japanese | `de-DE`, `fr-FR`, `es-ES`, `it-IT`, `pt-BR`, `pl-PL`, `uk-UA`, `tr-TR`, `zh-Hans`, `ja-JP` | **Machine-translated — needs review by a native speaker** |

To review one: run Wintor with that Windows display language, fix what reads wrong in its `.resw`, and open a PR
(screenshots welcome). When a language has been read through, change its status here to "Reviewed".

To add a language, copy `en-US` to a new folder (e.g. `nl-NL`) and translate the `<value>` elements.
Keep `{0}`-style placeholders intact, keep the "…" that marks items which open a dialog, and keep column and
status texts short — they sit in narrow table columns.

## Pull requests

1. Fork, create a branch, make one logical change.
2. Run `dotnet build Wintor.sln -p:Platform=x64` (zero warnings) and `dotnet test Wintor.sln -p:Platform=x64`.
3. For UI changes, attach screenshots in light and dark theme.
4. Fill in the PR checklist.

## Sign-off (DCO)

Every commit carries a `Signed-off-by` line — `git commit -s` adds it. With it you certify the
[Developer Certificate of Origin 1.1](https://developercertificate.org/): you wrote the change, or otherwise
have the right to submit it under the project's license. Use your real name or a name you are known by.
Forgot? `git commit --amend -s` for the last commit, `git rebase --signoff main` for a whole branch.

## License

By contributing you agree that your contribution is licensed under GPL-3.0-or-later together with the
additional permission for app stores in [LICENSE-STORE-EXCEPTION.md](LICENSE-STORE-EXCEPTION.md).
The name and logo are not part of the license: see [TRADEMARKS.md](TRADEMARKS.md).

Everyone taking part follows the [code of conduct](CODE_OF_CONDUCT.md). Security problems:
[SECURITY.md](SECURITY.md), not public issues.
