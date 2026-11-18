using System;
using Shooter.Core;
using UnityEngine;

namespace Shooter.Runtime
{
    public sealed partial class WeaponController
    {
        public const float SpreadPerShot = 0.25f;

        public const float SpreadRecoveryDelaySeconds = 0.15f;

        public const float SpreadRecoveryPerSecond = 2f;

        private float _spread;
        private float _secondsSinceShot;

        public Func<float> AdsProgressSource { get; set; }

        public Func<float> MoveFractionSource { get; set; }

        public Func<float> SpreadScaleSource { get; set; }

        public float SpreadScale
        {
            get
            {
                var scale = SpreadScaleSource != null ? SpreadScaleSource() : 1f;
                return float.IsNaN(scale) ? 1f : Mathf.Clamp01(scale);
            }
        }

        public float SpreadFraction => ModifySpreadFraction(_spread);

        public float CrosshairSpreadFraction => Mathf.Clamp01(SpreadFraction * SpreadScale);

        public float CrosshairConeDegrees
        {
            get
            {
                var weapon = Weapon;
                if (weapon == null)
                {
                    return 0f;
                }

                var ads = AdsProgressSource != null ? AdsProgressSource() : 0f;
                var move = MoveFractionSource != null ? MoveFractionSource() : 0f;
                // HUD が毎フレーム読むので、源が NaN を返したフレームに CurrentConeDegrees の例外で HUD を止めない。
                if (float.IsNaN(ads) || float.IsNaN(move))
                {
                    return 0f;
                }

                var cone = CurrentConeDegrees(weapon.Spec) + weapon.AmmoTypes.Round.PelletSpreadDegrees;
                return Mathf.Clamp(cone, 0f, SpreadPattern.MaxDegrees);
            }
        }

        public Vector3 ApplySpread(WeaponSpec spec, Vector3 direction, int shotIndex)
        {
            if (spec == null)
            {
                throw new ArgumentNullException(nameof(spec));
            }

            var cone = CurrentConeDegrees(spec);
            if (cone <= 0f)
            {
                return direction;
            }

            return SpreadPattern.Direction(direction.ToNumerics(), cone, PlayerId, shotIndex).ToUnity();
        }

        public float CurrentConeDegrees(WeaponSpec spec)
        {
            if (spec == null)
            {
                throw new ArgumentNullException(nameof(spec));
            }

            var ads = AdsProgressSource != null ? AdsProgressSource() : 0f;
            var move = MoveFractionSource != null ? MoveFractionSource() : 0f;
            var cone = SpreadPattern.ConeDegrees(spec, ads, move) * SpreadScale * ModifierSpreadMultiplier();
            if (_modifiers.Count == 0)
            {
                return cone;
            }

            // HUD が毎フレーム読むので、写しを作らずに回す (modifier の中で足し外しはしない約束)。
            for (var i = 0; i < _modifiers.Count; i++)
            {
                cone = _modifiers[i].ModifyConeDegrees(spec, cone);
            }

            // NaN を返す modifier があっても、SpreadPattern.Direction に渡して例外にしない。
            return float.IsNaN(cone) ? 0f : Mathf.Clamp(cone, 0f, SpreadPattern.MaxDegrees);
        }

        private float ModifySpreadFraction(float spread)
        {
            if (_modifiers.Count == 0)
            {
                return spread;
            }

            for (var i = 0; i < _modifiers.Count; i++)
            {
                spread = _modifiers[i].ModifySpreadFraction(spread);
            }

            return float.IsNaN(spread) ? 0f : Mathf.Clamp01(spread);
        }

        private void KickSpread()
        {
            _spread = Mathf.Min(1f, _spread + SpreadPerShot);
            _secondsSinceShot = 0f;
        }

        private void RecoverSpread(float deltaSeconds)
        {
            var waiting = Mathf.Max(0f, SpreadRecoveryDelaySeconds - _secondsSinceShot);
            var recovering = Mathf.Max(0f, deltaSeconds - waiting);
            _secondsSinceShot = Mathf.Min(SpreadRecoveryDelaySeconds, _secondsSinceShot + deltaSeconds);
            _spread = Mathf.Max(0f, _spread - (recovering * SpreadRecoveryPerSecond));
        }
    }
}
