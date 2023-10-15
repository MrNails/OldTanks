﻿namespace Common.Extensions;

public static class ICollectionExtensions
{
    public static void AddRange<T>(this ICollection<T> collection, IEnumerable<T> range)
    {
        foreach (var item in range)
            collection.Add(item);
    }
}