using System;

namespace Fps.Core
{
    // 技 1 回の時間だけを進める。位置合わせの動き・操作の停止・結果の適用は呼ぶ側 (ContextMeleeAction) が行う。
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

        /// <exception cref="ArgumentNullException">move が null。</exception>
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

        // 結果を出した Tick で技を終える。呼ぶ側は Tick の前に Move を覚えておき、Resolved を受けたらその技の結果を出す。
        /// <exception cref="ArgumentOutOfRangeException">deltaSeconds が負か有限でない。</exception>
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

        // 撃たれたなど、技の設定で止められる理由のとき。中断できない技なら止めずに false。
        public bool TryInterrupt()
        {
            if (Move == null || !Move.Interruptible)
            {
                return false;
            }

            Clear();
            return true;
        }

        // 自分が倒れた・相手が消えたなど、技の設定に関わらず続けられないとき。
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
