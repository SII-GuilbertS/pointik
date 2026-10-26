using Pointik.Core.Themes;

namespace Pointik.Core.Settings;

/// <summary>Ce que l'affichage du clavier montre.</summary>
public enum KeyboardDisplayMode
{
    /// <summary>Raccourcis et touches spéciales uniquement (défaut).</summary>
    ShortcutsAndSpecialKeys,
    /// <summary>Tout afficher : les caractères tapés sont regroupés sur une ligne.</summary>
    Everything,
}

/// <summary>Coin d'ancrage des bulles.</summary>
public enum ScreenCorner
{
    TopLeft,
    TopRight,
    BottomLeft,
    BottomRight,
}

/// <summary>Écran sur lequel afficher les bulles.</summary>
public enum TargetScreenMode
{
    /// <summary>L'écran où se trouve le curseur.</summary>
    CursorScreen,
    /// <summary>L'écran principal.</summary>
    PrimaryScreen,
    /// <summary>Un écran choisi (repli sur l'écran principal s'il est absent).</summary>
    SpecificScreen,
}

/// <summary>Alignement horizontal des bulles entre elles.</summary>
public enum BubbleAlignment
{
    Left,
    Center,
    Right,
}

/// <summary>Sens d'empilement des bulles.</summary>
public enum StackDirection
{
    /// <summary>La plus récente en bas, les anciennes montent.</summary>
    Up,
    /// <summary>La plus récente en haut, les anciennes descendent.</summary>
    Down,
}

/// <summary>Position des bulles du clavier.</summary>
public sealed class KeyboardPosition
{
    public ScreenCorner Corner { get; set; } = ScreenCorner.BottomRight;
    public TargetScreenMode Target { get; set; } = TargetScreenMode.CursorScreen;
    /// <summary>Identifiant de l'écran choisi quand <see cref="Target"/> vaut SpecificScreen.</summary>
    public string? ScreenId { get; set; }
    /// <summary>Décalage horizontal depuis le coin, en pixels logiques.</summary>
    public double OffsetX { get; set; } = 40;
    /// <summary>Décalage vertical depuis le coin, en pixels logiques.</summary>
    public double OffsetY { get; set; } = 60;
    public BubbleAlignment Alignment { get; set; } = BubbleAlignment.Right;
    public StackDirection Stack { get; set; } = StackDirection.Up;

    /// <summary>
    /// Position libre définie en mode placement. Quand elle est active, le coin et le décalage
    /// sont ignorés : le point d'ancrage est exprimé en fraction (0 à 1) de la zone de travail,
    /// ce qui résiste aux changements de résolution et de DPI.
    /// </summary>
    public bool UseCustomPosition { get; set; }
    public double CustomX { get; set; } = 0.5;
    public double CustomY { get; set; } = 0.85;

    internal void Normalize()
    {
        OffsetX = Math.Clamp(OffsetX, -2000, 2000);
        OffsetY = Math.Clamp(OffsetY, -2000, 2000);
        CustomX = Math.Clamp(double.IsFinite(CustomX) ? CustomX : 0.5, 0, 1);
        CustomY = Math.Clamp(double.IsFinite(CustomY) ? CustomY : 0.85, 0, 1);
    }
}

/// <summary>Affichage du clavier.</summary>
public sealed class KeyboardSettings
{
    public bool Enabled { get; set; } = true;
    public KeyboardDisplayMode Mode { get; set; } = KeyboardDisplayMode.ShortcutsAndSpecialKeys;
    /// <summary>Masque automatiquement l'affichage quand un champ de mot de passe a le focus.</summary>
    public bool HideOnPasswordFields { get; set; } = true;
    /// <summary>Affiche aussi les clics et la molette (« Ctrl + Clic gauche », « Molette ↓ ×3 »).</summary>
    public bool ShowMouseActions { get; set; }
    /// <summary>Nombre de bulles visibles (1 à 6).</summary>
    public int MaxVisible { get; set; } = 3;
    /// <summary>Durée d'affichage d'une bulle avant son effacement, en secondes.</summary>
    public double DisplaySeconds { get; set; } = 1.5;
    /// <summary>Pause après laquelle la ligne de texte (« Tout afficher ») se vide, en secondes.</summary>
    public double TextPauseSeconds { get; set; } = 2;
    public KeyboardPosition Position { get; set; } = new();

    /// <summary>Nom du thème dont le style courant est issu.</summary>
    public string ThemeName { get; set; } = BuiltInThemes.DarkName;
    /// <summary>Style courant (copie du thème, éventuellement retouchée).</summary>
    public KeyboardStyle Style { get; set; } = BuiltInThemes.Dark.Style.Clone();
    /// <summary>Thèmes personnels enregistrés.</summary>
    public List<KeyboardTheme> CustomThemes { get; set; } = new();

    internal void Normalize()
    {
        MaxVisible = Math.Clamp(MaxVisible, 1, 6);
        DisplaySeconds = Math.Clamp(DisplaySeconds, 0.3, 10);
        TextPauseSeconds = Math.Clamp(TextPauseSeconds, 0.5, 10);
        Position ??= new();
        Position.Normalize();
        Style ??= BuiltInThemes.Dark.Style.Clone();
        Style.Normalize();
        CustomThemes ??= new();
        CustomThemes.RemoveAll(t => t is null || string.IsNullOrWhiteSpace(t.Name) || t.Style is null);
        foreach (var t in CustomThemes) t.Style.Normalize();
        ThemeName ??= BuiltInThemes.DarkName;
    }
}
