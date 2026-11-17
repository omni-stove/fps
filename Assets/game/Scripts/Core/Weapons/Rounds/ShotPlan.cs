using System;
using System.Collections.Generic;

namespace Shooter.Core
{
    public sealed class ShotPlan
    {
        public ShotPlan(RoundSpec round, int shotIndex, IReadOnlyList<PlannedRound> rounds)
        {
            Round = round ?? throw new ArgumentNullException(nameof(round));
            ShotIndex = shotIndex;
            Rounds = rounds ?? throw new ArgumentNullException(nameof(rounds));
            if (rounds.Count == 0)
            {
                throw new ArgumentException("rounds must not be empty", nameof(rounds));
            }
        }

        public RoundSpec Round { get; }

        public int ShotIndex { get; }

        public IReadOnlyList<PlannedRound> Rounds { get; }
    }
}
