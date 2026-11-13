using System;

namespace Fps.Core
{
    /// <summary><see cref="ControlValue"/> が取れる値の範囲 (両端を含む) と初期値。</summary>
    public readonly struct ControlValueRange
    {
        public ControlValueRange(int min, int max, int defaultValue)
        {
            if (min > max)
            {
                throw new ArgumentOutOfRangeException(nameof(min), min, "min must not be greater than max");
            }

            if (defaultValue < min || defaultValue > max)
            {
                throw new ArgumentOutOfRangeException(nameof(defaultValue), defaultValue, "the default must be within min and max");
            }

            Min = min;
            Max = max;
            Default = defaultValue;
        }

        public int Min { get; }

        public int Max { get; }

        public int Default { get; }

        /// <summary>value が範囲に入っているか。</summary>
        public bool Contains(long value)
        {
            return value >= Min && value <= Max;
        }

        /// <summary>value を範囲に収めた値。</summary>
        public int Clamp(long value)
        {
            return (int)Math.Max(Min, Math.Min(Max, value));
        }
    }
}
