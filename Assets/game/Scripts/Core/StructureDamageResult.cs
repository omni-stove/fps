using System;
using System.Collections.Generic;

namespace Fps.Core
{
    /// <summary><see cref="StructureIntegrity.ApplyDamage"/> で壊れた部品。</summary>
    public sealed class StructureDamageResult
    {
        /// <summary>何も壊れなかったときの結果。</summary>
        public static StructureDamageResult None { get; } = new StructureDamageResult(Array.Empty<int>(), Array.Empty<int>());

        /// <summary>ダメージで耐久値が 0 になった部品の添字。昇順。</summary>
        public IReadOnlyList<int> Destroyed { get; }

        /// <summary>地面から切り離されて崩落した部品の添字。昇順。<see cref="Destroyed"/> とは重ならない。</summary>
        public IReadOnlyList<int> Collapsed { get; }

        /// <summary>1 つでも部品が壊れたか。</summary>
        public bool Any => Destroyed.Count > 0 || Collapsed.Count > 0;

        internal StructureDamageResult(int[] destroyed, int[] collapsed)
        {
            Destroyed = Array.AsReadOnly(destroyed);
            Collapsed = Array.AsReadOnly(collapsed);
        }
    }
}
