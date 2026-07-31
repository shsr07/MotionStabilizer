using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace MotionStabilizer.Models;

/// <summary>
/// Base class for observable objects that implement <see cref="INotifyPropertyChanged"/>.
/// Provides <see cref="SetProperty{T}"/> for property setters that automatically
/// fires the <see cref="PropertyChanged"/> event when the value changes.
/// </summary>
public abstract class ObservableObject : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;

    /// <summary>
    /// Set a backing field and fire <see cref="PropertyChanged"/> if the value changed.
    /// Returns true if the value was actually changed.
    /// </summary>
    protected bool SetProperty<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
            return false;
        field = value;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        return true;
    }

    /// <summary>Manually raise PropertyChanged for the given property name.</summary>
    protected void RaisePropertyChanged([CallerMemberName] string? propertyName = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}
