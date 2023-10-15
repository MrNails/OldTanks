namespace Common.Extensions;

public static class DictionaryExtensions
{
    /// <summary>
    /// Trying retrieve value by given key from <see cref="Dictionary{TKey,TValue}"/>
    /// and create new with adding to <see cref="Dictionary{TKey,TValue}"/> if not exists.
    /// </summary>
    /// <param name="dictionary">Dictionary that will be used</param>
    /// <param name="key">Key</param>
    /// <typeparam name="TKey">Key parameter</typeparam>
    /// <typeparam name="TValue">Value parameter</typeparam>
    /// <returns>Exists value from dictionary; or new if not exists</returns>
    public static TValue TryGetAndAddIfNotExists<TKey, TValue>(this Dictionary<TKey, TValue> dictionary, TKey key) 
        where TValue : class, new()
    {
        if (!dictionary.TryGetValue(key, out var value))
        {
            value = new TValue();
            dictionary.Add(key, value);
        }

        return value;
    }
}