using System;

namespace Shooter.Core
{
    public sealed class StaggerTuning
    {
        public static readonly StaggerTuning Default = new StaggerTuning(StaggerSpec.PlayerDefault, StaggerSpec.EnemyDefault);

        public StaggerTuning(StaggerSpec player, StaggerSpec enemy)
        {
            Player = player ?? throw new ArgumentNullException(nameof(player));
            Enemy = enemy ?? throw new ArgumentNullException(nameof(enemy));
        }

        public StaggerSpec Player { get; }

        public StaggerSpec Enemy { get; }
    }
}
