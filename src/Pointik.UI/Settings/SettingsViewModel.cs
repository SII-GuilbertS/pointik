using System.Collections.ObjectModel;
using System.Runtime.CompilerServices;
using Pointik.Core.Graphics;
using Pointik.Core.Input;
using Pointik.Core.Platform;
using Pointik.Core.Settings;
using Pointik.Core.Themes;
using Pointik.UI.Mvvm;

namespace Pointik.UI.Settings;

/// <summary>
/// View-model de la fenêtre de réglages. Chaque modification est appliquée immédiatement
/// (aperçu en direct sur les overlays) puis enregistrée de façon différée.
/// </summary>
public sealed class SettingsViewModel : ObservableObject
{
    private readonly AppController _app;
    private string _status = "";
    private string _newThemeName = "";
    private string? _selectedTheme;

    public SettingsViewModel(AppController app)
    {
        _app = app;
        _app.StateChanged += OnAppStateChanged;
        RefreshScreens();
        RefreshThemes();
        _selectedTheme = Keyboard.ThemeName;
        Hotkeys = new ObservableCollection<HotkeyRowViewModel>(
            Enum.GetValues<HotkeyAction>().Select(a => new HotkeyRowViewModel(this, a)));
        RefreshHotkeyErrors(new Dictionary<HotkeyAction, string>());

        TestCommand = new RelayCommand(() => _app.TestKeyboard());
        ApplyThemeCommand = new RelayCommand(ApplySelectedTheme);
        SaveThemeCommand = new RelayCommand(SaveCurrentAsTheme);
        DeleteThemeCommand = new RelayCommand(DeleteSelectedTheme);
        ResetHotkeysCommand = new RelayCommand(ResetHotkeys);
        DeleteTracesCommand = new RelayCommand(DeleteTraces);
        CancelDeleteTracesCommand = new RelayCommand(() => ConfirmingDelete = false);
    }

    private AppSettings S => _app.Settings;
    private KeyboardSettings Keyboard => S.Keyboard;
    private KeyboardStyle Style => S.Keyboard.Style;
    private KeyboardPosition Position => S.Keyboard.Position;

    /// <summary>Réglages complets (aperçus).</summary>
    public AppSettings Settings => S;

    /// <summary>Message d'état affiché en bas de la fenêtre.</summary>
    public string Status
    {
        get => _status;
        set => SetProperty(ref _status, value);
    }

    /// <summary>Applique une modification de réglage et notifie la vue.</summary>
    private void Edit(Action apply, [CallerMemberName] string? name = null)
    {
        apply();
        OnPropertyChanged(name);
        _app.ApplySettingsChange();
    }

    private void OnAppStateChanged()
    {
        // Un raccourci ou le menu de l'icône a pu changer un interrupteur.
        OnPropertyChanged(nameof(RingEnabled));
        OnPropertyChanged(nameof(SpotEnabled));
        OnPropertyChanged(nameof(ClicksEnabled));
        OnPropertyChanged(nameof(KeyboardEnabled));
        OnPropertyChanged(nameof(ZoomEnabled));
        OnPropertyChanged(nameof(ZoomLevel));
    }

    public void Detach() => _app.StateChanged -= OnAppStateChanged;

    // ================================================================== Anneau et spot

    public bool RingEnabled { get => S.Ring.Enabled; set => Edit(() => _app.RingEnabled = value); }
    public string RingColor { get => S.Ring.Color.ToRgbHex(); set => Edit(() => S.Ring.Color = ParseColor(value, S.Ring.Color)); }
    public double RingSize { get => S.Ring.Size; set => Edit(() => S.Ring.Size = Math.Round(value)); }
    public double RingThickness { get => S.Ring.Thickness; set => Edit(() => S.Ring.Thickness = Math.Round(value, 1)); }
    public double RingOpacity { get => S.Ring.Opacity * 100; set => Edit(() => S.Ring.Opacity = Math.Round(value) / 100); }
    public bool RingPressEffect { get => S.Ring.PressEffect; set => Edit(() => S.Ring.PressEffect = value); }
    public bool RingHideWhenIdle { get => S.Ring.HideWhenIdle; set => Edit(() => S.Ring.HideWhenIdle = value); }
    public double RingIdleSeconds { get => S.Ring.IdleSeconds; set => Edit(() => S.Ring.IdleSeconds = Math.Round(value)); }

