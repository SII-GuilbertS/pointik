using System.Globalization;
using Avalonia;
using Avalonia.Media;
using Pointik.Core.Animation;

namespace Pointik.UI.Overlay;

/// <summary>
/// Notification dessinée dans l'overlay (en haut à droite de l'écran) : traversable aux clics,
/// ne prend jamais le focus et reste lisible pendant le zoom (même compensation que les bulles).
/// </summary>
public sealed class OverlayNotification
{
    private static readonly TimeSpan FadeDuration = TimeSpan.FromMilliseconds(220);
    private const double MaxWidth = 380, Margin = 16, Padding = 14;

    public OverlayNotification(string title, string message, TimeSpan shownAt, TimeSpan duration)
    {
        Title = title;
        Message = message;
        ShownAt = shownAt;
        Duration = duration;
    }

    public string Title { get; }
    public string Message { get; }
    public TimeSpan ShownAt { get; }
    public TimeSpan Duration { get; }

    /// <summary>Instant où la notification a entièrement disparu.</summary>
    public TimeSpan EndsAt => ShownAt + Duration + FadeDuration;

    public bool IsExpired(TimeSpan now) => now >= EndsAt;

    private double Opacity(TimeSpan now)
    {
        var appear = Easing.CubicOut(Easing.Progress(now, ShownAt, FadeDuration));
        var vanish = Easing.Progress(now, ShownAt + Duration, FadeDuration);
        return appear * (1 - vanish);
    }

    /// <summary>Dessine la notification en haut à droite de la zone, sous la pastille de pause si besoin.</summary>
    public void Draw(DrawingContext ctx, Rect area, TimeSpan now, bool belowPauseBadge)
    {
        var opacity = Opacity(now);
        if (opacity <= 0.003) return;

        var title = new FormattedText(Title, CultureInfo.CurrentCulture, FlowDirection.LeftToRight,
            new Typeface(FontFamily.Default, FontStyle.Normal, FontWeight.SemiBold), 14,
            new SolidColorBrush(Color.FromRgb(0xFF, 0xC4, 0x1F)))
        {
            MaxTextWidth = MaxWidth - 2 * Padding,
        };
        var body = new FormattedText(Message, CultureInfo.CurrentCulture, FlowDirection.LeftToRight,
            new Typeface(FontFamily.Default), 13, Brushes.White)
        {
            MaxTextWidth = MaxWidth - 2 * Padding,
        };
        var width = Math.Min(MaxWidth, Math.Max(title.Width, body.Width) + 2 * Padding);
        var height = title.Height + 4 + body.Height + 2 * Padding;
        var top = area.Y + Margin + (belowPauseBadge ? 38 : 0);
        var rect = new Rect(area.Right - Margin - width, top, width, height);

        using var _ = ctx.PushOpacity(opacity);
        ctx.DrawRectangle(new SolidColorBrush(Color.FromArgb(240, 28, 31, 38)),
            new Pen(new SolidColorBrush(Color.FromArgb(80, 255, 255, 255)), 1),
            new RoundedRect(rect, 10),
            new BoxShadows(new BoxShadow { OffsetY = 3, Blur = 14, Color = Color.FromArgb(110, 0, 0, 0) }));
        ctx.DrawText(title, new Point(rect.X + Padding, rect.Y + Padding));
        ctx.DrawText(body, new Point(rect.X + Padding, rect.Y + Padding + title.Height + 4));
    }
}
