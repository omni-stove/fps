using System;
using System.Collections.Generic;

namespace Fps.Core
{
    /// <summary>
    /// 1 人 1 陣営のチーム分け。joinIndex 番目に参加した人を ID joinIndex の陣営に入れ、
    /// その陣営に誰かいれば (明示して入れた人がいれば) 次の空いている ID へずらす。
    /// </summary>
    public sealed class FreeForAll : ITeamLayout
    {
        private static readonly Side[] _none = new Side[0];

        public IReadOnlyList<Side> FixedSides => _none;

        public int? Capacity => 1;

        public bool Contains(Side side) => true;

        public Side? Assign(int joinIndex, Func<Side, int> memberCount)
        {
            if (joinIndex < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(joinIndex), joinIndex, "joinIndex must not be negative");
            }

            if (memberCount == null)
            {
                throw new ArgumentNullException(nameof(memberCount));
            }

            for (var id = joinIndex; id >= 0; id++)
            {
                var side = new Side(id);
                if (memberCount(side) == 0)
                {
                    return side;
                }
            }

            return null;
        }
    }
}
