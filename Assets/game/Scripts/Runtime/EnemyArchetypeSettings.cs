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

    [Serializable]
    public sealed class EnemyChargerSettings
    {
        [SerializeField, Min(0.01f), Tooltip("目標がこの距離 (m) の内に見えたら構え始める。突進の距離以下")]
        private float _triggerDistance = ChargerSpec.DefaultTriggerDistance;

        [SerializeField, Min(0.01f), Tooltip("構えを見せてから突っ込むまでの秒数")]
        private float _windupSeconds = ChargerSpec.DefaultWindupSeconds;

        [SerializeField, Min(0.01f), Tooltip("突進の速さ (m/s)")]
        private float _chargeSpeed = ChargerSpec.DefaultChargeSpeed;

        [SerializeField, Min(0.01f), Tooltip("1 回の突進で走る距離 (m)")]
        private float _chargeDistance = ChargerSpec.DefaultChargeDistance;

        [SerializeField, Min(0.01f), Tooltip("体の中心から相手の体までがこの距離 (m) の内なら当たる。自分の体の半径を含める")]
        private float _hitReach = ChargerSpec.DefaultHitReach;

        [SerializeField, Min(1), Tooltip("当たった相手に与えるダメージ")]
        private int _impactDamage = ChargerSpec.DefaultImpactDamage;

        [SerializeField, Min(0f), Tooltip("外れて壁などにぶつかったあと止まっている秒数")]
        private float _stunSeconds = ChargerSpec.DefaultStunSeconds;

        [SerializeField, Min(0f), Tooltip("当てたあと次に動き出すまでの秒数")]
        private float _recoverSeconds = ChargerSpec.DefaultRecoverSeconds;

        /// <exception cref="ArgumentException">値が不正。</exception>
        public ChargerSpec ToSpec()
        {
            return new ChargerSpec(
                _triggerDistance, _windupSeconds, _chargeSpeed, _chargeDistance, _hitReach, _impactDamage, _stunSeconds, _recoverSeconds);
        }

        public void CopyFrom(ChargerSpec spec)
        {
            if (spec == null)
            {
                throw new ArgumentNullException(nameof(spec));
            }

            _triggerDistance = spec.TriggerDistance;
            _windupSeconds = spec.WindupSeconds;
            _chargeSpeed = spec.ChargeSpeed;
            _chargeDistance = spec.ChargeDistance;
            _hitReach = spec.HitReach;
            _impactDamage = spec.ImpactDamage;
            _stunSeconds = spec.StunSeconds;
            _recoverSeconds = spec.RecoverSeconds;
        }
    }

    [Serializable]
    public sealed class EnemyBomberSettings
    {
        [SerializeField, Min(0.01f), Tooltip("目標がこの距離 (m) の内に見えたら合図を始める。爆発の半径未満")]
        private float _triggerDistance = BomberSpec.DefaultTriggerDistance;

        [SerializeField, Min(0.01f), Tooltip("合図を始めてから爆発するまでの秒数")]
        private float _fuseSeconds = BomberSpec.DefaultFuseSeconds;

        [SerializeField, Min(0.01f), Tooltip("近づくときの、歩きの速さに掛ける倍率")]
        private float _runSpeedMultiplier = BomberSpec.DefaultRunSpeedMultiplier;

        [SerializeField, Min(0), Tooltip("爆発の中心でのダメージ")]
        private int _blastDamage = BomberSpec.DefaultBlastDamage;

        [SerializeField, Min(0.01f), Tooltip("爆発が届く半径 (m)")]
        private float _blastRadius = BomberSpec.DefaultBlastRadius;

        [SerializeField, Min(0f), Tooltip("減らさずにダメージを与える半径 (m)。爆発の半径未満")]
        private float _blastFullDamageRadius = BomberSpec.DefaultBlastFullDamageRadius;

        [SerializeField, Min(0), Tooltip("合図の前に倒されたときの小さな爆発の、中心でのダメージ。爆発のダメージ以下")]
        private int _deathBlastDamage = BomberSpec.DefaultDeathBlastDamage;

        [SerializeField, Min(0.01f), Tooltip("合図の前に倒されたときの小さな爆発が届く半径 (m)。爆発の半径以下")]
        private float _deathBlastRadius = BomberSpec.DefaultDeathBlastRadius;

        [SerializeField, Min(0f), Tooltip("小さな爆発の、減らさずにダメージを与える半径 (m)")]
        private float _deathBlastFullDamageRadius = BomberSpec.DefaultDeathBlastFullDamageRadius;

        /// <exception cref="ArgumentException">値が不正。</exception>
        public BomberSpec ToSpec()
        {
            return new BomberSpec(
                _triggerDistance,
                _fuseSeconds,
                _runSpeedMultiplier,
                new ExplosionDamage(_blastDamage, _blastRadius, _blastFullDamageRadius),
                new ExplosionDamage(_deathBlastDamage, _deathBlastRadius, _deathBlastFullDamageRadius));
        }

        public void CopyFrom(BomberSpec spec)
        {
            if (spec == null)
            {
                throw new ArgumentNullException(nameof(spec));
            }

            _triggerDistance = spec.TriggerDistance;
            _fuseSeconds = spec.FuseSeconds;
            _runSpeedMultiplier = spec.RunSpeedMultiplier;
            _blastDamage = spec.Blast.MaxDamage;
            _blastRadius = spec.Blast.Radius;
            _blastFullDamageRadius = spec.Blast.FullDamageRadius;
            _deathBlastDamage = spec.DeathBlast.MaxDamage;
            _deathBlastRadius = spec.DeathBlast.Radius;
            _deathBlastFullDamageRadius = spec.DeathBlast.FullDamageRadius;
        }
    }
}
