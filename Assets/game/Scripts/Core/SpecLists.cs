using System;
using System.Collections.Generic;

namespace Shooter.Core
{
    // Id で引く一覧の検査。綴りの違う同じ Id を読み込みで止めるため、空・重なり・null をここでそろえて断る。
    internal static class SpecLists
    {
        public static string Id(string id, string paramName)
        {
            if (string.IsNullOrWhiteSpace(id))
            {
                throw new ArgumentException(paramName + " must not be empty", paramName);
            }

            return id.Trim();
        }

        public static T[] UniqueById<T>(IReadOnlyList<T> items, Func<T, string> idOf, string paramName)
            where T : class
        {
            var copy = new T[items.Count];
            var seen = new HashSet<string>(StringComparer.Ordinal);
            for (var i = 0; i < copy.Length; i++)
            {
                var item = items[i] ?? throw new ArgumentException(paramName + " must not contain null", paramName);
                var id = idOf(item);
                if (!seen.Add(id))
                {
                    throw new ArgumentException(paramName + " contains the id '" + id + "' twice", paramName);
                }

                copy[i] = item;
            }

            return copy;
        }

        public static T FindById<T>(IReadOnlyList<T> items, Func<T, string> idOf, string id)
            where T : class
        {
            if (id == null)
            {
                return null;
            }

            foreach (var item in items)
            {
                if (string.Equals(idOf(item), id, StringComparison.Ordinal))
                {
                    return item;
                }
            }

            return null;
        }
    }
}
