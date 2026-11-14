using System;
using System.Globalization;
using System.Numerics;

namespace Fps.Core
{
    /// <summary>
    /// スライディング (<see cref="Slide.On"/>) 1 回の、足場に沿った速さの移り変わり。<see cref="TryStart"/> した向きへ
    /// <see cref="StartSpeed"/> (それより速く走っていればその速さ) で滑り出し、毎秒 <see cref="Deceleration"/> ずつ遅くなる。
    /// 下り坂では傾き (sin) に <see cref="SlopeAcceleration"/> を掛けた分だけ速くなり、上り坂では同じだけ遅くなる。
    /// <see cref="MinSeconds"/> 滑った後に速さが <see cref="EndSpeed"/> 以下になるか、<see cref="MaxSeconds"/> 滑ったら終わる
    /// (速さが 0 になったら最短の秒数を待たずに終わる)。ジャンプ・宙に出た・外の理由での終わりは呼ぶ側が <see cref="Finish"/> で知らせる。
    /// 向きを変えるのと衝突判定・縦の動きは呼ぶ側が受け持つ。座標は <see cref="PlayerMotor"/> と同じく、水平ベクトルの X がワールドの X、Y がワールドの Z。
    /// </summary>
    public sealed class SlideMotion
    {
        public const string LogTag = "[Slide]";

        /// <summary>生成時に指定しなかったときの <see cref="StartSpeed"/> (m/s)。既定のダッシュ (8m/s) より速く滑り出す。</summary>
        public const float DefaultStartSpeed = 11f;

        /// <summary>生成時に指定しなかったときの <see cref="Deceleration"/> (m/s²)。平らな床で 1 秒ほどで止まる。</summary>
        public const float DefaultDeceleration = 8f;

        /// <summary>生成時に指定しなかったときの <see cref="EndSpeed"/> (m/s)。既定のしゃがみ歩き (2.5m/s) より少し速い。</summary>
        public const float DefaultEndSpeed = 3f;

        /// <summary>生成時に指定しなかったときの <see cref="MinSeconds"/>。</summary>
        public const float DefaultMinSeconds = 0.4f;

        /// <summary>生成時に指定しなかったときの <see cref="MaxSeconds"/>。</summary>
        public const float DefaultMaxSeconds = 3f;

        /// <summary>
        /// 生成時に指定しなかったときの <see cref="SlopeAcceleration"/> (m/s²)。既定の重力 (20m/s²) と同じで、
        /// 15° の下り坂では減速の 3 分の 2 ほどを打ち消して平らな床の 3 倍近く滑る。
        /// </summary>
        public const float DefaultSlopeAcceleration = 20f;

        private Vector2 _direction;
        private float _speed;
        private float _elapsed;
        private float _distance;
        private bool _isSliding;
        private SlideEnd _lastEnd = SlideEnd.None;

        /// <summary>既定の値 (<see cref="DefaultStartSpeed"/> など) で作る。</summary>
        public SlideMotion()
            : this(DefaultStartSpeed, DefaultDeceleration, DefaultEndSpeed, DefaultMinSeconds, DefaultMaxSeconds, DefaultSlopeAcceleration)
        {
        }

        /// <param name="startSpeed">滑り出す速さ (m/s)。endSpeed より大きい有限値。</param>
        /// <param name="deceleration">平らな床で 1 秒あたりに落ちる速さ (m/s²)。0 以上の有限値。</param>
        /// <param name="endSpeed">これ以下に落ちたら終わる速さ (m/s)。0 以上の有限値。</param>
        /// <param name="minSeconds">速さが落ちても続ける最短の秒数。0 以上、maxSeconds 以下の有限値。</param>
        /// <param name="maxSeconds">最長の秒数。0 より大きい有限値。</param>
        /// <param name="slopeAcceleration">傾きの sin 1 あたりに足す加速度 (m/s²)。0 以上の有限値。</param>
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

