using System;
using System.Numerics;

namespace Fps.Core
{
    /// <summary>
    /// 回避 (<see cref="Dodge.On"/>) 1 回の水平の動き。<see cref="TryStart"/> した向きへ <see cref="MotorSpec.DodgeDistance"/> を
    /// <see cref="MotorSpec.DodgeSeconds"/> かけて一定の速さで動き、終わってから <see cref="CooldownSeconds"/> は次を始めない。
    /// 時間は <see cref="Step"/> で進め、転がっていない間も呼んで待ち時間を減らす。衝突判定と縦の動きは呼ぶ側が受け持つ。
    /// 座標は <see cref="PlayerMotor"/> と同じく、水平ベクトルの X がワールドの X、Y がワールドの Z。
    /// </summary>
    public sealed class DodgeMotion
    {
        /// <summary>生成時に指定しなかったときの <see cref="CooldownSeconds"/>。</summary>
        public const float DefaultCooldownSeconds = 0.5f;

        private Vector2 _direction;
        private float _elapsed;
        private float _cooldownRemaining;
        private bool _isRolling;

        /// <param name="spec">距離と秒数を読む移動の設定。null 不可。</param>
        /// <param name="cooldownSeconds">終わってから次を始められるまでの秒数。0 以上の有限値。</param>
        public DodgeMotion(MotorSpec spec, float cooldownSeconds = DefaultCooldownSeconds)
        {
            if (spec == null)
            {
                throw new ArgumentNullException(nameof(spec));
            }

            if (!(cooldownSeconds >= 0f) || float.IsInfinity(cooldownSeconds))
            {
                throw new ArgumentOutOfRangeException(nameof(cooldownSeconds), cooldownSeconds, "cooldownSeconds must be finite and not negative");
            }

            Distance = spec.DodgeDistance;
            Seconds = spec.DodgeSeconds;
            CooldownSeconds = cooldownSeconds;
        }

        /// <summary>1 回で動く水平距離 (m)。</summary>
        public float Distance { get; }

        /// <summary>1 回にかかる秒数。0 より大きい。</summary>
        public float Seconds { get; }

        /// <summary>終わってから次を始められるまでの秒数。</summary>
        public float CooldownSeconds { get; }

        /// <summary>転がっている最中か。</summary>
        public bool IsRolling => _isRolling;

        /// <summary>今すぐ <see cref="TryStart"/> できるか (転がっておらず、待ち時間も残っていない)。</summary>
        public bool IsReady => !_isRolling && _cooldownRemaining <= 0f;

        /// <summary>転がっている向き (水平の単位ベクトル)。転がっていない間は最後に転がった向き (一度も無ければ 0)。</summary>
        public Vector2 Direction => _direction;

        /// <summary>今の転がりを始めてからの秒数。転がっていなければ 0。</summary>
        public float Elapsed => _isRolling ? _elapsed : 0f;

        /// <summary>次を始められるまでの残りの秒数。転がっている間は 0 (待ちは終わってから数える)。</summary>
        public float CooldownRemaining => _cooldownRemaining;

        /// <summary>転がっている間の水平速度 (m/s)。転がっていなければ 0。</summary>
        public Vector2 Velocity => _isRolling ? _direction * (Distance / Seconds) : Vector2.Zero;

        /// <summary>
        /// 移動入力 (X 右・Y 前) から転がる向きをワールドの水平の単位ベクトルで決める。入力が無ければ真後ろ。
        /// 入力の大きさは向きにだけ使い、距離は変えない (スティックを少し倒しただけでも同じだけ転がる)。
        /// </summary>
        /// <param name="moveInput">移動入力。各成分が有限値。</param>
        /// <param name="yawRadians">向き (<see cref="PlayerMotor.Step"/> と同じ)。有限値。</param>
        public static Vector2 ResolveDirection(Vector2 moveInput, float yawRadians)
        {
            if (!IsFinite(moveInput.X) || !IsFinite(moveInput.Y))
            {
                throw new ArgumentOutOfRangeException(nameof(moveInput), moveInput, "moveInput must be finite");
            }

            if (!IsFinite(yawRadians))
            {
                throw new ArgumentOutOfRangeException(nameof(yawRadians), yawRadians, "yawRadians must be finite");
            }

            // スティックの遊び程度の入力で向きが暴れないよう、ほぼ 0 は無入力として後ろへ転がす。
            var local = moveInput.LengthSquared() > 1e-4f ? Vector2.Normalize(moveInput) : new Vector2(0f, -1f);
            return Vector2.Normalize(PlayerMotor.ToWorld(local, yawRadians));
        }

        /// <summary>
        /// direction (ワールドの水平ベクトル、長さは問わない) へ転がり始める。転がっている最中か待ち時間が残っていれば何もせず false。
        /// </summary>
        public bool TryStart(Vector2 direction)
        {
            if (!IsFinite(direction.X) || !IsFinite(direction.Y) || direction.LengthSquared() <= 0f)
            {
                throw new ArgumentOutOfRangeException(nameof(direction), direction, "direction must be finite and not zero");
            }

            if (!IsReady)
            {
                return false;
            }

            _direction = Vector2.Normalize(direction);
            _elapsed = 0f;
            _isRolling = true;
            return true;
        }

        /// <summary>
        /// deltaSeconds 進め、その間の水平の変位 (m) を返す。転がっていなければ待ち時間を減らして 0 を返す。
        /// 終わる Step では残りの距離ちょうどを返し、余った時間は待ち時間に回すので、合計はフレームの刻みによらず <see cref="Distance"/> になる。
        /// </summary>
        /// <param name="deltaSeconds">経過秒数。0 以上の有限値。</param>
        public Vector2 Step(float deltaSeconds)
        {
            if (!(deltaSeconds >= 0f) || float.IsInfinity(deltaSeconds))
            {
                throw new ArgumentOutOfRangeException(nameof(deltaSeconds), deltaSeconds, "deltaSeconds must be finite and not negative");
            }

            if (!_isRolling)
            {
                _cooldownRemaining = Math.Max(0f, _cooldownRemaining - deltaSeconds);
                return Vector2.Zero;
            }

            var before = _elapsed;
            var after = Math.Min(Seconds, before + deltaSeconds);
            _elapsed = after;

            // 進んだ割合の差で出すと、途中の丸めが積み重ならず最後に始点からちょうど Distance の所で止まる。
            var displacement = _direction * (Distance * ((after / Seconds) - (before / Seconds)));
            if (after >= Seconds)
            {
                Finish(deltaSeconds - (after - before));
            }

            return displacement;
        }

        /// <summary>転がりを途中でやめ、待ち時間を始める (死亡など)。転がっていたら true。</summary>
        public bool Cancel()
        {
            if (!_isRolling)
            {
                return false;
            }

            Finish(0f);
            return true;
        }

        /// <summary>転がりも待ち時間も無かったことにする (リスポーンで位置を戻したときなど)。</summary>
        public void Reset()
        {
            _isRolling = false;
            _elapsed = 0f;
            _cooldownRemaining = 0f;
        }

        private void Finish(float leftoverSeconds)
        {
            _isRolling = false;
            _elapsed = 0f;
            _cooldownRemaining = Math.Max(0f, CooldownSeconds - leftoverSeconds);
        }

        private static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }
    }
}
