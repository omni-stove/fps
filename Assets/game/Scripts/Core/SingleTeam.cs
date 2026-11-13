using System;
using System.Collections.Generic;

namespace Fps.Core
{
    /// <summary>
    /// 全員を 1 つの陣営 (<see cref="Side"/>) に入れるチーム分け。PvE のように、相手が参加者ではない (外から出てくる敵と戦う) モードで使う。
    /// 人数の上限はない。
    /// </summary>
    public sealed class SingleTeam : ITeamLayout
    {
        private readonly Side[] _sides;

        /// <param name="side">全員が入る陣営。</param>
        public SingleTeam(Side side)
        {
            _sides = new[] { side };
        }

        /// <summary>全員が入る陣営。</summary>
        public Side Side => _sides[0];

        public IReadOnlyList<Side> FixedSides => _sides;

        public int? Capacity => null;

        public bool Contains(Side side) => side == _sides[0];

        public Side? Assign(int joinIndex, Func<Side, int> memberCount)
        {
            if (joinIndex < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(joinIndex), joinIndex, "joinIndex must not be negative");
            }

            return _sides[0];
        }
    }
}
