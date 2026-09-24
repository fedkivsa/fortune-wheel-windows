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
Reject(serialized.Replace("Field1Share=40", "Field1Share=41"), "Reject invalid percentage total");
Reject(serialized.Replace("Force=5", "Force=10"), "Reject invalid force");
Reject(serialized + "\n[Wheel]\nPlayers=3", "Reject duplicate sections");
Check(Ini.Parse(serialized).Players[0].Force is null, "Blank overrides inherit");
foreach (double invalid in new[] { double.NaN, double.PositiveInfinity, 1e308, -1, 0 })
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
        var winner = WheelMath.Winner(s, -(sector.Start + sector.Sweep / 2));
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

for (int force = 1; force <= 9; force++)
for (int drag = 1; drag <= 9; drag++)
foreach (int direction in new[] { -1, 1 })
{
    var motion = new SpinMotion(37, force, drag, direction);
    Check(motion.Duration > 0 && motion.Duration < 20, "Finite spin duration");
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
var clockwise = new SpinMotion(0, 5, 5, 1);
var counter = new SpinMotion(0, 5, 5, -1);
Check(clockwise.AngleAt(100) == -counter.AngleAt(100), "Symmetric directions");
Check(new SpinMotion(0, 5, 8, 1).AngleAt(100) < clockwise.AngleAt(100), "Higher drag reduces travel");
Check(new SpinMotion(0, 8, 5, 1).AngleAt(100) > clockwise.AngleAt(100), "Higher force increases travel");
Console.WriteLine($"PASS: {checks} checks (physics, sectors, order, INI and validation).");
