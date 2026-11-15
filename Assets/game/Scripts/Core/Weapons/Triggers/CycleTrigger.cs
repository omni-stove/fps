using System;

namespace Fps.Core
{
    public sealed class CycleTrigger : ITriggerModel
    {
        private readonly ITriggerModel _inner;
        private float _cycleRemaining;
        private float _deferredSeconds;
        private bool _pressedDuringCycle;

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

        // 装填中の押す・時間を内側へ渡さず覚えておくのは、内側が捨てられる弾で連射間隔の待ちを始め、装填の後まで撃てなくなるのを避けるため。
        public int Press()
        {
            if (IsCycling)
            {
                _pressedDuringCycle = true;
                return 0;
            }

            return Pass(_inner.Press());
        }

        // 離すのは装填中でも渡す。押しっぱなしの撃ち方が、離した後に装填が終わって撃たないようにするため。
        public int Release()
        {
            _pressedDuringCycle = false;
            return Pass(_inner.Release());
        }

        public int Tick(float deltaSeconds)
        {
            SpecGuard.NonNegativeFinite(deltaSeconds, nameof(deltaSeconds));
            if (!IsCycling)
            {
                return Pass(_inner.Tick(deltaSeconds));
            }

            _cycleRemaining = Math.Max(0f, _cycleRemaining - deltaSeconds);
            _deferredSeconds += deltaSeconds;
            if (IsCycling)
            {
                return 0;
            }

            var shots = _inner.Tick(_deferredSeconds);
            _deferredSeconds = 0f;
            if (_pressedDuringCycle)
            {
                _pressedDuringCycle = false;
                shots += _inner.Press();
            }

            return Pass(shots);
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

        // 装填は撃てなくなっても途中で終わらせない。持ち替えや弾切れでボルトを引く時間を飛ばせないようにするため。捨てるのは装填中に押した分だけ。
        public void Cancel()
        {
            _inner.Cancel();
            _pressedDuringCycle = false;
        }
    }
}