    public bool SpotEnabled { get => S.Spot.Enabled; set => Edit(() => _app.SpotEnabled = value); }
    public double SpotRadius { get => S.Spot.Radius; set => Edit(() => S.Spot.Radius = Math.Round(value)); }
    public double SpotDimming { get => S.Spot.Dimming * 100; set => Edit(() => S.Spot.Dimming = Math.Round(value) / 100); }

    // ================================================================== Clics

    public bool ClicksEnabled { get => S.Clicks.Enabled; set => Edit(() => _app.ClicksEnabled = value); }
    public string LeftColor { get => S.Clicks.LeftColor.ToRgbHex(); set => Edit(() => S.Clicks.LeftColor = ParseColor(value, S.Clicks.LeftColor)); }
    public string RightColor { get => S.Clicks.RightColor.ToRgbHex(); set => Edit(() => S.Clicks.RightColor = ParseColor(value, S.Clicks.RightColor)); }
    public string MiddleColor { get => S.Clicks.MiddleColor.ToRgbHex(); set => Edit(() => S.Clicks.MiddleColor = ParseColor(value, S.Clicks.MiddleColor)); }
    public double ClickDuration { get => S.Clicks.DurationMs; set => Edit(() => S.Clicks.DurationMs = Math.Round(value / 10) * 10); }
    public double ClickRadius { get => S.Clicks.MaxRadius; set => Edit(() => S.Clicks.MaxRadius = Math.Round(value)); }
    public bool ClickSound { get => S.Clicks.Sound; set => Edit(() => S.Clicks.Sound = value); }

    // ================================================================== Zoom

    public bool ZoomEnabled { get => S.Zoom.Enabled; set => Edit(() => _app.ZoomEnabled = value); }
    public double ZoomLevel { get => S.Zoom.Level; set => Edit(() => S.Zoom.Level = Math.Round(value * 4) / 4); }
    public int ZoomFollowIndex { get => (int)S.Zoom.Follow; set => Edit(() => S.Zoom.Follow = (ZoomFollowMode)Math.Max(0, value)); }
    public int ZoomActivationIndex { get => (int)S.Zoom.Activation; set => Edit(() => S.Zoom.Activation = (ZoomActivation)Math.Max(0, value)); }
    public double ZoomTransition { get => S.Zoom.TransitionMs; set => Edit(() => S.Zoom.TransitionMs = Math.Round(value / 10) * 10); }

    // ================================================================== Clavier : comportement

    public bool KeyboardEnabled { get => Keyboard.Enabled; set => Edit(() => _app.KeyboardEnabled = value); }
    public int KeyboardModeIndex { get => (int)Keyboard.Mode; set => Edit(() => Keyboard.Mode = (KeyboardDisplayMode)Math.Max(0, value)); }
    public double MaxVisible { get => Keyboard.MaxVisible; set => Edit(() => Keyboard.MaxVisible = (int)Math.Round(value)); }
    public double DisplaySeconds { get => Keyboard.DisplaySeconds; set => Edit(() => Keyboard.DisplaySeconds = Math.Round(value, 1)); }
    public double TextPauseSeconds { get => Keyboard.TextPauseSeconds; set => Edit(() => Keyboard.TextPauseSeconds = Math.Round(value, 1)); }

    // ================================================================== Clavier : position

