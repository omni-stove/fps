using System;
using System.Collections.Generic;

namespace Shooter.Core
{
    public sealed class TutorialSteps
    {
        public const float DefaultMoveMeters = 3f;

        public const float DefaultAimSeconds = 0.5f;

        public const int DefaultHitsRequired = 3;

        // 1 回の観測でこれより長く動いたら、歩いたのではなく位置を移された (出現し直した) と見なして数えない。
        // ダッシュ (8m/s) で 10fps まで落ちても 0.8m なので、歩きを捨てることはない。
        public const float DefaultTeleportMeters = 2f;

        public static IReadOnlyList<TutorialStep> Order { get; } = new[]
        {
            TutorialStep.Move,
            TutorialStep.Aim,
            TutorialStep.Shoot,
            TutorialStep.Reload,
            TutorialStep.Crouch,
            TutorialStep.Cover,
        };

        private readonly float _moveMeters;
        private readonly float _aimSeconds;
        private readonly int _hitsRequired;
        private readonly float _teleportMeters;

        private int _index;
        private float _movedMeters;
        private float _aimedSeconds;
        private int _hits;

        public TutorialSteps(
            float moveMeters = DefaultMoveMeters,
            float aimSeconds = DefaultAimSeconds,
            int hitsRequired = DefaultHitsRequired,
            float teleportMeters = DefaultTeleportMeters)
        {
            if (!(moveMeters > 0f) || float.IsInfinity(moveMeters))
            {
                throw new ArgumentOutOfRangeException(nameof(moveMeters), moveMeters, "moveMeters must be a positive finite number");
            }

            if (!(aimSeconds >= 0f) || float.IsInfinity(aimSeconds))
            {
                throw new ArgumentOutOfRangeException(nameof(aimSeconds), aimSeconds, "aimSeconds must be a non-negative finite number");
            }

            if (hitsRequired < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(hitsRequired), hitsRequired, "hitsRequired must be at least 1");
            }

            if (!(teleportMeters > 0f) || float.IsInfinity(teleportMeters))
            {
                throw new ArgumentOutOfRangeException(nameof(teleportMeters), teleportMeters, "teleportMeters must be a positive finite number");
            }

            _moveMeters = moveMeters;
            _aimSeconds = aimSeconds;
            _hitsRequired = hitsRequired;
            _teleportMeters = teleportMeters;
        }

        public float MoveMeters => _moveMeters;

        public float AimSeconds => _aimSeconds;

        public int HitsRequired => _hitsRequired;

        public bool IsComplete => _index >= Order.Count;

        public int CurrentIndex => _index;

        public TutorialStep Current
        {
            get
            {
                if (IsComplete)
                {
                    throw new InvalidOperationException("the tutorial is complete; there is no current step");
                }

                return Order[_index];
            }
        }

        public int Hits => _hits;

        public float StepProgress
        {
            get
            {
                if (IsComplete)
                {
                    return 1f;
                }

                switch (Current)
                {
                    case TutorialStep.Move:
                        return Math.Min(1f, _movedMeters / _moveMeters);
                    case TutorialStep.Aim:
                        return _aimSeconds > 0f ? Math.Min(1f, _aimedSeconds / _aimSeconds) : 0f;
                    case TutorialStep.Shoot:
                        return Math.Min(1f, (float)_hits / _hitsRequired);
                    default:
                        return 0f;
                }
            }
        }

        public static bool ShouldStart(bool alreadyCompleted, bool requestedFromMenu)
        {
            return requestedFromMenu || !alreadyCompleted;
        }

        public bool Observe(TutorialObservation observation)
        {
            if (IsComplete)
            {
                return false;
            }

            if (!IsStepDone(observation))
            {
                return false;
            }

            _index++;
            ResetCounts();
            return true;
        }

        public void Restart()
        {
            _index = 0;
            ResetCounts();
        }

        public static string JoinDirectionalKeys(IReadOnlyList<string> keys)
        {
            if (keys == null)
            {
                throw new ArgumentNullException(nameof(keys));
            }

            var parts = new List<string>(keys.Count);
            var allSingle = true;
            foreach (var key in keys)
            {
                if (string.IsNullOrWhiteSpace(key))
                {
                    continue;
                }

                var trimmed = key.Trim();
                parts.Add(trimmed);
                allSingle &= trimmed.Length == 1;
            }

            return string.Join(allSingle ? string.Empty : "/", parts);
        }

        private bool IsStepDone(TutorialObservation observation)
        {
            switch (Current)
            {
                case TutorialStep.Move:
                    // 位置を移されたときの跳びは歩いた距離に入れない
                    if (observation.MovedMeters <= _teleportMeters)
                    {
                        _movedMeters += observation.MovedMeters;
                    }

                    return _movedMeters >= _moveMeters;
                case TutorialStep.Aim:
                    if (!observation.Aiming)
                    {
                        return false;
                    }

                    _aimedSeconds += observation.DeltaSeconds;
                    return _aimedSeconds >= _aimSeconds;
                case TutorialStep.Shoot:
                    _hits = (int)Math.Min((long)_hits + observation.Hits, _hitsRequired);
                    return _hits >= _hitsRequired;
                case TutorialStep.Reload:
                    return observation.Reloading;
                case TutorialStep.Crouch:
                    return observation.Crouching;
                case TutorialStep.Cover:
                    return observation.InCover;
                default:
                    throw new InvalidOperationException("unknown tutorial step " + Current);
            }
        }

        private void ResetCounts()
        {
            _movedMeters = 0f;
            _aimedSeconds = 0f;
            _hits = 0;
        }
    }
}
