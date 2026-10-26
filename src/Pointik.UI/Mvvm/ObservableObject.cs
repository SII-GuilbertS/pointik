using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace Pointik.UI.Mvvm;

/// <summary>Base minimale des view-models (évite une dépendance NuGet MVVM).</summary>
public abstract class ObservableObject : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;

    protected void OnPropertyChanged([CallerMemberName] string? name = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

    /// <summary>Affecte le champ et notifie si la valeur change. Retourne true en cas de changement.</summary>
    protected bool SetProperty<T>(ref T field, T value, [CallerMemberName] string? name = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value)) return false;
        field = value;
        OnPropertyChanged(name);
        return true;
    }
}
