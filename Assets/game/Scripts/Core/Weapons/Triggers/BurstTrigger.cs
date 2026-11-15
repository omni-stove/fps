using System;

namespace Fps.Core
{
    public sealed class BurstTrigger : ITriggerModel
    {
        private readonly ITriggerModel _inner;
        private int _roundsLeft;
        private float _untilNextRound;

        public BurstTrigger(BurstSpec burst, ITriggerModel inner)
        {
            Spec = burst ?? throw new ArgumentNullException(nameof(burst));
            _inner = inner ?? throw new ArgumentNullException(nameof(inner));
        }

        public BurstSpec Spec { get; }

        private bool IsFiringBurst => _roundsLeft > 0;

        // 内側の間隔の方が短いデータでも、内側が許す速さより遅く絞らないよう短い方を取る。
        public float MinShotSpacingSeconds => Math.Min(Spec.RoundIntervalSeconds, _inner.MinShotSpacingSeconds);

        public bool BlocksAds => _inner.BlocksAds;

        public float ChargeFraction => _inner.ChargeFraction;

        // 連射の途中に押しても内側へは伝える。Auto が押されているかを覚えて、連射が終わった後も押し続けていれば次を撃てるようにするため。
        public int Press()
        {
            var starts = _inner.Press();
            return IsFiringBurst ? 0 : StartBursts(starts);
        }

        // 離しても連射は最後まで撃つ。1 回押せば決めた発数が出る撃ち方なので。
        public int Release()
        {
            var starts = _inner.Release();
            return IsFiringBurst ? 0 : StartBursts(starts);
        }

        // 連射の間は内側の時間を止める。内側の FireInterval の待ちを、最後の弾を撃った後から数えるため。
        public int Tick(float deltaSeconds)
        {
            SpecGuard.NonNegativeFinite(deltaSeconds, nameof(deltaSeconds));
            var shots = 0;
            while (IsFiringBurst)
            {
                if (deltaSeconds < _untilNextRound)
                {
                    _untilNextRound -= deltaSeconds;
                    return shots;
                }

                deltaSeconds -= _untilNextRound;
                _untilNextRound = Spec.RoundIntervalSeconds;
                _roundsLeft--;
                shots++;
            }

            return shots + StartBursts(_inner.Tick(deltaSeconds));
        }

        private int StartBursts(int starts)
        {
            if (starts <= 0)
            {
                return 0;
            }

            _roundsLeft = Spec.RoundsPerBurst - 1;
            _untilNextRound = Spec.RoundIntervalSeconds;
            return 1;
        }
    }
}
