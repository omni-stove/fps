using System;
using System.Collections.Generic;

namespace Shooter.Core
{
    public sealed class SingleTeam : ITeamLayout
    {
        private readonly Side[] _sides;

        public SingleTeam(Side side)
        {
            _sides = new[] { side };
        }

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
