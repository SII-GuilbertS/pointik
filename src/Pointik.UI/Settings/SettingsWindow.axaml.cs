using System.ComponentModel;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Avalonia.Platform.Storage;
using Pointik.Core.Input;

namespace Pointik.UI.Settings;

/// <summary>Fenêtre de réglages avec aperçu en direct.</summary>
public partial class SettingsWindow : Window
{
    private readonly SettingsViewModel? _vm;
    private readonly AppController? _app;
    private CursorPreview? _cursor1, _cursor2;
    private KeyboardPreview? _keyboard, _theme;

    /// <summary>Constructeur requis par le chargeur XAML.</summary>
    public SettingsWindow()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public SettingsWindow(SettingsViewModel vm, AppController app) : this()
    {
        _vm = vm;
        _app = app;
        DataContext = vm;
        _cursor1 = this.FindControl<CursorPreview>("CursorPreview1");
        _cursor2 = this.FindControl<CursorPreview>("CursorPreview2");
        _keyboard = this.FindControl<KeyboardPreview>("KeyboardPreview");
        _theme = this.FindControl<KeyboardPreview>("ThemePreview");
        RefreshPreviews();
        vm.PropertyChanged += OnVmChanged;
        // Capture des raccourcis : avant que les contrôles ne consomment la touche.
        AddHandler(KeyDownEvent, OnPreviewKeyDown, RoutingStrategies.Tunnel);
    }

    private void OnVmChanged(object? sender, PropertyChangedEventArgs e) => RefreshPreviews();

    private void RefreshPreviews()
    {
        if (_vm is null) return;
        foreach (var p in new[] { _cursor1, _cursor2 })
        {
            if (p is null) continue;
            p.Settings = _vm.Settings;
            p.Refresh();
        }
        foreach (var p in new[] { _keyboard, _theme })
        {
            if (p is null) continue;
            p.Settings = _vm.Settings.Keyboard;
        }
    }

    private void OnPreviewKeyDown(object? sender, KeyEventArgs e)
    {
        if (_vm?.CapturingRow is null) return;
        e.Handled = true;
        if (e.Key == Key.Escape)
        {
            _vm.EndCapture(null);
            return;
        }
        var key = AvaloniaKeyMap.ToKeyId(e.Key);
        // On attend la touche principale : un modificateur seul ne termine pas la capture.
        if (key == KeyId.None || key.IsModifier()) return;
        _vm.EndCapture(new Hotkey(AvaloniaKeyMap.ToModifiers(e.KeyModifiers), key));
    }

    private void OnClose(object? sender, RoutedEventArgs e) => Close();

    private void OnPlace(object? sender, RoutedEventArgs e)
    {
        if (_vm is null || _app is null) return;
        var placement = new PlacementWindow(_vm, _app);
        placement.Show();
    }

    private async void OnImportTheme(object? sender, RoutedEventArgs e)
    {
        if (_vm is null) return;
        try
        {
            var files = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
            {
                Title = "Importer un thème Pointik",
                AllowMultiple = false,
                FileTypeFilter = [new FilePickerFileType("Thème Pointik (JSON)") { Patterns = ["*.json"] }],
            });
            if (files.Count == 0) return;
            await using var stream = await files[0].OpenReadAsync();
            using var reader = new StreamReader(stream);
            _vm.ImportTheme(await reader.ReadToEndAsync());
        }
        catch (Exception ex)
        {
            _vm.Status = "Import impossible : " + ex.Message;
        }
    }

    private async void OnExportTheme(object? sender, RoutedEventArgs e)
    {
        if (_vm is null) return;
        try
        {
            var (name, json) = _vm.ExportTheme();
            var file = await StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
            {
                Title = "Exporter le thème",
                SuggestedFileName = $"Pointik-theme-{SafeFileName(name)}.json",
                DefaultExtension = "json",
                FileTypeChoices = [new FilePickerFileType("Thème Pointik (JSON)") { Patterns = ["*.json"] }],
            });
            if (file is null) return;
            await using var stream = await file.OpenWriteAsync();
            stream.SetLength(0);
            await using var writer = new StreamWriter(stream);
            await writer.WriteAsync(json);
            _vm.Status = $"Thème « {name} » exporté.";
        }
        catch (Exception ex)
        {
            _vm.Status = "Export impossible : " + ex.Message;
        }
    }

    private static string SafeFileName(string name) =>
        string.Concat(name.Select(c => Path.GetInvalidFileNameChars().Contains(c) || c == ' ' ? '-' : c));

    protected override void OnClosed(EventArgs e)
    {
        if (_vm is not null)
        {
            // Une capture en cours est annulée : les raccourcis sont réenregistrés.
            if (_vm.CapturingRow is not null) _vm.EndCapture(null);
            _vm.PropertyChanged -= OnVmChanged;
            _vm.Detach();
        }
        base.OnClosed(e);
    }
}
