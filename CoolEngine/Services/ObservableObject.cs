using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace CoolEngine.Services;

public class ObservableObject : INotifyPropertyChanged
{
    private static readonly Action EmptyAction = () => { };

    public event PropertyChangedEventHandler? PropertyChanged;

    protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));

    protected bool SetField<T>(ref T field, T value, [CallerMemberName] string? propertyName = null) => 
        SetField(ref field, value, EmptyAction, propertyName);
    
    protected bool SetField<T>(ref T field, T value, Action valueChanged, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value)) 
            return false;
        
        field = value;
        valueChanged.Invoke();
        
        OnPropertyChanged(propertyName);
        return true;
    }
}