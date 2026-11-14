using System;

namespace Fps.Core
{
    /// <summary>
    /// SquadCommand のボタンの押し方を、押されているかの並びから決める。時刻は呼ぶ側が決めた物差し (秒) で渡す。
    /// <list type="bullet">
    /// <item>押してから holdSeconds 経つ前に離したら、離した tick に <see cref="SquadOrderGesture.Tap"/>。</item>
    /// <item>押したまま holdSeconds 経ったら、その tick に 1 度だけ <see cref="SquadOrderGesture.Hold"/>。離すまで次は出さない。</item>
    /// </list>
    /// 指示は離したときに出すので、長押しのつもりで押した瞬間に照準の先へ指示が出てしまうことはない。
    /// </summary>
    public sealed class SquadOrderButton
    {
        private readonly float _holdSeconds;
        private bool _down;
        private bool _held;
        private float _pressedAt;

        /// <param name="holdSeconds">長押しとみなす秒数。0 より大きい有限値。</param>
        public SquadOrderButton(float holdSeconds)
        {
            if (!(holdSeconds > 0f) || float.IsInfinity(holdSeconds))
            {
                throw new ArgumentOutOfRangeException(nameof(holdSeconds), holdSeconds, "holdSeconds must be positive and finite");
            }

            _holdSeconds = holdSeconds;
        }

        /// <summary>押されているか (前の <see cref="Tick"/> で)。</summary>
        public bool IsDown => _down;

        /// <param name="down">この tick にボタンが押されているか。</param>
        /// <param name="now">この tick の時刻 (秒)。有限の値。</param>
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

        /// <summary>押していないことにする。ポーズを開いたときや倒れたときに呼び、離したときに指示が出ないようにする。</summary>
        public void Reset()
        {
            _down = false;
            _held = false;
        }
    }
}
