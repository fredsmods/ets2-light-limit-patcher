# ETS2 Light Limit Patcher

Raises the **light list capacity of Euro Truck Simulator 2 (v1.62.3.3)** from 480 to a value you choose
(default 600). Useful when mods with lots of lights (beacons, light bars, decorations) overflow the limit and the game logs

```
Light amount N exceeds the light list capacity (480). Expect rendering artifacts.
```

## Quick start

1. Download the zip from the **[Releases](../../releases)** page (or *Code -> Download ZIP*) and extract the **whole** folder (right-click the zip -> *Properties* -> **Unblock** first, if Windows offers it).
2. Close ETS2.
3. Double-click **`Start-ETS2-Light-Patcher.bat`**.
4. The patcher finds your Steam install. If not, click *Browse...* and pick `...\Euro Truck Simulator 2\bin\win_x64\eurotrucks2.exe`.
5. Choose the limit and click **Patch**.

A backup `eurotrucks2.exe.bak` is created next to the exe first (an existing `.bak` is never overwritten).
**Restore original** puts it back; patching to 480 also restores the original bytes exactly.

## Why a .bat + readable source and not just an exe?

Unsigned exes that modify other exes look exactly like malware to SmartScreen/antivirus, and you should not have to trust a binary.
The launcher is a plain PowerShell script that compiles `src/LightPatcher.cs` on your PC with the compiler built into Windows -
read the code before you run it. A prebuilt exe of the very same source is attached to each release (`prebuilt/` and `SHA256SUMS.txt` are in the release zip only) if you prefer it;
you can rebuild it yourself with `build-exe.ps1`.

Nothing is downloaded or installed, nothing is sent anywhere, no game files are included.

## What you need to know

- **Only ETS2 1.62.3.3** is supported. The patcher checks 71 byte positions against the known layout and refuses anything else
  (other versions, other mods of the exe). After a game update or Steam's *verify files* the exe is original again: run the patcher again.
  If the game version changed, the patch has to be regenerated (see `tools/`).
- **Status:** limits **500 and 600 are confirmed working** in game; larger values are untested.
  The window allows 480-1000. If a value causes crashes or artifacts, go back to 600 or 500, or restore the original.
- **Do not use a modified exe in multiplayer** (e.g. TruckersMP) - they forbid it. Use at your own risk; keep the backup.
- Writing into `Program Files` may need *Run as administrator* (right-click the .bat). Steam libraries on other drives normally don't.
- Shader/GPU-side limits of the game are unknown; the patch only changes the engine's CPU-side list.

## What it actually changes

A plain "480 -> 600" is **not enough**: the light pipeline stores its lists in containers with *fixed inline stack storage* for 480 entries.
Their "grow" only checks `n <= capacity` and returns NULL otherwise, so just raising the limit crashes at the 481st light
(null write in `0x14cf29b`). The patcher therefore makes **71 byte edits in two functions** (RVA `0x14d1060` and helper `0x14cfac0`), no code is moved:

- the comparisons/sizes that truncate the light list at 480,
- capacity **and** storage of four inline containers (extra space is added to the two stack frames; the largest frame grows from ~96 KB to ~207 KB at limit 600, thread stack is 1 MB),
- the matching Windows unwind data (so stack unwinding stays correct).

## Folder contents

| path | what |
|---|---|
| `Start-ETS2-Light-Patcher.bat` | double-click launcher (runs the .ps1 with the PowerShell built into Windows) |
| `ETS2-Light-Patcher.ps1` | compiles and starts the window |
| `src/LightPatcher.cs` | **all** the code (patch logic + window) |
| `assets/` | background picture and icon graphic |
| `prebuilt/ETS2-Light-Patcher.exe` | optional prebuilt exe of the same source (release zip only) |
| `build-exe.ps1` | rebuilds that exe with the C# compiler that ships with Windows |
| `tools/ets2_light_patcher.py` | same patcher as a command line tool (pure Python 3) |
| `tools/build_light500.py` | reference implementation that derives the edits by disassembly (needs `capstone`, `pefile`); the patchers are verified byte-identical against it |
| `LICENSE` | MIT license |
| `SHA256SUMS.txt` | checksums of every file in the release zip (release zip only) |

## Command line (optional)

```
python tools/ets2_light_patcher.py --info                  # state of your exe
python tools/ets2_light_patcher.py --limit 600 --inplace   # patch (backup first)
python tools/ets2_light_patcher.py --limit 480 --inplace   # restore original bytes
```

## Troubleshooting

- *"Not supported: this exe is not ETS2 1.62.3.3"* - game version differs, or the exe was modified by something else. Verify game files in Steam and retry.
- *"ETS2 is running"* - close the game (and the launcher) first.
- Window doesn't open / red error box - make sure you extracted the whole zip and that PowerShell isn't blocked by your security software.
- Crash after patching - restore the original (button or `--limit 480`) and tell us the crash offset from Windows Event Viewer (Application log, `eurotrucks2.exe`, "Fault offset").

## License

MIT, Copyright (c) 2026 Freds Mods - see `LICENSE`. Free to use, modify and share; no warranty.

Euro Truck Simulator 2 is a trademark of SCS Software. This project is not affiliated with or endorsed by SCS Software.
