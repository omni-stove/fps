using System;
using Shooter.Core;
using UnityEngine;

namespace Shooter.Runtime
{
    [Serializable]
    public sealed class MeleeWeaponSettings
    {
        [SerializeField, Tooltip("近接武器にする。持っている間は撃たず、攻撃のボタンを離したときに振る")]
        private bool _enabled;

        [SerializeField, Min(1), Tooltip("ふつうの振りのダメージ")]
        private int _damage = MeleeWeaponSpec.DefaultSwingDamage;

        [SerializeField, Min(0.01f), Tooltip("体の中心から相手の体の表面までの届く距離 (m)")]
        private float _reach = MeleeWeaponSpec.DefaultSwingReach;

        [SerializeField, UnityEngine.Range(1f, MeleeSpec.MaxArcDegrees), Tooltip("前方の扇の全体の角度 (度)")]
        private float _arcDegrees = MeleeWeaponSpec.DefaultSwingArcDegrees;

        [SerializeField, Min(0.01f), Tooltip("振りの秒数")]
        private float _swingSeconds = MeleeWeaponSpec.DefaultSwingSeconds;

        [SerializeField, Min(0f), Tooltip("振り終えてから次に振れるまでの秒数")]
        private float _cooldownSeconds = MeleeWeaponSpec.DefaultSwingCooldownSeconds;

        [SerializeField, Min(0f), Tooltip("振り始めから当たりを判定するまでの秒数。振りの秒数以下")]
        private float _impactSeconds = MeleeWeaponSpec.DefaultSwingImpactSeconds;

        [SerializeField, Min(0f), Tooltip("振るときに前へ踏み込む距離 (m)")]
        private float _lungeDistance = MeleeWeaponSpec.DefaultSwingLungeDistance;

        [SerializeField, Tooltip("攻撃のボタンを長く押して溜め、強く振れるようにする")]
        private bool _canCharge;

        [SerializeField, Min(1), Tooltip("溜め切って振ったときのダメージ")]
        private int _heavyDamage = MeleeWeaponSpec.DefaultSwingDamage * 2;

        [SerializeField, Min(0.01f), Tooltip("溜め切って振ったときの届く距離 (m)")]
        private float _heavyReach = MeleeWeaponSpec.DefaultSwingReach;

        [SerializeField, UnityEngine.Range(1f, MeleeSpec.MaxArcDegrees), Tooltip("溜め切って振ったときの扇の全体の角度 (度)")]
        private float _heavyArcDegrees = MeleeWeaponSpec.DefaultSwingArcDegrees;

        [SerializeField, Min(0.01f), Tooltip("溜め切って振ったときの振りの秒数")]
        private float _heavySwingSeconds = MeleeWeaponSpec.DefaultSwingSeconds * 1.5f;

        [SerializeField, Min(0f), Tooltip("溜め切って振り終えてから次に振れるまでの秒数")]
        private float _heavyCooldownSeconds = MeleeWeaponSpec.DefaultSwingCooldownSeconds;

        [SerializeField, Min(0f), Tooltip("溜め切って振ったときに当たりを判定するまでの秒数。振りの秒数以下")]
        private float _heavyImpactSeconds = MeleeWeaponSpec.DefaultSwingImpactSeconds * 1.5f;

        [SerializeField, Min(0f), Tooltip("溜め切って振ったときに前へ踏み込む距離 (m)")]
        private float _heavyLungeDistance = MeleeWeaponSpec.DefaultSwingLungeDistance * 2f;

        [SerializeField, Min(0f), Tooltip("これより短く押して離したら溜めずに振る秒数")]
        private float _chargeDelaySeconds = MeleeWeaponSpec.DefaultChargeDelaySeconds;

        [SerializeField, Min(0.01f), Tooltip("溜め始めから溜め切るまでの秒数")]
        private float _chargeSeconds = MeleeWeaponSpec.DefaultChargeSeconds;

        [SerializeField, Tooltip("覗き込みのボタンで投げられるようにする。投げた武器は止まった所で拾える")]
        private bool _canThrow;

        [SerializeField, Min(1), Tooltip("投げて当てたときのダメージ")]
        private int _throwDamage = MeleeThrowSpec.DefaultDamage;

        [SerializeField, Min(0.01f), Tooltip("投げる速さ (m/s)")]
        private float _throwSpeed = MeleeThrowSpec.DefaultSpeed;