        /// <summary>滑り出す速さ (m/s)。</summary>
        public float StartSpeed { get; }

        /// <summary>平らな床で 1 秒あたりに落ちる速さ (m/s²)。</summary>
        public float Deceleration { get; }

        /// <summary><see cref="MinSeconds"/> の後、これ以下に落ちたら終わる速さ (m/s)。</summary>
        public float EndSpeed { get; }

        /// <summary>速さが落ちても続ける最短の秒数。</summary>
        public float MinSeconds { get; }

        /// <summary>最長の秒数。</summary>
        public float MaxSeconds { get; }

        /// <summary>坂の傾きの sin 1 あたりの加速度 (m/s²)。下りで足し、上りで引く。</summary>
        public float SlopeAcceleration { get; }

        /// <summary>滑っている最中か。</summary>
        public bool IsSliding => _isSliding;

        /// <summary>滑っている向き (水平の単位ベクトル)。滑っていない間は最後に滑った向き (一度も無ければ 0)。</summary>
        public Vector2 Direction => _direction;

        /// <summary>今の足場に沿った速さ (m/s)。滑っていない間は最後に滑っていたときの速さ。</summary>
        public float Speed => _speed;

        /// <summary>今の (終わっていれば最後の) スライディングを始めてからの秒数。</summary>
        public float Elapsed => _elapsed;

        /// <summary>今の (終わっていれば最後の) スライディングで足場に沿って進んだ距離 (m)。</summary>
        public float Distance => _distance;

        /// <summary>最後のスライディングが終わった理由。滑っている最中と一度も滑っていなければ <see cref="SlideEnd.None"/>。</summary>
        public SlideEnd LastEnd => _lastEnd;

        /// <summary>
        /// direction (ワールドの水平ベクトル、長さは問わない) へ、<see cref="StartSpeed"/> と entrySpeed の速いほうで滑り始める。
        /// 滑っている最中なら何もせず false。
        /// </summary>
        /// <param name="direction">滑る向き。有限で 0 でない。</param>
        /// <param name="entrySpeed">滑り始める直前の水平の速さ (m/s)。0 以上の有限値。</param>
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

        /// <summary>
        /// deltaSeconds 進め、その間に足場に沿って進む距離 (m) を返す。滑っていなければ 0。
        /// 速さは一定の加速度で変わるとして始点と終点の平均で進めるので、フレームの刻みによらず同じ距離になる。
        /// <see cref="MaxSeconds"/> を超える分の時間は進めない。終わったら <see cref="IsSliding"/> が false になり、<see cref="LastEnd"/> に理由が入る。
        /// </summary>
        /// <param name="deltaSeconds">経過秒数。0 以上の有限値。</param>
        /// <param name="slopeSine">滑る向きに進んだときの足場の傾きの sin。下りが正、上りが負。-1 以上 1 以下。</param>
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

        /// <summary>
        /// 速さを maxSpeed までに抑える (壁に当たって実際には進めなかったときなど)。滑っていないか既に遅ければ何もしない。
        /// 抑えた結果は次の <see cref="Step"/> で終わりの判定に使う。
        /// </summary>
        /// <param name="maxSpeed">上限の速さ (m/s)。0 以上の有限値。</param>
        public void LimitSpeed(float maxSpeed)
        {
            RequireNonNegative(maxSpeed, nameof(maxSpeed));
            if (_isSliding && _speed > maxSpeed)
            {
                _speed = maxSpeed;
            }
        }

        /// <summary>reason で終える。滑っていなければ何もせず false。</summary>
        /// <exception cref="ArgumentOutOfRangeException">reason が <see cref="SlideEnd.None"/> か定義されていない値。</exception>
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

        /// <summary>滑り始めのログ。例: <c>[Slide] start speed=11.00 direction=(0.00, 1.00)</c>。</summary>
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

        /// <summary>終わりのログ。例: <c>[Slide] end reason=Stopped speed=2.90 seconds=1.02 distance=7.40</c>。</summary>
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
