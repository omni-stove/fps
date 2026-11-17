using System;

namespace Shooter.Core
{
    // 長押しのつもりで押した瞬間に照準の先へ指示が出ないよう、Tap は離したときに出す。
    public sealed class SquadOrderButton
    {
        private readonly float _holdSeconds;
        private bool _down;
        private bool _held;
        private float _pressedAt;

        public SquadOrderButton(float holdSeconds)
        {
            if (!(holdSeconds > 0f) || float.IsInfinity(holdSeconds))
            {
                throw new ArgumentOutOfRangeException(nameof(holdSeconds), holdSeconds, "holdSeconds must be positive and finite");
            }

            _holdSeconds = holdSeconds;
        }

        public bool IsDown => _down;

        public SquadOrderGesture Tick(bool down, float now)
        {
            if (float.IsNaN(now) || float.IsInfinity(now))
            {
                throw new ArgumentOutOfRangeException(nameof(now), now, "now must be finite");
            }

            if (down)
            {
                if (!_down)
                {
                    _down = true;
                    _held = false;
                    _pressedAt = now;
                    return SquadOrderGesture.None;
                }

                if (!_held && now - _pressedAt >= _holdSeconds)
                {
                    _held = true;
                    return SquadOrderGesture.Hold;
                }

                return SquadOrderGesture.None;
            }

            if (!_down)
            {
                return SquadOrderGesture.None;
            }

            _down = false;
            var wasHeld = _held;
            _held = false;
            return wasHeld ? SquadOrderGesture.None : SquadOrderGesture.Tap;
        }

        // ポーズを開いたときや倒れたときに、その後で離しても指示が出ないよう押していないことにする。
        public void Reset()
        {
            _down = false;
            _held = false;
        }
    }
}
