# Attribution

Myosu! PP Counter/Overlay is an independent C# application. Stable signatures and memory-layout
facts were checked against `packages/tosu/src/memory/stable.ts` in
[tosuapp/tosu](https://github.com/tosuapp/tosu/blob/master/packages/tosu/src/memory/stable.ts),
retrieved 2026-10-30. That project identifies its license as LGPL version 3.
Copyright (C) 2023-2026 Mikhail Babynichev and the tosu contributors.

The reader sources and their layout adaptation are provided under
LGPL-3.0-or-later. All source required to rebuild this application is in `src/`,
with the Windows .NET Framework compilation command in `build.ps1`. There are no
tosu executables, extracted addons, or tosu runtime dependencies in this package.
The framework libraries are supplied by Windows/.NET Framework.

PP is calculated using MaxOhn's `rosu-pp-js` 4.0.1, distributed under MIT with
the package's license at `calculator/package/LICENSE` and in `licenses/`.
Package source: https://github.com/MaxOhn/rosu-pp-js . Its WebAssembly and JS
are bundled unmodified. `calculator/worker.js` is the local integration code.
The existing Windows Node.js runtime is bundled for offline operation; its
license and third-party notices are in `licenses/Node-LICENSE.txt`.
The briefly evaluated Rosu.Net native library is not used or distributed.

The user-supplied `alexandra-vezhnovets-neon-room.gif` is preserved as
`background.gif`. Its original artwork rights remain with the original creator.
It is separate from the software license. No redistribution license for this
artwork is asserted.

The character image for the app icon was supplied by the user. Its white
background was removed with the built-in image editor, then converted to a
multi-resolution Windows icon. Character artwork rights remain with their
respective owners; the asset is not covered by the source-code license.
