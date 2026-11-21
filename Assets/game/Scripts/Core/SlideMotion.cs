using System;
using System.Globalization;
using System.Numerics;

namespace Shooter.Core
{
    public sealed class SlideMotion
    {
        public const string LogTag = "[Slide]";

        // 既定のダッシュ (8m/s) より速く滑り出す。
        public const float DefaultStartSpeed = 11f;

        // 平らな床で 1 秒ほどで止まる。
        public const float DefaultDeceleration = 8f;

        // 既定のしゃがみ歩き (2.5m/s) より少し速い。
        public const float DefaultEndSpeed = 3f;

        public const float DefaultMinSeconds = 0.4f;

        public const float DefaultMaxSeconds = 3f;

        // 既定の重力 (20m/s²) と同じで、15° の下り坂では減速の 3 分の 2 ほどを打ち消して平らな床の 3 倍近く滑る。
        public const float DefaultSlopeAcceleration = 20f;

        private Vector2 _direction;
        private float _speed;
        private float _elapsed;
        private float _distance;
        private bool _isSliding;
        private SlideEnd _lastEnd = SlideEnd.None;

        public SlideMotion()
            : this(DefaultStartSpeed, DefaultDeceleration, DefaultEndSpeed, DefaultMinSeconds, DefaultMaxSeconds, DefaultSlopeAcceleration)
        {
        }

        public SlideMotion(float startSpeed, float deceleration, float endSpeed, float minSeconds, float maxSeconds, float slopeAcceleration)
        {
            RequireNonNegative(endSpeed, nameof(endSpeed));

            // 同じ値を許すと、滑り出した最初の Step で速さが落ちたと見なして終わってしまう。
            if (!(startSpeed > endSpeed) || float.IsInfinity(startSpeed))
            {
                throw new ArgumentOutOfRangeException(nameof(startSpeed), startSpeed, "startSpeed must be finite and greater than endSpeed");
            }

            RequireNonNegative(deceleration, nameof(deceleration));
            if (!(maxSeconds > 0f) || float.IsInfinity(maxSeconds))
            {
                throw new ArgumentOutOfRangeException(nameof(maxSeconds), maxSeconds, "maxSeconds must be positive and finite");
            }

            if (!(minSeconds >= 0f && minSeconds <= maxSeconds))
            {
                throw new ArgumentOutOfRangeException(nameof(minSeconds), minSeconds, "minSeconds must be between 0 and maxSeconds");
            }

            RequireNonNegative(slopeAcceleration, nameof(slopeAcceleration));

            StartSpeed = startSpeed;
            Deceleration = deceleration;
            EndSpeed = endSpeed;
            MinSeconds = minSeconds;
            MaxSeconds = maxSeconds;
            SlopeAcceleration = slopeAcceleration;
        }

        public float StartSpeed { get; }

        public float Deceleration { get; }

        public float EndSpeed { get; }

        public float MinSeconds { get; }

        public float MaxSeconds { get; }

        public float SlopeAcceleration { get; }

        public bool IsSliding => _isSliding;

        public Vector2 Direction => _direction;

        public float Speed => _speed;

        public float Elapsed => _elapsed;

        public float Distance => _distance;

        public SlideEnd LastEnd => _lastEnd;

        public bool TryStart(Vector2 direction, float entrySpeed)
        {
            if (!IsFinite(direction.X) || !IsFinite(direction.Y) || direction.LengthSquared() <= 0f)
            {
                throw new ArgumentOutOfRangeException(nameof(direction), direction, "direction must be finite and not zero");
            }

            RequireNonNegative(entrySpeed, nameof(entrySpeed));
            if (_isSliding)
            {
                return false;
            }

            _direction = Vector2.Normalize(direction);
            _speed = Math.Max(StartSpeed, entrySpeed);
            _elapsed = 0f;
            _distance = 0f;
            _isSliding = true;
            _lastEnd = SlideEnd.None;
            return true;
        }

