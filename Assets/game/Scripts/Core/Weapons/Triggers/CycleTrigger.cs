using System;

namespace Fps.Core
{
    public sealed class CycleTrigger : ITriggerModel
    {
        private readonly ITriggerModel _inner;
        private float _cycleRemaining;

        public CycleTrigger(CycleSpec cycle, ITriggerModel inner)
        {
            Spec = cycle ?? throw new ArgumentNullException(nameof(cycle));
            _inner = inner ?? throw new ArgumentNullException(nameof(inner));
        }

        public CycleSpec Spec { get; }

        public bool IsCycling => _cycleRemaining > 0f;

        // Weapon とサーバーの ShotBudget はこの間隔で撃てるかを見るので、装填の間も撃たせないよう長い方を返す。
        public float MinShotSpacingSeconds => Math.Max(_inner.MinShotSpacingSeconds, Spec.CycleSeconds);

        public bool BlocksAds => (Spec.LeavesAds && IsCycling) || _inner.BlocksAds;

        public float ChargeFraction => _inner.ChargeFraction;

        // 装填中も内側へ押す・離すを渡すのは、押しっぱなしの撃ち方や溜めが装填の後に続けて撃てるようにするため。出た弾だけを捨てる。
        public int Press()
        {
            return Pass(_inner.Press());
        }

        public int Release()
        {
            return Pass(_inner.Release());
        }

        // 先に装填を進めるのは、この時間の中で装填が終わって内側が撃った弾を捨てないため。
        public int Tick(float deltaSeconds)
        {
            SpecGuard.NonNegativeFinite(deltaSeconds, nameof(deltaSeconds));
            _cycleRemaining = Math.Max(0f, _cycleRemaining - deltaSeconds);
            return Pass(_inner.Tick(deltaSeconds));
        }

        private int Pass(int shots)
        {
            if (shots <= 0)
            {
                return 0;
            }

            if (IsCycling)
            {
                return 0;
            }

            _cycleRemaining = Spec.CycleSeconds;
            return shots;
        }
    }
}
