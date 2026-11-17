using System;

namespace Fps.Core
{
    /// <summary>プレイヤー (<see cref="Player"/>) と敵 (<see cref="Enemy"/>) のよろめきの値の組。<see cref="StaggerJson"/> が読む。</summary>
    public sealed class StaggerTuning
    {
        public static readonly StaggerTuning Default = new StaggerTuning(StaggerSpec.PlayerDefault, StaggerSpec.EnemyDefault);

        /// <exception cref="ArgumentNullException">player か enemy が null。</exception>
        public StaggerTuning(StaggerSpec player, StaggerSpec enemy)
        {
            Player = player ?? throw new ArgumentNullException(nameof(player));
            Enemy = enemy ?? throw new ArgumentNullException(nameof(enemy));
        }

        public StaggerSpec Player { get; }

        public StaggerSpec Enemy { get; }
    }
}
