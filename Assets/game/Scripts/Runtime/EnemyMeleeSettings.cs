using System;
using Shooter.Core;
using UnityEngine;

namespace Shooter.Runtime
{
    // 処刑技・背後から倒す技の値は敵が出さないので並べず、MeleeSpec の既定のままにする。
    [Serializable]
    public sealed class EnemyMeleeSettings
    {
        [SerializeField, Min(1), Tooltip("1 回の当たりのダメージ")]
        private int _damage = MeleeSpec.DefaultDamage;

        [SerializeField, Min(0.01f), Tooltip("体の中心から相手の体の表面までの届く距離 (m)。これより近づいた目標を殴る")]
        private float _reach = MeleeSpec.DefaultReach;

        [SerializeField, Range(1f, MeleeSpec.MaxArcDegrees), Tooltip("前方の扇の全体の角度 (度)")]
        private float _arcDegrees = MeleeSpec.DefaultArcDegrees;

        [SerializeField, Min(0.01f), Tooltip("振りの秒数。この間は撃たない")]
        private float _swingSeconds = MeleeSpec.DefaultSwingSeconds;

        [SerializeField, Min(0f), Tooltip("振り終えてから次に振れるまでの秒数")]
        private float _cooldownSeconds = MeleeSpec.DefaultCooldownSeconds;

        [SerializeField, Min(0f), Tooltip("振り始めから当たりを判定するまでの秒数。振りの秒数以下")]
        private float _impactSeconds = MeleeSpec.DefaultImpactSeconds;

        [SerializeField, Min(0f), Tooltip("振るときに前へ踏み込む距離 (m)")]
        private float _lungeDistance = MeleeSpec.DefaultLungeDistance;

        public MeleeSpec ToSpec()
        {
            return new MeleeSpec(_damage, _reach, _arcDegrees, _swingSeconds, _cooldownSeconds, _impactSeconds, _lungeDistance);
        }

        public void CopyFrom(MeleeSpec spec)
        {
            if (spec == null)
            {
                throw new ArgumentNullException(nameof(spec));
            }

            _damage = spec.Damage;
            _reach = spec.Reach;
            _arcDegrees = spec.ArcDegrees;
            _swingSeconds = spec.SwingSeconds;
            _cooldownSeconds = spec.CooldownSeconds;
            _impactSeconds = spec.ImpactSeconds;
            _lungeDistance = spec.LungeDistance;
        }
    }
}
