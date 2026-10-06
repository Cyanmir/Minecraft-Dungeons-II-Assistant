# Third-party resources

Original code, original icon assets and project documentation use the MIT License. That license does not relicense third-party fonts or trademarks.

- **Minecraft Ten** (`src/assets/minecraft-ten.ttf`): unmodified `MinecraftTen.ttf` from Mojang's `web-theme-bootstrap` repository. Upstream source: https://github.com/Mojang/web-theme-bootstrap/blob/main/assets/fonts/MinecraftTen.ttf . Preserve the upstream project notice in `Mojang-fonts-license.txt` and its fonts-directory notice in `Mojang-fonts-OFL.txt`. No upstream image assets are included. Font metadata credits Mojang AB, P22 / Canada Type and Patrick Griffin.
- **Minecraft** (`src/assets/minecraft-body.ttf`): Copyright Pwnage_Block 2011, supplied by the project owner. Its embedded license records identify Creative Commons Attribution-ShareAlike 3.0: https://creativecommons.org/licenses/by-sa/3.0/ . Attribution and link are retained; the font is unmodified and remains under CC BY-SA 3.0, not the project's MIT code license. It is distributed as a separate resource in this collection.
- **GNU Unifont 18.0.01** (`unifont.hex.gz`): preserve the bundled upstream `OFL-1.1.txt` and upstream notices.
- **Source Han Sans SC/HK/TW/JP/KR**, Regular/Heavy: Adobe and contributors, SIL OFL 1.1, with Reserved Font Name Source. See `SourceHanSans-LICENSE.txt`; upstream https://github.com/adobe-fonts/source-han-sans .
- The original project icon is retained in `src/assets/toolbox.png` and `src/assets/toolbox.ico`.
- Minecraft / Minecraft Dungeons names and game references remain the property of their owners. MCD2A is an unofficial community tool.

## Optional equipment component

Automatic equipment salvaging, native combat and direct nearby collection require Blueprint Loader by ewanhowell5195, acquired from https://www.nexusmods.com/minecraftdungeons2/mods/2 . When missing, the installer opens the author's Nexus download page and detects the completed ZIP, then installs the loader and requested component. Nexus login/download confirmation remains in the browser. A previously downloaded ZIP can also be selected or reused from local cache. Blueprint Loader is not bundled, mirrored or relicensed. Direct collection uses its own component and does not modify the equipment salvaging component.

The original component is compiled using NeoRune 0.3.1 and includes its timer/world helpers and watermark under MIT. The developer compiler and SDK are not required on players' machines.

MIT License

Copyright (c) 2026 NeoMakesGames / NeoPlayzGames

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



## Optional local equipment presentation

Extracted game artwork, localization catalogs, game SDK binaries and private diagnostics are not included in this repository or its public release. The equipment list works with built-in interface labels and type names. A separately supplied local `equipment-presentation.bin.gz` may be loaded from the application directory; it remains subject to its original owners’ rights and is excluded from Git.
