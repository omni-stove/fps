using System;

namespace Fps.Core
{
    /// <summary>
    /// 能力 1 つ分の回数と時間 (<see cref="AbilitySlot"/>) の設定。生成時に値を検査し、以後は変わらない。
    /// </summary>
    public sealed class AbilitySpec
    {
        public const int DefaultMaxCharges = 1;

        public const float DefaultRechargeSeconds = 10f;

        public const float DefaultCooldownSeconds = 1f;

        public const float DefaultWindupSeconds = 0f;

        /// <summary>既定値 (<see cref="DefaultMaxCharges"/> など) の設定。</summary>
        public static AbilitySpec Default { get; } =
            new AbilitySpec(DefaultMaxCharges, DefaultRechargeSeconds, DefaultCooldownSeconds, DefaultWindupSeconds);

        /// <param name="maxCharges">続けて使える回数の上限。1 以上。</param>
        /// <param name="rechargeSeconds">回数を 1 つ戻すのにかかる秒数。0 以上の有限値。0 なら回数は戻らない。</param>
        /// <param name="cooldownSeconds">使い始めてから次に使えるまでの秒数。0 以上の有限値。</param>
        /// <param name="windupSeconds">使い始めてから効果が出るまでの秒数。0 以上の有限値。0 なら使ったその場で効果が出る。</param>
        public AbilitySpec(int maxCharges, float rechargeSeconds, float cooldownSeconds, float windupSeconds)
        {
            if (maxCharges < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(maxCharges), maxCharges, "maxCharges must be at least 1");
            }

            RequireSeconds(rechargeSeconds, nameof(rechargeSeconds));
            RequireSeconds(cooldownSeconds, nameof(cooldownSeconds));
            RequireSeconds(windupSeconds, nameof(windupSeconds));

            MaxCharges = maxCharges;
            RechargeSeconds = rechargeSeconds;
            CooldownSeconds = cooldownSeconds;
            WindupSeconds = windupSeconds;
        }

        /// <summary>続けて使える回数の上限。1 以上。</summary>
        public int MaxCharges { get; }

        /// <summary>回数を 1 つ戻すのにかかる秒数。0 なら戻らない。</summary>
        public float RechargeSeconds { get; }

        /// <summary>使い始めてから次に使えるまでの秒数。</summary>
        public float CooldownSeconds { get; }

        /// <summary>使い始めてから効果が出るまでの秒数。</summary>
        public float WindupSeconds { get; }

        /// <summary>回数が戻るか (<see cref="RechargeSeconds"/> が 0 より大きい)。</summary>
        public bool Recharges => RechargeSeconds > 0f;

        private static void RequireSeconds(float seconds, string paramName)
        {
            if (!(seconds >= 0f) || float.IsInfinity(seconds))
            {
                throw new ArgumentOutOfRangeException(paramName, seconds, paramName + " must not be negative and must be finite");
            }
        }
    }
}
