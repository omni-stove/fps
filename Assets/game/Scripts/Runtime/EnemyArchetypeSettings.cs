using System;
using Fps.Core;
using UnityEngine;

namespace Fps.Runtime
{
    // EnemyAgent の Inspector に並べる値。EnemyAgent のほかの判断の値と同じく、ここから EnemySpec を作り、ApplySpec で書き戻す。
    [Serializable]
    public sealed class EnemyShieldSettings
    {
        [SerializeField, Range(0.01f, 1f), Tooltip("盾を構えて近づくときの、歩きの速さに掛ける倍率")]
        private float _approachSpeedMultiplier = ShieldSpec.DefaultApproachSpeedMultiplier;

        [SerializeField, Min(0.01f), Tooltip("目標にこの距離 (m) まで近づいたら止まる。殴りの届く距離以下")]
        private float _holdDistance = ShieldSpec.DefaultHoldDistance;

        [SerializeField, Min(0f), Tooltip("近接攻撃を受けてから盾を下ろしている秒数")]
        private float _meleeLowerSeconds = ShieldSpec.DefaultMeleeLowerSeconds;

        [SerializeField, Min(0f), Tooltip("爆発を受けてから盾を下ろしている秒数")]
        private float _explosionLowerSeconds = ShieldSpec.DefaultExplosionLowerSeconds;

        [SerializeField, Min(1), Tooltip("盾ごと殴る一撃のダメージ")]
        private int _bashDamage = ShieldSpec.DefaultBashDamage;

        [SerializeField, Min(0.01f), Tooltip("殴りの届く距離 (m)")]
        private float _bashReach = ShieldSpec.DefaultBashReach;

        [SerializeField, Range(1f, MeleeSpec.MaxArcDegrees), Tooltip("殴りの前方の扇の全体の角度 (度)")]
        private float _bashArcDegrees = ShieldSpec.DefaultBashArcDegrees;

        [SerializeField, Min(0.01f), Tooltip("殴りの振りの秒数")]
        private float _bashSwingSeconds = ShieldSpec.DefaultBashSwingSeconds;

        [SerializeField, Min(0f), Tooltip("振り終えてから次に殴れるまでの秒数")]
        private float _bashCooldownSeconds = ShieldSpec.DefaultBashCooldownSeconds;

        [SerializeField, Min(0f), Tooltip("振り始めから当たりを判定するまでの秒数。振りの秒数以下")]
        private float _bashImpactSeconds = ShieldSpec.DefaultBashImpactSeconds;

        [SerializeField, Min(0f), Tooltip("殴るときに前へ踏み込む距離 (m)")]
        private float _bashLungeDistance = ShieldSpec.DefaultBashLungeDistance;

        /// <exception cref="ArgumentException">値が不正。</exception>
        public ShieldSpec ToSpec()
        {
            var bash = new MeleeSpec(
                _bashDamage, _bashReach, _bashArcDegrees, _bashSwingSeconds, _bashCooldownSeconds, _bashImpactSeconds, _bashLungeDistance);
            return new ShieldSpec(_approachSpeedMultiplier, _holdDistance, _meleeLowerSeconds, _explosionLowerSeconds, bash);
        }

        public void CopyFrom(ShieldSpec spec)
        {
            if (spec == null)
            {
                throw new ArgumentNullException(nameof(spec));
            }

            _approachSpeedMultiplier = spec.ApproachSpeedMultiplier;
            _holdDistance = spec.HoldDistance;
            _meleeLowerSeconds = spec.MeleeLowerSeconds;
            _explosionLowerSeconds = spec.ExplosionLowerSeconds;
            _bashDamage = spec.Bash.Damage;
            _bashReach = spec.Bash.Reach;
            _bashArcDegrees = spec.Bash.ArcDegrees;
            _bashSwingSeconds = spec.Bash.SwingSeconds;
            _bashCooldownSeconds = spec.Bash.CooldownSeconds;
            _bashImpactSeconds = spec.Bash.ImpactSeconds;
            _bashLungeDistance = spec.Bash.LungeDistance;
        }
    }

    [Serializable]
    public sealed class EnemySniperSettings
    {
        [SerializeField, Min(0.01f), Tooltip("照準の光線を見せてから撃つまでの秒数")]
        private float _aimSeconds = SniperSpec.DefaultAimSeconds;

        [SerializeField, Min(0.01f), Tooltip("狙った一発の、武器のダメージに掛ける倍率")]
        private float _shotDamageMultiplier = SniperSpec.DefaultShotDamageMultiplier;

        [SerializeField, Min(0f), Tooltip("次の位置に着いてから、また狙い始めるまでの秒数")]
        private float _reaimDelaySeconds = SniperSpec.DefaultReaimDelaySeconds;

        [SerializeField, Min(0.01f), Tooltip("次の位置へ向かってこれだけ経っても着かなければ、その場から狙い直す (秒)")]
        private float _relocateGiveUpSeconds = SniperSpec.DefaultRelocateGiveUpSeconds;

        /// <exception cref="ArgumentException">値が不正。</exception>
        public SniperSpec ToSpec()
        {
            return new SniperSpec(_aimSeconds, _shotDamageMultiplier, _reaimDelaySeconds, _relocateGiveUpSeconds);
        }

        public void CopyFrom(SniperSpec spec)
        {
            if (spec == null)
            {
                throw new ArgumentNullException(nameof(spec));
            }

            _aimSeconds = spec.AimSeconds;
            _shotDamageMultiplier = spec.ShotDamageMultiplier;
            _reaimDelaySeconds = spec.ReaimDelaySeconds;
            _relocateGiveUpSeconds = spec.RelocateGiveUpSeconds;
        }
    }
}
