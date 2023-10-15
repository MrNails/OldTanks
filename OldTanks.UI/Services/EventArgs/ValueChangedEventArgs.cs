namespace OldTanks.UI.Services.EventArgs;

public readonly struct ValueChangedEventArgs<TValue> : IEquatable<ValueChangedEventArgs<TValue>>
{
    private readonly TValue m_oldValue;
    private readonly TValue m_newValue;

    public ValueChangedEventArgs(TValue oldValue, TValue newValue)
    {
        m_oldValue = oldValue;
        m_newValue = newValue;
    }

    public TValue OldValue => m_oldValue;
    public TValue NewValue => m_newValue;

    public bool Equals(ValueChangedEventArgs<TValue> other)
    {
        return Equals(other, EqualityComparer<TValue>.Default);
    }
    
    public bool Equals(ValueChangedEventArgs<TValue> other, IEqualityComparer<TValue> comparer)
    {
        return comparer.Equals(m_oldValue, other.m_oldValue) && 
               comparer.Equals(m_newValue, other.m_newValue);
    }

    public override bool Equals(object? obj)
    {
        return obj is ValueChangedEventArgs<TValue> other && Equals(other);
    }

    public override int GetHashCode()
    {
        return HashCode.Combine(m_oldValue, m_newValue);
    }

    public static bool operator ==(ValueChangedEventArgs<TValue> left, ValueChangedEventArgs<TValue> right)
    {
        return left.Equals(right);
    }

    public static bool operator !=(ValueChangedEventArgs<TValue> left, ValueChangedEventArgs<TValue> right)
    {
        return !left.Equals(right);
    }
}