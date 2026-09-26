# Console prototype

Run on Windows, macOS or Linux with the .NET 10 SDK, from the repository root:

```sh
dotnet run --project src/FortuneWheel.Console -c Release
```

The console project directly compiles the existing WPF project's platform-independent models, physics and INI implementation. It has no WPF dependency or third-party runtime packages.

## Controls

- **S** spins once per player in shuffled order. Each spin can land on any player's field.
- **E** edits players, fields, relative share values, default force/drag and player overrides. Enter retains a value; D clears an override to inherit the default. Changes stay in memory until saved. Invalid settings leave the previous configuration intact.
- **L** loads an INI file and resets the pointer to twelve o'clock.
- **W** writes settings to the current or supplied INI path (overwrites that file).
- **Q** quits. Ctrl+C cancels a running round, preserving completed results and the current pointer position.

Positive force is clockwise, negative force is counterclockwise; force accepts -10 through 10, drag accepts 1 through 10. Force zero does not move the pointer. Optional impulse variation is ±10%. Animated rounds pause two seconds between spins. Pointer position persists between rounds.

The letter ring stays fixed and `*` moves around it. The current field is also printed by name. Animation uses elapsed time at approximately 20 frames per second, so terminal rendering speed cannot change the outcome. A text grid has coarse angular resolution; narrow fields may not occupy a visible character, but still participate at their exact angular size in the physics.

Use an ANSI-compatible terminal (for example Windows Terminal), ideally at least 80 columns by 35 rows. The display clears on each animated frame. Small terminals can wrap or scroll; use `--no-animation` if needed. Redirected input/output automatically disables animation and pauses.

## Command line

```sh
dotnet run --project src/FortuneWheel.Console -- --config fortune-wheel.ini
dotnet run --project src/FortuneWheel.Console -- --once --no-animation
dotnet run --project src/FortuneWheel.Console -- --help
```

`--once` runs a single round and exits; required when standard input is redirected. `--no-animation` computes final physics positions immediately, without delays. Without `--config`, the app uses `fortune-wheel.ini` in the working directory, or the bundled sample beside the executable. Invalid files/options produce an error and exit code 1.

Publish a portable, framework-dependent build:

```sh
dotnet publish src/FortuneWheel.Console -c Release -o out/console
dotnet out/console/FortuneWheel.Console.dll
```
