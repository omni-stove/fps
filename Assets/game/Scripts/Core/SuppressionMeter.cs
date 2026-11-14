using System;

namespace Fps.Core
{
    // 時間を自分で読まず Tick で受けるのは、Unity の外 (dotnet のテスト) でも同じ計算を回すため。
    public sealed class SuppressionMeter
    {
        private float _sinceNearMiss;

        public SuppressionMeter(SuppressionSpec spec)
        {
            Spec = spec ?? throw new ArgumentNullException(nameof(spec));
        }

        public SuppressionSpec Spec { get; }

        public float Level { get; private set; }

        public bool IsSuppressed { get; private set; }

        // 度合いが 0 に戻ったら数え直す。ログで「何発で制圧されたか」を読めるように。
        public int NearMisses { get; private set; }

        /// <exception cref="ArgumentOutOfRangeException">distance が 0 以上の有限値でない。</exception>
        public float AmountFor(float distance)
        {
            if (!(distance >= 0f) || float.IsInfinity(distance))
            {
                throw new ArgumentOutOfRangeException(nameof(distance), distance, "distance must be non-negative and finite");
            }

            if (distance >= Spec.NearMissRadius)
            {
                return 0f;
            }

            return Spec.BuildPerNearMiss * (1f - (distance / Spec.NearMissRadius));
        }

        /// <exception cref="ArgumentOutOfRangeException">distance が 0 以上の有限値でない。</exception>
        public bool AddNearMiss(float distance)
        {
            var amount = AmountFor(distance);
            if (amount <= 0f)
            {
                return false;
            }

            Level = Math.Min(1f, Level + amount);
            NearMisses = NearMisses < int.MaxValue ? NearMisses + 1 : NearMisses;
            _sinceNearMiss = 0f;
            if (Level >= Spec.SuppressedLevel)
            {
                IsSuppressed = true;
            }

            return true;
        }

        /// <exception cref="ArgumentOutOfRangeException">deltaSeconds が 0 以上の有限値でない。</exception>
        public void Tick(float deltaSeconds)
        {
            if (!(deltaSeconds >= 0f) || float.IsInfinity(deltaSeconds))
            {
                throw new ArgumentOutOfRangeException(nameof(deltaSeconds), deltaSeconds, "deltaSeconds must be non-negative and finite");
            }

            if (Level <= 0f)
            {
                return;
            }

            // 1 回の Tick が待ちの終わりをまたいでも、またいだ後の分だけ抜く (フレームの長さで抜け方が変わらないように)。
            var waiting = Math.Max(0f, Spec.DecayDelaySeconds - _sinceNearMiss);
            var decaying = Math.Max(0f, deltaSeconds - waiting);
            _sinceNearMiss = Math.Min(Spec.DecayDelaySeconds, _sinceNearMiss + deltaSeconds);
            Level = Math.Max(0f, Level - (decaying * Spec.DecayPerSecond));

            // 解ける度合いが 0 の設定でも、抜け切ったら解く。
            if (IsSuppressed && (Level < Spec.ReleasedLevel || Level <= 0f))
            {
                IsSuppressed = false;
            }

            if (Level <= 0f)
            {
                NearMisses = 0;
            }
        }

        public void Reset()
        {
            Level = 0f;
            IsSuppressed = false;
            NearMisses = 0;
            _sinceNearMiss = 0f;
        }
    }
}
