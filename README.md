# Mini Sweeper

Minesweeper in the Windows system tray.

Click the tray icon and a small flyout opens above the taskbar with a minefield inside. Open a few squares, click away, and the game waits with its clock paused until you come back.

Mini Sweeper is a WinUI 3 app with no main window. You get the classic game on the classic boards, drawn either as a Windows 11 style board with emoji or as the gray Windows 95 original, with more animation than either version had.

It's fully offline. There are no network calls, accounts, or telemetry.

## Features

- **The classic boards:** Beginner (9 × 9, 10 mines), Intermediate (16 × 16, 40 mines) and Expert (30 × 16, 99 mines), from the dropdown in the flyout.
- **Custom boards:** any size from 8 × 8 to 30 × 24, with as many mines as will fit. Once you set one up, it joins the difficulty list.
- **A fair first click:** mines are placed after your first click and kept away from that square and its neighbors, so the first click is never a mine and always opens an area.
- **Chording:** when a number already has all its flags, click it (or middle-click it, or press both buttons on it) to open every square around it.
- **Two looks:**
  - **Modern** is a Windows 11 style board with emoji that follows the taskbar's light or dark mode.
  - **Classic** is the Windows 95 board, with gray bevels, red LED counters and the smiley. It looks the same in light and dark mode.
- **Animated:**
  - Big openings ripple outward from where you clicked.
  - Hitting a mine sets off a shockwave, a red flash and a shake, then uncovers the other mines outward from the blast.
  - Clearing the board flags every mine in a wave and throws confetti.
- **Best times** for each board, and for each custom size separately. Beating one shows "New best time!".
- **The clock only runs while you can see it.** Hiding the flyout pauses the game. It's kept until you exit the app, so the flyout closing never loses it.
- **Fits your screen:** the flyout grows to hold the board. On a small display the squares shrink until the board fits.
- **Tray tooltip:** hover over the icon to see the game's state, such as "Expert: 42 mines left".
- **Tray menu:** right-click the icon for Open Mini Sweeper, New game, Settings and Exit.
- **Looks like part of Windows:**
  - The flyout opens next to the tray icon, slides and fades in, and hides when you click away.
  - Its acrylic background is tinted to match the taskbar.
  - The tray icon switches between a white and a dark glyph so it stays visible on light and dark taskbars.
- **Single instance:** launching the app again opens the flyout of the copy that's already running instead of adding a second icon.
- **Start with Windows** (optional), from Settings.

## How to play

Every square is either a mine or a number, which counts the mines in the eight squares around it. You win when every square that isn't a mine is open.

| Input | What it does |
|---|---|
| Left-click a square | Opens it. If it has no neighboring mines, everything around it opens too. |
| Right-click a square | Plants a flag. With question marks turned on, right-click again for `?` and once more to clear it. |
| Click a number, middle-click, or press both buttons | Chord: when the flags around a number match it, every other square around it opens. A misplaced flag means one of them is a mine. |
| Click the face, or press `F2` | Starts a new game. |
| `Esc` | Hides the flyout, or goes back from Settings. |

The left counter shows mines minus flags, and goes negative if you place more flags than there are mines. The right counter shows seconds. The info button in the flyout's header shows a hint for the current game and your best times.

## Settings

Open Settings from the gear in the flyout or from the tray menu. Changes take effect immediately.

| Setting | What it does |
|---|---|
| Start with Windows | Keeps the tray icon ready after you sign in. Only works when the app is installed. |
| Board theme | Modern (Windows 11 style with emoji) or Classic (Windows 95). |
| Question marks | When on, right-click cycles through flag, `?` and clear, like the original. When off, it switches between flag and clear. |
| Reset best times | Clears every best time. |
| Custom board | Width, height and mine count for a custom game. The mine limit adjusts to the board size. |

To go straight to the custom board settings, choose **+ Custom…** at the bottom of the difficulty dropdown.

## About the game

Robert Donner and Curt Johnson wrote Minesweeper, and Microsoft first shipped it in Microsoft Entertainment Pack 3 in 1990. It was included with Windows from Windows 3.1 in 1992 until Windows 8, which moved it to the Store. The Classic theme recreates the Windows 95 version.

## Install

Download the signed MSIX for your architecture (x64 or ARM64) from the [releases](../../releases) page. If the tray icon is hidden after the first launch, look in the taskbar overflow (^).

Mini Sweeper requires Windows 10 version 1809 (build 17763) or later.

## Build from source

You need:

