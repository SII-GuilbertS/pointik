using Pointik.Core.Graphics;

namespace Pointik.Core.Themes;

/// <summary>Présentation des bulles du clavier.</summary>
public enum KeyPresentation
{
    /// <summary>Une bulle de texte : « Ctrl + Maj + S ».</summary>
    TextBubble,
    /// <summary>Chaque touche dans sa propre petite touche de clavier.</summary>
    Keycaps,
}

/// <summary>Animation d'apparition ou de disparition d'une bulle.</summary>
public enum BubbleAnimation
{
    Fade,
    Slide,
    None,
}

/// <summary>Style visuel des bulles du clavier.</summary>
public sealed class KeyboardStyle
{
    public string FontFamily { get; set; } = "Segoe UI";
    public double FontSize { get; set; } = 22;
    public bool Bold { get; set; } = true;
    public ArgbColor TextColor { get; set; } = ArgbColor.FromRgb(0xFF, 0xFF, 0xFF);
    /// <summary>Couleur du fond (l'alpha est ignoré au profit de <see cref="BackgroundOpacity"/>).</summary>
    public ArgbColor BackgroundColor { get; set; } = ArgbColor.FromRgb(0x1E, 0x22, 0x2A);
    public double BackgroundOpacity { get; set; } = 0.88;
    public double CornerRadius { get; set; } = 10;
    public double BorderThickness { get; set; }
    public ArgbColor BorderColor { get; set; } = ArgbColor.FromRgb(0xFF, 0xFF, 0xFF);
    public bool Shadow { get; set; } = true;
    public double PaddingHorizontal { get; set; } = 16;
    public double PaddingVertical { get; set; } = 8;
    /// <summary>Espacement vertical entre deux bulles.</summary>
    public double Spacing { get; set; } = 8;
    /// <summary>Échelle globale en pourcentage (50 à 300).</summary>
    public double ScalePercent { get; set; } = 100;
    public KeyPresentation Presentation { get; set; } = KeyPresentation.TextBubble;
    public BubbleAnimation EnterAnimation { get; set; } = BubbleAnimation.Slide;
    public BubbleAnimation ExitAnimation { get; set; } = BubbleAnimation.Fade;

    /// <summary>Copie indépendante (toutes les propriétés sont des valeurs ou des chaînes).</summary>
    public KeyboardStyle Clone() => (KeyboardStyle)MemberwiseClone();

    /// <summary>Ramène les valeurs dans des plages raisonnables.</summary>
    public void Normalize()
    {
        if (string.IsNullOrWhiteSpace(FontFamily)) FontFamily = "Segoe UI";
        FontSize = Math.Clamp(FontSize, 8, 96);
        BackgroundOpacity = Math.Clamp(BackgroundOpacity, 0, 1);
        CornerRadius = Math.Clamp(CornerRadius, 0, 60);
        BorderThickness = Math.Clamp(BorderThickness, 0, 12);
        PaddingHorizontal = Math.Clamp(PaddingHorizontal, 0, 80);
        PaddingVertical = Math.Clamp(PaddingVertical, 0, 80);
        Spacing = Math.Clamp(Spacing, 0, 80);
        ScalePercent = Math.Clamp(ScalePercent, 50, 300);
        if (!Enum.IsDefined(Presentation)) Presentation = KeyPresentation.TextBubble;
        if (!Enum.IsDefined(EnterAnimation)) EnterAnimation = BubbleAnimation.Fade;
        if (!Enum.IsDefined(ExitAnimation)) ExitAnimation = BubbleAnimation.Fade;
    }
}

/// <summary>Thème nommé du clavier (prédéfini ou personnel).</summary>
public sealed class KeyboardTheme
{
    public string Name { get; set; } = "";
    public KeyboardStyle Style { get; set; } = new();
}
