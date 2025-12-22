using System.Collections.Generic;
using System.Linq;
using JetBrains.Annotations;

namespace SolastaUnfinishedBusiness.Api.LanguageExtensions;

internal static class ListExtensions
{
    internal static void AddRange<T>([NotNull] this List<T> list, params T[] range)
    {
        list.AddRange(range);
    }

    internal static void TryAddRange<T>([NotNull] this List<T> list, [NotNull] IEnumerable<T> range)
    {
        foreach (var item in range)
        {
            list.TryAdd(item);
        }
    }
    
    internal static void RemoveAll<T>([NotNull] this List<T> list, [NotNull] IEnumerable<T> range)
    {
        list.RemoveAll(range.Contains);
    }


    internal static void SetRange<T>([NotNull] this List<T> list, [NotNull] params T[] range)
    {
        list.Clear();
        list.AddRange(range);
    }

    internal static void SetRange<T>([NotNull] this List<T> list, [NotNull] IEnumerable<T> range)
    {
        list.Clear();
        list.AddRange(range);
    }
    
    internal static void AddRange<T>([NotNull] this HashSet<T> hash, params T[] range)
    {
        foreach (var item in range)
        {
            hash.Add(item);
        }
    }
    
    internal static void AddRange<T>([NotNull] this HashSet<T> hash, IEnumerable<T> items)
    {
        foreach (var item in items)
        {
            hash.Add(item);
        }
    }
}