    public int CornerIndex { get => (int)Position.Corner; set => Edit(() => { Position.Corner = (ScreenCorner)Math.Max(0, value); Position.UseCustomPosition = false; OnPropertyChanged(nameof(UseCustomPosition)); }); }
    public int TargetIndex { get => (int)Position.Target; set => Edit(() => Position.Target = (TargetScreenMode)Math.Max(0, value)); }
    public double OffsetX { get => Position.OffsetX; set => Edit(() => Position.OffsetX = Math.Round(value)); }
    public double OffsetY { get => Position.OffsetY; set => Edit(() => Position.OffsetY = Math.Round(value)); }
    public int AlignmentIndex { get => (int)Position.Alignment; set => Edit(() => Position.Alignment = (BubbleAlignment)Math.Max(0, value)); }
    public int StackIndex { get => (int)Position.Stack; set => Edit(() => Position.Stack = (StackDirection)Math.Max(0, value)); }
    public bool UseCustomPosition { get => Position.UseCustomPosition; set => Edit(() => Position.UseCustomPosition = value); }

    public ObservableCollection<ScreenInfo> Screens { get; } = new();

    public int SelectedScreenIndex
    {
        get
        {
            var i = Screens.ToList().FindIndex(s => s.Id == Position.ScreenId);
            return i;
        }
        set => Edit(() =>
        {
            if (value >= 0 && value < Screens.Count) Position.ScreenId = Screens[value].Id;
        });
    }

    public void RefreshScreens()
    {
        Screens.Clear();
        foreach (var s in _app.Screens) Screens.Add(s);
        OnPropertyChanged(nameof(SelectedScreenIndex));
    }

    /// <summary>Enregistre la position libre choisie en mode placement.</summary>
    public void SetCustomPosition(double fractionX, double fractionY, ScreenInfo screen)
    {
        Edit(() =>
        {
            Position.UseCustomPosition = true;
            Position.CustomX = fractionX;
            Position.CustomY = fractionY;
            // La position libre s'applique à l'écran sur lequel l'aperçu a été déposé.
            if (Position.Target == TargetScreenMode.SpecificScreen || !screen.IsPrimary)
            {
                Position.Target = TargetScreenMode.SpecificScreen;
                Position.ScreenId = screen.Id;
            }
        }, nameof(UseCustomPosition));
        OnPropertyChanged(nameof(TargetIndex));
        OnPropertyChanged(nameof(SelectedScreenIndex));
        Status = $"Position enregistrée : {fractionX:P0} × {fractionY:P0} de l'écran.";
    }

    // ================================================================== Clavier : style

    public IReadOnlyList<string> FontFamilies { get; } = LoadFonts();

    private static IReadOnlyList<string> LoadFonts()
    {
        try
        {
            return Avalonia.Media.FontManager.Current.SystemFonts.Select(f => f.Name).Distinct().OrderBy(n => n).ToList();
        }
        catch
        {
            return ["Segoe UI"];
        }
    }

