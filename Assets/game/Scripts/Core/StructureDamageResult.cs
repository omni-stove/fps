using System;
using System.Collections.Generic;

namespace Shooter.Core
{
    public sealed class StructureDamageResult
    {
        public static StructureDamageResult None { get; } = new StructureDamageResult(Array.Empty<int>(), Array.Empty<int>());

        public IReadOnlyList<int> Destroyed { get; }

        public IReadOnlyList<int> Collapsed { get; }

        public bool Any => Destroyed.Count > 0 || Collapsed.Count > 0;

        internal StructureDamageResult(int[] destroyed, int[] collapsed)
        {
            Destroyed = Array.AsReadOnly(destroyed);
            Collapsed = Array.AsReadOnly(collapsed);
        }
    }
}
