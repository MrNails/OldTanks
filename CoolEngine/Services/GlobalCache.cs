using System.Collections.Concurrent;

namespace CoolEngine.Services;

public class GlobalCache<T>
{
    private static GlobalCache<T>? s_instance;

    public static GlobalCache<T> Default
    {
        get
        {
            lock (typeof(GlobalCache<T>))
            {
                s_instance ??= new GlobalCache<T>();
            }

            return s_instance;
        }
    }

    private readonly ConcurrentDictionary<string, T> m_cache;

    private GlobalCache()
    {
        m_cache = new ConcurrentDictionary<string, T>();
    }

    public int Count => m_cache.Count;
    
    public bool AddOrUpdateItem(string name, T item)
    {
        m_cache[name] = item;
        return true;
    }

    public bool RemoveItem(string name)
    {
        return m_cache.Remove(name, out _);
    }

    public T? GetItemOrDefault(string name)
    {
        m_cache.TryGetValue(name, out var item);

        return item;
    }

    public void Dispose()
    {
        m_cache.Clear();
    }
}