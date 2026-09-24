using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace FortuneWheel;

public abstract class Observable : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;
    protected void Set<T>(ref T storage, T value, [CallerMemberName] string? name = null)
    {
        if (EqualityComparer<T>.Default.Equals(storage, value)) return;
        storage = value;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}

public sealed class Field : Observable
{
    private string name = "New field";
    private double share = 100;
    public string Name { get => name; set => Set(ref name, value); }
    public double Share { get => share; set => Set(ref share, value); }
}

public sealed class Player : Observable
{
    private string name = "Player";
    private int? force;
    private int? drag;
    public string Name { get => name; set => Set(ref name, value); }
    public int? Force { get => force; set => Set(ref force, value); }
    public int? Drag { get => drag; set => Set(ref drag, value); }
    public ObservableCollection<Field> Fields { get; } = new();
}

public sealed class Settings : Observable
{
    private int force = 5;
    private int drag = 5;
    private bool varyImpulse = true;
    public int DefaultForce { get => force; set => Set(ref force, value); }
    public int DefaultDrag { get => drag; set => Set(ref drag, value); }
    public bool VaryImpulse { get => varyImpulse; set => Set(ref varyImpulse, value); }
    public ObservableCollection<Player> Players { get; } = new();

    public string? Validate()
    {
        if (Players.Count is < 1 or > 5) return "Choose between 1 and 5 players.";
        if (DefaultForce is < 1 or > 9 || DefaultDrag is < 1 or > 9)
            return "Default force and drag must be from 1 to 9.";
        foreach (var player in Players)
        {
            if (string.IsNullOrWhiteSpace(player.Name)) return "Every player needs a name.";
            if (player.Force is < 1 or > 9 || player.Drag is < 1 or > 9)
                return $"{player.Name}: force and drag must be 1–9, or blank for default.";
            if (player.Fields.Count is < 1 or > 5) return $"{player.Name}: choose 1–5 fields.";
            if (player.Fields.Any(f => string.IsNullOrWhiteSpace(f.Name))) return $"{player.Name}: every field needs a name.";
            if (player.Fields.Any(f => !double.IsFinite(f.Share) || f.Share <= 0 || f.Share > 100))
                return $"{player.Name}: each share must be greater than 0 and at most 100%.";
            if (Math.Abs(player.Fields.Sum(f => f.Share) - 100) > 0.000001)
                return $"{player.Name}: field shares must total 100% (currently {player.Fields.Sum(f => f.Share):0.##}%).";
        }
        return null;
    }

    public static Settings Example()
    {
        var result = new Settings();
        string[][] names = [["Start", "Bonus", "Trap"], ["Coin", "Challenge", "Lose a turn"], ["Move", "Draw", "Swap"]];
        double[][] shares = [[40, 30, 30], [50, 30, 20], [40, 40, 20]];
        for (int i = 0; i < 3; i++)
        {
            var player = new Player { Name = $"Player {i + 1}" };
            for (int j = 0; j < 3; j++) player.Fields.Add(new Field { Name = names[i][j], Share = shares[i][j] });
            result.Players.Add(player);
        }
        return result;
    }
}
