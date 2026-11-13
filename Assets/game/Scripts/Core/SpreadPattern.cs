using System;
using System.Numerics;

namespace Fps.Core
{
    /// <summary>
    /// 武器の弾のばらつき。散らばりの半角は腰撃ち／覗き込み (ADS) と移動の速さから <see cref="WeaponSpec"/> の値で決め、
    /// 向きはその円錐の中から種 (プレイヤー Id・発射番号) だけで決める。同じ種なら必ず同じ向きを返す。
    /// 座標は Y が上。
    /// </summary>
    public static class SpreadPattern
    {
        /// <summary>散らばりの半角の上限 (度)。<see cref="WeaponSpec.HipSpreadDegrees"/> などの上限と同じ。</summary>
        public const float MaxDegrees = 90f;

        /// <summary>円錐の中の向きの極角 (中心からの角度) を選ぶ値の系列。</summary>
        public const uint PolarStream = 1u;

        /// <summary>円錐の中の向きの方位角 (中心の周りの角度) を選ぶ値の系列。</summary>
        public const uint AzimuthStream = 2u;

        /// <summary>反動の左右 (<see cref="RecoilState"/>) を選ぶ値の系列。</summary>
        public const uint RecoilYawStream = 3u;

        private const float _minLength = 1e-6f;

        // 2^-53。64 ビットの上位 53 ビットを [0, 1) の double にする。
        private const double _unitScale = 1.0 / 9007199254740992.0;

        /// <summary>
        /// 今撃ったときの散らばりの半角 (度)。腰撃ちの <see cref="WeaponSpec.HipSpreadDegrees"/> と ADS の <see cref="WeaponSpec.AdsSpreadDegrees"/> を
        /// adsProgress で補間し、<see cref="WeaponSpec.MoveSpreadDegrees"/> に moveFraction を掛けて足す。<see cref="MaxDegrees"/> を超えない。
        /// </summary>
        /// <param name="spec">撃つ武器。</param>
        /// <param name="adsProgress">覗き込みの進み具合。0 で腰撃ち、1 で覗き込み切り。範囲外は 0〜1 に収める。</param>
        /// <param name="moveFraction">移動の速さの割合 (<see cref="MoveFraction"/>)。0 で止まっている。範囲外は 0〜1 に収める。</param>
        /// <exception cref="ArgumentNullException">spec が null のとき。</exception>
        /// <exception cref="ArgumentOutOfRangeException">adsProgress か moveFraction が NaN のとき。</exception>
        public static float ConeDegrees(WeaponSpec spec, float adsProgress, float moveFraction)
        {
            if (spec == null)
            {
                throw new ArgumentNullException(nameof(spec));
            }

            if (float.IsNaN(adsProgress))
            {
                throw new ArgumentOutOfRangeException(nameof(adsProgress), adsProgress, "adsProgress must not be NaN");
            }

            if (float.IsNaN(moveFraction))
            {
                throw new ArgumentOutOfRangeException(nameof(moveFraction), moveFraction, "moveFraction must not be NaN");
            }

            var ads = Clamp01(adsProgress);
            var move = Clamp01(moveFraction);
            var aim = spec.HipSpreadDegrees + ((spec.AdsSpreadDegrees - spec.HipSpreadDegrees) * ads);
            return Math.Min(MaxDegrees, aim + (spec.MoveSpreadDegrees * move));
        }

        /// <summary>
        /// 移動の速さの割合。speed が fullSpeed 以上なら 1、0 なら 0。fullSpeed が 0 以下なら、動いていれば 1。
        /// </summary>
        /// <param name="speed">今の水平の速さ (m/秒)。負は 0 として扱う。</param>
        /// <param name="fullSpeed">散らばりを <see cref="WeaponSpec.MoveSpreadDegrees"/> まで足し切る速さ (m/秒)。</param>
        /// <exception cref="ArgumentOutOfRangeException">値が有限でないとき。</exception>
        public static float MoveFraction(float speed, float fullSpeed)
        {
            if (!IsFinite(speed))
            {
                throw new ArgumentOutOfRangeException(nameof(speed), speed, "speed must be finite");
            }

            if (!IsFinite(fullSpeed))
            {
                throw new ArgumentOutOfRangeException(nameof(fullSpeed), fullSpeed, "fullSpeed must be finite");
            }

            if (speed <= 0f)
            {
                return 0f;
            }

            return fullSpeed <= 0f ? 1f : Clamp01(speed / fullSpeed);
        }

