using System;

namespace Fps.Core
{
    public sealed class BeamState : ITriggerModel
    {
        // 間隔ちょうどに届いた時刻を float の足し引きの誤差で取りこぼさないよう、間隔に対してこの割合だけ早く数える。
        private const double _tickTolerance = 1e-4;

        private double _untilNextTick;

        public BeamState(BeamSpec beam)
        {
            Spec = beam ?? throw new ArgumentNullException(nameof(beam));
        }

        public BeamSpec Spec { get; }

        public bool IsFiring { get; private set; }

        public float FiringSeconds { get; private set; }

        // 照射の間隔は Tick が数えるので 0 にする。間隔を返すと、Weapon.TryFire が 1 フレームに 2 回ぶん届いた照射の 2 回目を弾き、
        // 間隔の揺れで次の照射が少し早いフレームに来たときにも弾いて、照射の回数が時間と合わなくなる。
        public float MinShotSpacingSeconds => 0f;

        public bool BlocksAds => false;

        public float ChargeFraction => 0f;

        public int Start()
        {
            if (IsFiring)
            {
                return 0;
            }

            IsFiring = true;
            FiringSeconds = 0f;

            // 離した後も前の照射からの待ちを数え続けるのは、押し直しの連打で間隔より速く当てられないようにするため。
            if (_untilNextTick > Spec.TickSeconds * _tickTolerance)
            {
                return 0;
            }

            _untilNextTick = Spec.TickSeconds;
            return 1;
        }

        public void Stop()
        {
            IsFiring = false;
        }

        public int Tick(float deltaSeconds)
        {
            SpecGuard.NonNegativeFinite(deltaSeconds, nameof(deltaSeconds));
            _untilNextTick -= deltaSeconds;
            if (!IsFiring)
            {
                _untilNextTick = Math.Max(0d, _untilNextTick);
                return 0;
            }

            FiringSeconds += deltaSeconds;
            var tick = (double)Spec.TickSeconds;
            if (_untilNextTick > tick * _tickTolerance)
            {
                return 0;
            }

            // 1 回ずつ回して数えないのは、長いフレームで間隔を何度も跨いでも、跨いだ回数ぶんループを回さずに済ませるため。
            var ticks = Math.Floor(((tick * _tickTolerance) - _untilNextTick) / tick) + 1d;
            _untilNextTick += ticks * tick;
            return ticks >= int.MaxValue ? int.MaxValue : (int)ticks;
        }

        int ITriggerModel.Press()
        {
            return Start();
        }

        int ITriggerModel.Release()
        {
            Stop();
            return 0;
        }

        // 持ち替えで離した扱いになっても、次の照射までの待ちは残す。持ち替えの連打で間隔より速く当てられないようにするため。
        public void Cancel()
        {
            Stop();
        }
    }
}
