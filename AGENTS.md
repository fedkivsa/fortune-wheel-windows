# Development handoff

This repository is a Windows C# / WPF fortune wheel prototype. Keep the app small and usable on one screen.

## Agreed behavior

- Dark design C: editable player fields on the left, wheel on the right, per-player force/drag table below the editor. No navigation to game settings.
- 1–5 players, 1–5 fields each, equal total sectors per player, nonnegative relative weights normalized within each player's sector; at least one positive weight per player.
- Spins can land on any player's field. Every player spins exactly once in a shuffled order, direction determined by signed force.
- Force -10–10, drag 1–10, defaults 5/5, blank player overrides inherit defaults.
- The checkbox controls ±10% impulse variation. Physics determines the outcome; do not preselect a winner.
- Drag always brakes and must be at least 1, including overrides and INI values. Force zero means no motion.
- Full stop, then two seconds before the next spin. Keep angle between spins.
- Force 1 / drag 10 travels 2 degrees without impulse variation. The field editor must be foldable.
- Configuration is saved explicitly to INI files.

## Development

- Target .NET 10. WPF runs only on Windows. No third-party runtime dependencies.
- `dotnet build src/FortuneWheel/FortuneWheel.csproj -c Release`
- `dotnet run --project tests/FortuneWheel.Checks -c Release`
- `dotnet run --project src/FortuneWheel` (Windows)
- The core checks compile the production model, physics and INI files directly; keep them platform-independent.
- Animate one rotation transform with elapsed time. Do not rebuild wheel geometry every frame or integrate physics using an assumed frame rate.
- WPF bindings can retain the previous numeric model value after invalid text input; check binding validation errors before starting/saving.
- Test WPF layout, input, cancellation and file dialogs interactively on Windows. Do not claim visual verification from a cross-platform build alone.

See README.md for configuration format, publishing and the initial local test checklist.
