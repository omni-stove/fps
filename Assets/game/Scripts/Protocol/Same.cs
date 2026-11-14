using System;
using System.Numerics;

namespace Fps.Protocol
{
    /// <summary>
    /// スナップショットの差分を取るための、浮動小数をビット列で比べる等しさ。
    /// <c>==</c> だと NaN が自分と等しくならず、毎回変わったとみなして送り続けてしまうため。
    /// </summary>
    internal static class Same
    {
        public static bool Bits(float a, float b)
        {
            return BitConverter.SingleToInt32Bits(a) == BitConverter.SingleToInt32Bits(b);
        }

        public static bool Bits(double a, double b)
        {
            return BitConverter.DoubleToInt64Bits(a) == BitConverter.DoubleToInt64Bits(b);
        }

        public static bool Bits(Vector2 a, Vector2 b)
        {
            return Bits(a.X, b.X) && Bits(a.Y, b.Y);
        }

        public static bool Bits(Vector3 a, Vector3 b)
        {
            return Bits(a.X, b.X) && Bits(a.Y, b.Y) && Bits(a.Z, b.Z);
        }

        public static bool Bits(Quaternion a, Quaternion b)
        {
            return Bits(a.X, b.X) && Bits(a.Y, b.Y) && Bits(a.Z, b.Z) && Bits(a.W, b.W);
        }
    }
}
