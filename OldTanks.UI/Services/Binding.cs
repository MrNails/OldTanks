using System.ComponentModel;
using Common;
using Common.Infrastructure.Delegates;
using Common.Infrastructure.EventArgs;

namespace OldTanks.UI.Services;

public sealed class Binding<TObject, TObjectData, TControlData> : IDisposable
    where TObject: INotifyPropertyChanged
{
    public event EventHandler<Binding<TObject, TObjectData, TControlData>, ValueChangedEventArgs<TObjectData>>? ObjectDataChanged; 

    public Binding(string propertyName, TObject source, Func<TObject, TObjectData> getData, Action<TObject, TControlData> setData)
    {
        PropertyName = propertyName;
        Source = source;
        GetData = getData;
        SetData = setData;
        
        Source.PropertyChanged += SourceOnPropertyChanged;
    }

    public string PropertyName { get; init; }
    public TObject Source { get; init; }
    public Func<TObject, TObjectData> GetData { get; init; }
    public Action<TObject, TControlData> SetData { get; init; }
    
    public void Dispose()
    {
        Source.PropertyChanged -= SourceOnPropertyChanged;
    }
    
    private void SourceOnPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == PropertyName)
        {
            ObjectDataChanged?.Invoke(this, new ValueChangedEventArgs<TObjectData>(default, GetData(Source)));
        }
    }
}