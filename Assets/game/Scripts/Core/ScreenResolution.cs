using System;
using System.Globalization;

namespace Shooter.Core
{
    // 0×0 を「画面 (ディスプレイ) の解像度のまま」として持つのは、遊ぶ機器ごとに違う解像度を既定値に書けないため。
    public readonly struct ScreenResolution : IEquatable<ScreenResolution>
    {
        public const int MinSide = 320;

        public const int MaxSide = 16384;

        public ScreenResolution(int width, int height)
        {
            if (!IsValid(width, height))
            {
                throw new ArgumentOutOfRangeException(
                    nameof(width),
                    Format(width) + "x" + Format(height),
                    "a resolution must be 0x0 (the display's own) or have both sides within " + Format(MinSide) + " to " + Format(MaxSide));
            }

            Width = width;
            Height = height;
        }

        public static ScreenResolution Native => default;

        public int Width { get; }

        public int Height { get; }

        public bool IsNative => Width == 0 && Height == 0;

        public static bool IsValid(int width, int height)
        {
            if (width == 0 && height == 0)
            {
                return true;
            }

            return width >= MinSide && width <= MaxSide && height >= MinSide && height <= MaxSide;
        }

        public bool Equals(ScreenResolution other)
        {
            return Width == other.Width && Height == other.Height;
        }

        public override bool Equals(object obj)
        {
            return obj is ScreenResolution other && Equals(other);
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(Width, Height);
        }

        public static bool operator ==(ScreenResolution left, ScreenResolution right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(ScreenResolution left, ScreenResolution right)
        {
            return !left.Equals(right);
        }

        public override string ToString()
        {
            return IsNative ? "native" : Format(Width) + "x" + Format(Height);
        }

        private static string Format(int value)
        {
            return value.ToString(CultureInfo.InvariantCulture);
        }
    }
}
