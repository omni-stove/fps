using System;
using System.Numerics;

namespace Shooter.Core
{
    public sealed class LadderClimb
    {
        public const float InputDeadZone = 0.1f;

        public const float LandingLift = 0.02f;

        // 降りた Tick に下へ動かすのは、床に触れさせて接地にするため。
        public const float LandingDrop = 0.1f;

        // 上の端からつかまったときに押していた向きを、前後の入力を一度離すまで下りとして読むのは、
        // 足場の縁へ進んだ入力のまま下りていき、つかまった途端に上の端へ戻らないようにするため。
        private bool _latchUntilReleased;
        private int _latchedDownSign;

        public LadderClimb(LadderSpan span, LadderSpec spec, LadderEntry entry, float feetHeight, MotorSpec motor)
        {
            if (span.Height <= 0f)
            {
                throw new ArgumentException("span must be a constructed LadderSpan", nameof(span));
            }

            if (spec == null)
            {
                throw new ArgumentNullException(nameof(spec));
            }

            if (motor == null)
            {
                throw new ArgumentNullException(nameof(motor));
            }

            if (entry != LadderEntry.Front && entry != LadderEntry.Top)
            {
                throw new ArgumentOutOfRangeException(nameof(entry), entry, "entry must be Front or Top");
            }

            if (float.IsNaN(feetHeight) || float.IsInfinity(feetHeight))
            {
                throw new ArgumentOutOfRangeException(nameof(feetHeight), feetHeight, "feetHeight must be finite");
            }

            Span = span;
            Spec = spec;
            Entry = entry;
            var highest = Math.Max(0f, span.Height - motor.LadderTopEntryDepth);
            Height = entry == LadderEntry.Top ? highest : Clamp(feetHeight, 0f, highest);
            _latchUntilReleased = entry == LadderEntry.Top;
        }

        public LadderSpan Span { get; }

        public LadderSpec Spec { get; }

        public LadderEntry Entry { get; }

        public float Height { get; private set; }

        public float VerticalSpeed { get; private set; }

        public bool IsInputLatched => _latchUntilReleased;

        public int LatchedDownSign => _latchUntilReleased ? _latchedDownSign : 0;

        // 予測の巻き戻しとサーバーの tick の間で、写した途中の中身から同じ結果になるよう作り直すために要る。
        public static LadderClimb Resume(
            LadderSpan span, LadderSpec spec, LadderEntry entry, float height, bool inputLatched, int latchedDownSign, MotorSpec motor)
        {
            if (latchedDownSign < -1 || latchedDownSign > 1)
            {
                throw new ArgumentOutOfRangeException(nameof(latchedDownSign), latchedDownSign, "latchedDownSign must be -1, 0 or 1");
            }

            var climb = new LadderClimb(span, spec, entry, height, motor);
            climb.Height = Clamp(height, 0f, span.Height);
            climb._latchUntilReleased = inputLatched;
            climb._latchedDownSign = inputLatched ? latchedDownSign : 0;
            return climb;
        }

        public LadderOutcome Step(float forward, bool jumpPressed, bool grounded, float deltaSeconds)
        {
            if (float.IsNaN(forward) || float.IsInfinity(forward))
            {
                throw new ArgumentOutOfRangeException(nameof(forward), forward, "forward must be finite");
            }

            if (!(deltaSeconds >= 0f) || float.IsInfinity(deltaSeconds))
            {
                throw new ArgumentOutOfRangeException(nameof(deltaSeconds), deltaSeconds, "deltaSeconds must be finite and not negative");
            }

            if (jumpPressed)
            {
                VerticalSpeed = 0f;
                return LadderOutcome.JumpOff;
            }

            var input = Clamp(forward, -1f, 1f);
            if (Math.Abs(input) < InputDeadZone)
            {
                input = 0f;
                _latchUntilReleased = false;
                _latchedDownSign = 0;
            }
            else if (_latchUntilReleased)
            {
                var sign = Math.Sign(input);
                if (_latchedDownSign == 0)
                {
                    _latchedDownSign = sign;
                }

                input = sign == _latchedDownSign ? -Math.Abs(input) : Math.Abs(input);
            }

            VerticalSpeed = input * Spec.ClimbSpeed;
            var next = Height + (VerticalSpeed * deltaSeconds);
            if (VerticalSpeed > 0f && next >= Span.Height)
            {
                Height = Span.Height;
                return LadderOutcome.ExitTop;
            }

            if (VerticalSpeed < 0f && (next <= 0f || grounded))
            {
                Height = Math.Max(0f, next);
                return LadderOutcome.ExitBottom;
            }

            Height = Clamp(next, 0f, Span.Height);
            return LadderOutcome.Climbing;
        }

        public Vector3 Feet(float radius)
        {
            return Span.FeetAt(Height, radius);
        }

        private static float Clamp(float value, float min, float max)
        {
            return value < min ? min : (value > max ? max : value);
        }
    }
}
