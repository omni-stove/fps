using System;
using Fps.Core;
using UnityEngine;

namespace Fps.Runtime
{
    public sealed partial class EnemyAgent
    {
        [Header("Shield")]
        [SerializeField, Tooltip("種類が Shield のときに使う値")]
        private EnemyShieldSettings _shieldSettings = new EnemyShieldSettings();

        [Header("Sniper")]
        [SerializeField, Tooltip("種類が Sniper のときに使う値")]
        private EnemySniperSettings _sniperSettings = new EnemySniperSettings();

        [Header("Charger")]
        [SerializeField, Tooltip("種類が Charger のときに使う値")]
        private EnemyChargerSettings _chargerSettings = new EnemyChargerSettings();

        [Header("Bomber")]
        [SerializeField, Tooltip("種類が Bomber のときに使う値")]
        private EnemyBomberSettings _bomberSettings = new EnemyBomberSettings();

        private bool _sprintAllowed = true;
        private float _gaitSpeedMultiplier = 1f;
        private float _shotDamageMultiplier = 1f;

        // 盾持ちのように追うときもダッシュさせない種類のため。種類ごとの部品 (EnemyShield など) が毎フレーム書く。
        public bool SprintAllowed
        {
            get => _sprintAllowed;
            set => _sprintAllowed = value;
        }

        // ボスの段階の倍率 (MoveSpeedMultiplier) とは別に持つ。同じ値を両方が書くと、段階の切り替えと種類の歩き方が互いを消し合う。
        public float GaitSpeedMultiplier
        {
            get => _gaitSpeedMultiplier;
            set
            {
                if (!(value > 0f) || float.IsInfinity(value))
                {
                    throw new ArgumentOutOfRangeException(nameof(value), value, "GaitSpeedMultiplier must be positive and finite");
                }

                _gaitSpeedMultiplier = value;
            }
        }

        // 狙撃兵の強い一発のため。武器の定義を種類ごとに増やさず、持たせた武器のダメージに掛ける。
        public float ShotDamageMultiplier
        {
            get => _shotDamageMultiplier;
            set
            {
                if (!(value > 0f) || float.IsInfinity(value))
                {
                    throw new ArgumentOutOfRangeException(nameof(value), value, "ShotDamageMultiplier must be positive and finite");
                }

                _shotDamageMultiplier = value;
            }
        }

        private float GaitWalkSpeed => _walkSpeed * _moveSpeedMultiplier * _gaitSpeedMultiplier;

        public static int ScaleDamage(int baseDamage, float multiplier)
        {
            if (baseDamage < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(baseDamage), baseDamage, "baseDamage must not be negative");
            }

            if (!(multiplier > 0f) || float.IsInfinity(multiplier))
            {
                throw new ArgumentOutOfRangeException(nameof(multiplier), multiplier, "multiplier must be positive and finite");
            }

            var scaled = Math.Round((double)baseDamage * multiplier, MidpointRounding.AwayFromZero);
            return (int)Math.Min(int.MaxValue, scaled);
        }

        private ShieldSpec CreateShieldSpec()
        {
            return _shieldSettings.ToSpec();
        }

        private SniperSpec CreateSniperSpec()
        {
            return _sniperSettings.ToSpec();
        }

        private ChargerSpec CreateChargerSpec()
        {
            return _chargerSettings.ToSpec();
        }

        private BomberSpec CreateBomberSpec()
        {
            return _bomberSettings.ToSpec();
        }

        private void CopyArchetypeSpecs(EnemySpec spec)
        {
            _shieldSettings.CopyFrom(spec.Shield);
            _sniperSettings.CopyFrom(spec.Sniper);
            _chargerSettings.CopyFrom(spec.Charger);
            _bomberSettings.CopyFrom(spec.Bomber);
        }
    }
}
