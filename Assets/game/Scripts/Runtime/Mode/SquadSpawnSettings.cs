using System;
using Shooter.Core;
using UnityEngine;

namespace Shooter.Runtime
{
    [Serializable]
    public sealed class SquadSpawnSettings
    {
        [SerializeField, Min(0f), Tooltip("いちばん近い敵がこの距離 (m) より近い味方の近くには戻さない")]
        private float _minEnemyDistance = SquadSpawnSpec.DefaultMinEnemyDistance;

        [SerializeField, Min(0f), Tooltip("撃たれてからこの秒数がたっていない味方の近くには戻さない")]
        private float _damageCooldownSeconds = SquadSpawnSpec.DefaultDamageCooldownSeconds;

        [SerializeField, Min(0.01f), Tooltip("味方の足元から、戻す位置までの水平距離 (m)。下の Clearance 以上にする")]
        private float _mateDistance = SquadSpawnSpec.DefaultMateDistance;

        [SerializeField, Min(0f), Tooltip("戻す位置から、ほかの体までこの距離 (m) は空ける")]
        private float _clearance = SquadSpawnSpec.DefaultClearance;

        [SerializeField, Min(1), Tooltip("味方のまわりに等間隔に並べて試す、戻す位置の数")]
        private int _slotCount = SquadSpawnSpec.DefaultSlotCount;

        public SquadSpawnSpec ToSpec()
        {
            return new SquadSpawnSpec(_minEnemyDistance, _damageCooldownSeconds, _mateDistance, _clearance, _slotCount);
        }

        public void CopyFrom(SquadSpawnSpec spec)
        {
            if (spec == null)
            {
                throw new ArgumentNullException(nameof(spec));
            }

            _minEnemyDistance = spec.MinEnemyDistance;
            _damageCooldownSeconds = spec.DamageCooldownSeconds;
            _mateDistance = spec.MateDistance;
            _clearance = spec.Clearance;
            _slotCount = spec.SlotCount;
        }
    }
}
