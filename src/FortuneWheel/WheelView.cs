using System.Globalization;
using System.Windows;
using System.Windows.Media;

namespace FortuneWheel;

public sealed class WheelView : FrameworkElement
{
    private static readonly Color[] Colors = [Color.FromRgb(83, 155, 245), Color.FromRgb(235, 115, 138), Color.FromRgb(74, 190, 158), Color.FromRgb(192, 151, 245), Color.FromRgb(232, 177, 76)];
    public Settings? Settings { get; set; }
    public Sector? Selected { get; set; }

    protected override void OnRender(DrawingContext dc)
    {
        base.OnRender(dc);
        if (Settings is null) return;
        double radius = Math.Min(ActualWidth, ActualHeight) / 2 - 6;
        var center = new Point(ActualWidth / 2, ActualHeight / 2);
        foreach (var s in WheelMath.Sectors(Settings))
        {
            if (s.Sweep <= 0) continue;
            Color color = Colors[s.PlayerIndex];
            double shade = 1 - 0.10 * s.FieldIndex;
            var brush = new SolidColorBrush(Color.FromRgb((byte)(color.R * shade), (byte)(color.G * shade), (byte)(color.B * shade)));
            brush.Freeze();
            dc.DrawGeometry(brush, new Pen(new SolidColorBrush(Color.FromRgb(22, 32, 48)), 2), Pie(center, radius, s.Start, s.Sweep));
            var field = Settings.Players[s.PlayerIndex].Fields[s.FieldIndex];
            if (s.Sweep >= 12)
            {
                var pos = At(center, radius * 0.74, s.Start + s.Sweep / 2);
                double percent = s.Sweep / (360.0 / Settings.Players.Count) * 100;
                DrawLabel(dc, field.Name + $"\n{percent:0.##}%", pos, 14, 95, Brushes.White);
            }
            if (Selected?.PlayerIndex == s.PlayerIndex && Selected.FieldIndex == s.FieldIndex)
                dc.DrawGeometry(null, new Pen(Brushes.White, 5), Pie(center, radius - 3, s.Start, s.Sweep));
        }
        double playerSweep = 360.0 / Settings.Players.Count;
        for (int p = 0; p < Settings.Players.Count; p++)
        {
            var color = Colors[p];
            var brush = new SolidColorBrush(Color.FromRgb((byte)(color.R * 0.48), (byte)(color.G * 0.48), (byte)(color.B * 0.48)));
            dc.DrawGeometry(brush, new Pen(new SolidColorBrush(Color.FromRgb(220, 231, 247)), 1), Pie(center, radius * 0.44, p * playerSweep, playerSweep));
            DrawLabel(dc, Settings.Players[p].Name, At(center, radius * 0.28, (p + 0.5) * playerSweep), 13, 90, Brushes.White);
        }
        dc.DrawEllipse(new SolidColorBrush(Color.FromRgb(17, 27, 41)), new Pen(Brushes.White, 3), center, 15, 15);
        dc.DrawEllipse(null, new Pen(new SolidColorBrush(Color.FromRgb(134, 157, 187)), 3), center, radius, radius);
    }

    private void DrawLabel(DrawingContext dc, string value, Point position, double fontSize, double width, Brush brush)
    {
        var text = new FormattedText(value, CultureInfo.CurrentUICulture, FlowDirection.LeftToRight,
            new Typeface("Segoe UI"), fontSize, brush, VisualTreeHelper.GetDpi(this).PixelsPerDip)
        { MaxTextWidth = width, MaxTextHeight = 45, Trimming = TextTrimming.CharacterEllipsis, TextAlignment = TextAlignment.Center };
        dc.DrawText(text, new Point(position.X - width / 2, position.Y - text.Height / 2));
    }

    private static Point At(Point center, double radius, double angle)
    {
        double radians = angle * Math.PI / 180;
        return new Point(center.X + radius * Math.Sin(radians), center.Y - radius * Math.Cos(radians));
    }

    private static Geometry Pie(Point center, double radius, double start, double sweep)
    {
        if (sweep >= 359.999999) return new EllipseGeometry(center, radius, radius);
        var geometry = new StreamGeometry();
        using (var c = geometry.Open())
        {
            c.BeginFigure(center, true, true);
            c.LineTo(At(center, radius, start), true, false);
            c.ArcTo(At(center, radius, start + sweep), new Size(radius, radius), 0, sweep > 180, SweepDirection.Clockwise, true, false);
        }
        geometry.Freeze();
        return geometry;
    }
}
