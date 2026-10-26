using Pointik.Core.Drawing;
using Pointik.Core.Graphics;
using Pointik.Core.Input;

namespace Pointik.Core.Settings;

/// <summary>
/// Ensemble des réglages de l'application, sérialisés dans « Pointik.settings.json ».
/// Toutes les valeurs ont un défaut sûr : un fichier absent ou partiel reste utilisable.
/// </summary>
public sealed class AppSettings
{
    public const int CurrentVersion = 1;

    public int Version { get; set; } = CurrentVersion;
    public RingSettings Ring { get; set; } = new();
    public SpotSettings Spot { get; set; } = new();
    public ClickSettings Clicks { get; set; } = new();
    public ZoomSettings Zoom { get; set; } = new();
    public KeyboardSettings Keyboard { get; set; } = new();
    public HotkeySettings Hotkeys { get; set; } = new();
    public GeneralSettings General { get; set; } = new();
    public DrawingSettings Drawing { get; set; } = new();

    /// <summary>Ramène chaque valeur dans sa plage autorisée (fichier modifié à la main, version ancienne…).</summary>
    public AppSettings Normalize()
    {
        Ring ??= new();
        Spot ??= new();
        Clicks ??= new();
        Zoom ??= new();
        Keyboard ??= new();
        Hotkeys ??= new();
        General ??= new();
        Drawing ??= new();
        Drawing.Normalize();
        Ring.Normalize();
        Spot.Normalize();
        Clicks.Normalize();
        Zoom.Normalize();
        Keyboard.Normalize();
        Version = CurrentVersion;
        return this;
    }
}

/// <summary>Anneau autour du curseur.</summary>
public sealed class RingSettings
{
    public const double MinSize = 20, MaxSize = 120;

    public bool Enabled { get; set; } = true;
    public ArgbColor Color { get; set; } = ArgbColor.FromRgb(0xFF, 0xC4, 0x1F);
    /// <summary>Diamètre en pixels logiques (20 à 120).</summary>
    public double Size { get; set; } = 54;
    /// <summary>Épaisseur du trait (1 à 16 px).</summary>
    public double Thickness { get; set; } = 4;
    /// <summary>Opacité (0,1 à 1).</summary>
    public double Opacity { get; set; } = 0.85;
    /// <summary>L'anneau se resserre légèrement tant qu'un bouton est maintenu.</summary>
    public bool PressEffect { get; set; } = true;
    /// <summary>L'anneau s'efface quand la souris reste immobile (évite un anneau figé dans une vidéo).</summary>
    public bool HideWhenIdle { get; set; } = true;
    /// <summary>Inactivité avant l'effacement, en secondes (1 à 30).</summary>
    public double IdleSeconds { get; set; } = 3;

    internal void Normalize()
    {
        Size = Math.Clamp(Size, MinSize, MaxSize);
        Thickness = Math.Clamp(Thickness, 1, 16);
        Opacity = Math.Clamp(Opacity, 0.1, 1);
        IdleSeconds = Math.Clamp(IdleSeconds, 1, 30);
    }
}

/// <summary>Mode projecteur : assombrit l'écran sauf autour du curseur.</summary>
public sealed class SpotSettings
{
    public bool Enabled { get; set; }
    /// <summary>Rayon du disque éclairé en pixels logiques (40 à 600).</summary>
    public double Radius { get; set; } = 160;
    /// <summary>Niveau d'assombrissement (0,1 à 0,95).</summary>
    public double Dimming { get; set; } = 0.6;

    internal void Normalize()
    {
        Radius = Math.Clamp(Radius, 40, 600);
        Dimming = Math.Clamp(Dimming, 0.1, 0.95);
    }
}

/// <summary>Animation au clic.</summary>
public sealed class ClickSettings
{
    public bool Enabled { get; set; } = true;
    public ArgbColor LeftColor { get; set; } = ArgbColor.FromRgb(0x2D, 0x7F, 0xF9);
    public ArgbColor RightColor { get; set; } = ArgbColor.FromRgb(0xFF, 0x8A, 0x1E);
    public ArgbColor MiddleColor { get; set; } = ArgbColor.FromRgb(0x2E, 0xC2, 0x7E);
    /// <summary>Durée de l'onde en millisecondes (200 à 1500).</summary>
    public double DurationMs { get; set; } = 500;
    /// <summary>Rayon final de l'onde en pixels logiques (15 à 150).</summary>
    public double MaxRadius { get; set; } = 42;
    /// <summary>Son de clic, désactivé par défaut.</summary>
    public bool Sound { get; set; }

    internal void Normalize()
    {
        DurationMs = Math.Clamp(DurationMs, 200, 1500);
        MaxRadius = Math.Clamp(MaxRadius, 15, 150);
    }

    public ArgbColor ColorFor(MouseButton button) => button switch
    {
        MouseButton.Left => LeftColor,
        MouseButton.Right => RightColor,
        _ => MiddleColor,
    };
}