        [SerializeField, UnityEngine.Range(0f, 90f), Tooltip("照準より上へ起こして投げる角度 (度)")]
        private float _throwUpwardDegrees = MeleeThrowSpec.DefaultUpwardDegrees;

        [SerializeField, Min(0f), Tooltip("投げた武器が縦に回る速さ (度/秒)")]
        private float _throwSpinDegreesPerSecond = MeleeThrowSpec.DefaultSpinDegreesPerSecond;

        [SerializeField, Min(0.01f), Tooltip("投げた武器の当たり判定の球の半径 (m)")]
        private float _throwRadius = MeleeThrowSpec.DefaultRadius;

        [SerializeField, Min(0.01f), Tooltip("止まらなくてもこの秒数で拾える物に変える")]
        private float _throwMaxFlightSeconds = MeleeThrowSpec.DefaultMaxFlightSeconds;

        [SerializeField, Tooltip("投げて止まった武器の拾い方")]
        private PickupCollectMode _thrownCollectMode = PickupCollectMode.Touch;

        [SerializeField, Tooltip("投げた武器と、止まって拾える物になった武器の見た目。前が +Z。空なら細長い仮の箱")]
        private GameObject _thrownPrefab;

        public bool Enabled { get => _enabled; set => _enabled = value; }

        public bool CanCharge { get => _canCharge; set => _canCharge = value; }

        public bool CanThrow { get => _canThrow; set => _canThrow = value; }

        public PickupCollectMode ThrownCollectMode { get => _thrownCollectMode; set => _thrownCollectMode = value; }

        public GameObject ThrownPrefab { get => _thrownPrefab; set => _thrownPrefab = value; }

        public MeleeWeaponSpec ToSpec()
        {
            if (!_enabled)
            {
                return null;
            }

            var swing = new MeleeSpec(_damage, _reach, _arcDegrees, _swingSeconds, _cooldownSeconds, _impactSeconds, _lungeDistance);
            var heavy = _canCharge
                ? new MeleeSpec(_heavyDamage, _heavyReach, _heavyArcDegrees, _heavySwingSeconds, _heavyCooldownSeconds, _heavyImpactSeconds, _heavyLungeDistance)
                : null;
            var thrown = _canThrow
                ? new MeleeThrowSpec(_throwDamage, _throwSpeed, _throwUpwardDegrees, _throwSpinDegreesPerSecond, _throwRadius, _throwMaxFlightSeconds)
                : null;
            return new MeleeWeaponSpec(swing, heavy, _chargeDelaySeconds, _chargeSeconds, thrown);
        }

        // 拾い方と見た目 (ThrownCollectMode・ThrownPrefab) は spec に無いので、入っているものを残す。
        public void Set(MeleeWeaponSpec spec)
        {
            _enabled = spec != null;
            if (spec == null)
            {
                return;
            }

            _damage = spec.Swing.Damage;
            _reach = spec.Swing.Reach;
            _arcDegrees = spec.Swing.ArcDegrees;
            _swingSeconds = spec.Swing.SwingSeconds;
            _cooldownSeconds = spec.Swing.CooldownSeconds;
            _impactSeconds = spec.Swing.ImpactSeconds;
            _lungeDistance = spec.Swing.LungeDistance;
            _chargeDelaySeconds = spec.ChargeDelaySeconds;
            _chargeSeconds = spec.ChargeSeconds;

            _canCharge = spec.CanCharge;
            if (spec.CanCharge)
            {
                _heavyDamage = spec.HeavySwing.Damage;
                _heavyReach = spec.HeavySwing.Reach;
                _heavyArcDegrees = spec.HeavySwing.ArcDegrees;
                _heavySwingSeconds = spec.HeavySwing.SwingSeconds;
                _heavyCooldownSeconds = spec.HeavySwing.CooldownSeconds;
                _heavyImpactSeconds = spec.HeavySwing.ImpactSeconds;
                _heavyLungeDistance = spec.HeavySwing.LungeDistance;
            }

            _canThrow = spec.CanThrow;
            if (spec.CanThrow)
            {
                _throwDamage = spec.Throw.Damage;
                _throwSpeed = spec.Throw.Speed;
                _throwUpwardDegrees = spec.Throw.UpwardDegrees;
                _throwSpinDegreesPerSecond = spec.Throw.SpinDegreesPerSecond;
                _throwRadius = spec.Throw.Radius;
                _throwMaxFlightSeconds = spec.Throw.MaxFlightSeconds;
            }
        }
    }
}
