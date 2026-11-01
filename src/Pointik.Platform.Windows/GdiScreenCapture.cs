using System.Runtime.InteropServices;
using Pointik.Core.Geometry;
using Pointik.Core.Platform;

namespace Pointik.Platform.Windows;

/// <summary>
/// Capture d'écran GDI (BitBlt depuis l'écran) et presse-papiers CF_DIB.
/// Les fenêtres de Pointik sont exclues de la capture le temps de l'opération grâce à
/// <c>SetWindowDisplayAffinity(WDA_EXCLUDEFROMCAPTURE)</c> (Windows 10 2004 et plus) :
/// elles restent visibles à l'écran, sans clignotement, mais n'apparaissent pas dans l'image.
/// </summary>
internal sealed class GdiScreenCapture : IScreenCapture
{
    public CapturedImage? Capture(ScreenRect bounds)
    {
        var x = (int)Math.Round(bounds.X);
        var y = (int)Math.Round(bounds.Y);
        var w = (int)Math.Round(bounds.Width);
        var h = (int)Math.Round(bounds.Height);
        if (w <= 0 || h <= 0) return null;

        var excluded = ExcludeOwnWindows();
        nint screenDc = 0, memDc = 0, bitmap = 0, previous = 0;
        try
        {
            // Laisse le compositeur appliquer l'exclusion avant de lire l'écran.
            DwmFlush();
            DwmFlush();

            screenDc = GetDC(0);
            memDc = CreateCompatibleDC(screenDc);
            var info = new BITMAPINFOHEADER
            {
                biSize = Marshal.SizeOf<BITMAPINFOHEADER>(),
                biWidth = w,
                biHeight = -h, // négatif : lignes de haut en bas
                biPlanes = 1,
                biBitCount = 32,
                biCompression = BI_RGB,
            };
            bitmap = CreateDIBSection(screenDc, ref info, DIB_RGB_COLORS, out var bits, 0, 0);
            if (bitmap == 0 || bits == 0) return null;
            previous = SelectObject(memDc, bitmap);
            // CAPTUREBLT : inclut les fenêtres superposées des autres applications (menus, info-bulles).
            if (!BitBlt(memDc, 0, 0, w, h, screenDc, x, y, SRCCOPY | CAPTUREBLT)) return null;

            var pixels = new byte[w * h * 4];
            Marshal.Copy(bits, pixels, 0, pixels.Length);
            // GDI laisse l'alpha indéfini : image opaque.
            for (var i = 3; i < pixels.Length; i += 4) pixels[i] = 255;
            return new CapturedImage(w, h, pixels);
        }
        catch
        {
            return null;
        }
        finally
        {
            if (previous != 0) SelectObject(memDc, previous);
            if (bitmap != 0) DeleteObject(bitmap);
            if (memDc != 0) DeleteDC(memDc);
            if (screenDc != 0) ReleaseDC(0, screenDc);
            foreach (var (hwnd, affinity) in excluded) SetWindowDisplayAffinity(hwnd, affinity);
        }
    }

    public bool CopyToClipboard(CapturedImage image)
    {
        var headerSize = Marshal.SizeOf<BITMAPINFOHEADER>();
        var size = headerSize + image.Bgra.Length;
        var memory = GlobalAlloc(GMEM_MOVEABLE, (nuint)size);
        if (memory == 0) return false;
        var ownedByClipboard = false;
        try
        {
            var p = GlobalLock(memory);
            if (p == 0) return false;
            try
            {
                var header = new BITMAPINFOHEADER
                {
                    biSize = headerSize,
                    biWidth = image.Width,
                    biHeight = image.Height, // positif : DIB de bas en haut, le plus compatible
                    biPlanes = 1,
                    biBitCount = 32,
                    biCompression = BI_RGB,
                    biSizeImage = image.Bgra.Length,
                };
                Marshal.StructureToPtr(header, p, false);
                for (var row = 0; row < image.Height; row++)
                {
                    var source = (image.Height - 1 - row) * image.Stride;
                    Marshal.Copy(image.Bgra, source, p + headerSize + row * image.Stride, image.Stride);
                }
            }
            finally
            {
                GlobalUnlock(memory);
            }

            // Le presse-papiers peut être brièvement tenu par une autre application.
            for (var attempt = 0; attempt < 5; attempt++)
            {
                if (OpenClipboard(0))
                {
                    try
                    {
                        EmptyClipboard();
                        ownedByClipboard = SetClipboardData(CF_DIB, memory) != 0;
                        return ownedByClipboard;
                    }
                    finally
                    {
                        CloseClipboard();
                    }
                }
                Thread.Sleep(30);
            }
            return false;
        }
        catch
        {
            return false;
        }
        finally
        {
            if (!ownedByClipboard) GlobalFree(memory);
        }
    }

