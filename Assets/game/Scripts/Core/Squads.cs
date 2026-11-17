using System;
using System.Collections.Generic;

namespace Shooter.Core
{
    public sealed class Squads : ITeamLayout
    {
        private readonly Side[] _sides;

        public int SquadCount { get; }

        public int SquadSize { get; }

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
