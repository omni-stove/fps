using System;
using System.Globalization;

namespace Fps.Core
{
    /// <summary>
    /// <see cref="TeamPalette"/> の 1 色。赤・緑・青を 0〜1 で持つ (不透明)。Core は UnityEngine を参照しないので、Unity の Color には使う側で直す。
    /// </summary>
    public readonly struct PaletteColor : IEquatable<PaletteColor>
    {
        /// <param name="r">赤 (0〜1)。</param>
        /// <param name="g">緑 (0〜1)。</param>
        /// <param name="b">青 (0〜1)。</param>
        /// <exception cref="ArgumentOutOfRangeException">どれかが 0〜1 の外か有限でない。</exception>
        public PaletteColor(float r, float g, float b)
        {
            R = Check(r, nameof(r));
            G = Check(g, nameof(g));
            B = Check(b, nameof(b));
        }

        /// <summary>赤 (0〜1)。</summary>
        public float R { get; }

        /// <summary>緑 (0〜1)。</summary>
        public float G { get; }

        /// <summary>青 (0〜1)。</summary>
        public float B { get; }

        /// <summary>0〜255 の値から作る。</summary>
        public static PaletteColor FromBytes(byte r, byte g, byte b)
        {
            return new PaletteColor(r / 255f, g / 255f, b / 255f);
        }

        public bool Equals(PaletteColor other)
        {
            return R.Equals(other.R) && G.Equals(other.G) && B.Equals(other.B);
        }

        public override bool Equals(object obj)
        {
            return obj is PaletteColor other && Equals(other);
        }

        public static bool operator ==(PaletteColor left, PaletteColor right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(PaletteColor left, PaletteColor right)
        {
            return !left.Equals(right);
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(R, G, B);
        }

        public override string ToString()
        {
            return string.Format(CultureInfo.InvariantCulture, "rgb({0:0.###}, {1:0.###}, {2:0.###})", R, G, B);
        }

        private static float Check(float value, string name)
        {
            if (!(value >= 0f && value <= 1f))
            {
                throw new ArgumentOutOfRangeException(name, value, "a color component must be between 0 and 1");
            }

            return value;
        }
    }
}
