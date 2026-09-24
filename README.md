# Fortune Wheel for Windows — rotating pointer prototype

## Browser version on `prototype/web`

This branch also includes a cross-platform version in [`web/`](web/). It preserves the dark single-screen editor, stationary wheel, pointer physics, relative field weights, shuffled player turns and two-second pause. No backend or third-party packages are needed. With Node.js installed:

```sh
git switch prototype/web
cd web
npm run dev
```

Open `http://127.0.0.1:4173` in a browser. Run `npm test` for the web rule and INI checks. The browser saves valid settings in local storage; **Import .ini** reads a selected file and **Export .ini** downloads a WPF-compatible configuration. It cannot automatically write to an arbitrary disk path. An imported preset also becomes the browser's saved settings. Start the local server instead of opening `index.html` directly so browser module and storage behavior is consistent.

Only the pointer's SVG group changes during the animation; SVG sectors and text are rebuilt when an edit or winning outline changes. The animation uses elapsed time and pauses while its tab is hidden. Pointer angle persists between spins and rounds. If you cancel during a spin, it freezes immediately and keeps completed results.

For deployment the `web/` folder is a static site. Any static HTTP host can serve it; there are no API keys or server data. Hosting is not configured in this prototype.

A minimal C# / WPF prototype for 1–5 players. Dark, resizable interface based on the agreed design C: field editor on the left, wheel on the right, and per-player spin settings below the editor. All game controls are on one screen; the editor scrolls when needed.

This branch, `prototype/rotating-pointer`, keeps the wheel and its labels stationary. A small inward-pointing arrow travels around the rim. Only the arrow has an animated rotation transform.

## Run locally

