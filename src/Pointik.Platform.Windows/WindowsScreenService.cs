using System.Runtime.InteropServices;
using Pointik.Core.Geometry;
using Pointik.Core.Platform;
using Pointik.Platform.Windows.Interop;
using static Pointik.Platform.Windows.Interop.Native;

namespace Pointik.Platform.Windows;

/// <summary>Énumération des écrans (positions physiques, zones de travail, DPI par écran).</summary>
internal sealed class WindowsScreenService : IScreenService
{
    private const nint SPI_SETWORKAREA = 0x002F;

    private readonly IUiDispatcher _ui;
    private IReadOnlyList<ScreenInfo>? _cache;
    private int _notifyPending;

    public WindowsScreenService(Win32MessageThread thread, IUiDispatcher ui)
    {
        _ui = ui;
        thread.MessageReceived += OnMessage;
    }

    public event Action? ScreensChanged;

    public IReadOnlyList<ScreenInfo> GetScreens() => _cache ??= Enumerate();

    public ScreenInfo GetScreenFromPoint(ScreenPoint point)
    {
        var screens = GetScreens();
        foreach (var s in screens)
            if (s.Bounds.Contains(point)) return s;
        // Point hors de tout écran (transition rapide) : écran le plus proche.
        return screens.MinBy(s => s.Bounds.DistanceSquaredTo(point)) ?? screens[0];
    }

    public ScreenPoint GetCursorPosition() =>
        GetCursorPos(out var p) ? new ScreenPoint(p.X, p.Y) : default;

    private bool OnMessage(uint msg, nint wParam, nint lParam)
    {
        var changed = msg is WM_DISPLAYCHANGE or WM_DPICHANGED
            || (msg == WM_SETTINGCHANGE && wParam == SPI_SETWORKAREA);
        if (changed && Interlocked.Exchange(ref _notifyPending, 1) == 0)
        {
            _ui.Post(() =>
            {
                Interlocked.Exchange(ref _notifyPending, 0);
                _cache = null;
                ScreensChanged?.Invoke();
            });
        }
        return false;
    }

    private static IReadOnlyList<ScreenInfo> Enumerate()
    {
        var list = new List<ScreenInfo>();
        MonitorEnumProc proc = (nint hMonitor, nint _, ref RECT _, nint _) =>
        {
            var info = new MONITORINFOEXW { cbSize = (uint)Marshal.SizeOf<MONITORINFOEXW>() };
            if (!GetMonitorInfoW(hMonitor, ref info)) return true;
            var scale = 1.0;
            if (GetDpiForMonitor(hMonitor, MDT_EFFECTIVE_DPI, out var dpiX, out _) == 0 && dpiX > 0)
                scale = dpiX / 96.0;
            list.Add(new ScreenInfo(
                info.szDevice,
                "",
                ToRect(info.rcMonitor),
                ToRect(info.rcWork),
                scale,
                (info.dwFlags & MONITORINFOF_PRIMARY) != 0));
            return true;
        };
        EnumDisplayMonitors(0, 0, proc, 0);
        GC.KeepAlive(proc);

        if (list.Count == 0)
            list.Add(new ScreenInfo("défaut", "", new ScreenRect(0, 0, 1920, 1080), new ScreenRect(0, 0, 1920, 1040), 1, true));

        // Ordre stable et lisible : principal d'abord, puis de gauche à droite.
        return list
            .OrderByDescending(s => s.IsPrimary).ThenBy(s => s.Bounds.X).ThenBy(s => s.Bounds.Y)
            .Select((s, i) => s with { Name = $"Écran {i + 1}{(s.IsPrimary ? " (principal)" : "")} — {s.Bounds.Width}×{s.Bounds.Height}, {s.Scale * 100:0} %" })
            .ToList();
    }

    private static ScreenRect ToRect(RECT r) => new(r.Left, r.Top, r.Right - r.Left, r.Bottom - r.Top);
}
