using System;

namespace Fps.Core
{
    /// <summary>
    /// 崩れ落ちる建物の破片がキャラクターに当たったときのダメージ。破片の速さから <see cref="MinSpeed"/> を引いた分に質量を掛けた運動量に
    /// <see cref="DamagePerMomentum"/> を掛け、端数を切り捨てて <see cref="MaxDamage"/> に収める。
    /// 速さが <see cref="MinSpeed"/> 以下なら 0 (床に転がっている破片や、飛び散る小さな破片が触れただけでは減らない)。
    /// 生成時に値を検査し、以後は変わらない。
    /// </summary>
    public sealed class DebrisCrushDamage
    {
        public const float DefaultMinSpeed = 2f;
        public const float DefaultDamagePerMomentum = 0.25f;
        public const int DefaultMaxDamage = 200;

        /// <param name="minSpeed">これ以下の速さ (m/s) ではダメージを与えない。0 以上の有限値。</param>
        /// <param name="damagePerMomentum">運動量 1 kg·m/s あたりのダメージ。0 以上の有限値。</param>
        /// <param name="maxDamage">1 回に与えるダメージの上限。0 以上。</param>
        public DebrisCrushDamage(float minSpeed, float damagePerMomentum, int maxDamage)
        {
            if (!(minSpeed >= 0f) || float.IsInfinity(minSpeed))
            {
                throw new ArgumentOutOfRangeException(nameof(minSpeed), minSpeed, "minSpeed must be a non-negative finite number");
            }

            if (!(damagePerMomentum >= 0f) || float.IsInfinity(damagePerMomentum))
            {
                throw new ArgumentOutOfRangeException(
                    nameof(damagePerMomentum), damagePerMomentum, "damagePerMomentum must be a non-negative finite number");
            }

            if (maxDamage < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(maxDamage), maxDamage, "maxDamage must not be negative");
            }

            MinSpeed = minSpeed;
            DamagePerMomentum = damagePerMomentum;
            MaxDamage = maxDamage;
        }

        /// <summary>既定値 (<see cref="DefaultMinSpeed"/>・<see cref="DefaultDamagePerMomentum"/>・<see cref="DefaultMaxDamage"/>)。</summary>
        public static DebrisCrushDamage Default => new DebrisCrushDamage(DefaultMinSpeed, DefaultDamagePerMomentum, DefaultMaxDamage);

        /// <summary>これ以下の速さ (m/s) ではダメージを与えない。</summary>
        public float MinSpeed { get; }

        /// <summary>運動量 1 kg·m/s あたりのダメージ。</summary>
        public float DamagePerMomentum { get; }

        /// <summary>1 回に与えるダメージの上限。</summary>
        public int MaxDamage { get; }

        /// <summary>速さ speed・質量 mass の破片が当たったときのダメージ。0 以上 <see cref="MaxDamage"/> 以下。</summary>
        /// <param name="speed">当たったときの相対的な速さ (m/s)。0 以上 (正の無限大は <see cref="MaxDamage"/>)。</param>
        /// <param name="mass">破片の質量 (kg)。0 より大きい有限値。</param>
        public int At(float speed, float mass)
        {
            if (!(speed >= 0f))
            {
                throw new ArgumentOutOfRangeException(nameof(speed), speed, "speed must not be negative or NaN");
            }

            if (!(mass > 0f) || float.IsInfinity(mass))
            {
                throw new ArgumentOutOfRangeException(nameof(mass), mass, "mass must be positive and finite");
            }

            if (speed <= MinSpeed)
            {
                return 0;
            }

            if (float.IsPositiveInfinity(speed))
            {
                return DamagePerMomentum > 0f ? MaxDamage : 0;
            }

            // 切り捨てにするのは、MinSpeed をわずかに超えただけの接触で 1 ずつ削れ続けないようにするため。
            var damage = Math.Floor(((double)speed - MinSpeed) * mass * DamagePerMomentum);
            return damage >= MaxDamage ? MaxDamage : (int)damage;
        }
    }
}
