using System;

namespace Fps.Core
{
    /// <summary>
    /// 最後に被弾してから <see cref="RegenDelaySeconds"/> たつと、<see cref="Health"/> を 1 秒あたり
    /// <see cref="RegenPerSecond"/> ずつ回復させる。<see cref="Health"/> は整数なので、1 未満の端数は内部にためて
    /// 整数になった分だけ <see cref="Fps.Core.Health.Heal"/> する。
    /// </summary>
    /// <remarks>
    /// <list type="bullet">
    /// <item>まだ一度も <see cref="NotifyDamaged"/> を呼んでいないときは、待ち時間は過ぎているものとする。</item>
    /// <item>待ち時間が 1 回の <see cref="Tick"/> の途中で過ぎたときは、過ぎた後の分だけ回復する。</item>
    /// <item>死んでいる間・満タンのとき・<see cref="RegenPerSecond"/> が 0 のときは何もせず、ためた端数も捨てる
    /// (満タンになった後に端数を持ち越すと、次の被弾直後の回復が早まるため)。</item>
    /// </list>
    /// </remarks>
    public sealed class HealthRegenerator
    {
        public Health Health { get; }

        /// <summary>最後に被弾してから回復が始まるまでの秒数。</summary>
        public float RegenDelaySeconds { get; }

        /// <summary>1 秒あたりの回復量。0 なら回復しない。</summary>
        public float RegenPerSecond { get; }

        /// <summary>回復が始まるまでの残り秒数。待っていないときは 0。</summary>
        public float DelayRemainingSeconds => _delayRemaining;

        private float _delayRemaining;

        // float だと大きな Tick を足したときに桁落ちしやすいので double でためる
        private double _pending;

        /// <param name="health">回復させる体力。</param>
        /// <param name="rules"><see cref="GameRules.RegenDelaySeconds"/> と <see cref="GameRules.RegenPerSecond"/> を使う。</param>
        public HealthRegenerator(Health health, GameRules rules)
            : this(
                health,
                (rules ?? throw new ArgumentNullException(nameof(rules))).RegenDelaySeconds,
                rules.RegenPerSecond)
        {
        }

        /// <param name="health">回復させる体力。</param>
        /// <param name="regenDelaySeconds">回復が始まるまでの秒数。0 以上の有限値。</param>
        /// <param name="regenPerSecond">1 秒あたりの回復量。0 以上の有限値。</param>
        public HealthRegenerator(Health health, float regenDelaySeconds, float regenPerSecond)
        {
            if (!(regenDelaySeconds >= 0f) || float.IsInfinity(regenDelaySeconds))
            {
                throw new ArgumentOutOfRangeException(nameof(regenDelaySeconds), regenDelaySeconds, "regenDelaySeconds must be a non-negative finite number");
            }

            if (!(regenPerSecond >= 0f) || float.IsInfinity(regenPerSecond))
            {
                throw new ArgumentOutOfRangeException(nameof(regenPerSecond), regenPerSecond, "regenPerSecond must be a non-negative finite number");
            }

            Health = health ?? throw new ArgumentNullException(nameof(health));
            RegenDelaySeconds = regenDelaySeconds;
            RegenPerSecond = regenPerSecond;
        }

        /// <summary>被弾したことを知らせる。待ち時間を <see cref="RegenDelaySeconds"/> に戻し、ためた端数を捨てる。</summary>
        public void NotifyDamaged()
        {
            _delayRemaining = RegenDelaySeconds;
            _pending = 0d;
        }

        /// <summary>時間を deltaSeconds 進め、待ち時間が過ぎていれば回復する。</summary>
        /// <param name="deltaSeconds">進める秒数。0 以上。</param>
        /// <returns>実際に回復した量。</returns>
        public int Tick(float deltaSeconds)
        {
            if (!(deltaSeconds >= 0f))
            {
                throw new ArgumentOutOfRangeException(nameof(deltaSeconds), deltaSeconds, "deltaSeconds must not be negative");
            }

            if (RegenPerSecond == 0f || Health.IsDead || Health.Current >= Health.Max)
            {
                _pending = 0d;
                return 0;
            }

            if (deltaSeconds <= _delayRemaining)
            {
                _delayRemaining -= deltaSeconds;
                return 0;
            }

            var regenSeconds = (double)deltaSeconds - _delayRemaining;
            _delayRemaining = 0f;

            _pending += regenSeconds * RegenPerSecond;
            var missing = Health.Max - Health.Current;
            if (_pending >= missing)
            {
                _pending = 0d;
                Health.Heal(missing);
                return missing;
            }

            var whole = (int)Math.Floor(_pending);
            if (whole == 0)
            {
                return 0;
            }

            _pending -= whole;
            Health.Heal(whole);
            return whole;
        }
    }
}