    /// <summary>Exclut de la capture les fenêtres visibles du processus ; retourne leur affinité d'origine.</summary>
    private static List<(nint Hwnd, uint Affinity)> ExcludeOwnWindows()
    {
        var excluded = new List<(nint, uint)>();
        var pid = (uint)Environment.ProcessId;
        EnumWindows((hwnd, _) =>
        {
            GetWindowThreadProcessId(hwnd, out var owner);
            if (owner == pid && IsWindowVisible(hwnd))
            {
                GetWindowDisplayAffinity(hwnd, out var current);
                if (SetWindowDisplayAffinity(hwnd, WDA_EXCLUDEFROMCAPTURE)) excluded.Add((hwnd, current));
            }
            return true;
        }, 0);
        return excluded;
    }

    // ---------------------------------------------------------------- P/Invoke

    private const int BI_RGB = 0;
    private const uint DIB_RGB_COLORS = 0;
    private const uint SRCCOPY = 0x00CC0020;
    private const uint CAPTUREBLT = 0x40000000;
    private const uint WDA_EXCLUDEFROMCAPTURE = 0x11;
    private const uint CF_DIB = 8;
    private const uint GMEM_MOVEABLE = 0x0002;

    [StructLayout(LayoutKind.Sequential)]
    private struct BITMAPINFOHEADER
    {
        public int biSize;
        public int biWidth;
        public int biHeight;
        public short biPlanes;
        public short biBitCount;
        public int biCompression;
        public int biSizeImage;
        public int biXPelsPerMeter;
        public int biYPelsPerMeter;
        public int biClrUsed;
        public int biClrImportant;
    }

    private delegate bool EnumWindowsProc(nint hwnd, nint lParam);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool EnumWindows(EnumWindowsProc callback, nint lParam);

    [DllImport("user32.dll")]
    private static extern uint GetWindowThreadProcessId(nint hwnd, out uint processId);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool IsWindowVisible(nint hwnd);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetWindowDisplayAffinity(nint hwnd, out uint affinity);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetWindowDisplayAffinity(nint hwnd, uint affinity);

    [DllImport("dwmapi.dll")]
    private static extern int DwmFlush();

    [DllImport("user32.dll")]
    private static extern nint GetDC(nint hwnd);

    [DllImport("user32.dll")]
    private static extern int ReleaseDC(nint hwnd, nint hdc);

    [DllImport("gdi32.dll")]
    private static extern nint CreateCompatibleDC(nint hdc);

    [DllImport("gdi32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool DeleteDC(nint hdc);

    [DllImport("gdi32.dll")]
    private static extern nint CreateDIBSection(nint hdc, ref BITMAPINFOHEADER info, uint usage, out nint bits, nint section, uint offset);

    [DllImport("gdi32.dll")]
    private static extern nint SelectObject(nint hdc, nint obj);

    [DllImport("gdi32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool DeleteObject(nint obj);

    [DllImport("gdi32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool BitBlt(nint dest, int x, int y, int w, int h, nint source, int sx, int sy, uint rop);

    [DllImport("kernel32.dll")]
    private static extern nint GlobalAlloc(uint flags, nuint bytes);

    [DllImport("kernel32.dll")]
    private static extern nint GlobalLock(nint memory);

    [DllImport("kernel32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GlobalUnlock(nint memory);

    [DllImport("kernel32.dll")]
    private static extern nint GlobalFree(nint memory);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool OpenClipboard(nint owner);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool EmptyClipboard();

    [DllImport("user32.dll")]
    private static extern nint SetClipboardData(uint format, nint memory);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool CloseClipboard();
}
