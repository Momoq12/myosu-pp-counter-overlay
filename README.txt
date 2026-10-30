MYOSU! PP COUNTER/OVERLAY 1.0
Your own local osu! stable memory reader + animated desktop overlay.

START
Double-click Myosu.exe, then open osu! stable. The reader finds the
32-bit osu!.exe automatically. Open song select if it says CONNECTING.
tosu is not needed and Myosu! PP Counter/Overlay never connects to its server.
Keep background.gif beside the executable; it is your supplied animation.

CONTROLS
Drag anywhere to move. Right-click for the menu.
Ctrl+Alt+N: show / hide.
Ctrl+Alt+C: toggle click-through (also works when the window ignores clicks).
Drag an edge or corner to resize (400-1200 px wide; aspect ratio is preserved).
Ctrl + mouse wheel: resize.
Shift + mouse wheel: background visibility (0-100%, text stays readable).
Right-click > Transparency: separate background and whole-overlay sliders.
Set background visibility to 0 for text and stats over a transparent background.
Purple system tray icon: double-click to show and unlock; right-click to quit.
Menu options: stay on top, animate GIF, transparency, explicit sample preview.
Position, size, topmost, transparency and animation persist on exit.
Preview and click-through are deliberately off at every normal launch.

LIVE DATA
Song title, artist, difficulty, player name, score, accuracy, combo, max combo,
hit counts, mods, play time, map object count, HP, UR and standard key states.
UR is population standard deviation of recorded hit errors multiplied by 10;
it is in game-time units and is not clock-rate-normalized for DT/HT.
Standard, taiko, catch and mania accuracy formulas are provided; the compact
visual hit row shows 300 / 100 / 50 / misses. Extra mania judgements are in JSON.
The thin progress line is judged objects / beatmap object count, not audio time.
Live PP and stars use the bundled rosu-pp-js 4.0.1 calculator with lazer:false
(stable scoring). PP uses maximum combo reached, mods, exact hit counts and
judged object count for the partial play. It resets on a retry or map change.
The calculator reads the local .osu file in Songs (or BeatmapDirectory in your
osu! user configuration). Missing files show unavailable PP, never a fake value.
It runs offline in the bundled Node runtime and needs no tosu or npm installation.
PP is a calculated estimate and may differ from a server using another ruleset.
No gameplay modification, injection, write-memory operations or input automation.

LOCAL API
http://127.0.0.1:24060/       live JSON viewer
http://127.0.0.1:24060/state  JSON snapshot (also /json and /api/state)
http://127.0.0.1:24060/health server health
Polling: about 30 Hz. The JSON includes statsAvailable and connection; clients
must check statsAvailable before displaying gameplay stats. Unavailable accuracy,
HP and UR are null. Every snapshot has a UTC timestamp.
The API always reports real reader state; the UI's sample preview never feeds
fake values into the API. It binds only to loopback. No cloud or telemetry.
This is an independent JSON schema; no tosu WebSocket compatibility is claimed.
Change port in settings.json while the app is closed if 24060 is occupied.

GAME DISPLAY / OBS
For OBS, use Window Capture for the Myosu! PP Counter/Overlay window. Borderless/windowed osu!
works with a topmost desktop overlay. Exclusive fullscreen may cover desktop
windows: switch osu! to borderless/windowed to see the overlay while playing.
No in-game rendering hook is installed.

TROUBLESHOOTING
WAITING: launch stable (not lazer).
CONNECTING: enter song select or start a map. Signatures retry automatically.
Unreadable memory: run at the same privilege level as osu!; normally neither
program needs administrator rights. No automatic elevation is attempted.
Updates to osu! can change the layout. Incompatible signatures will need a
source update. This version does not fetch offsets or auto-update itself.
If the window disappears, use Ctrl+Alt+N or the tray icon.
reader.log contains local diagnostic messages, including feed-port conflicts.

BUILD / VERIFICATION
All C# source is in src. Run build.ps1 with PowerShell to rebuild using the
Windows .NET Framework compiler. No NuGet or SDK download is needed.
Myosu.exe --self-test writes test-results.json and exits with 0 on success.
Tests use a controlled child process with artificial game-memory structures.
They exercise real Windows process reads, scanning, pointer decoding, accuracy,
result/menu transitions and invalid pointers. They do not verify a real osu!
session. See VERIFICATION.txt for additional live and PP checks during delivery.
Myosu.exe --demo explicitly shows sample data.
Myosu.exe --render path.png saves a sample-data design preview.

LICENSE / ASSET
Reader source: LGPL-3.0-or-later. Full source and rebuild script are supplied.
See THIRD-PARTY.md and licenses for public layout-reference attribution.
background.gif was supplied by the user and retains its original rights;
it is not included under the reader source license.

