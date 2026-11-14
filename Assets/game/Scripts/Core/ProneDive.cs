using System;
using System.Numerics;

namespace Fps.Core
{
    /// <summary>
    /// ダッシュ中に伏せたときの飛び込み 1 回の水平の動き。<see cref="TryStart"/> した向きへ <see cref="ProneSpec.DiveDistance"/> を
    /// <see cref="ProneSpec.DiveSeconds"/> かけて動く。速さは始めが最も速く、終わりに 0 になるよう一定の割合で落とす (腹で滑って止まる)。
    /// 時間は <see cref="Step"/> で進める。衝突判定と縦の動きは呼ぶ側が受け持つ。
    /// 座標は <see cref="PlayerMotor"/> と同じく、水平ベクトルの X がワールドの X、Y がワールドの Z。
    /// </summary>
    public sealed class ProneDive
    {
        private Vector2 _direction;
        private float _elapsed;
        private bool _isDiving;

        /// <param name="spec">距離と秒数を読む伏せの設定。null 不可。</param>
        public ProneDive(ProneSpec spec)
        {
            if (spec == null)
            {
                throw new ArgumentNullException(nameof(spec));
            }

            Distance = spec.DiveDistance;
            Seconds = spec.DiveSeconds;
        }

        /// <summary>1 回で動く水平距離 (m)。</summary>
        public float Distance { get; }

        /// <summary>1 回にかかる秒数。0 より大きい。</summary>
        public float Seconds { get; }

        /// <summary>飛び込んでいる最中か。</summary>
        public bool IsDiving => _isDiving;

        /// <summary>飛び込んでいる向き (水平の単位ベクトル)。飛び込んでいない間は最後の向き (一度も無ければ 0)。</summary>
        public Vector2 Direction => _direction;

        /// <summary>今の飛び込みを始めてからの秒数。飛び込んでいなければ 0。</summary>
        public float Elapsed => _isDiving ? _elapsed : 0f;

        /// <summary>今の水平速度 (m/s)。始めは平均の 2 倍で、終わりに 0。飛び込んでいなければ 0。</summary>
        public Vector2 Velocity => _isDiving ? _direction * (2f * Distance / Seconds * (1f - (_elapsed / Seconds))) : Vector2.Zero;

        /// <summary>direction (ワールドの水平ベクトル、長さは問わない) へ飛び込み始める。飛び込んでいる最中なら何もせず false。</summary>
        /// <exception cref="ArgumentOutOfRangeException">direction が有限でないか長さ 0。</exception>
        public bool TryStart(Vector2 direction)
        {
            if (!IsFinite(direction.X) || !IsFinite(direction.Y) || direction.LengthSquared() <= 0f)
            {
                throw new ArgumentOutOfRangeException(nameof(direction), direction, "direction must be finite and not zero");
            }

            if (_isDiving)
            {
                return false;
            }

            _direction = Vector2.Normalize(direction);
            _elapsed = 0f;
            _isDiving = true;
            return true;
        }

        /// <summary>
        /// deltaSeconds 進め、その間の水平の変位 (m) を返す。飛び込んでいなければ 0。
        /// 終わる Step では残りの距離ちょうどを返すので、合計はフレームの刻みによらず <see cref="Distance"/> になる。
        /// </summary>
        /// <param name="deltaSeconds">経過秒数。0 以上の有限値。</param>
        public Vector2 Step(float deltaSeconds)
        {
            if (!(deltaSeconds >= 0f) || float.IsInfinity(deltaSeconds))
            {
                throw new ArgumentOutOfRangeException(nameof(deltaSeconds), deltaSeconds, "deltaSeconds must be finite and not negative");
            }

            if (!_isDiving)
            {
                return Vector2.Zero;
            }

            var before = _elapsed;
            var after = Math.Min(Seconds, before + deltaSeconds);
            _elapsed = after;

            // 進んだ距離の割合 (速さを一定の割合で落とすので 2u - u²) の差で出し、最後に始点からちょうど Distance の所で止める。
            var displacement = _direction * (Distance * (Travelled(after / Seconds) - Travelled(before / Seconds)));
            if (after >= Seconds)
            {
                Finish();
            }

            return displacement;
        }

        /// <summary>
        /// 予測の巻き戻しとサーバーの tick の間で、写した途中の中身 (<see cref="IsDiving"/>・<see cref="Direction"/>・<see cref="Elapsed"/>) に戻す。
        /// 同じ中身から同じ秒数で <see cref="Step"/> すれば同じ変位になる。
        /// </summary>
        /// <exception cref="ArgumentOutOfRangeException">飛び込んでいるのに direction が有限でないか長さ 0、または elapsed が 0 以上 <see cref="Seconds"/> 未満でない。</exception>
        public void Restore(bool diving, Vector2 direction, float elapsed)
        {
            if (!diving)
            {
                Finish();
                return;
            }

            if (!IsFinite(direction.X) || !IsFinite(direction.Y) || direction.LengthSquared() <= 0f)
            {
                throw new ArgumentOutOfRangeException(nameof(direction), direction, "direction must be finite and not zero");
            }

            if (!(elapsed >= 0f && elapsed < Seconds))
            {
                throw new ArgumentOutOfRangeException(nameof(elapsed), elapsed, "elapsed must be in [0, Seconds)");
            }

            _direction = direction;
            _elapsed = elapsed;
            _isDiving = true;
        }

        /// <summary>飛び込みを途中でやめる (死亡・リスポーンなど)。飛び込んでいたら true。</summary>
        public bool Cancel()
        {
            if (!_isDiving)
            {
                return false;
            }

            Finish();
            return true;
        }

        private static float Travelled(float fraction)
        {
            return (2f * fraction) - (fraction * fraction);
        }

        private void Finish()
        {
            _isDiving = false;
            _elapsed = 0f;
        }

        private static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }
    }
}
