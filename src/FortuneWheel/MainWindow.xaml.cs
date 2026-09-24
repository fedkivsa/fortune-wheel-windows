using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Markup;
using System.Windows.Media;
using Microsoft.Win32;

namespace FortuneWheel;

public partial class MainWindow : Window
{
    private Settings settings = Settings.Example();
    private readonly ObservableCollection<string> results = new();
    private readonly List<INotifyPropertyChanged> observed = new();
    private readonly Random random = new();
    private CancellationTokenSource? round;
    private string configPath = System.IO.Path.Combine(AppContext.BaseDirectory, "fortune-wheel.ini");
    private bool dirty;

    public MainWindow()
    {
        InitializeComponent();
        Language = XmlLanguage.GetLanguage(CultureInfo.CurrentCulture.IetfLanguageTag);
        ResultsList.ItemsSource = results;
        string? loadError = null;
        if (File.Exists(configPath))
        {
            try { settings = Ini.Load(configPath); }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException or FormatException)
            { loadError = $"Could not load configuration: {e.Message} Example settings are shown; the file was not overwritten."; }
        }
        BindSettings();
        if (loadError is not null) Feedback.Text = loadError;
    }

    private void BindSettings()
    {
        foreach (var item in observed) item.PropertyChanged -= SettingChanged;
        observed.Clear();
        observed.Add(settings);
        foreach (var p in settings.Players)
        {
            observed.Add(p);
            observed.AddRange(p.Fields);
        }
        foreach (var item in observed) item.PropertyChanged += SettingChanged;
        DataContext = settings;
        Wheel.Settings = settings;
        Wheel.Selected = null;
        Wheel.InvalidateVisual();
        RefreshValidation();
    }

    private void SettingChanged(object? sender, PropertyChangedEventArgs e)
    {
        dirty = true;
        Wheel.Selected = null;
        Wheel.InvalidateVisual();
        RefreshValidation();
    }

    private void RefreshValidation() => Feedback.Text = settings.Validate() ?? "Ready · Changes are saved only when you choose Save .ini.";

    private static bool HasInvalidInput(DependencyObject obj)
    {
        if (Validation.GetHasError(obj)) return true;
        for (int i = 0; i < VisualTreeHelper.GetChildrenCount(obj); i++)
            if (HasInvalidInput(VisualTreeHelper.GetChild(obj, i))) return true;
        return false;
    }

    private bool CanUseSettings()
    {
        Keyboard.ClearFocus();
        string? error = HasInvalidInput(Editor) ? "Correct the highlighted numeric input before continuing." : settings.Validate();
        if (error is null) return true;
        Feedback.Text = error;
        return false;
    }

    private void ChangedStructure()
    {
        dirty = true;
        BindSettings();
    }

    private void AddPlayer(object sender, RoutedEventArgs e)
    {
        if (settings.Players.Count >= 5) { Feedback.Text = "The maximum is 5 players."; return; }
        var p = new Player { Name = $"Player {settings.Players.Count + 1}" };
        p.Fields.Add(new Field { Name = "New field", Share = 100 });
        settings.Players.Add(p);
        ChangedStructure();
    }

    private void RemovePlayer(object sender, RoutedEventArgs e)
    {
        if (settings.Players.Count <= 1) { Feedback.Text = "Keep at least one player."; return; }
        settings.Players.Remove((Player)((Button)sender).Tag);
        ChangedStructure();
    }

    private void AddField(object sender, RoutedEventArgs e)
    {
        var p = (Player)((Button)sender).Tag;
        if (p.Fields.Count >= 5) { Feedback.Text = "Each player can have up to 5 fields."; return; }
        p.Fields.Add(new Field { Share = 1 });
        ChangedStructure();
    }

    private void RemoveField(object sender, RoutedEventArgs e)
    {
        var field = (Field)((Button)sender).Tag;
        var p = settings.Players.First(p => p.Fields.Contains(field));
        if (p.Fields.Count <= 1) { Feedback.Text = "Keep at least one field per player."; return; }
        p.Fields.Remove(field);
        ChangedStructure();
    }

    private void EqualShares(object sender, RoutedEventArgs e)
    {
        var p = (Player)((Button)sender).Tag;
        foreach (var field in p.Fields) field.Share = 1;
    }

    private async void StartRound(object sender, RoutedEventArgs e)
    {
        if (round is not null || !CanUseSettings()) return;
        using var cts = new CancellationTokenSource();
        round = cts;
        Editor.IsEnabled = StartButton.IsEnabled = false;
        StopButton.IsEnabled = true;
        results.Clear();
        int[] order = WheelMath.Order(settings.Players.Count, random);
        OrderText.Text = "Order: " + string.Join(" → ", order.Select(i => settings.Players[i].Name));
        try
        {
            for (int i = 0; i < order.Length; i++)
            {
                cts.Token.ThrowIfCancellationRequested();
                var p = settings.Players[order[i]];
                int force = p.Force ?? settings.DefaultForce;
                int drag = p.Drag ?? settings.DefaultDrag;
                double scale = settings.VaryImpulse ? 0.9 + random.NextDouble() * 0.2 : 1;
                var motion = new SpinMotion(Rotation.Angle, force, drag, scale);
                RoundStatus.Text = $"Spin {i + 1}/{order.Length} · {p.Name} · {(force > 0 ? "↻" : force < 0 ? "↺" : "No impulse")}";
                Feedback.Text = $"Force {force} · Drag {drag} · Impulse {scale:0.00}×";
                Wheel.Selected = null;
                Wheel.InvalidateVisual();
                await Animate(motion, cts.Token);
                cts.Token.ThrowIfCancellationRequested();
                var selected = WheelMath.Winner(settings, Rotation.Angle);
                Wheel.Selected = selected;
                Wheel.InvalidateVisual();
                var owner = settings.Players[selected.PlayerIndex];
                string result = $"{i + 1}. {p.Name} spun → {owner.Name}: {owner.Fields[selected.FieldIndex].Name}";
                results.Add(result);
                RoundStatus.Text = $"{owner.Name}: {owner.Fields[selected.FieldIndex].Name}";
                if (i < order.Length - 1)
                {
                    Feedback.Text = "Next spin in 2 seconds…";
                    await Task.Delay(TimeSpan.FromSeconds(2), cts.Token);
                }
            }
            Feedback.Text = "Round complete. Start another round whenever you're ready.";
        }
        catch (OperationCanceledException)
        {
            RoundStatus.Text = "Round cancelled";
            Feedback.Text = "Completed results are kept. The interrupted spin has no result.";
        }
        finally
        {
            round = null;
            Editor.IsEnabled = StartButton.IsEnabled = true;
            StopButton.IsEnabled = false;
        }
    }

    private async Task Animate(SpinMotion motion, CancellationToken token)
    {
        var completion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var clock = Stopwatch.StartNew();
        void Render(object? sender, EventArgs e)
        {
            double seconds = clock.Elapsed.TotalSeconds;
            Rotation.Angle = WheelMath.Normalize(motion.AngleAt(seconds));
            if (seconds >= motion.Duration) completion.TrySetResult();
        }
        CompositionTarget.Rendering += Render;
        using var registration = token.Register(() => completion.TrySetCanceled(token));
        try { await completion.Task; }
        finally { CompositionTarget.Rendering -= Render; }
    }

    private void CancelRound(object sender, RoutedEventArgs e) => round?.Cancel();

    private void LoadIni(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog { Filter = "INI configuration (*.ini)|*.ini", FileName = configPath };
        if (dialog.ShowDialog(this) != true) return;
        if ((dirty || HasInvalidInput(Editor)) && MessageBox.Show(this, "Replace your unsaved edits with this configuration?", "Load configuration", MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes) return;
        try
        {
            var loaded = Ini.Load(dialog.FileName);
            settings = loaded;
            configPath = dialog.FileName;
            BindSettings();
            dirty = false;
            Rotation.Angle = 0;
            results.Clear();
            RoundStatus.Text = "Ready to spin";
            OrderText.Text = "Each player spins once. Two seconds between spins.";
            Feedback.Text = $"Loaded {System.IO.Path.GetFileName(configPath)}";
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or FormatException)
        { Feedback.Text = $"Load failed: {ex.Message} Current settings are kept."; }
    }

    private void SaveIni(object sender, RoutedEventArgs e)
    {
        if (!CanUseSettings()) return;
        var dialog = new SaveFileDialog { Filter = "INI configuration (*.ini)|*.ini", FileName = System.IO.Path.GetFileName(configPath), DefaultExt = ".ini" };
        if (dialog.ShowDialog(this) != true) return;
        try
        {
            Ini.Save(dialog.FileName, settings);
            configPath = dialog.FileName;
            dirty = false;
            Feedback.Text = $"Saved {configPath}";
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or FormatException)
        { Feedback.Text = $"Save failed: {ex.Message}"; }
    }

    private void Window_Closing(object? sender, CancelEventArgs e)
    {
        if ((dirty || HasInvalidInput(Editor)) && MessageBox.Show(this, "Close without saving your configuration edits?", "Unsaved configuration", MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes)
        { e.Cancel = true; return; }
        round?.Cancel();
        foreach (var item in observed) item.PropertyChanged -= SettingChanged;
    }
}
