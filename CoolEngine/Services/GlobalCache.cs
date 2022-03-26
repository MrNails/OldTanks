namespace CoolEngine.Services;

public static class GlobalCache<T>
{
    private static readonly ReaderWriterLockSlim s_cacheLock;
    private static readonly Dictionary<string, T> s_cache;

    static GlobalCache()
    {
        s_cacheLock = new ReaderWriterLockSlim();
        s_cache = new Dictionary<string, T>();
    }

    public static int Count
    {
        get
        {
            s_cacheLock.EnterReadLock();
            try
            {
                return s_cache.Count;
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
            }
            finally
            {
                s_cacheLock.ExitReadLock();
            }

            return -1;
        }
    }

    public static bool AddOrUpdateItem(string name, T item)
    {
        s_cacheLock.EnterWriteLock();
        try
        {
            s_cache[name] = item;
            return true;
        }
        catch (Exception e)
        {
            Console.WriteLine(e);
        }
        finally
        {
            s_cacheLock.ExitWriteLock();
        }

        return false;
    }

    public static bool RemoveItem(string name)
    {
        s_cacheLock.EnterWriteLock();
        try
        {
            return s_cache.Remove(name);
        }
        catch (Exception e)
        {
            Console.WriteLine(e);
        }
        finally
        {
            s_cacheLock.ExitWriteLock();
        }

        return false;
    }

    public static T? GetItemOrDefault(string name)
    {
        s_cacheLock.EnterReadLock();
        try
        {
            T item;
            if (!s_cache.TryGetValue(name, out item))
                item = default;

            return item;
        }
        catch (Exception e)
        {
            Console.WriteLine(e);
        }
        finally
        {
            s_cacheLock.ExitReadLock();
        }

        return default;
    }

    public static bool Dispose()
    {
        while (s_cacheLock.WaitingWriteCount > 0 ||
               s_cacheLock.WaitingReadCount > 0 ||
               s_cacheLock.WaitingUpgradeCount > 0)
        {
            Thread.Sleep(10);
        }
        
        s_cacheLock.Dispose();
        s_cache.Clear();

        return true;
    }
}