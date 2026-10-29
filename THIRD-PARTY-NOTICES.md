# Third-party notices

Wintor is licensed under GPL-3.0-or-later. It includes or depends on the following
third-party components, each under its own license.

| Component | License | Source | Used for |
|---|---|---|---|
| MonoTorrent | MIT | https://github.com/alanmcgovern/monotorrent | BitTorrent engine; `OrderedPieceRequester` is adapted from its `StandardPieceRequester` |
| CommunityToolkit.Mvvm | MIT | https://github.com/CommunityToolkit/dotnet | MVVM |
| CommunityToolkit.WinUI.Controls.SettingsControls | MIT | https://github.com/CommunityToolkit/Windows | Settings cards |
| Microsoft.Extensions.DependencyInjection | MIT | https://github.com/dotnet/runtime | Dependency injection |
| Windows App SDK | MIT | https://github.com/microsoft/WindowsAppSDK | WinUI 3 |
| Hugeicons free icons (`@hugeicons/core-free-icons`) | MIT | https://hugeicons.com | Icons in `src/Wintor.App/Assets/Icons` and the logo |
| SkiaSharp | MIT | https://github.com/mono/SkiaSharp | `tools/Wintor.IconTool` only; not shipped in the app |

## Hugeicons

The icons in `src/Wintor.App/Assets/Icons/` and the outlines generated from them in
`src/Wintor.App/Icons/HugeIcons.g.cs` come from the free Hugeicons set, under this license:

```
MIT License

Copyright (c) 2025 Hugeicons

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the "Software"), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in all
copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
SOFTWARE.
```