Requires **Windows 10/11** and the **.NET 10 SDK**. Open the repository folder in VS Code (C# extension recommended), or open `src/FortuneWheel/FortuneWheel.csproj` in a Visual Studio version that supports .NET 10.

```powershell
git clone https://github.com/fedkivsa/fortune-wheel-windows.git
cd fortune-wheel-windows
git switch prototype/rotating-pointer
dotnet run --project src/FortuneWheel
```

The VS Code build task and debugger launch configuration are included. Press F5 with the C# debugger installed. A native Windows runtime is required to launch WPF; use a Windows terminal, rather than WSL.

## Rules and controls

- 1–5 players, each with 1–5 named fields.
- Every player owns `360 / playerCount` degrees, regardless of field count.
- Field values are relative weights, with no required total. Field angle = player angle × weight / sum of that player's weights. For example, 1 and 3 mean 25% and 75%. Zero excludes a field; at least one weight per player must be positive.
- **Start round** shuffles the players and spins exactly once per player. Pointer direction follows the force sign: positive clockwise, negative counterclockwise. Force zero selects the field at the current angle without motion.
- Force and drag default to 5. Force accepts integers from -10 through 10; drag accepts 1 through 10. Player overrides use these same ranges; clear an override to inherit the corresponding default.
- **Vary spin impulse (±10%)** applies a uniformly random factor from 0.9 to 1.1 to the starting speed. Turn it off for exact configured impulses.
- Any spin can select any player's field. Results show both the spinner and the selected field's owner.
- The pointer stops completely, shows the result, and waits **two seconds** before the next spin. The pointer angle carries over between spins and rounds.
- **Cancel round** immediately freezes the pointer, retains completed results, and records no result for the interrupted spin. It also cancels the pause between spins.
- A new round clears the previous round's results. No scores, elimination rules, or persistent history are imposed.
- **+ Field** adds weight 1. Removing a field preserves the other weights. **Equal weights** sets every field to 1.
- Click the **Field editor** header to fold or unfold it. Spin settings remain below it.
- Tiny wedges omit labels; all field names and shares remain available in the editor. The winning field is outlined after stopping.
- Editing is disabled during a round. Invalid numbers and all-zero player weights prevent starting or saving. Weights need not sum to 100.

## Physics and rendering

No winning field is preselected. The final angle is calculated from the pointer motion and mapped directly to the stationary field beneath its tip. Zero degrees is twelve o'clock; positive angles are clockwise.

```
initial speed = 20 × |force|^1.5 × impulse multiplier [degrees/second]
deceleration  = 10 × drag [degrees/second²]
stop time    = initial speed / deceleration
direction    = sign(force)
angle(t)     = start + direction × (initial speed × t − 0.5 × deceleration × t²)
```

Drag always brakes opposite the motion and must be from **1 to 10**. Zero and negative drag are rejected in the UI, INI loader and physics model, so every spin stops naturally. Force zero stays still.

Force 1 and drag 10 move the pointer exactly **2°** with variation off (1.62–2.42° with ±10% variation). Default 5/5 spins travel 500° in about 4.47 seconds.

Time is clamped at the exact stop time. WPF's `CompositionTarget.Rendering` updates only the pointer’s rotation transform using a `Stopwatch`. Wheel geometry and text remain stationary between frames, so physics results do not depend on rendering frame rate. This is a simple constant-friction model, with no pointer bounce or mechanical detents. Higher force magnitude increases travel and duration; higher drag decreases them. Identical impulses can produce repeated patterns; outcomes are not guaranteed to be uniformly distributed.

Sector boundaries use clockwise, half-open intervals: the boundary belongs to the following sector. Text labels remain stationary and readable throughout the spin. Smoothness, DPI appearance, and window behavior still need interactive testing on your Windows machine.

## INI configuration

`fortune-wheel.ini` provides a three-player example. It is copied beside the executable during build/publish and loaded at startup. Blank per-player Force/Drag values inherit defaults. Weights use a decimal **point** in files, regardless of the Windows locale. The UI uses your current culture for numeric editing.

Use **Load .ini** and **Save .ini** for other presets. No automatic writes occur. A malformed file reports an error and retains the current configuration; at startup the app shows example settings instead. Saving uses a temporary sibling file and replaces the destination after a successful write. To make a preset your startup configuration, save it as `fortune-wheel.ini` beside the executable. The last selected path is not remembered between sessions.

Comments start with `;` or `#` on their own lines. Keys and sections are case-insensitive. Each player uses a `[PlayerN]` section containing `Name`, optional `Force` and `Drag`, `Fields`, and `FieldNName` / `FieldNShare` pairs. The existing `FieldNShare` key is retained for compatibility but now stores a relative weight, so old percentage presets still work. Unknown keys are ignored; duplicate keys/sections and missing required values are rejected.

## Checks and portable build

Run the dependency-free core checks (also supported on Linux/macOS):

```powershell
dotnet run --project tests/FortuneWheel.Checks -c Release
dotnet build src/FortuneWheel -c Release
```

Publish a portable Windows x64 folder with the runtime included:

```powershell
dotnet publish src/FortuneWheel -c Release -r win-x64 --self-contained true -o artifacts/windows-x64
```

Run `artifacts/windows-x64/FortuneWheel.exe`. Keep the entire published folder together. The included GitHub Actions workflow builds and checks on Windows, then uploads this folder as `fortune-wheel-windows-x64`.

## First local test

1. Run a three-player round: verify one result per player, random order and force-controlled direction, and the two-second pauses.
2. Verify the pointer moves clockwise for positive force and counterclockwise for negative force, and the selected field is under its tip. Try one player/one field and five players/five fields. Resize the window and inspect the wheel at your normal DPI scaling.
3. Test force from -10 to 10 and drag from 1 to 10, blank overrides, and both states of the impulse checkbox.
4. Try invalid numeric text, all-zero or negative weights, and an invalid INI file. Starting/saving must fail with useful feedback.
5. Save and reload a preset. Check names, weights, overrides, and the checkbox.
6. Cancel during a spin and during a pause; then start another round.

## Project structure

- `src/FortuneWheel/MainWindow.xaml` — dark layout and bindings.
- `MainWindow.xaml.cs` — editor actions, round lifecycle, file dialogs.
- `WheelView.cs` — stationary vector wheel, labels, winner outline.
- `Physics.cs` — spin motion, sector mapping, shuffle.
- `Models.cs` / `Ini.cs` — configuration model, validation, persistence.
- `tests/FortuneWheel.Checks` — executable checks sharing the production core files.

No third-party runtime packages are required. The project is a first prototype, not an installer or signed release.
