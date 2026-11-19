using System;
using System.Collections.Generic;

namespace Shooter.Core
{
    public static class DirectionalKeys
    {
        public static string Join(IReadOnlyList<string> keys)
        {
            if (keys == null)
            {
                throw new ArgumentNullException(nameof(keys));
            }

            var parts = new List<string>(keys.Count);
            var allSingle = true;
            foreach (var key in keys)
            {
                if (string.IsNullOrWhiteSpace(key))
                {
                    continue;
                }

                var trimmed = key.Trim();
                parts.Add(trimmed);
                allSingle &= trimmed.Length == 1;
            }

            return string.Join(allSingle ? string.Empty : "/", parts);
        }
    }
}
