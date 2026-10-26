namespace Pointik.Core.Animation;

/// <summary>Fonctions d'interpolation (entrée et sortie entre 0 et 1).</summary>
public static class Easing
{
    public static double Clamp01(double t) => t < 0 ? 0 : t > 1 ? 1 : t;

    public static double Linear(double t) => Clamp01(t);

    /// <summary>Décélération douce (rapide au début, lente à la fin).</summary>
    public static double CubicOut(double t)
    {
        t = Clamp01(t);
        var u = 1 - t;
        return 1 - u * u * u;
    }

    /// <summary>Accélération puis décélération.</summary>
    public static double CubicInOut(double t)
    {
        t = Clamp01(t);
        return t < 0.5 ? 4 * t * t * t : 1 - Math.Pow(-2 * t + 2, 3) / 2;
    }

    public static double QuadOut(double t)
    {
        t = Clamp01(t);
        return 1 - (1 - t) * (1 - t);
    }

    public static double Lerp(double a, double b, double t) => a + (b - a) * t;

    /// <summary>Progression (0 à 1) d'une animation démarrée à <paramref name="start"/>.</summary>
    public static double Progress(TimeSpan now, TimeSpan start, TimeSpan duration) =>
        duration <= TimeSpan.Zero ? 1 : Clamp01((now - start).TotalMilliseconds / duration.TotalMilliseconds);
}
