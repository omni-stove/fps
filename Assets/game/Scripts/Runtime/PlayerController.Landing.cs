using System;
using System.Globalization;
using Shooter.Core;
using UnityEngine;

namespace Shooter.Runtime
{
    public sealed partial class PlayerController
    {
        private FallDamage _fallDamage = FallDamage.Off;
        private Damageable _fallDamageTarget;

        public FallDamage FallDamage
        {
            get => _fallDamage;
            set
            {
                if (!Enum.IsDefined(typeof(FallDamage), value))
                {
                    throw new ArgumentOutOfRangeException(nameof(value), value, "value is not a defined FallDamage");
                }

                _fallDamage = value;
            }
        }

        public void ApplyRules(GameRules rules)
        {
            if (rules == null)
            {
                throw new ArgumentNullException(nameof(rules));
            }

            FallDamage = rules.FallDamage;
            Debug.Log(FormatFallDamageRuleLog(rules.FallDamage), this);
        }

        public static string FormatFallDamageRuleLog(FallDamage fallDamage)
        {
            return $"{LogTag} fallDamage={fallDamage}";
        }

        public static string FormatFallDamageLog(int damage, float fallSpeed, int health)
        {
            return LogTag
                + " fall damage=" + damage.ToString(CultureInfo.InvariantCulture)
                + " speed=" + fallSpeed.ToString("F2", CultureInfo.InvariantCulture)
                + " health=" + health.ToString(CultureInfo.InvariantCulture);
        }

        // 初期化直後の最初の接地は、空中から降りたのか分からないので着地として扱わない。
        private void OnLanded(float fallSpeed)
        {
            if (_fallDamage != FallDamage.On)
            {
                return;
            }

            var target = ResolveFallDamageTarget();
            if (target == null || target.IsDead)
            {
                return;
            }

            var health = target.Health;
            var damage = FallDamageCalculator.Calculate(fallSpeed, _motor.Spec, health.Max, IsLandingInWater());
            if (damage <= 0)
            {
                return;
            }

            // 帰属 (CombatEvents.CurrentAttribution) を付けずに与えるので、Damageable が自滅としてキルログに出す
            target.TakeDamage(damage);
            Debug.Log(FormatFallDamageLog(damage, fallSpeed, target.Health.Current), this);
        }

        // Damageable はプレイヤーの組み立てで PlayerController より後に付くので、初めて要るときに探す
        private Damageable ResolveFallDamageTarget()
        {
            if (_fallDamageTarget == null)
            {
                _fallDamageTarget = GetComponent<Damageable>();
            }

            return _fallDamageTarget;
        }

        // 接地したフレームの PlayerMotor.Velocity は、まだ接地中の吸着の速度に戻る前の落下速度を持っている
        private void ReportLanding(bool grounded)
        {
            if (_lastGrounded == false && grounded)
            {
                var fallSpeed = Mathf.Max(0f, -_motor.Velocity.Y);
                CombatEvents.RaiseLanded(gameObject, fallSpeed);
                OnLanded(fallSpeed);
            }
        }
    }
}
