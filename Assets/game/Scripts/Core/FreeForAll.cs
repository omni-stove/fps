using System;
using System.Collections.Generic;

namespace Shooter.Core
{
    public sealed class FreeForAll : ITeamLayout
    {
        private static readonly Side[] _none = Array.Empty<Side>();

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
