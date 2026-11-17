using System;
using System.Collections.Generic;

namespace Shooter.Core
{
    public interface ITeamLayout
    {
        IReadOnlyList<Side> FixedSides { get; }

        int? Capacity { get; }

        bool Contains(Side side);

        Side? Assign(int joinIndex, Func<Side, int> memberCount);
    }
}
