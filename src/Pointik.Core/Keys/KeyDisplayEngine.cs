using Pointik.Core.Animation;
using Pointik.Core.Input;
using Pointik.Core.Settings;

namespace Pointik.Core.Keys;

/// <summary>Une bulle affichée. Ne vit qu'en mémoire, le temps de l'affichage.</summary>
public sealed class KeyBubble
{
    internal KeyBubble(long id, IReadOnlyList<string> parts, string text, bool isTextLine, TimeSpan now)
    {
        Id = id;
        Parts = parts;
        Text = text;
        IsTextLine = isTextLine;
        CreatedAt = now;
        LastActivity = now;
        Count = 1;
    }

    public long Id { get; }

    /// <summary>Éléments du raccourci (« Ctrl », « Maj », « S ») pour la présentation en touches.</summary>
    public IReadOnlyList<string> Parts { get; }

    /// <summary>Texte du raccourci (« Ctrl + Maj + S ») ou de la ligne tapée.</summary>
    public string Text { get; internal set; }

    /// <summary>Ligne de caractères tapés (mode « Tout afficher »).</summary>
    public bool IsTextLine { get; }

    /// <summary>Nombre de répétitions consécutives fusionnées.</summary>
    public int Count { get; internal set; }

    public TimeSpan CreatedAt { get; }
    public TimeSpan LastActivity { get; internal set; }

    /// <summary>Instant où la bulle commence à disparaître.</summary>
    public TimeSpan ExitAt { get; internal set; }

    /// <summary>Suffixe de répétition : « ×3 » (vide si une seule frappe).</summary>
    public string CountSuffix => Count > 1 ? $"×{Count}" : "";

    /// <summary>Texte complet affiché : « Ctrl + Z ×3 ».</summary>
    public string DisplayText => Count > 1 ? $"{Text} {CountSuffix}" : Text;
}

/// <summary>État d'animation d'une bulle à un instant donné.</summary>
/// <param name="Enter">Progression de l'apparition (0 à 1).</param>
/// <param name="Exit">Progression de la disparition (0 à 1).</param>
public readonly record struct BubbleVisualState(double Enter, double Exit)
{
    /// <summary>Opacité combinée (apparition × disparition).</summary>
    public double Opacity => Enter * (1 - Exit);
}

/// <summary>
/// Moteur des bulles du clavier : classe les frappes, fusionne les répétitions (« ×3 »),
/// regroupe le texte tapé, limite le nombre de bulles et gère leurs durées de vie.
/// Aucune frappe n'est conservée au-delà de l'affichage.
/// </summary>
public sealed class KeyDisplayEngine
{
    public static readonly TimeSpan EnterDuration = TimeSpan.FromMilliseconds(160);
    public static readonly TimeSpan ExitDuration = TimeSpan.FromMilliseconds(280);

    /// <summary>Longueur maximale de la ligne de texte (les caractères les plus anciens sont retirés).</summary>
    public const int MaxTextLength = 40;

    private readonly List<KeyBubble> _bubbles = new();
    private readonly Func<KeyboardSettings> _settings;
    private KeyNames _names;
    private long _nextId = 1;

    public KeyDisplayEngine(Func<KeyboardSettings> settings, KeyNames names)
    {
        _settings = settings;
        _names = names;
    }

    /// <summary>Bulles de la plus ancienne à la plus récente.</summary>
    public IReadOnlyList<KeyBubble> Bubbles => _bubbles;

    public KeyNames Names
    {
        get => _names;
        set => _names = value;
    }

    /// <summary>Traite une frappe. Retourne true si l'affichage a changé.</summary>
    public bool Process(in KeyboardInputEvent e, TimeSpan now)
    {
        var s = _settings();
        var action = KeyClassifier.Classify(e, s.Mode, _names);
        return action.Kind switch
        {
            KeyDisplayKind.Shortcut => AddShortcut(action.Parts, now),
            KeyDisplayKind.Text => AddText(action.Text!, now),
            _ => false,
        };
    }

    /// <summary>Ajoute (ou fusionne) une bulle de raccourci.</summary>
    public bool AddShortcut(IReadOnlyList<string> parts, TimeSpan now)
    {
        if (parts.Count == 0) return false;
        var text = string.Join(KeyNames.Separator, parts);
        var last = _bubbles.Count > 0 ? _bubbles[^1] : null;
        if (last is not null && !last.IsTextLine && last.Text == text && now < last.ExitAt + ExitDuration)
        {
            // Même raccourci répété : « Ctrl + Z ×3 ».
            last.Count++;
            Refresh(last, now);
            return true;
        }
        Add(new KeyBubble(_nextId++, parts, text, false, now), now);
        return true;
    }

    /// <summary>Ajoute des caractères à la ligne de texte en cours, ou en démarre une.</summary>
    public bool AddText(string text, TimeSpan now)
    {
        var last = _bubbles.Count > 0 ? _bubbles[^1] : null;
        if (last is not null && last.IsTextLine && now < last.ExitAt)
        {
            var combined = last.Text + text;
            if (combined.Length > MaxTextLength)
                combined = "…" + combined[^(MaxTextLength - 1)..];
            last.Text = combined;
            Refresh(last, now);
            return true;
        }
        // Une ligne qui ne contiendrait qu'une espace serait invisible.
        if (string.IsNullOrWhiteSpace(text)) return false;
        Add(new KeyBubble(_nextId++, Array.Empty<string>(), text, true, now), now);
        return true;
    }

    /// <summary>Retire les bulles terminées. Retourne true si une animation est encore en cours.</summary>
    public bool Update(TimeSpan now)
    {
        _bubbles.RemoveAll(b => now >= b.ExitAt + ExitDuration);
        return _bubbles.Count > 0;
    }

    /// <summary>État d'animation d'une bulle.</summary>
    public BubbleVisualState GetVisualState(KeyBubble b, TimeSpan now) =>
        new(Easing.CubicOut(Easing.Progress(now, b.CreatedAt, EnterDuration)),
            Easing.Progress(now, b.ExitAt, ExitDuration));

    /// <summary>Efface immédiatement toutes les bulles (pause, désactivation).</summary>
    public void Clear() => _bubbles.Clear();

    private void Add(KeyBubble bubble, TimeSpan now)
    {
        // La ligne de texte en cours est figée : elle s'efface normalement.
        Refresh(bubble, now);
        _bubbles.Add(bubble);
        var max = Math.Max(1, _settings().MaxVisible);
        // Au-delà du maximum, les plus anciennes disparaissent sans attendre.
        while (_bubbles.Count > max) _bubbles.RemoveAt(0);
    }

    private void Refresh(KeyBubble b, TimeSpan now)
    {
        var s = _settings();
        b.LastActivity = now;
        var life = b.IsTextLine ? s.TextPauseSeconds : s.DisplaySeconds;
        b.ExitAt = now + TimeSpan.FromSeconds(life);
    }
}
