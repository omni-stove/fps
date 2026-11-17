using System;

namespace Shooter.Core
{
    public sealed class ContextMeleeRun
    {
        private float _elapsed;

        public ContextMeleeMove Move { get; private set; }

        public ContextMeleePhase Phase { get; private set; }

        public bool IsActive => Move != null;

        public float Elapsed => Move != null ? _elapsed : 0f;

        // 位置合わせの秒数が 0 の技は、始めた時点で合わせ終えたものとして 1 を返す (0 で割らない)。
        public float AlignProgress
        {
            get
            {
                if (Move == null)
                {
                    return 0f;
                }

                return Move.AlignSeconds > 0f ? Math.Min(1f, _elapsed / Move.AlignSeconds) : 1f;
            }
        }

        public bool Begin(ContextMeleeMove move)
        {
            if (move == null)
            {
                throw new ArgumentNullException(nameof(move));
            }

            if (IsActive)
            {
                return false;
            }

            Move = move;
            Phase = ContextMeleePhase.Aligning;
            _elapsed = 0f;
            return true;
        }

        public ContextMeleeEvents Tick(float deltaSeconds)
        {
            if (!(deltaSeconds >= 0f) || float.IsInfinity(deltaSeconds))
            {
                throw new ArgumentOutOfRangeException(nameof(deltaSeconds), deltaSeconds, "deltaSeconds must be a non-negative finite number");
            }

            if (Move == null)
            {
                return ContextMeleeEvents.None;
            }

            _elapsed += deltaSeconds;
            var events = ContextMeleeEvents.None;
            if (Phase == ContextMeleePhase.Aligning && _elapsed >= Move.AlignSeconds)
            {
                Phase = ContextMeleePhase.Performing;
                events |= ContextMeleeEvents.Aligned;
            }

            if (Phase == ContextMeleePhase.Performing && _elapsed >= Move.TotalSeconds)
            {
                events |= ContextMeleeEvents.Resolved;
                Clear();
            }

            return events;
        }

        public bool TryInterrupt()
        {
            if (Move == null || !Move.Interruptible)
            {
                return false;
            }

            Clear();
            return true;
        }

        public bool Cancel()
        {
            if (Move == null)
            {
                return false;
            }

            Clear();
            return true;
        }

        private void Clear()
        {
            Move = null;
            Phase = ContextMeleePhase.Idle;
            _elapsed = 0f;
        }
    }
}
