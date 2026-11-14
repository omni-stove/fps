using System;

namespace Fps.Core
{
    public sealed class EnemyInterruption
    {
        public bool IsActive => SecondsRemaining > 0f;

        public float SecondsRemaining { get; private set; }

        public string Reason { get; private set; } = string.Empty;

        // 止まっている間に別の理由でまた止められたら、長く続く方を残す。短い方で上書きすると、背後から倒される最中に怯みで早く動き出してしまう。
        public bool Begin(float seconds, string reason)
        {
            Validate(seconds, reason);
            if (seconds <= SecondsRemaining)
            {
                return false;
            }

            SecondsRemaining = seconds;
            Reason = reason;
            return true;
        }

        public bool Tick(float deltaSeconds)
        {
            if (!(deltaSeconds >= 0f) || float.IsInfinity(deltaSeconds))
            {
                throw new ArgumentOutOfRangeException(nameof(deltaSeconds), deltaSeconds, "deltaSeconds must be non-negative and finite");
            }

            if (!IsActive)
            {
                return false;
            }

            SecondsRemaining = Math.Max(0f, SecondsRemaining - deltaSeconds);
            return !IsActive;
        }

        // 理由はログの 1 語として出すので、空白を含むものは受け付けない。
        public static void Validate(float seconds, string reason)
        {
            if (!(seconds > 0f) || float.IsInfinity(seconds))
            {
                throw new ArgumentOutOfRangeException(nameof(seconds), seconds, "seconds must be positive and finite");
            }

            if (string.IsNullOrEmpty(reason))
            {
                throw new ArgumentException("reason must not be empty", nameof(reason));
            }

            foreach (var c in reason)
            {
                if (char.IsWhiteSpace(c))
                {
                    throw new ArgumentException("reason must not contain whitespace", nameof(reason));
                }
            }
        }

        public void Clear()
        {
            SecondsRemaining = 0f;
            Reason = string.Empty;
        }
    }
}
