using System.Globalization;
using Fps.Core;
using UnityEngine;

namespace Fps.Net
{
    /// <summary>
    /// <see cref="NetworkPlayerMotor"/> のうち、サーバーで確定した移動の着地 (空中から接地に変わった tick) の処理。
    /// 試合のルールの <see cref="GameRules.FallDamage"/> が On なら、着地の速さから <see cref="FallDamageCalculator"/> で落下ダメージを求め、
    /// 攻撃者なしで <see cref="NetTeamDeathmatch.ApplyDamage"/> に渡す (倒れたら自滅として数える)。
    /// </summary>
    public sealed partial class NetworkPlayerMotor
    {
        private NetPlayer _fallDamageVictim;

        /// <summary>
        /// 落下ダメージを与えたときのログ。例: <c>[Net] fall owner=1 tick=42 speed=17.90 damage=45 outcome=Damaged</c>。
        /// </summary>
        public static string FormatFallDamageLog(ulong ownerClientId, int tick, float fallSpeed, int damage, DamageOutcome outcome)
        {
            return "[Net] fall owner=" + ownerClientId.ToString(CultureInfo.InvariantCulture)
                + " tick=" + tick.ToString(CultureInfo.InvariantCulture)
                + " speed=" + fallSpeed.ToString("F2", CultureInfo.InvariantCulture)
                + " damage=" + damage.ToString(CultureInfo.InvariantCulture)
                + " outcome=" + outcome;
        }

        /// <summary>
        /// サーバー (ホスト自身のプレイヤーを含む) で、確定した tick の移動で着地したときに呼ばれる。
        /// fallSpeed は着地したときの下向きの速さ (m/s、0 以上)。オーナーの予測と再シミュレーションでは呼ばれない。
        /// 出現・テレポートの直後は接地していない状態から始まるので、最初の接地でもごく小さい fallSpeed で呼ばれる
        /// (<see cref="MotorSpec.SafeFallSpeed"/> 以下なのでダメージにはならない)。
        /// </summary>
        private void OnServerLanded(int tick, float fallSpeed)
        {
            var match = NetTeamDeathmatch.Current;
            if (match == null)
            {
                return;
            }

            var rules = match.ServerRules;
            if (rules == null || rules.FallDamage != FallDamage.On)
            {
                return;
            }

            var damage = FallDamageCalculator.Calculate(fallSpeed, _simulation.Spec, match.ServerMaxHealth);
            if (damage <= 0)
            {
                return;
            }

            if (_fallDamageVictim == null)
            {
                _fallDamageVictim = GetComponent<NetPlayer>();
                if (_fallDamageVictim == null)
                {
                    WarnThrottled("[Net] " + name + " landed hard but has no NetPlayer to take fall damage");
                    return;
                }
            }

            var outcome = match.ApplyDamage(null, _fallDamageVictim, damage);
            if (outcome != DamageOutcome.Ignored)
            {
                Debug.Log(FormatFallDamageLog(OwnerClientId, tick, fallSpeed, damage, outcome), this);
            }
        }

        // 着地した tick の確定状態の速度は、まだ接地中の吸着の速度に戻る前の落下速度を持っている
        private void ReportServerLanding(int tick, bool wasGrounded, MotorState state)
        {
            if (!wasGrounded && state.IsGrounded)
            {
                OnServerLanded(tick, Mathf.Max(0f, -state.Velocity.y));
            }
        }
    }
}
