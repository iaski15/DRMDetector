# DRM Detector

A small, fast Windows tool that scans game executables (`.exe` / `.dll`) for common
DRM and packer signatures — Denuvo, VMProtect, Themida, UPX, Aspack, PECompact,
Obsidium, CodeVeil, Arxan, and Steam stubs.

It works by extracting readable strings (ASCII + UTF‑16) from the binary and matching
them against known markers, so it's quick enough to run over a whole game folder.

## Quick start

Build once (from this folder):

```bat
publish.cmd
```

That produces a self-contained `publish\DRMDetector.exe` — no .NET runtime needs to be
installed on the target machine. The file is ~68 MB and under 100 MB.

Then just run it:

```bat
publish\DRMDetector.exe
```

Pick a game folder (or a single executable) and click **START SCAN**.

## Command line

You can also scan without the GUI, which is handy for scripts or batch checking:

 ```bat
:: Scan a single file
publish\DRMDetector.exe --scan "C:\Games\SomeGame\game.exe"

:: Scan a whole folder (top-level .exe/.dll only)
publish\DRMDetector.exe --scan "C:\Games\SomeGame"
```

Example output:

```text
DRM Detector - command line scan
scanning game.exe ... Denuvo, SteamStub
scanning helper.dll ... no known DRM detected

2 file(s) scanned, 1 with detections.
```

## How detection works

| Detection | What it looks for |
|-----------|-------------------|
| **Denuvo**    | `denuvo` in the filename or binary strings |
| **Arxan**     | `arxan.dll`, `arxanapp.dll`, `arxan_clr.dll`, or name contains `arxan` |
| **SteamStub** | A small (< 5 MB) `.exe` referencing `steam_api` / `steamworks`, or a `steamstub` marker. Dummy `steam_api*.dll` shipped by non-Steam publishers (Ubisoft, EA, GOG) are intentionally ignored. |
| **VMProtect** | `vmprotect`, or `.vmp0` / `.vmp1` sections |
| **Themida**   | `themida.dll`, `themida.sys`, `winlicense.dll`, or `.themida` |
| **Aspack**    | `.aspack` / `.adata` |
| **UPX**       | `.upx0` / `.upx1` sections, or the `Upx!` magic |
| **PECompact** | `pec2.exe`, `pec1.exe`, or `pecompact` |
| **Obsidium**  | `obsidium` string |
| **CodeVeil**  | `codeveil` string |

Detection is heuristic (string/section based), not a full PE analysis, so results are a
good first pass rather than a guarantee. Custom or very new DRMs may be missed.

## Building from source

Requires the [.NET 8 SDK](https://dotnet.microsoft.com/download) (or newer).

```bat
:: Debug build (fast, for development)
dotnet build -c Debug

:: Release: single self-contained exe in .\publish
publish.cmd
```

`publish.cmd` is just a wrapper around `dotnet publish ... -c Release`. The project is
configured to emit one compressed, self-contained executable into `.\publish`
(`<PublishDir>publish\</PublishDir>`), keeping it under 100 MB.

## Project layout

```text
DRMDetector/
  App.xaml(.cs)          WPF entry point + --scan CLI mode
  MainWindow.xaml(.cs)   UI and result display
  DrmScanner.cs          string extraction + signature matching (no GUI dependency)
  DRMDetector.csproj     build/publish configuration
  publish.cmd            one-shot publish to .\publish
  README.md / LICENSE    docs and MIT license
```

The scanning logic lives in `DrmScanner` so it can be reused headlessly (the CLI mode and
any future tooling both call the same code path).

## License

MIT — see [LICENSE](LICENSE).
