namespace FortuneWheel;

// Angles are clockwise from twelve o'clock. Speed is degrees/second.
public sealed class SpinMotion
{
    private readonly double start;
    private readonly double speed;
    private readonly double deceleration;
    private readonly int direction;
    public double Duration { get; }

    public SpinMotion(double startAngle, int force, int drag, int direction, double impulseScale = 1)
    {
        if (force is < 1 or > 9 || drag is < 1 or > 9 || Math.Abs(direction) != 1 ||
            !double.IsFinite(startAngle) || !double.IsFinite(impulseScale) || impulseScale <= 0)
            throw new ArgumentOutOfRangeException(nameof(force), "Invalid spin parameters.");
        start = startAngle;
        this.direction = direction;
        speed = (420 + force * 100) * impulseScale;
        deceleration = 65 + drag * 24;
        Duration = speed / deceleration;
    }

    // Closed-form integration: rendering cadence never changes the final angle.
    public double AngleAt(double seconds)
    {
        double t = Math.Clamp(seconds, 0, Duration);
        return start + direction * (speed * t - 0.5 * deceleration * t * t);
    }
}

public sealed record Sector(int PlayerIndex, int FieldIndex, double Start, double Sweep);

public static class WheelMath
{
    public static double Normalize(double angle) => (angle % 360 + 360) % 360;

    public static IReadOnlyList<Sector> Sectors(Settings settings)
    {
        var result = new List<Sector>();
        double size = 360.0 / settings.Players.Count;
        for (int p = 0; p < settings.Players.Count; p++)
        {
            var fields = settings.Players[p].Fields;
            // Normalize only the preview while the user is editing an incomplete total.
            // Starting a round requires strictly validated settings.
            double total = fields.Sum(f => double.IsFinite(f.Share) && f.Share is > 0 and <= 100 ? f.Share : 0);
            double start = p * size;
            for (int f = 0; f < fields.Count; f++)
            {
                double weight = double.IsFinite(fields[f].Share) && fields[f].Share is > 0 and <= 100 ? fields[f].Share : 0;
                double sweep = total > 0 ? size * weight / total : size / fields.Count;
                result.Add(new Sector(p, f, start, sweep));
                start += sweep;
            }
        }
        return result;
    }

    public static Sector Winner(Settings settings, double wheelAngle)
    {
        double pointerAngle = Normalize(-wheelAngle);
        // Half-open intervals give a consistent boundary rule.
        return Sectors(settings).First(s => pointerAngle >= s.Start && pointerAngle < s.Start + s.Sweep);
    }

    public static int[] Order(int count, Random random)
    {
        var order = Enumerable.Range(0, count).ToArray();
        for (int i = order.Length - 1; i > 0; i--)
        {
            int j = random.Next(i + 1);
            (order[i], order[j]) = (order[j], order[i]);
        }
        return order;
    }
}
