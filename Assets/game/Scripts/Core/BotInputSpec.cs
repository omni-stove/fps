using System;

namespace Fps.Core
{
    /// <summary>
    /// Bot の入力変換 (<see cref="BotDriver"/>) の設定。角度は度、距離はメートル、時間は秒。
    /// 人と同じ移動・射撃を通すので、強さは旋回の速さと照準の誤差で人並みに抑える。
    /// </summary>
    public sealed class BotInputSpec
    {
        /// <summary>既定の旋回の上限 (度/秒)。マウスで素早く振り向く人よりは遅い。</summary>
        public const float DefaultMaxTurnDegreesPerSecond = 240f;

        /// <summary>既定の照準の誤差。10m 先で半径 0.3m の円の中に狙いがぶれる。</summary>
        public const float DefaultAimErrorPerMeter = 0.03f;

        /// <summary>既定の、照準の誤差を引き直す間隔。</summary>
        public const float DefaultAimErrorRefreshSeconds = 0.5f;

        /// <summary>既定の、狙った点からこの角度以内を向いていれば撃つ角度。</summary>
        public const float DefaultFireToleranceDegrees = 3f;

        /// <summary>既定の、ダッシュする残りの道のり。</summary>
        public const float DefaultSprintDistance = 8f;

        /// <summary>既定の、ダッシュする向きの許容 (進む向きと正面のなす角)。</summary>
        public const float DefaultSprintMaxAngleDegrees = 30f;

        public static BotInputSpec Default { get; } = new BotInputSpec(
            DefaultMaxTurnDegreesPerSecond,
            DefaultAimErrorPerMeter,
            DefaultAimErrorRefreshSeconds,
            DefaultFireToleranceDegrees,
            EnemyBrain.ArrivalDistance,
            DefaultSprintDistance,
            DefaultSprintMaxAngleDegrees,
            false);

        /// <summary>1 秒に回せる角度 (yaw と pitch を合わせた角度)。</summary>
        public float MaxTurnDegreesPerSecond { get; }

        /// <summary>照準の誤差の半径を、狙う相手までの距離 1m あたり何 m にするか。0 なら誤差なし。</summary>
        public float AimErrorPerMeter { get; }

        /// <summary>照準の誤差を引き直す間隔。</summary>
        public float AimErrorRefreshSeconds { get; }

        /// <summary>誤差を足した狙う点から、向きがこの角度以内に入っていれば撃つ。</summary>
        public float FireToleranceDegrees { get; }

        /// <summary>経路の角にこの水平距離まで近づいたら次の角へ向かう。目的地なら止まる。</summary>
        public float ArrivalDistance { get; }

        /// <summary>撃たずに進むとき、残りの道のりがこれ以上ならダッシュする。</summary>
        public float SprintDistance { get; }

        /// <summary>進む向きと正面のなす角がこれ以下のときだけダッシュする (横歩き・後ろ歩きではダッシュしない)。</summary>
        public float SprintMaxAngleDegrees { get; }

        /// <summary>止まって相手を狙っているときにしゃがむか。</summary>
        public bool CrouchWhenHolding { get; }

        /// <param name="maxTurnDegreesPerSecond">旋回の上限。0 より大きく有限。</param>
        /// <param name="aimErrorPerMeter">1m あたりの誤差の半径。0 以上 1 以下。</param>
        /// <param name="aimErrorRefreshSeconds">誤差を引き直す間隔。0 より大きく有限。</param>
        /// <param name="fireToleranceDegrees">撃つ角度の許容。0 より大きく 180 以下。</param>
        /// <param name="arrivalDistance">着いたとみなす水平距離。0 より大きく有限。</param>
        /// <param name="sprintDistance">ダッシュする残りの道のり。0 以上 (無限大ならダッシュしない)。</param>
        /// <param name="sprintMaxAngleDegrees">ダッシュする向きの許容。0 以上 90 以下。</param>
        /// <param name="crouchWhenHolding">止まって狙うときにしゃがむか。</param>
        public BotInputSpec(
            float maxTurnDegreesPerSecond,
            float aimErrorPerMeter,
            float aimErrorRefreshSeconds,
            float fireToleranceDegrees,
            float arrivalDistance,
            float sprintDistance,
            float sprintMaxAngleDegrees,
            bool crouchWhenHolding)
        {
            if (!(maxTurnDegreesPerSecond > 0f) || float.IsInfinity(maxTurnDegreesPerSecond))
            {
                throw new ArgumentOutOfRangeException(
                    nameof(maxTurnDegreesPerSecond), maxTurnDegreesPerSecond, "maxTurnDegreesPerSecond must be positive and finite");
            }

            if (!(aimErrorPerMeter >= 0f && aimErrorPerMeter <= 1f))
            {
                throw new ArgumentOutOfRangeException(nameof(aimErrorPerMeter), aimErrorPerMeter, "aimErrorPerMeter must be in [0, 1]");
            }

            if (!(aimErrorRefreshSeconds > 0f) || float.IsInfinity(aimErrorRefreshSeconds))
            {
                throw new ArgumentOutOfRangeException(
                    nameof(aimErrorRefreshSeconds), aimErrorRefreshSeconds, "aimErrorRefreshSeconds must be positive and finite");
            }

            if (!(fireToleranceDegrees > 0f && fireToleranceDegrees <= 180f))
            {
                throw new ArgumentOutOfRangeException(
                    nameof(fireToleranceDegrees), fireToleranceDegrees, "fireToleranceDegrees must be in (0, 180]");
            }

            if (!(arrivalDistance > 0f) || float.IsInfinity(arrivalDistance))
            {
                throw new ArgumentOutOfRangeException(nameof(arrivalDistance), arrivalDistance, "arrivalDistance must be positive and finite");
            }

            if (!(sprintDistance >= 0f))
            {
                throw new ArgumentOutOfRangeException(nameof(sprintDistance), sprintDistance, "sprintDistance must not be negative");
            }

            if (!(sprintMaxAngleDegrees >= 0f && sprintMaxAngleDegrees <= 90f))
            {
                throw new ArgumentOutOfRangeException(
                    nameof(sprintMaxAngleDegrees), sprintMaxAngleDegrees, "sprintMaxAngleDegrees must be in [0, 90]");
            }

            MaxTurnDegreesPerSecond = maxTurnDegreesPerSecond;
            AimErrorPerMeter = aimErrorPerMeter;
            AimErrorRefreshSeconds = aimErrorRefreshSeconds;
            FireToleranceDegrees = fireToleranceDegrees;
            ArrivalDistance = arrivalDistance;
            SprintDistance = sprintDistance;
            SprintMaxAngleDegrees = sprintMaxAngleDegrees;
            CrouchWhenHolding = crouchWhenHolding;
        }
    }
}
