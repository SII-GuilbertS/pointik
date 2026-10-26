using Avalonia.Threading;
using Pointik.Core.Platform;

namespace Pointik.UI.Services;

/// <summary>Accès au thread UI d'Avalonia pour les services de plateforme.</summary>
public sealed class AvaloniaUiDispatcher : IUiDispatcher
{
    public void Post(Action action) => Dispatcher.UIThread.Post(action, DispatcherPriority.Input);

    public bool CheckAccess() => Dispatcher.UIThread.CheckAccess();
}
