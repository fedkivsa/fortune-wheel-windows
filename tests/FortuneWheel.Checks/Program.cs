using FortuneWheel;
using System.Globalization;

int checks = 0;
void Check(bool ok, string name)
{
    checks++;
    if (!ok) throw new Exception("FAIL: " + name);
}
void Reject(string text, string name)
{
    bool rejected = false;
    try { Ini.Parse(text); } catch (FormatException) { rejected = true; }
    Check(rejected, name);
}
var settings = Settings.Example();
Check(settings.Validate() is null, "Valid example");
var serialized = Ini.Serialize(settings);
Check(Ini.Serialize(Ini.Parse(serialized)) == serialized, "INI round trip");
CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("de-DE");
Check(Ini.Serialize(Ini.Parse(serialized)) == serialized, "INI is culture invariant");
Reject(serialized.Replace("Field1Share=40", "Field1Share=NaN"), "Reject NaN");
Reject(serialized.Replace("Players=3", "Players=6"), "Reject too many players");
Check(Ini.Parse(serialized.Replace("Field1Share=40", "Field1Share=41")).Validate() is null, "Arbitrary positive totals accepted");
Reject(serialized.Replace("Force=5", "Force=11"), "Reject invalid force");
Reject(serialized + "\n[Wheel]\nPlayers=3", "Reject duplicate sections");
Check(Ini.Parse(serialized).Players[0].Force is null, "Blank overrides inherit");
foreach (double invalid in new[] { double.NaN, double.PositiveInfinity, -1 })
{
    var preview = Settings.Example();
    preview.Players[0].Fields[0].Share = invalid;
    Check(preview.Validate() is not null, "Invalid share prevents spinning");
    Check(WheelMath.Sectors(preview).All(s => double.IsFinite(s.Start) && double.IsFinite(s.Sweep)), "Invalid editing values keep preview finite");
}

for (int count = 1; count <= 5; count++)
{
    var s = new Settings();
    for (int p = 0; p < count; p++)
    {
        var player = new Player { Name = $"P{p}" };
        player.Fields.Add(new Field { Name = "A", Share = 25 });
        player.Fields.Add(new Field { Name = "B", Share = 75 });
        s.Players.Add(player);
    }
    var sectors = WheelMath.Sectors(s);
    Check(Math.Abs(sectors.Sum(x => x.Sweep) - 360) < 1e-9, "Full wheel coverage");
    foreach (var sector in sectors)
    {
        var winner = WheelMath.Winner(s, sector.Start + sector.Sweep / 2);
        Check(winner == sector, "Pointer matches sector center");
    }
    Check(WheelMath.Winner(s, 0).PlayerIndex == 0, "Zero angle boundary");
    Check(WheelMath.Winner(s, 360).PlayerIndex == 0, "Full rotation boundary");
    var order = WheelMath.Order(count, new Random(20));
    Check(order.Order().SequenceEqual(Enumerable.Range(0, count)), "Every player exactly once");
}
var one = new Settings();
var solo = new Player(); solo.Fields.Add(new Field()); one.Players.Add(solo);
Check(WheelMath.Sectors(one).Single().Sweep == 360, "One player one field");
Check(WheelMath.Winner(one, -99999).FieldIndex == 0, "Single field wins all angles");

for (int force = 1; force <= 10; force++)
for (int drag = 1; drag <= 10; drag++)
foreach (int direction in new[] { -1, 1 })
{
    var motion = new SpinMotion(37, force * direction, drag);
    Check(motion.Duration > 0 && motion.Duration < 70, "Finite spin duration");
    Check(motion.AngleAt(0) == 37, "Continuous start");
    Check(motion.AngleAt(motion.Duration) == motion.AngleAt(motion.Duration + 50), "Rest after stop");
    double previous = 37;
    for (int frame = 1; frame <= 60; frame++)
    {
        double angle = motion.AngleAt(motion.Duration * frame / 60);
        Check((angle - previous) * direction >= -1e-9, "No direction reversal");
        previous = angle;
    }
    Check(Math.Abs(previous - motion.AngleAt(motion.Duration)) < 1e-9, "Sampled final position");
}
var pointerSettings = new Settings();
var pointerPlayer = new Player();
foreach (string name in new[] { "NE", "SE", "SW", "NW" }) pointerPlayer.Fields.Add(new Field { Name = name, Share = 1 });
pointerSettings.Players.Add(pointerPlayer);
foreach (var (angle, field) in new[] { (45.0, 0), (135.0, 1), (225.0, 2), (315.0, 3), (-45.0, 3), (405.0, 0), (90.0, 1) })
    Check(WheelMath.Winner(pointerSettings, angle).FieldIndex == field, "Pointer position matches stationary field, including wrap and boundary");
var clockwise = new SpinMotion(0, 5, 5);
var counter = new SpinMotion(0, -5, 5);
Check(clockwise.AngleAt(100) == -counter.AngleAt(100), "Symmetric directions");
Check(new SpinMotion(0, 5, 8).AngleAt(100) < clockwise.AngleAt(100), "Higher drag reduces travel");
Check(new SpinMotion(0, 8, 5).AngleAt(100) > clockwise.AngleAt(100), "Higher force increases travel");
Check(Math.Abs(new SpinMotion(0, 1, 10).AngleAt(10) - 2) < 1e-9, "Force 1 drag 10 travels two degrees");
foreach (double scale in new[] { 0.9, 1.1 })
{
    double travel = new SpinMotion(0, 1, 10, scale).AngleAt(10);
    Check(travel >= 1 && travel <= 3, "Varied tiny spin remains within 1–3 degrees");
}
for (int drag = 1; drag <= 10; drag++)
{
    var idle = new SpinMotion(37, 0, drag);
    Check(idle.Duration == 0 && idle.AngleAt(10) == 37, "Zero force stays still for all drag settings");
    var config = Settings.Example(); config.DefaultForce = -10; config.DefaultDrag = drag;
    Check(Ini.Parse(Ini.Serialize(config)).DefaultDrag == drag, "Signed settings round trip");
}
foreach (int drag in new[] { -10, -1, 0, 11 })
{
    Reject(serialized.Replace("Drag=5", $"Drag={drag}"), "Reject drag outside 1–10 in INI");
    bool rejected = false;
    try { _ = new SpinMotion(0, 1, drag); } catch (ArgumentOutOfRangeException) { rejected = true; }
    Check(rejected, "Physics rejects non-braking drag");
    var config = Settings.Example(); config.Players[0].Drag = drag;
    Check(config.Validate() is not null, "Reject invalid per-player drag override");
}
foreach (double factor in new[] { 1.0, 1000.0, 1e307 })
{
    var weights = new Settings();
    var player = new Player();
    player.Fields.Add(new Field { Share = factor });
    player.Fields.Add(new Field { Share = 3 * factor });
    weights.Players.Add(player);
    Check(weights.Validate() is null, "Large weights accepted");
    Check(Math.Abs(WheelMath.Sectors(weights)[0].Sweep - 90) < 1e-9, "Weights 1:3 normalize to 25:75 at all scales");
    Check(Ini.Serialize(Ini.Parse(Ini.Serialize(weights))) == Ini.Serialize(weights), "Weights preserved in INI");
    player.Fields[0].Share = 0;
    Check(weights.Validate() is null && WheelMath.Winner(weights, 0).FieldIndex == 1, "Zero weight has no winning sector");
    player.Fields[1].Share = 0;
    Check(weights.Validate() is not null, "Reject all-zero weights");
}
Console.WriteLine($"PASS: {checks} checks (physics, sectors, order, INI and validation).");
