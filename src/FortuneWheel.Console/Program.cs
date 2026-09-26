using System.Diagnostics;
using System.Globalization;
using System.Text;
using FortuneWheel;

return await ConsoleWheel.Run(args);

internal static class ConsoleWheel
{
    static string Safe(string value) => new(value.Select(c => char.IsControl(c) ? ' ' : c).ToArray());

    public static async Task<int> Run(string[] args)
    {
        string path = File.Exists("fortune-wheel.ini") ? Path.GetFullPath("fortune-wheel.ini")
            : Path.Combine(AppContext.BaseDirectory, "fortune-wheel.ini");
        bool once = false, animate = !Console.IsOutputRedirected && !Console.IsInputRedirected;
        try
        {
            for (int i = 0; i < args.Length; i++)
                switch (args[i])
                {
                    case "--config" when i + 1 < args.Length: path = Path.GetFullPath(args[++i]); break;
                    case "--once": once = true; break;
                    case "--no-animation": animate = false; break;
                    case "--help":
                        Console.WriteLine("Fortune Wheel: [--config FILE] [--once] [--no-animation]\n" +
                            "--once runs one round and exits. --no-animation resolves spins immediately.\n" +
                            "Interactive menu: spin, edit, load, save, quit. Ctrl+C cancels a round.");
                        return 0;
                    default: throw new ArgumentException($"Unknown or incomplete option: {args[i]}");
                }
            if (Console.IsInputRedirected && !once)
                throw new ArgumentException("Use --once when input is redirected.");
            Settings settings = Ini.Load(path);
            double angle = 0;
            do
            {
                Show(settings, path);
                string command = once ? "s" : Read("[S]pin  [E]dit  [L]oad  [W]rite INI  [Q]uit: ").Trim().ToLowerInvariant();
                try
                {
                    switch (command)
                    {
                        case "s": angle = await Spin(settings, angle, animate); break;
                        case "e": settings = Edit(settings); break;
                        case "l":
                            string source = Path.GetFullPath(Read("INI path: ").Trim());
                            var loaded = Ini.Load(source);
                            settings = loaded; path = source; angle = 0;
                            break;
                        case "w":
                            string destination = Read($"Save path (Enter = {Safe(path)}): ").Trim();
                            string target = destination.Length == 0 ? path : Path.GetFullPath(destination);
                            Ini.Save(target, settings); path = target;
                            Console.WriteLine("Saved."); break;
                        case "q": return 0;
                        default: Console.WriteLine("Choose S, E, L, W or Q."); break;
                    }
                }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or FormatException or ArgumentException)
                {
                    Console.Error.WriteLine(Safe(ex.Message));
                    if (once) return 1;
                }
            } while (!once);
            return 0;
        }
        catch (EndOfStreamException) { return 0; }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or FormatException or ArgumentException)
        {
            Console.Error.WriteLine(Safe(ex.Message)); return 1;
        }
    }

    static void Show(Settings settings, string path)
    {
        Console.WriteLine($"\nFORTUNE WHEEL | {Safe(path)}\nDefaults: force {settings.DefaultForce}, drag {settings.DefaultDrag}; impulse variation: {settings.VaryImpulse}");
        foreach (var sector in WheelMath.Sectors(settings))
        {
            var player = settings.Players[sector.PlayerIndex];
            var field = player.Fields[sector.FieldIndex];
            char symbol = (char)('A' + sector.PlayerIndex * 5 + sector.FieldIndex);
            Console.WriteLine($" {symbol}  {Safe(player.Name)} / {Safe(field.Name)}  ({sector.Sweep / 3.6:F1}% of wheel)");
        }
        for (int i = 0; i < settings.Players.Count; i++)
        {
            var p = settings.Players[i];
            Console.WriteLine($" {i + 1}. {Safe(p.Name)}: force {p.Force ?? settings.DefaultForce}, drag {p.Drag ?? settings.DefaultDrag}");
        }
    }

    static async Task<double> Spin(Settings settings, double angle, bool animate)
    {
        using var cancellation = new CancellationTokenSource();
        ConsoleCancelEventHandler handler = (_, e) => { e.Cancel = true; cancellation.Cancel(); };
        Console.CancelKeyPress += handler;
        var results = new List<string>();
        var order = WheelMath.Order(settings.Players.Count, Random.Shared);
        try
        {
            foreach (int index in order)
            {
                cancellation.Token.ThrowIfCancellationRequested();
                var p = settings.Players[index];
                int force = p.Force ?? settings.DefaultForce, drag = p.Drag ?? settings.DefaultDrag;
                var motion = new SpinMotion(angle, force, drag,
                    settings.VaryImpulse ? 0.9 + Random.Shared.NextDouble() * 0.2 : 1);
                string status = $"Spin: {Safe(p.Name)} | force {force}, drag {drag} | Ctrl+C to cancel";
                Console.WriteLine(status);
                if (animate)
                {
                    var clock = Stopwatch.StartNew();
                    while (clock.Elapsed.TotalSeconds < motion.Duration)
                    {
                        angle = WheelMath.Normalize(motion.AngleAt(clock.Elapsed.TotalSeconds));
                        Draw(settings, angle, status, results);
                        await Task.Delay(50, cancellation.Token);
                    }
                }
                cancellation.Token.ThrowIfCancellationRequested();
                angle = WheelMath.Normalize(motion.AngleAt(motion.Duration));
                var winner = WheelMath.Winner(settings, angle);
                var owner = settings.Players[winner.PlayerIndex];
                string result = $"{Safe(p.Name)} -> {Safe(owner.Name)} / {Safe(owner.Fields[winner.FieldIndex].Name)} ({angle:F2} deg)";
                results.Add(result);
                if (animate) Draw(settings, angle, status, results);
                else Console.WriteLine(result);
                if (animate && results.Count < order.Length) await Task.Delay(2000, cancellation.Token);
            }
        }
        catch (OperationCanceledException) { Console.WriteLine("\nRound cancelled. Completed results are retained below."); }
        finally { Console.CancelKeyPress -= handler; }
        if (animate) { Console.WriteLine("\nRound results:"); foreach (string result in results) Console.WriteLine(result); }
        return angle;
    }

    static void Draw(Settings settings, double angle, string status, List<string> results)
    {
        // A fixed field ring; only the '*' pointer moves. Two columns per row compensate for cell shape.
        const int width = 49, height = 23;
        var grid = new char[height, width];
        for (int y = 0; y < height; y++) for (int x = 0; x < width; x++) grid[y, x] = ' ';
        foreach (var sector in WheelMath.Sectors(settings).Where(s => s.Sweep > 0))
            for (double a = sector.Start; a < sector.Start + sector.Sweep; a += 1)
            {
                double radians = a * Math.PI / 180;
                grid[11 - (int)Math.Round(8 * Math.Cos(radians)), 24 + (int)Math.Round(17 * Math.Sin(radians))]
                    = (char)('A' + sector.PlayerIndex * 5 + sector.FieldIndex);
            }
        double r = angle * Math.PI / 180;
        grid[11 - (int)Math.Round(10 * Math.Cos(r)), 24 + (int)Math.Round(22 * Math.Sin(r))] = '*';
        var frame = new StringBuilder("\u001b[H\u001b[2J");
        frame.AppendLine("FORTUNE WHEEL — * is the moving pointer");
        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++) frame.Append(grid[y, x]);
            frame.AppendLine();
        }
        var selected = WheelMath.Winner(settings, angle);
        var owner = settings.Players[selected.PlayerIndex];
        frame.AppendLine(status).AppendLine($"Pointer: {Safe(owner.Name)} / {Safe(owner.Fields[selected.FieldIndex].Name)} ({angle:F2} deg)");
        foreach (string result in results) frame.AppendLine(result);
        Console.Write(frame);
    }

    static string Read(string prompt)
    {
        Console.Write(prompt);
        return Console.ReadLine() ?? throw new EndOfStreamException();
    }

    static int? Number(string label, int minimum, int maximum, int? current, bool inherit = false)
    {
        while (true)
        {
            string input = Read($"{label} [{current?.ToString() ?? "default"}] (Enter keeps{(inherit ? ", D inherits" : "")}): ").Trim();
            if (input.Length == 0) return current;
            if (inherit && input.Equals("d", StringComparison.OrdinalIgnoreCase)) return null;
            if (int.TryParse(input, out int value) && value >= minimum && value <= maximum) return value;
            Console.WriteLine($"Enter an integer from {minimum} to {maximum}.");
        }
    }

    static Settings Edit(Settings original)
    {
        var edited = new Settings
        {
            DefaultForce = Number("Default force", -10, 10, original.DefaultForce)!.Value,
            DefaultDrag = Number("Default drag", 1, 10, original.DefaultDrag)!.Value,
            VaryImpulse = original.VaryImpulse
        };
        string variation;
        do { variation = Read($"Vary impulse ±10%? [{(original.VaryImpulse ? "Y" : "N")}] Y/N, Enter keeps: ").Trim().ToLowerInvariant(); }
        while (variation is not ("" or "y" or "n"));
        if (variation.Length > 0) edited.VaryImpulse = variation == "y";
        int count = Number("Players", 1, 5, original.Players.Count)!.Value;
        for (int i = 0; i < count; i++)
        {
            var previous = i < original.Players.Count ? original.Players[i] : new Player { Name = $"Player {i + 1}" };
            string name = Read($"Player {i + 1} name [{Safe(previous.Name)}]: ").Trim();
            var player = new Player
            {
                Name = name.Length == 0 ? previous.Name : name,
                Force = Number("Force", -10, 10, previous.Force, true),
                Drag = Number("Drag", 1, 10, previous.Drag, true)
            };
            int fields = Number("Fields", 1, 5, Math.Max(1, previous.Fields.Count))!.Value;
            for (int j = 0; j < fields; j++)
            {
                var old = j < previous.Fields.Count ? previous.Fields[j] : new Field { Name = $"Field {j + 1}" };
                string fieldName = Read($"Field {j + 1} name [{Safe(old.Name)}]: ").Trim();
                double share = old.Share;
                while (true)
                {
                    string input = Read($"Share value [{old.Share.ToString(CultureInfo.InvariantCulture)}] (relative, decimal point): ").Trim();
                    if (input.Length == 0) break;
                    if (double.TryParse(input, NumberStyles.Float, CultureInfo.InvariantCulture, out double entered) && double.IsFinite(entered) && entered >= 0) { share = entered; break; }
                    Console.WriteLine("Enter a finite number at least zero.");
                }
                player.Fields.Add(new Field { Name = fieldName.Length == 0 ? old.Name : fieldName, Share = share });
            }
            edited.Players.Add(player);
        }
        if (edited.Validate() is string error) throw new FormatException(error + " Previous settings retained.");
        Console.WriteLine("Settings applied in memory. Use Write INI to save them.");
        return edited;
    }
}
