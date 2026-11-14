using System;

namespace Fps.Core
{
    /// <summary>
    /// 爆発する置き物 1 つの耐久と、爆発までの残り時間。被害 (<see cref="ApplyDamage"/>) で耐久が 0 になると爆発を待ち始め、
    /// 待つ秒数 (<see cref="ExplosivePropSpec.DetonationDelay"/>) が経った <see cref="Tick"/> で 1 度だけ爆発する。
    /// 待つ秒数が 0 でも、0 になった呼び出しの中では爆発せず次の <see cref="Tick"/> で爆発する。
    /// 爆発を待ち始めた後の被害は何も変えない。
    /// </summary>
    public sealed class ExplosiveCharge
    {
        public ExplosiveCharge(ExplosivePropSpec spec)
        {
            Spec = spec ?? throw new ArgumentNullException(nameof(spec));
            Health = spec.MaxHealth;
        }

        public ExplosivePropSpec Spec { get; }

        /// <summary>残りの耐久。0 以上 <see cref="ExplosivePropSpec.MaxHealth"/> 以下。</summary>
        public int Health { get; private set; }

        /// <summary>耐久が 0 になり、爆発を待っている (燃えているなら燃えている) か、爆発したなら true。</summary>
        public bool IsTriggered { get; private set; }

        /// <summary>燃えていて、まだ爆発していないなら true。</summary>
        public bool IsBurning => IsTriggered && !HasDetonated && Spec.Burns;

        /// <summary>ほかの置き物の爆発で耐久が 0 になったなら true。</summary>
        public bool IsChained { get; private set; }

        /// <summary>爆発したなら true。</summary>
        public bool HasDetonated { get; private set; }

        /// <summary>爆発までの残り秒数。爆発を待っていない間と爆発した後は 0。</summary>
        public float RemainingSeconds { get; private set; }

        /// <summary>
        /// 耐久を damage だけ減らす。0 になったら爆発を待ち始める。爆発を待ち始めた後と爆発した後は何もしない。
        /// </summary>
        /// <param name="damage">被害。0 以上。</param>
        /// <param name="chained">ほかの置き物の爆発による被害なら true。待つ秒数が <see cref="ExplosivePropSpec.ChainDelaySeconds"/> 以上になる。</param>
        public ExplosiveChargeStep ApplyDamage(int damage, bool chained)
        {
            if (damage < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(damage), damage, "damage must not be negative");
            }

            if (IsTriggered || damage == 0)
            {
                return ExplosiveChargeStep.None;
            }

            Health = Math.Max(0, Health - damage);
            if (Health > 0)
            {
                return ExplosiveChargeStep.Damaged;
            }

            IsTriggered = true;
            IsChained = chained;
            RemainingSeconds = Spec.DetonationDelay(chained);
            return Spec.Burns ? ExplosiveChargeStep.Ignited : ExplosiveChargeStep.Triggered;
        }

        /// <summary>
        /// 爆発までの時間を deltaSeconds 進め、残りが 0 以下になったら爆発したことにして true を返す。
        /// 爆発を待っていない間と爆発した後は false。
        /// </summary>
        /// <param name="deltaSeconds">前の呼び出しからの秒数。0 以上の有限値。</param>
        public bool Tick(float deltaSeconds)
        {
            if (!(deltaSeconds >= 0f) || float.IsInfinity(deltaSeconds))
            {
                throw new ArgumentOutOfRangeException(nameof(deltaSeconds), deltaSeconds, "deltaSeconds must be a non-negative finite number");
            }

            if (!IsTriggered || HasDetonated)
            {
                return false;
            }

            RemainingSeconds -= deltaSeconds;
            if (RemainingSeconds > 0f)
            {
                return false;
            }

            RemainingSeconds = 0f;
            HasDetonated = true;
            return true;
        }
    }
}
