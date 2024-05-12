using System.Buffers;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using Common;
using Common.Extensions;
using Common.Infrastructure.Delegates;
using Common.Infrastructure.EventArgs;
using Common.Models;
using ImGuiNET;
using OldTanks.UI.Services;

namespace OldTanks.UI.ImGuiControls;

public record struct SelectionChangedArgs<T>(int Index, T? Item);

public class ImGuiListBox<T> : ImGuiControl, IDisposable
{
    private static readonly Func<T, string> EmptyBindingFunction = v => v!.ToString()!;

    private readonly ArrayPool<string> m_arrayPool;

    private string[] m_valuesToDraw;
    
    private int m_selectedIndex;
    
    private ObservableCollection<T>? m_items;
    private Func<T, string> m_bindingFunction;

    public event EventHandler<ImGuiListBox<T>, ValueChangedEventArgs<SelectionChangedArgs<T>>>? SelectionChanged; 
    
    public ImGuiListBox(string name, ArrayPool<string> arrayPool) : base(name)
    {
        m_arrayPool = arrayPool;
        m_bindingFunction = EmptyBindingFunction;

        m_valuesToDraw = arrayPool.Rent(16);
        m_valuesToDraw.FillDefaults(string.Empty);

        m_selectedIndex = -1;
    }

    public string? Label { get; set; }

    public ObservableCollection<T>? Items
    {
        get => m_items;
        set
        {
            if (m_items != null)
            {
                m_items.CollectionChanged -= ItemsOnCollectionChanged;

                if (m_items.FirstOrDefault() is ObservableObject)
                {
                    for (int i = 0; i < m_items.Count; i++)
                        (m_items[i] as ObservableObject)!.PropertyChanged -= ItemOnPropertyChanged;
                }
            }

            if (value != null)
            {
                value.CollectionChanged += ItemsOnCollectionChanged;
                
                if (value.FirstOrDefault() is ObservableObject)
                {
                    for (int i = 0; i < value.Count; i++)
                        (value[i] as ObservableObject)!.PropertyChanged += ItemOnPropertyChanged;
                }

                if (m_valuesToDraw.Length < value.Count)
                    UpdateValuesArrayIfNeeded();
            }

            m_items = value;
            OnPropertyChanged();
            
            if (m_items != null)
                FillValuesArray(0, m_items.Count);
            
            SelectedIndex = -1;
        }
    }
    
    public Func<T, string> BindingFunction
    {
        get => m_bindingFunction;
        set
        {
            ArgumentNullException.ThrowIfNull(value, nameof(value));
            m_bindingFunction = value;
        }
    }

    public int SelectedIndex
    {
        get => m_selectedIndex;
        set
        {
            if (value == m_selectedIndex)
            {
                return;
            }

            if (value < -1 || value > (m_items?.Count ?? 0))
                throw new ArgumentOutOfRangeException("Selected index must be in range {-1;Items.Count-1}");

            var oldItem = SelectedItem;
            var oldIndex = m_selectedIndex;
            
            m_selectedIndex = value;
            
            SelectionChanged?.Invoke(this, new ValueChangedEventArgs<SelectionChangedArgs<T>>(
                new SelectionChangedArgs<T>(oldIndex, oldItem),
                new SelectionChangedArgs<T>(m_selectedIndex, SelectedItem)));
        }
    }

    public T? SelectedItem
    {
        get => m_selectedIndex == -1 || m_items == null || m_selectedIndex >= m_items.Count 
            ? default 
            : m_items[m_selectedIndex];
        set
        {
            if (value?.Equals(SelectedItem) ?? false)
            {
                return;
            }

            SelectedIndex = m_items?.IndexOf(value) ?? -1;
        }
    }

    public override void Draw()
    {
        if (!IsVisible || m_items == null)
            return;
        
        base.Draw();

        var index = m_selectedIndex;
        
        ImGui.ListBox(Label ?? Name, ref index, m_valuesToDraw, m_items!.Count, 5);

        SelectedIndex = index;
    }

    public void ClearSelection()
    {
        SelectedIndex = -1;
    }

    public void Dispose()
    {
        m_arrayPool.Return(m_valuesToDraw, true);
    }
    
    private void UpdateValuesArrayIfNeeded()
    {
        if (m_valuesToDraw.Length >= m_items!.Count) 
            return;
        
        m_arrayPool.Return(m_valuesToDraw, true);
        m_valuesToDraw = m_arrayPool.Rent(m_items.Count);
        
        m_valuesToDraw.FillDefaults(string.Empty);
    }

    private void FillValuesArray(int startIndex, int amount)
    {
        for (int i = 0; i < amount; i++)
        {
            m_valuesToDraw[startIndex + i] = BindingFunction(m_items![i]);
        }
    }
    
    private void ItemsOnCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        ObservableObject? item;
        switch (e.Action)
        {
            case NotifyCollectionChangedAction.Add:
                UpdateValuesArrayIfNeeded();

                item = e.NewItems[0] as ObservableObject;

                if (item != null)
                {
                    item.PropertyChanged -= ItemOnPropertyChanged;
                    item.PropertyChanged += ItemOnPropertyChanged;
                }
                
                m_valuesToDraw[e.NewStartingIndex] = BindingFunction((T)e.NewItems[0]);
                break;
            case NotifyCollectionChangedAction.Remove:
                if (m_items!.Count != e.OldStartingIndex) 
                {
                    FillValuesArray(e.OldStartingIndex, m_items.Count - e.OldStartingIndex);
                }

                m_valuesToDraw.FillDefaultsUntil(string.Empty, FillOption, m_items.Count);

                item = e.OldItems[0] as ObservableObject;

                if (item != null)
                    item.PropertyChanged -= ItemOnPropertyChanged;

                if (SelectedIndex >= Items.Count)
                {
                    SelectedIndex--;
                }

                break;
            case NotifyCollectionChangedAction.Replace:
                m_valuesToDraw[e.NewStartingIndex] = BindingFunction((T)e.NewItems[0]);
                m_valuesToDraw[e.OldStartingIndex] = BindingFunction((T)e.OldItems[0]);
                break;
        }
    }
    
    private void ItemOnPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (sender is not T item)
            return;

        var idx = m_items?.IndexOf(item) ?? -1;

        if (idx != -1)
        {
            m_valuesToDraw[idx] = BindingFunction(item);
        }
    }

    private static bool FillOption(string str) => str != string.Empty;
}