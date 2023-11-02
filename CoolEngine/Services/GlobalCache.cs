using Serilog;

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

    private readonly ReaderWriterLockSlim m_cacheLock;
    private readonly Dictionary<string, T> m_cache;

    private GlobalCache()
    {
        m_cacheLock = new ReaderWriterLockSlim();
        m_cache = new Dictionary<string, T>();
    }

    public int Count
    {
        get
        {
            try
            {
                m_cacheLock.EnterReadLock();
                return m_cache.Count;
            }
            catch (Exception e)
            {
                Log.Warning(e, "Error attempting get amount of items.");
            }
            finally
            {
                m_cacheLock.ExitReadLock();
            }

            return -1;
        }
    }

    public bool AddOrUpdateItem(string name, T item)
    {
        try
        {
            m_cacheLock.EnterWriteLock();
            m_cache[name] = item;
            return true;
        }
        catch (Exception e)
        {
            Log.Warning(e, "Error attempting add or update item.");
        }
        finally
        {
            m_cacheLock.ExitWriteLock();
        }

        return false;
    }

    public bool RemoveItem(string name)
    {
        try
        {
            m_cacheLock.EnterWriteLock();
            return m_cache.Remove(name);
        }
        catch (Exception e)
        {
            Log.Warning(e, "Error attempting remove item.");
        }
        finally
        {
            m_cacheLock.ExitWriteLock();
        }

        return false;
    }

    public T? GetItemOrDefault(string name)
    {
        try
        {
            m_cacheLock.EnterReadLock();

            if (!m_cache.TryGetValue(name, out var item))
                item = default;

            return item;
        }
        catch (Exception e)
        {
            Log.Warning(e, "Error attempting get item.");
        }
        finally
        {
            m_cacheLock.ExitReadLock();
        }

        return default;
    }

    public async Task<bool> Dispose()
    {
        while (m_cacheLock.WaitingWriteCount > 0 ||
               m_cacheLock.WaitingReadCount > 0 ||
               m_cacheLock.WaitingUpgradeCount > 0)
        {
            await Task.Delay(100);
        }

        m_cacheLock.Dispose();
        m_cache.Clear();

        return true;
    }
}