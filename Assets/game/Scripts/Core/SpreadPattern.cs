using System;
using System.Numerics;

namespace Shooter.Core
{
    public static class SpreadPattern
    {
        public const float MaxDegrees = 90f;

        public const uint PolarStream = 1u;

        public const uint AzimuthStream = 2u;

        public const uint RecoilYawStream = 3u;

        public const uint BlindFirePolarStream = 4u;

        public const uint BlindFireAzimuthStream = 5u;

        public const uint PelletPolarStream = 6u;

        public const uint PelletAzimuthStream = 7u;

        private const float _minLength = 1e-6f;

        private const double _maxScreenDegrees = 89.9;

        // 2^-53。64 ビットの上位 53 ビットを [0, 1) の double にする。
        private const double _unitScale = 1.0 / 9007199254740992.0;

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
            var moving = spec.MoveSpreadDegrees + ((spec.AdsMoveSpreadDegrees - spec.MoveSpreadDegrees) * ads);
            return Math.Min(MaxDegrees, aim + (moving * move));
        }

        public static float MoveShare(WeaponSpec spec, float adsProgress, float moveFraction)
        {
            var moving = ConeDegrees(spec, adsProgress, moveFraction);
            var still = ConeDegrees(spec, adsProgress, 0f);
            var full = ConeDegrees(spec, adsProgress, 1f);
            return full <= 0f ? 0f : Clamp01((moving - still) / full);
        }

        // 円錐が 90° 以上は画面の外の無限遠になるので、MaxDegrees の手前で止める。
        public static float ScreenRadius(float coneDegrees, float verticalFieldOfViewDegrees, float halfHeight)
        {
            if (!IsFinite(coneDegrees))
            {
                throw new ArgumentOutOfRangeException(nameof(coneDegrees), coneDegrees, "coneDegrees must be finite");
            }

            if (!IsFinite(verticalFieldOfViewDegrees) || verticalFieldOfViewDegrees <= 0f || verticalFieldOfViewDegrees >= 180f)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(verticalFieldOfViewDegrees), verticalFieldOfViewDegrees, "verticalFieldOfViewDegrees must be between 0 and 180");
            }

            if (!IsFinite(halfHeight) || halfHeight < 0f)
            {
                throw new ArgumentOutOfRangeException(nameof(halfHeight), halfHeight, "halfHeight must be finite and not negative");
            }

            var cone = Math.Min(Math.Max(0.0, coneDegrees), _maxScreenDegrees) * Math.PI / 180.0;
            var halfFov = verticalFieldOfViewDegrees * 0.5 * Math.PI / 180.0;
            return (float)(Math.Tan(cone) / Math.Tan(halfFov) * halfHeight);
        }

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

        public static Vector3 Direction(Vector3 aim, float halfAngleDegrees, int playerId, int shotIndex)
        {
            return Direction(aim, halfAngleDegrees, playerId, shotIndex, PolarStream, AzimuthStream);
        }

        // 同じ弾に 2 度目の散らばりを掛ける (ブラインドファイア) とき、1 度目と別の系列にして同じ向きへ重ならないよう、系列を指定できるようにする。
        public static Vector3 Direction(Vector3 aim, float halfAngleDegrees, int playerId, int shotIndex, uint polarStream, uint azimuthStream)
        {
            return Cone(aim, halfAngleDegrees, playerId, shotIndex, null, polarStream, azimuthStream);
        }

        public static Vector3 Direction(
            Vector3 aim, float halfAngleDegrees, int playerId, int shotIndex, int subIndex, uint polarStream, uint azimuthStream)
        {
            return Cone(aim, halfAngleDegrees, playerId, shotIndex, subIndex, polarStream, azimuthStream);
        }

        private static Vector3 Cone(
            Vector3 aim, float halfAngleDegrees, int playerId, int shotIndex, int? subIndex, uint polarStream, uint azimuthStream)
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
            var polar = subIndex.HasValue ? Sample(playerId, shotIndex, subIndex.Value, polarStream) : Sample(playerId, shotIndex, polarStream);
            var azimuth = subIndex.HasValue ? Sample(playerId, shotIndex, subIndex.Value, azimuthStream) : Sample(playerId, shotIndex, azimuthStream);
            var cosTheta = 1.0 - (polar * (1.0 - cosMax));
            var sinTheta = Math.Sqrt(Math.Max(0.0, 1.0 - (cosTheta * cosTheta)));
            var phi = azimuth * 2.0 * Math.PI;

            Perpendiculars(forward, out var u, out var v);
            var sideways = (u * (float)Math.Cos(phi)) + (v * (float)Math.Sin(phi));
            return Vector3.Normalize((forward * (float)cosTheta) + (sideways * (float)sinTheta));
        }

        public static double Sample(int playerId, int shotIndex, uint stream)
        {
            // System.Random は実装 (Mono と .NET) で列が変わりうるので使わず、整数の演算だけで決める (クライアントとサーバーで揃えるため)。
            var x = Mix(((ulong)(uint)playerId << 32) | (uint)shotIndex);
            x = Mix(x ^ (stream * 0x9E3779B97F4A7C15UL));
            return (x >> 11) * _unitScale;
        }

        public static double Sample(int playerId, int shotIndex, int subIndex, uint stream)
        {
            var x = Mix(((ulong)(uint)playerId << 32) | (uint)shotIndex);
            x = Mix(x ^ ((ulong)(uint)subIndex * 0xD1B54A32D192ED03UL));
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
