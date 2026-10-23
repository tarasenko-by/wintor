## What and why

<!-- What does this change, and why is it needed? Link the issue if there is one. -->

## Screenshots

<!-- Required for UI changes: light and dark theme. -->

## Checklist

- [ ] Follows [AGENTS.md](../AGENTS.md) (and [docs/DESIGN.md](../docs/DESIGN.md) for UI changes)
- [ ] `dotnet build Wintor.sln -p:Platform=x64` passes with zero warnings
- [ ] `dotnet test Wintor.sln -p:Platform=x64` passes; tests added for new logic
- [ ] No user-visible strings in code/XAML; new strings added to **every** `Resources.resw`
- [ ] Icons only from the Hugeicons **free** set (no Pro icons, no Segoe glyphs)
- [ ] Colors only via `ThemeResource`; checked light, dark and high-contrast themes
- [ ] New dependencies are GPL-compatible, pinned, and listed in `THIRD-PARTY-NOTICES.md`
- [ ] Every commit is signed off (`git commit -s`, [DCO](../CONTRIBUTING.md#sign-off-dco))
