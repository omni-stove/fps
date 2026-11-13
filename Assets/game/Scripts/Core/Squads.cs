using System;
using System.Collections.Generic;

namespace Fps.Core
{
    /// <summary>
    /// <see cref="SquadCount"/> 個の分隊 (ID 0 から SquadCount - 1 の陣営) に、1 隊 <see cref="SquadSize"/> 人までで分ける。
    /// joinIndex 番目に参加した人を joinIndex を隊の数で割った余りの隊に入れ、満員なら次の隊 (最後の隊の次は 0) へずらす。
    /// </summary>
    public sealed class Squads : ITeamLayout
    {
        private readonly Side[] _sides;

        /// <summary>分隊の数。2 以上。</summary>
        public int SquadCount { get; }

        /// <summary>1 隊の人数の上限。1 以上。</summary>
        public int SquadSize { get; }

        /// <param name="squadCount">分隊の数。2 以上。</param>
        /// <param name="squadSize">1 隊の人数の上限。1 以上。</param>
        public Squads(int squadCount, int squadSize)
        {
            if (squadCount < 2)
            {
                throw new ArgumentOutOfRangeException(nameof(squadCount), squadCount, "squadCount must be at least 2");
            }

            if (squadSize < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(squadSize), squadSize, "squadSize must be at least 1");
            }

            SquadCount = squadCount;
            SquadSize = squadSize;
            _sides = new Side[squadCount];
            for (var i = 0; i < squadCount; i++)
            {
                _sides[i] = new Side(i);
            }
        }

        public IReadOnlyList<Side> FixedSides => _sides;

        public int? Capacity => SquadSize;

        public bool Contains(Side side) => side.Id < SquadCount;

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

            var first = joinIndex % SquadCount;
            for (var offset = 0; offset < SquadCount; offset++)
            {
                var side = _sides[(first + offset) % SquadCount];
                if (memberCount(side) < SquadSize)
                {
                    return side;
                }
            }

            return null;
        }
    }
}