- The [.NET 10 SDK](https://dotnet.microsoft.com/download).
- [Developer Mode](https://learn.microsoft.com/windows/apps/get-started/enable-your-device-for-development) turned on, so the app can be registered with package identity.
- Optionally, the [winapp CLI](https://github.com/microsoft/WinAppCli) (`winget install Microsoft.WinAppCli --source winget`). `dotnet run` gets it through a NuGet package, so you only need it to run `winapp` commands yourself.

```powershell
git clone https://github.com/Tray-Land/mini-sweeper.git
cd mini-sweeper

$Platform = if ($env:PROCESSOR_ARCHITECTURE -eq 'AMD64') { 'x64' } else { $env:PROCESSOR_ARCHITECTURE }
dotnet run -c Debug -p:Platform=$Platform
```

`dotnet run` uses the `Microsoft.Windows.SDK.BuildTools.WinApp` package to register a loose-layout package and launch the app with package identity, which settings storage and the startup task need. To delete saved settings and best times and see the first-run experience again, use `winapp run --clean`. To quit, right-click the tray icon and choose **Exit**.

In Debug builds, `F9` clears the board so you can check the win celebration without playing a whole game.

### Tests

```powershell
dotnet test Tests
```

The rules in `Logic/MinesweeperGame.cs` have no WinRT dependency, so the test project compiles that file directly and runs on plain `net10.0`. The tests cover first-click safety, flood fill, flags and question marks, chording, winning, losing, and the limits on custom boards.

## How it works

| Path | Contents |
|---|---|
| `App.xaml.cs` | The tray icon and its menu, single-instance handling, the crash log, and the only exit path. There is no main window. |
| `Logic/MinesweeperGame.cs` | The rules: boards, mine placement, flood fill, chording, winning and losing. No UI dependencies. |
| `Services/GameSession.cs` | The current game, the selected difficulty and the best times. Kept outside the flyout so the game survives it closing. |
| `Views/TrayFlyoutWindow` | The flyout window: placement next to the icon, sizing to the board, the slide and fade, light dismiss, and closing after a minute hidden. |
| `Views/FlyoutPage` | The game page: input, the counters and face, the reveal ripple, and the explosion and win effects. |
| `Views/SettingsPage` | Settings, shown inside the flyout instead of a separate window. |
| `Controls/` | The board, drawn in code: cells (`CellView`), the face (`FaceView`), the LED counters (`LedDisplay`), the bevels and Windows 95 palette (`Win95`), the Modern and Classic palettes (`Skin`), the animations (`Fx`), and the acrylic backdrop (`ShellBackdrop`). |
| `Services/` | Settings, the startup task, the system theme, window placement, and releasing idle memory. `Secrets` and `CredentialStore` come from the project template and aren't used. |
| `Tests/` | xUnit tests for the rules. |
| `build-msix-for-gh.ps1` | Builds signed x64 and ARM64 MSIX packages and drafts a GitHub release. |

**Rules.** `MinesweeperGame` places the mines on the first reveal. `Reveal` and `Chord` return every square they open along with its flood-fill depth, meaning how many steps it is from the click. The page delays each square's reveal in proportion to its depth, which makes big openings ripple outward.

**Lifetime.** Dismissing the flyout hides it. After a minute hidden it closes, and the next click builds a new one. The game lives in `GameSession`, so it outlasts the window. While the flyout is hidden nothing runs: the clock and effect timers stop, and any effects still pending finish immediately.

**Footprint.** Once the window closes, `MemoryService` returns its memory to Windows, so the idle app is little more than a tray icon. The project also turns off concurrent garbage collection and tiered PGO, and uses Windows NLS instead of ICU for globalization.

**Drawing.** The board is built in code rather than with XAML templates. Each cell's bevel is a few nested `Border`s, and a cell creates its glyph only when it needs one, so the 480 cells of an Expert board stay cheap. The effects are short Composition animations.

## Privacy

- The app makes no network calls and has no accounts or telemetry.
- Your settings, difficulty, custom board and best times are stored in the app's local settings. The game in progress is held in memory only.
- Unhandled exceptions are appended to `%TEMP%\MiniSweeper-crash.log`.

## Releasing

1. Bump `Version` on the `Identity` element in `Package.appxmanifest`.
2. Run `build-msix-for-gh.ps1`.

The script builds and signs an MSIX for each architecture and creates a draft GitHub release. To build and sign without creating a release, pass `-SkipRelease`.

Before you can release from a fresh clone, you need two things it doesn't include:

- The maintainer's local `sign-msix` PowerShell function, which does the signing.
- The publish profiles in `Properties\PublishProfiles`, which are gitignored.

## Troubleshooting

- **I can't find the tray icon.** Look in the taskbar overflow (^), and drag the icon onto the taskbar to keep it visible. You can also launch Mini Sweeper from Start: if it's already running, that opens its flyout.
- **The Start with Windows switch is disabled.** Settings says why. Either the app is running without package identity (launch it with `dotnet run` or `winapp run`), or startup is controlled in Settings > Apps > Startup or by your organization.
- **Launch fails because an old instance is running.** Exit it from the tray menu, or run `taskkill /IM MiniSweeper.exe /F`.
- **I want to remove the development registration.** Run `winapp unregister` from the repository root.

## License

MIT. See [LICENSE](LICENSE).

Built with [WinUI 3](https://learn.microsoft.com/windows/apps/winui/winui3/), the [Windows App SDK](https://learn.microsoft.com/windows/apps/windows-app-sdk/), [WinUIEx](https://github.com/dotMorten/WinUIEx) and [CsWin32](https://github.com/microsoft/CsWin32). Part of [Tray-Land](https://github.com/Tray-Land), a set of small apps that live in the Windows tray.
