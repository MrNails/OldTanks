namespace Common.Extensions;

public static class ArrayExtensions
{
    public static void FillDefaultsUntil<T>(this T[] array, T defaultValue, Func<T, bool> fillFilter, int startIndex = 0)
    {
        for (int i = startIndex; i < array.Length; i++)
        {
            if (fillFilter(array[i]))
                array[i] = defaultValue;
        }
    }
    
    public static void FillDefaults<T>(this T[] array, T defaultValue, int startIndex = 0)
    {
        for (int i = startIndex; i < array.Length; i++)
        {
            array[i] = defaultValue;
        }
    }

    public static bool Contains<T>(this T[] array, T element) where T: notnull
    {
        for (int i = 0; i < array.Length; i++)
        {
            if (array[i].Equals(element))
            {
                return true;
            }
        }

        return false;
    }
}