    public string FontFamily { get => Style.FontFamily; set => Edit(() => Style.FontFamily = value ?? "Segoe UI"); }
    public double FontSize { get => Style.FontSize; set => Edit(() => Style.FontSize = Math.Round(value)); }
    public bool Bold { get => Style.Bold; set => Edit(() => Style.Bold = value); }
    public string TextColor { get => Style.TextColor.ToRgbHex(); set => Edit(() => Style.TextColor = ParseColor(value, Style.TextColor)); }
    public string BackgroundColor { get => Style.BackgroundColor.ToRgbHex(); set => Edit(() => Style.BackgroundColor = ParseColor(value, Style.BackgroundColor)); }
    public double BackgroundOpacity { get => Style.BackgroundOpacity * 100; set => Edit(() => Style.BackgroundOpacity = Math.Round(value) / 100); }
    public double CornerRadius { get => Style.CornerRadius; set => Edit(() => Style.CornerRadius = Math.Round(value)); }
    public double BorderThickness { get => Style.BorderThickness; set => Edit(() => Style.BorderThickness = Math.Round(value)); }
    public string BorderColor { get => Style.BorderColor.ToRgbHex(); set => Edit(() => Style.BorderColor = ParseColor(value, Style.BorderColor)); }
    public bool Shadow { get => Style.Shadow; set => Edit(() => Style.Shadow = value); }
    public double PaddingHorizontal { get => Style.PaddingHorizontal; set => Edit(() => Style.PaddingHorizontal = Math.Round(value)); }
    public double PaddingVertical { get => Style.PaddingVertical; set => Edit(() => Style.PaddingVertical = Math.Round(value)); }
    public double Spacing { get => Style.Spacing; set => Edit(() => Style.Spacing = Math.Round(value)); }
    public double ScalePercent { get => Style.ScalePercent; set => Edit(() => Style.ScalePercent = Math.Round(value / 5) * 5); }
    public int PresentationIndex { get => (int)Style.Presentation; set => Edit(() => Style.Presentation = (KeyPresentation)Math.Max(0, value)); }
    public int EnterAnimationIndex { get => (int)Style.EnterAnimation; set => Edit(() => Style.EnterAnimation = (BubbleAnimation)Math.Max(0, value)); }
    public int ExitAnimationIndex { get => (int)Style.ExitAnimation; set => Edit(() => Style.ExitAnimation = (BubbleAnimation)Math.Max(0, value)); }

    public RelayCommand TestCommand { get; }

    // ================================================================== Thèmes

    public ObservableCollection<string> ThemeNames { get; } = new();

    public string? SelectedTheme
    {
        get => _selectedTheme;
        set
        {
            if (SetProperty(ref _selectedTheme, value))
                OnPropertyChanged(nameof(CanDeleteTheme));
        }
    }

    public bool CanDeleteTheme => SelectedTheme is not null && !BuiltInThemes.IsBuiltIn(SelectedTheme);

    public string NewThemeName
    {
        get => _newThemeName;
        set => SetProperty(ref _newThemeName, value);
    }

    public RelayCommand ApplyThemeCommand { get; }
    public RelayCommand SaveThemeCommand { get; }
    public RelayCommand DeleteThemeCommand { get; }

    private void RefreshThemes()
    {
        ThemeNames.Clear();
        foreach (var t in BuiltInThemes.All) ThemeNames.Add(t.Name);
        foreach (var t in Keyboard.CustomThemes) ThemeNames.Add(t.Name);
    }

    private KeyboardTheme? FindTheme(string? name) =>
        name is null ? null
            : BuiltInThemes.Find(name)
              ?? Keyboard.CustomThemes.FirstOrDefault(t => string.Equals(t.Name, name, StringComparison.OrdinalIgnoreCase));

    private void ApplySelectedTheme()
    {
        var theme = FindTheme(SelectedTheme);
        if (theme is null) return;
        Keyboard.Style = theme.Style.Clone();
        Keyboard.ThemeName = theme.Name;
        _app.ApplySettingsChange();
        // Toutes les propriétés de style ont changé.
        OnPropertyChanged(string.Empty);
        Status = $"Thème « {theme.Name} » appliqué.";
    }

    private void SaveCurrentAsTheme()
    {
        var name = NewThemeName.Trim();
        if (name.Length == 0)
        {
            Status = "Donnez un nom au thème.";
            return;
        }
        if (BuiltInThemes.IsBuiltIn(name))
        {
            Status = "Ce nom est celui d'un thème prédéfini : choisissez-en un autre.";
            return;
        }
        AddOrReplaceCustomTheme(new KeyboardTheme { Name = name, Style = Style.Clone() });
        Status = $"Thème « {name} » enregistré.";
        NewThemeName = "";
    }

