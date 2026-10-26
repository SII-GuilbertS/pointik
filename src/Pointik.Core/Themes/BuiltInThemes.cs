using Pointik.Core.Graphics;

namespace Pointik.Core.Themes;

/// <summary>Thèmes prédéfinis du clavier.</summary>
public static class BuiltInThemes
{
    public const string DarkName = "Sombre";
    public const string LightName = "Clair";
    public const string HighContrastName = "Contraste élevé";
    public const string SubtleName = "Discret";
    public const string KeycapsName = "Touches";

    public static KeyboardTheme Dark { get; } = new()
    {
        Name = DarkName,
        Style = new KeyboardStyle(),
    };

    public static KeyboardTheme Light { get; } = new()
    {
        Name = LightName,
        Style = new KeyboardStyle
        {
            TextColor = ArgbColor.FromRgb(0x1C, 0x1F, 0x26),
            BackgroundColor = ArgbColor.FromRgb(0xFA, 0xFA, 0xF7),
            BackgroundOpacity = 0.95,
            BorderThickness = 1,
            BorderColor = ArgbColor.FromRgb(0xC9, 0xCD, 0xD4),
        },
    };

    public static KeyboardTheme HighContrast { get; } = new()
    {
        Name = HighContrastName,
        Style = new KeyboardStyle
        {
            FontSize = 26,
            TextColor = ArgbColor.FromRgb(0xFF, 0xFF, 0x00),
            BackgroundColor = ArgbColor.FromRgb(0x00, 0x00, 0x00),
            BackgroundOpacity = 1,
            BorderThickness = 3,
            BorderColor = ArgbColor.FromRgb(0xFF, 0xFF, 0xFF),
            CornerRadius = 4,
            Shadow = false,
            EnterAnimation = BubbleAnimation.None,
            ExitAnimation = BubbleAnimation.None,
        },
    };

    public static KeyboardTheme Subtle { get; } = new()
    {
        Name = SubtleName,
        Style = new KeyboardStyle
        {
            FontSize = 16,
            Bold = false,
            TextColor = ArgbColor.FromRgb(0xF2, 0xF2, 0xF2),
            BackgroundColor = ArgbColor.FromRgb(0x30, 0x30, 0x30),
            BackgroundOpacity = 0.55,
            CornerRadius = 14,
            Shadow = false,
            PaddingHorizontal = 12,
            PaddingVertical = 5,
            Spacing = 5,
            EnterAnimation = BubbleAnimation.Fade,
        },
    };

    public static KeyboardTheme Keycaps { get; } = new()
    {
        Name = KeycapsName,
        Style = new KeyboardStyle
        {
            FontSize = 20,
            TextColor = ArgbColor.FromRgb(0x23, 0x26, 0x2E),
            BackgroundColor = ArgbColor.FromRgb(0xEC, 0xEE, 0xF2),
            BackgroundOpacity = 1,
            BorderThickness = 1,
            BorderColor = ArgbColor.FromRgb(0x9A, 0xA1, 0xAD),
            CornerRadius = 7,
            PaddingHorizontal = 12,
            PaddingVertical = 7,
            Presentation = KeyPresentation.Keycaps,
        },
    };

    /// <summary>Tous les thèmes prédéfinis, dans l'ordre d'affichage.</summary>
    public static IReadOnlyList<KeyboardTheme> All { get; } = [Dark, Light, HighContrast, Subtle, Keycaps];

    public static bool IsBuiltIn(string name) =>
        All.Any(t => string.Equals(t.Name, name, StringComparison.OrdinalIgnoreCase));

    public static KeyboardTheme? Find(string name) =>
        All.FirstOrDefault(t => string.Equals(t.Name, name, StringComparison.OrdinalIgnoreCase));
}