        // フレームの刻みによらず同じ距離になるよう、速さは一定の加速度で変わるとして始点と終点の平均で進める。
        public float Step(float deltaSeconds, float slopeSine)
        {
            RequireNonNegative(deltaSeconds, nameof(deltaSeconds));
            if (!(slopeSine >= -1f && slopeSine <= 1f))
            {
                throw new ArgumentOutOfRangeException(nameof(slopeSine), slopeSine, "slopeSine must be between -1 and 1");
            }

            if (!_isSliding)
            {
                return 0f;
            }

            var seconds = Math.Min(deltaSeconds, MaxSeconds - _elapsed);
            var acceleration = (SlopeAcceleration * slopeSine) - Deceleration;
            var before = _speed;
            var after = before + (acceleration * seconds);
            float travelled;
            if (after <= 0f)
            {
                // 途中で止まった分だけ進める (止まった後に後ろへ戻らない)。
                var stopSeconds = acceleration < 0f ? before / -acceleration : seconds;
                travelled = before * 0.5f * Math.Min(stopSeconds, seconds);
                after = 0f;
            }
            else
            {
                travelled = (before + after) * 0.5f * seconds;
            }

            _speed = after;
            _elapsed += seconds;
            _distance += travelled;

            if (after <= 0f)
            {
                End(SlideEnd.Stopped);
            }
            else if (_elapsed >= MaxSeconds)
            {
                End(SlideEnd.TimedOut);
            }
            else if (_elapsed >= MinSeconds && after <= EndSpeed)
            {
                End(SlideEnd.Stopped);
            }

            return travelled;
        }

        // ネットの移動は tick ごとに状態から戻して進めるので、前の終わった理由は持ち越さない。
        public void Restore(CharacterSlideState state)
        {
            if (!state.IsValid)
            {
                throw new ArgumentException("state must be valid", nameof(state));
            }

            _isSliding = state.IsSliding;
            _direction = state.Direction;
            _speed = state.Speed;
            _elapsed = state.Elapsed;
            _distance = state.Distance;
            _lastEnd = SlideEnd.None;
        }

        public CharacterSlideState Capture(float airborneSeconds)
        {
            return _isSliding
                ? CharacterSlideState.Sliding(_direction, _speed, _elapsed, _distance, airborneSeconds)
                : CharacterSlideState.None;
        }

        public void LimitSpeed(float maxSpeed)
        {
            RequireNonNegative(maxSpeed, nameof(maxSpeed));
            if (_isSliding && _speed > maxSpeed)
            {
                _speed = maxSpeed;
            }
        }

        public bool Finish(SlideEnd reason)
        {
            if (reason == SlideEnd.None || !Enum.IsDefined(typeof(SlideEnd), reason))
            {
                throw new ArgumentOutOfRangeException(nameof(reason), reason, "reason must be a defined SlideEnd other than None");
            }

            if (!_isSliding)
            {
                return false;
            }

            End(reason);
            return true;
        }

        public static string FormatStartLog(float speed, Vector2 direction)
        {
            return string.Format(
                CultureInfo.InvariantCulture,
                "{0} start speed={1:0.00} direction=({2:0.00}, {3:0.00})",
                LogTag,
                speed,
                direction.X,
                direction.Y);
        }

        public static string FormatEndLog(SlideEnd reason, float speed, float seconds, float distance)
        {
            return string.Format(
                CultureInfo.InvariantCulture,
                "{0} end reason={1} speed={2:0.00} seconds={3:0.00} distance={4:0.00}",
                LogTag,
                reason,
                speed,
                seconds,
                distance);
        }

        private void End(SlideEnd reason)
        {
            _isSliding = false;
            _lastEnd = reason;
        }

        private static void RequireNonNegative(float value, string name)
        {
            if (!(value >= 0f) || float.IsInfinity(value))
            {
                throw new ArgumentOutOfRangeException(name, value, $"{name} must be finite and not negative");
            }
        }

        private static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }
    }
}
