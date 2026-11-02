using Avalonia;
using Avalonia.Controls;
using Avalonia.Platform;
using Pointik.UI.Settings;

namespace Pointik.UI;

/// <summary>
/// Icône de la zone de notification et son menu ; ouvre la fenêtre de réglages.
/// </summary>
public sealed class Shell : IDisposable
{
    private readonly AppController _app;
    private readonly TrayIcon _tray;
    private readonly NativeMenuItem _ring, _spot, _clicks, _keyboard, _pause, _zoom, _draw, _clearDrawings;
    private SettingsWindow? _settingsWindow;

    public Shell(Application application, AppController app)
    {
        _app = app;
        _ring = Toggle("Anneau autour du curseur", () => _app.RingEnabled = !_app.RingEnabled);
        _spot = Toggle("Spot (projecteur)", () => _app.SpotEnabled = !_app.SpotEnabled);
        _clicks = Toggle("Animation des clics", () => _app.ClicksEnabled = !_app.ClicksEnabled);
        _keyboard = Toggle("Affichage du clavier", () => _app.KeyboardEnabled = !_app.KeyboardEnabled);
        _pause = Toggle("Pause totale du clavier", () => _app.KeyboardPaused = !_app.KeyboardPaused);
        _zoom = Toggle("Zoom (Ctrl + Alt + Z)", () => _app.ZoomEnabled = !_app.ZoomEnabled);
        _draw = Toggle("Dessiner à l'écran", () => _app.ToggleDrawing());
        _clearDrawings = new NativeMenuItem("Effacer les dessins");
        _clearDrawings.Click += (_, _) => _app.ClearDrawings();

        var settings = new NativeMenuItem("Réglages…");
        settings.Click += (_, _) => OpenSettings();
        var quit = new NativeMenuItem("Quitter Pointik");
        quit.Click += (_, _) => _app.RequestExit();

        var menu = new NativeMenu();
        foreach (var item in new NativeMenuItemBase[]
                 {
                     _ring, _spot, _clicks, _keyboard, _pause, _zoom,
                     new NativeMenuItemSeparator(), _draw, _clearDrawings,
                     new NativeMenuItemSeparator(), settings, new NativeMenuItemSeparator(), quit,
                 })
        {
            menu.Add(item);
        }

        _tray = new TrayIcon
        {
            Icon = new WindowIcon(AssetLoader.Open(new Uri("avares://Pointik.UI/Assets/Pointik.ico"))),
            Menu = menu,
            IsVisible = true,
        };
        _tray.Clicked += (_, _) => OpenSettings();
        TrayIcon.SetIcons(application, new TrayIcons { _tray });

        _app.StateChanged += Refresh;
        Refresh();
    }

    private static NativeMenuItem Toggle(string header, Action onClick)
    {
        var item = new NativeMenuItem(header) { ToggleType = NativeMenuItemToggleType.CheckBox };
        item.Click += (_, _) => onClick();
        return item;
    }

    /// <summary>Met à jour les coches du menu et l'infobulle.</summary>
    private void Refresh()
    {
        _ring.IsChecked = _app.RingEnabled;
        _spot.IsChecked = _app.SpotEnabled;
        _clicks.IsChecked = _app.ClicksEnabled;
        _keyboard.IsChecked = _app.KeyboardEnabled;
        _pause.IsChecked = _app.KeyboardPaused;
        _zoom.IsChecked = _app.ZoomEnabled;
        _draw.IsChecked = _app.IsDrawing;
        // Pendant le dessin, la planche change sans notification : l'entrée reste alors disponible.
        _clearDrawings.IsEnabled = _app.IsDrawing || _app.HasDrawings;
        _draw.Header = $"Dessiner à l'écran ({_app.KeyNames.Format(_app.Settings.Hotkeys.Drawing)})";
        _tray.ToolTipText = _app.KeyboardPaused
            ? "Pointik — clavier en pause"
            : _app.CursorHighlightOn ? "Pointik" : "Pointik — anneau et spot masqués";
    }

    /// <summary>Ouvre (ou ramène au premier plan) la fenêtre de réglages.</summary>
    public void OpenSettings()
    {
        if (_settingsWindow is { } existing)
        {
            if (existing.WindowState == WindowState.Minimized) existing.WindowState = WindowState.Normal;
            existing.Activate();
            return;
        }
        var window = new SettingsWindow(new SettingsViewModel(_app), _app);
        window.Closed += (_, _) => _settingsWindow = null;
        _settingsWindow = window;
        window.Show();
        window.Activate();
    }

    public void Dispose()
    {
        _app.StateChanged -= Refresh;
        _settingsWindow?.Close();
        _tray.IsVisible = false;
        _tray.Dispose();
    }
}
