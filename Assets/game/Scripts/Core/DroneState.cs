using System;

namespace Shooter.Core
{
    // 最初の Tick で 1 回見回し、そこから間隔ごとに見回す。電池が切れた時刻ちょうどの見回しは出さない (見つけた直後に落ちて、印だけが残る)。
    // 届く距離は置いた所から測り、操縦している人が後から歩いても縮まないようにする。
    public sealed class DroneState
    {
        private readonly DroneSpec _spec;
        private float _elapsed;
        private float _nextSweepAt;
        private float _distance;

        public DroneState(DroneSpec spec)
        {
            _spec = spec ?? throw new ArgumentNullException(nameof(spec));
        }

        public DroneSpec Spec => _spec;

        public int Sweeps { get; private set; }

        public int SpottedCount { get; private set; }

        public float Distance => _distance;

        public float BatterySecondsLeft => Math.Max(0f, _spec.BatterySeconds - _elapsed);

        public float BatteryFraction => Clamp01(BatterySecondsLeft / _spec.BatterySeconds);

        public float RangeFraction => Clamp01(1f - (_distance / _spec.ControlRange));

        // 電池と距離が同じ tick に尽きたときは、電池を先に見る。電池が切れたのは距離と違って操縦で避けられないため、ログでそちらを示す。
        public DeployRemoval? Over
        {
            get
            {
                if (BatterySecondsLeft <= 0f)
                {
                    return DeployRemoval.Expired;
                }

                return _distance > _spec.ControlRange ? DeployRemoval.OutOfRange : (DeployRemoval?)null;
            }
        }

        public int Tick(float deltaSeconds, float distanceFromLaunch)
        {
            if (!(deltaSeconds >= 0f) || float.IsInfinity(deltaSeconds))
            {
                throw new ArgumentOutOfRangeException(nameof(deltaSeconds), deltaSeconds, "deltaSeconds must not be negative and must be finite");
            }

            if (!(distanceFromLaunch >= 0f) || float.IsInfinity(distanceFromLaunch))
            {
                throw new ArgumentOutOfRangeException(
                    nameof(distanceFromLaunch), distanceFromLaunch, "distanceFromLaunch must not be negative and must be finite");
            }

            if (Over.HasValue)
            {
                return 0;
            }

            _distance = distanceFromLaunch;
            _elapsed = Math.Min(_spec.BatterySeconds, _elapsed + deltaSeconds);
            if (Over.HasValue)
            {
                return 0;
            }

            var sweeps = 0;
            while (_nextSweepAt <= _elapsed && _nextSweepAt < _spec.BatterySeconds)
            {
                sweeps++;
                _nextSweepAt += _spec.SpotIntervalSeconds;
            }

            Sweeps += sweeps;
            return sweeps;
        }

        // 同じ敵を見回しのたびに数え直さないよう、初めて見つけた数だけを受ける。
        public void CountSpotted(int newlySpotted)
        {
            if (newlySpotted < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(newlySpotted), newlySpotted, "newlySpotted must not be negative");
            }

            SpottedCount += newlySpotted;
        }

        public DroneStatus Status(bool piloting)
        {
            return new DroneStatus(BatteryFraction, RangeFraction, SpottedCount, piloting);
        }

        private static float Clamp01(float value)
        {
            return value < 0f ? 0f : value > 1f ? 1f : value;
        }
    }
}