    private void AddOrReplaceCustomTheme(KeyboardTheme theme)
    {
        Keyboard.CustomThemes.RemoveAll(t => string.Equals(t.Name, theme.Name, StringComparison.OrdinalIgnoreCase));
        Keyboard.CustomThemes.Add(theme);
        Keyboard.ThemeName = theme.Name;
        _app.ApplySettingsChange();
        RefreshThemes();
        SelectedTheme = theme.Name;
    }

    private void DeleteSelectedTheme()
    {
        if (!CanDeleteTheme) return;
        var name = SelectedTheme!;
        Keyboard.CustomThemes.RemoveAll(t => string.Equals(t.Name, name, StringComparison.OrdinalIgnoreCase));
        _app.ApplySettingsChange();
        RefreshThemes();
        SelectedTheme = BuiltInThemes.DarkName;
        Status = $"Thème « {name} » supprimé.";
    }

    /// <summary>Importe un thème depuis le contenu d'un fichier JSON.</summary>
    public void ImportTheme(string json)
    {
        try
        {
            var theme = ThemeSerializer.Import(json);
            if (BuiltInThemes.IsBuiltIn(theme.Name)) theme.Name += " (importé)";
            AddOrReplaceCustomTheme(theme);
            ApplySelectedTheme();
            Status = $"Thème « {theme.Name} » importé et appliqué.";
        }
        catch (FormatException ex)
        {
            Status = "Import impossible : " + ex.Message;
        }
    }

    /// <summary>Contenu JSON du thème sélectionné (ou du style courant).</summary>
    public (string Name, string Json) ExportTheme()
    {
        var theme = FindTheme(SelectedTheme) ?? new KeyboardTheme { Name = Keyboard.ThemeName, Style = Style };
        return (theme.Name, ThemeSerializer.Export(theme));
    }

    // ================================================================== Raccourcis

    public ObservableCollection<HotkeyRowViewModel> Hotkeys { get; }
    public RelayCommand ResetHotkeysCommand { get; }

    internal string FormatHotkey(Hotkey h) => _app.KeyNames.Format(h);

    internal Hotkey GetHotkey(HotkeyAction a) => S.Hotkeys.Get(a);

    /// <summary>Pendant la capture d'un raccourci, les raccourcis globaux sont suspendus.</summary>
    internal void BeginCapture(HotkeyRowViewModel row)
    {
        foreach (var r in Hotkeys) r.IsCapturing = ReferenceEquals(r, row);
        _app.SuspendHotkeys();
        Status = "Appuyez sur la nouvelle combinaison (Échap pour annuler).";
    }

    /// <summary>Ligne en cours de capture, le cas échéant.</summary>
    public HotkeyRowViewModel? CapturingRow => Hotkeys.FirstOrDefault(r => r.IsCapturing);

    /// <summary>Termine la capture : <paramref name="hotkey"/> null = annulation.</summary>
    public void EndCapture(Hotkey? hotkey)
    {
        var row = CapturingRow;
        if (row is null) return;
        row.IsCapturing = false;
        if (hotkey is { } h)
        {
            var problem = HotkeyConflicts.Validate(h);
            if (problem is not null)
            {
                Status = problem;
            }
            else
            {
                S.Hotkeys.Set(row.Action, h);
                Status = $"{HotkeyConflicts.DisplayName(row.Action)} : {FormatHotkey(h)}";
            }
        }
        else
        {
            Status = "";
        }
        ApplyHotkeys();
    }

    internal void ClearHotkey(HotkeyAction action)
    {
        S.Hotkeys.Set(action, Hotkey.None);
        ApplyHotkeys();
    }

    private void ResetHotkeys()
    {
        S.Hotkeys = new HotkeySettings();
        ApplyHotkeys();
        Status = "Raccourcis par défaut rétablis.";
    }

    /// <summary>Réenregistre les raccourcis et affiche conflits internes et refus de l'OS.</summary>
    private void ApplyHotkeys()
    {
        var errors = new Dictionary<HotkeyAction, string>(HotkeyConflicts.Find(S.Hotkeys));
        foreach (var (action, error) in _app.RegisterHotkeys(notify: false))
            errors.TryAdd(action, error);
        _app.SaveSettings();
        RefreshHotkeyErrors(errors);
    }

