using System;
using System.Numerics;

namespace Fps.Core
{
    /// <summary>
    /// はしご 1 本に 1 回つかまっている間の上り下り。足元の高さ (<see cref="Height"/>、下端から) だけを持ち、
    /// <see cref="Step"/> が前後の入力で <see cref="LadderSpec.ClimbSpeed"/> で上り下りさせ、上の端・下の端・ジャンプで離れる結果を返す。
    /// 体の置き場所 (<see cref="LadderSpan.FeetAt"/>) と衝突は呼ぶ側が受け持つ。
    /// 上の端からつかまったときは、つかまったときに押していた向き (前でも後ろでも) を、前後の入力を一度離すまで下りとして読む
    /// (足場の縁へ進んだ入力のまま下りていき、つかまった途端に上の端へ戻らない)。
    /// </summary>
    public sealed class LadderClimb
    {
        /// <summary>前後の入力の大きさがこれ未満なら、押していないとみなす。</summary>
        public const float InputDeadZone = 0.1f;

        /// <summary>上の端・下の端で降りた足元を床から浮かせて置く高さ (m)。置いた Tick の <see cref="LandingDrop"/> で床に着く。</summary>
        public const float LandingLift = 0.02f;

        /// <summary>降りた Tick に下へ動かす量 (m)。床に触れさせて接地にする。</summary>
        public const float LandingDrop = 0.1f;

        private bool _latchUntilReleased;
        private int _latchedDownSign;

        /// <param name="span">はしごの形。</param>
        /// <param name="spec">上り下りの調整値。null 不可。</param>
        /// <param name="entry">つかまった所。<see cref="LadderEntry.None"/> は不可。</param>
        /// <param name="feetHeight">つかまったときの足元の、下端からの高さ (m)。<see cref="LadderEntry.Front"/> のときだけ使う。有限値。</param>
        public LadderClimb(LadderSpan span, LadderSpec spec, LadderEntry entry, float feetHeight)
        {
            if (span.Height <= 0f)
            {
                throw new ArgumentException("span must be a constructed LadderSpan", nameof(span));
            }

            if (spec == null)
            {
                throw new ArgumentNullException(nameof(spec));
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
            var highest = Math.Max(0f, span.Height - LadderSpan.TopEntryDepth);
            Height = entry == LadderEntry.Top ? highest : Clamp(feetHeight, 0f, highest);
            _latchUntilReleased = entry == LadderEntry.Top;
        }

        /// <summary>はしごの形。</summary>
        public LadderSpan Span { get; }

        /// <summary>上り下りの調整値。</summary>
        public LadderSpec Spec { get; }

        /// <summary>つかまった所。</summary>
        public LadderEntry Entry { get; }

        /// <summary>今の足元の、下端からの高さ (m)。0 以上 <see cref="LadderSpan.Height"/> 以下。</summary>
        public float Height { get; private set; }

        /// <summary>直前の <see cref="Step"/> の上下の速さ (m/s、上が正)。</summary>
        public float VerticalSpeed { get; private set; }

        /// <summary>前後の入力を読み替えているか (上の端からつかまり、まだ入力を離していない)。</summary>
        public bool IsInputLatched => _latchUntilReleased;

        /// <summary>読み替えている入力の向き (最初に押した向きの符号)。読み替えていないか、まだ押していなければ 0。</summary>
        public int LatchedDownSign => _latchUntilReleased ? _latchedDownSign : 0;

        /// <summary>
        /// 予測の巻き戻しとサーバーの tick の間で、写した途中の中身 (<see cref="Height"/>・<see cref="IsInputLatched"/>・<see cref="LatchedDownSign"/>) から作り直す。
        /// 同じ中身から同じ入力で <see cref="Step"/> すれば同じ結果になる。height は 0 以上 span の高さ以下に丸め、latchedDownSign は -1・0・1 のどれか。
        /// </summary>
        public static LadderClimb Resume(
            LadderSpan span, LadderSpec spec, LadderEntry entry, float height, bool inputLatched, int latchedDownSign)
        {
            if (latchedDownSign < -1 || latchedDownSign > 1)
            {
                throw new ArgumentOutOfRangeException(nameof(latchedDownSign), latchedDownSign, "latchedDownSign must be -1, 0 or 1");
            }

            var climb = new LadderClimb(span, spec, entry, height);
            climb.Height = Clamp(height, 0f, span.Height);
            climb._latchUntilReleased = inputLatched;
            climb._latchedDownSign = inputLatched ? latchedDownSign : 0;
            return climb;
        }

        /// <summary>
        /// 1 Tick 進める。jumpPressed なら高さを変えずに <see cref="LadderOutcome.JumpOff"/>。
        /// そうでなければ forward (前が正、-1〜1 に丸める) の向きに上り下りし、上の端に届けば <see cref="LadderOutcome.ExitTop"/>、
        /// 下りていて下の端に届くか grounded (直前の移動で足が地面に着いた) なら <see cref="LadderOutcome.ExitBottom"/>、
        /// どちらでもなければ <see cref="LadderOutcome.Climbing"/>。
        /// </summary>
        /// <param name="forward">前後の入力。有限値。</param>
        /// <param name="jumpPressed">この Tick にジャンプが押されたか。</param>
        /// <param name="grounded">直前の移動で足が地面に着いたか。</param>
        /// <param name="deltaSeconds">進める秒数。0 以上の有限値。</param>
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

        /// <summary>今の高さで、半径 radius の体がつかまっている足元 (<see cref="LadderSpan.FeetAt"/>)。</summary>
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
