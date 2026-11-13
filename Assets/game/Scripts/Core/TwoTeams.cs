using System;
using System.Collections.Generic;

namespace Fps.Core
{
    /// <summary>
    /// <see cref="Side.Alpha"/> と <see cref="Side.Bravo"/> の 2 陣営に参加順で交互に分ける (<see cref="TeamAssigner"/> と同じ順)。
    /// 人数の上限はなく、抜けた人がいても順番は戻さない。
    /// </summary>
    public sealed class TwoTeams : ITeamLayout
    {
        private static readonly Side[] _sides = { Side.Alpha, Side.Bravo };

        public IReadOnlyList<Side> FixedSides => _sides;

        public int? Capacity => null;

        public bool Contains(Side side) => side.IsTeam;

        public Side? Assign(int joinIndex, Func<Side, int> memberCount)
        {
            if (joinIndex < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(joinIndex), joinIndex, "joinIndex must not be negative");
            }

            return _sides[joinIndex % _sides.Length];
        }
    }
}
