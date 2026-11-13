using System;
using System.Globalization;
using Fps.Core;
using UnityEngine;

namespace Fps.Runtime
{
    /// <summary>
    /// <see cref="PlayerController"/> のうち、空中から接地に変わった (着地した) ときの処理。
    /// <see cref="FallDamage"/> が On なら、着地の速さから <see cref="FallDamageCalculator"/> で落下ダメージを求め、
    /// 同じ GameObject の <see cref="Damageable"/> に与える。撃った者はいないので、倒れたらキルログには自滅として出る。
    /// </summary>
    public sealed partial class PlayerController
    {
        private FallDamage _fallDamage = FallDamage.Off;
        private Damageable _fallDamageTarget;

        /// <summary>
        /// 落下ダメージを受けるか。既定は Off。オフラインのルールを読む <see cref="GameBootstrap"/> と <see cref="CampaignRunner"/> が
        /// <see cref="ApplyRules"/> で決める (シーンには保存しない)。
        /// </summary>
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

        /// <summary>rules のうち移動に関わるもの (<see cref="GameRules.FallDamage"/>) を効かせる。</summary>
        public void ApplyRules(GameRules rules)
        {
            if (rules == null)
            {
                throw new ArgumentNullException(nameof(rules));
            }

            FallDamage = rules.FallDamage;
            Debug.Log(FormatFallDamageRuleLog(rules.FallDamage), this);
        }

        /// <summary>落下ダメージのルールのログ。例: <c>[Player] fallDamage=On</c>。</summary>
        public static string FormatFallDamageRuleLog(FallDamage fallDamage)
        {
            return $"{LogTag} fallDamage={fallDamage}";
        }

        /// <summary>落下ダメージを与えたときのログ。例: <c>[Player] fall damage=42 speed=17.90 health=58</c>。</summary>
        public static string FormatFallDamageLog(int damage, float fallSpeed, int health)
        {
            return LogTag
                + " fall damage=" + damage.ToString(CultureInfo.InvariantCulture)
                + " speed=" + fallSpeed.ToString("F2", CultureInfo.InvariantCulture)
                + " health=" + health.ToString(CultureInfo.InvariantCulture);
        }

        /// <summary>
        /// 着地した <see cref="Tick"/> で呼ばれる。fallSpeed は着地したときの下向きの速さ (m/s、0 以上)。
        /// 初期化直後の最初の接地は、空中から降りたのか分からないので着地として扱わない。
        /// </summary>
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
            var damage = FallDamageCalculator.Calculate(fallSpeed, _motor.Spec, health.Max);
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
                OnLanded(Mathf.Max(0f, -_motor.Velocity.Y));
            }
        }
    }
}