/// <summary>Suivi du curseur pendant le zoom.</summary>
public enum ZoomFollowMode
{
    /// <summary>Le curseur reste au centre de la zone agrandie.</summary>
    CenterOnCursor,
    /// <summary>Le cadre ne bouge que lorsque le curseur approche du bord.</summary>
    EdgePush,
}

/// <summary>Mode d'activation du zoom.</summary>
public enum ZoomActivation
{
    /// <summary>Le raccourci active puis désactive le zoom.</summary>
    Toggle,
    /// <summary>Le zoom dure tant que le raccourci est maintenu.</summary>
    Hold,
}

/// <summary>Zoom d'écran.</summary>
public sealed class ZoomSettings
{
    public const double MinLevel = 1.5, MaxLevel = 8;

    public bool Enabled { get; set; } = true;
    /// <summary>Niveau de zoom (1,5 à 8).</summary>
    public double Level { get; set; } = 2;
    /// <summary>Pas d'un cran de molette avec Ctrl+Alt.</summary>
    public double WheelStep { get; set; } = 0.25;
    public ZoomFollowMode Follow { get; set; } = ZoomFollowMode.CenterOnCursor;
    public ZoomActivation Activation { get; set; } = ZoomActivation.Toggle;
    /// <summary>Durée des transitions d'entrée et de sortie en millisecondes.</summary>
    public double TransitionMs { get; set; } = 200;
    /// <summary>Marge (fraction de la zone visible) qui déclenche le défilement en mode « bord ».</summary>
    public double EdgeMargin { get; set; } = 0.15;

    internal void Normalize()
    {
        Level = Math.Clamp(Level, MinLevel, MaxLevel);
        WheelStep = Math.Clamp(WheelStep, 0.05, 2);
        TransitionMs = Math.Clamp(TransitionMs, 0, 1000);
        EdgeMargin = Math.Clamp(EdgeMargin, 0.02, 0.45);
    }
}

/// <summary>Raccourcis globaux (modifiables).</summary>
public sealed class HotkeySettings
{
    public Hotkey RingAndSpot { get; set; } = new(KeyModifiers.Ctrl | KeyModifiers.Alt, KeyId.M);
    public Hotkey Spot { get; set; } = new(KeyModifiers.Ctrl | KeyModifiers.Alt, KeyId.S);
    public Hotkey Keyboard { get; set; } = new(KeyModifiers.Ctrl | KeyModifiers.Alt, KeyId.K);
    public Hotkey Pause { get; set; } = new(KeyModifiers.Ctrl | KeyModifiers.Alt, KeyId.P);
    public Hotkey Zoom { get; set; } = new(KeyModifiers.Ctrl | KeyModifiers.Alt, KeyId.Z);
    public Hotkey Drawing { get; set; } = new(KeyModifiers.Ctrl | KeyModifiers.Alt, KeyId.D);

    public Hotkey Get(HotkeyAction action) => action switch
    {
        HotkeyAction.ToggleRingAndSpot => RingAndSpot,
        HotkeyAction.ToggleSpot => Spot,
        HotkeyAction.ToggleKeyboard => Keyboard,
        HotkeyAction.TogglePause => Pause,
        HotkeyAction.ToggleZoom => Zoom,
        HotkeyAction.ToggleDrawing => Drawing,
        _ => Hotkey.None,
    };

    public void Set(HotkeyAction action, Hotkey value)
    {
        switch (action)
        {
            case HotkeyAction.ToggleRingAndSpot: RingAndSpot = value; break;
            case HotkeyAction.ToggleSpot: Spot = value; break;
            case HotkeyAction.ToggleKeyboard: Keyboard = value; break;
            case HotkeyAction.TogglePause: Pause = value; break;
            case HotkeyAction.ToggleZoom: Zoom = value; break;
            case HotkeyAction.ToggleDrawing: Drawing = value; break;
        }
    }
}

/// <summary>Dessin à l'écran : dernier outil, couleur et épaisseur utilisés.</summary>
public sealed class DrawingSettings
{
    public DrawingTool Tool { get; set; } = DrawingTool.Rectangle;
    public ArgbColor Color { get; set; } = ArgbColor.FromRgb(0xE5, 0x2B, 0x2B);
    /// <summary>Épaisseur du trait en pixels logiques (1 à 40).</summary>
    public double Thickness { get; set; } = 6;

    internal void Normalize()
    {
        if (!Enum.IsDefined(Tool)) Tool = DrawingTool.Rectangle;
        Thickness = Math.Clamp(Thickness, 1, 40);
    }
}

/// <summary>Réglages généraux.</summary>
public sealed class GeneralSettings
{
    /// <summary>Lancer avec Windows (désactivé par défaut ; l'état réel est relu depuis l'OS).</summary>
    public bool LaunchAtStartup { get; set; }
}
