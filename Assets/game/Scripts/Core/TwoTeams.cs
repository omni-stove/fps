using System;
using System.Collections.Generic;

namespace Shooter.Core
{
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
