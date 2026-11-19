using System;
using System.Numerics;

namespace Shooter.Core
{
    public readonly struct PlatformRide
    {
        public PlatformRide(PlatformMotion motion, Vector3 intended, bool hitCeiling)
        {
            if (!IsFinite(intended.X) || !IsFinite(intended.Y) || !IsFinite(intended.Z))
            {
                throw new ArgumentOutOfRangeException(nameof(intended), intended, "intended must be finite");
            }

            Motion = motion;
            Intended = intended;
            IsBlocked = hitCeiling && intended.Y > 0f;
        }

        public static PlatformRide None => default;

        public PlatformMotion Motion { get; }

        public Vector3 Intended { get; }

        public bool IsRiding => Intended != Vector3.Zero || Motion.YawDelta != 0f;

        // 上がる床に頭上を塞がれた。床を止めないと体に床がめり込む (挟まれる) ので、呼び手が床に知らせる。
        public bool IsBlocked { get; }

        // 降りたときに引き継ぐ速さ。頭上に塞がれた分の上向きは持ち越さない (天井から離れたとたん跳ね上がらないため)。
        public Vector3 Velocity(float seconds)
        {
            if (!(seconds > 0f) || float.IsInfinity(seconds))
            {
                throw new ArgumentOutOfRangeException(nameof(seconds), seconds, "seconds must be positive and finite");
            }

            var y = IsBlocked ? 0f : Intended.Y;
            return new Vector3(Intended.X / seconds, y / seconds, Intended.Z / seconds);
        }

        private static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }
    }
}