    private void RefreshHotkeyErrors(IReadOnlyDictionary<HotkeyAction, string> errors)
    {
        var all = new Dictionary<HotkeyAction, string>(HotkeyConflicts.Find(S.Hotkeys));
        foreach (var (a, e) in errors) all[a] = e;
        foreach (var (a, e) in _app.HotkeyErrors) all.TryAdd(a, e);
        foreach (var row in Hotkeys) row.Refresh(all.GetValueOrDefault(row.Action));
    }

    // ================================================================== Général (mode portable)

    private bool _confirmingDelete;

    public string StorageDescription => _app.IsPortable
        ? "Mode portable : les réglages sont enregistrés à côté de Pointik.exe."
        : "Le dossier de Pointik.exe est en lecture seule : les réglages sont enregistrés dans le dossier utilisateur.";

    public string SettingsFilePath => _app.SettingsFilePath;

    public bool LaunchAtStartup
    {
        get => _app.LaunchAtStartup;
        set
        {
            var error = _app.SetLaunchAtStartup(value);
            Status = error ?? (value ? "Pointik se lancera à l'ouverture de session." : "Pointik ne se lancera plus avec Windows.");
            OnPropertyChanged();
        }
    }

    /// <summary>Deuxième étape de « Supprimer toutes les traces » (confirmation).</summary>
    public bool ConfirmingDelete
    {
        get => _confirmingDelete;
        set => SetProperty(ref _confirmingDelete, value);
    }

    public RelayCommand DeleteTracesCommand { get; }
    public RelayCommand CancelDeleteTracesCommand { get; }

    private void DeleteTraces()
    {
        if (!ConfirmingDelete)
        {
            ConfirmingDelete = true;
            return;
        }
        ConfirmingDelete = false;
        var failures = _app.DeleteAllTracesAndExit();
        if (failures.Count > 0)
            Status = "Certains éléments n'ont pas pu être supprimés : " + string.Join(", ", failures);
    }

    // ================================================================== Divers

    private static ArgbColor ParseColor(string? hex, ArgbColor fallback) =>
        ArgbColor.TryParse(hex, out var c) ? c with { A = 255 } : fallback;
}

/// <summary>Une ligne de l'onglet « Raccourcis ».</summary>
public sealed class HotkeyRowViewModel : ObservableObject
{
    private readonly SettingsViewModel _owner;
    private bool _isCapturing;
    private string _display = "";
    private string? _error;

    public HotkeyRowViewModel(SettingsViewModel owner, HotkeyAction action)
    {
        _owner = owner;
        Action = action;
        Label = HotkeyConflicts.DisplayName(action);
        CaptureCommand = new RelayCommand(() => _owner.BeginCapture(this));
        ClearCommand = new RelayCommand(() => _owner.ClearHotkey(Action));
    }

    public HotkeyAction Action { get; }
    public string Label { get; }
    public RelayCommand CaptureCommand { get; }
    public RelayCommand ClearCommand { get; }

    public bool IsCapturing
    {
        get => _isCapturing;
        set
        {
            if (SetProperty(ref _isCapturing, value)) OnPropertyChanged(nameof(ButtonText));
        }
    }

    public string ButtonText => IsCapturing ? "Appuyez sur les touches…" : _display;

    public string? Error
    {
        get => _error;
        private set
        {
            if (SetProperty(ref _error, value)) OnPropertyChanged(nameof(HasError));
        }
    }

    public bool HasError => !string.IsNullOrEmpty(Error);

    internal void Refresh(string? error)
    {
        _display = _owner.FormatHotkey(_owner.GetHotkey(Action));
        OnPropertyChanged(nameof(ButtonText));
        Error = error;
    }
}