        /// <summary>
        /// aim を中心とし、半角 halfAngleDegrees の円錐の中で、種 (playerId・shotIndex) から決めた向き (長さ 1)。
        /// 種を一様な乱数と見なすと、円錐の上で一様 (立体角あたり同じ確からしさ) になる。0° なら aim を長さ 1 にしたものをそのまま返す。
        /// </summary>
        /// <param name="aim">狙った向き。長さは問わない。</param>
        /// <param name="halfAngleDegrees">散らばりの半角 (度)。0 以上 <see cref="MaxDegrees"/> 以下。</param>
        /// <param name="playerId">撃ったプレイヤーの Id。</param>
        /// <param name="shotIndex">そのプレイヤーの発射番号。</param>
        /// <exception cref="ArgumentOutOfRangeException">aim が有限でないか長さ 0、halfAngleDegrees が範囲外のとき。</exception>
        public static Vector3 Direction(Vector3 aim, float halfAngleDegrees, int playerId, int shotIndex)
        {
            if (!IsFinite(aim.X) || !IsFinite(aim.Y) || !IsFinite(aim.Z) || aim.Length() < _minLength)
            {
                throw new ArgumentOutOfRangeException(nameof(aim), aim, "aim must be finite and not zero");
            }

            if (!(halfAngleDegrees >= 0f) || halfAngleDegrees > MaxDegrees)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(halfAngleDegrees), halfAngleDegrees, "halfAngleDegrees must be between 0 and " + MaxDegrees);
            }

            var forward = Vector3.Normalize(aim);
            if (halfAngleDegrees == 0f)
            {
                return forward;
            }

            // 円錐の上で一様にするには、極角ではなくその cos を [cos(半角), 1] で一様に選ぶ (球帯の面積は高さに比例する)。
            var cosMax = Math.Cos(halfAngleDegrees * Math.PI / 180.0);
            var cosTheta = 1.0 - (Sample(playerId, shotIndex, PolarStream) * (1.0 - cosMax));
            var sinTheta = Math.Sqrt(Math.Max(0.0, 1.0 - (cosTheta * cosTheta)));
            var phi = Sample(playerId, shotIndex, AzimuthStream) * 2.0 * Math.PI;

            Perpendiculars(forward, out var u, out var v);
            var sideways = (u * (float)Math.Cos(phi)) + (v * (float)Math.Sin(phi));
            return Vector3.Normalize((forward * (float)cosTheta) + (sideways * (float)sinTheta));
        }

        /// <summary>
        /// 種 (playerId・shotIndex) と系列 (stream) だけで決まる [0, 1) の値。系列を変えると同じ種から別の値が出る。
        /// </summary>
        public static double Sample(int playerId, int shotIndex, uint stream)
        {
            // System.Random は実装 (Mono と .NET) で列が変わりうるので使わず、整数の演算だけで決める (クライアントとサーバーで揃えるため)。
            var x = Mix(((ulong)(uint)playerId << 32) | (uint)shotIndex);
            x = Mix(x ^ (stream * 0x9E3779B97F4A7C15UL));
            return (x >> 11) * _unitScale;
        }

        // SplitMix64 の仕上げ。近い入力 (連番の発射番号) からでも偏りの無い値を出す。
        private static ulong Mix(ulong z)
        {
            z += 0x9E3779B97F4A7C15UL;
            z = (z ^ (z >> 30)) * 0xBF58476D1CE4E5B9UL;
            z = (z ^ (z >> 27)) * 0x94D049BB133111EBUL;
            return z ^ (z >> 31);
        }

        // forward (長さ 1) に直交し、互いにも直交する長さ 1 の 2 本。forward と平行になりにくい軸を補助に選ぶ。
        private static void Perpendiculars(Vector3 forward, out Vector3 u, out Vector3 v)
        {
            var helper = Math.Abs(forward.Y) < 0.9f ? Vector3.UnitY : Vector3.UnitX;
            u = Vector3.Normalize(Vector3.Cross(helper, forward));
            v = Vector3.Cross(forward, u);
        }

        private static float Clamp01(float value)
        {
            return value < 0f ? 0f : (value > 1f ? 1f : value);
        }

        private static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }
    }
}
