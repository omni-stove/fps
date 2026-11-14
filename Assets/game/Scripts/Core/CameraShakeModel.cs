using System;
using System.Numerics;

namespace Fps.Core
{
    public sealed class CameraShakeModel
    {
        public const float DefaultDecayPerSecond = 1.25f;

        public const float DefaultFrequency = 15f;

        // 時刻をそのまま持つと長い試合で float の桁が落ちて揺れが粗くなる。ノイズを周期的にして位置をこの周期で巻き戻せば、途切れずに桁を保てる。
        public const int NoisePeriod = 4096;

        // ずらさないと Perlin ノイズが 0 になる整数の位置で 6 軸がそろって止まり、揺れが脈打つ。
        private const float _channelPhaseOffset = 0.37f;

        // 同じ種から 6 軸を引くので、軸どうしが似た動きにならないよう大きな奇数で離す。
        private const int _channelSeedStride = 7919;

        private const int _pitchChannel = 0;
        private const int _yawChannel = 1;
        private const int _rollChannel = 2;
        private const int _offsetXChannel = 3;
        private const int _offsetYChannel = 4;
        private const int _offsetZChannel = 5;

        private float _decayPerSecond = DefaultDecayPerSecond;
        private float _frequency = DefaultFrequency;
        private Vector3 _maxAngles;
        private Vector3 _maxOffset;
        private float _phase;

        public CameraShakeModel(int seed = 0)
        {
            Seed = seed;
        }

        public int Seed { get; }

        public float Trauma { get; private set; }

        public float Phase => _phase;

        public float DecayPerSecond
        {
            get => _decayPerSecond;
            set => _decayPerSecond = RequireNonNegativeFinite(value, nameof(value));
        }

        public float Frequency
        {
            get => _frequency;
            set => _frequency = RequireNonNegativeFinite(value, nameof(value));
        }

        public Vector3 MaxAngles
        {
            get => _maxAngles;
            set => _maxAngles = RequireNonNegativeFinite(value, nameof(value));
        }

        public Vector3 MaxOffset
        {
            get => _maxOffset;
            set => _maxOffset = RequireNonNegativeFinite(value, nameof(value));
        }

        public float AddTrauma(float amount)
        {
            RequireNonNegativeFinite(amount, nameof(amount));

            var before = Trauma;
            Trauma = Math.Min(1f, before + amount);
            return Trauma - before;
        }

        public void Tick(float deltaSeconds)
        {
            RequireNonNegativeFinite(deltaSeconds, nameof(deltaSeconds));

            Trauma = Math.Max(0f, Trauma - (_decayPerSecond * deltaSeconds));
            _phase = (float)((_phase + ((double)deltaSeconds * _frequency)) % NoisePeriod);
        }

        public void Reset()
        {
            Trauma = 0f;
            _phase = 0f;
        }

        public float Intensity(float multiplier)
        {
            RequireNonNegativeFinite(multiplier, nameof(multiplier));
            return Trauma * Trauma * multiplier;
        }

        public CameraShakeSample Sample(float multiplier)
        {
            var intensity = Intensity(multiplier);
            if (intensity <= 0f)
            {
                return CameraShakeSample.Zero;
            }

            var angles = new Vector3(
                _maxAngles.X * intensity * Channel(_pitchChannel),
                _maxAngles.Y * intensity * Channel(_yawChannel),
                _maxAngles.Z * intensity * Channel(_rollChannel));
            var offset = new Vector3(
                _maxOffset.X * intensity * Channel(_offsetXChannel),
                _maxOffset.Y * intensity * Channel(_offsetYChannel),
                _maxOffset.Z * intensity * Channel(_offsetZChannel));
            return new CameraShakeSample(angles, offset);
        }

        public static float Noise(int seed, float x)
        {
            if (float.IsNaN(x) || float.IsInfinity(x))
            {
                throw new ArgumentOutOfRangeException(nameof(x), x, "x must be finite");
            }

            var floor = Math.Floor(x);
            var t = (float)(x - floor);
            var cell = (int)((((long)floor % NoisePeriod) + NoisePeriod) % NoisePeriod);
            var next = (cell + 1) % NoisePeriod;

            var from = Gradient(seed, cell) * t;
            var to = Gradient(seed, next) * (t - 1f);
            var fade = t * t * t * ((t * ((t * 6f) - 15f)) + 10f);

            // 勾配が [-1, 1] の 1 次元 Perlin ノイズは [-0.5, 0.5] にしか届かないので、最大の揺れを使い切れるよう 2 倍する。
            var value = (from + (fade * (to - from))) * 2f;
            return Math.Max(-1f, Math.Min(1f, value));
        }

        private float Channel(int channel)
        {
            unchecked
            {
                return Noise(Seed + (channel * _channelSeedStride), _phase + (channel * _channelPhaseOffset));
            }
        }

        private static float Gradient(int seed, int cell)
        {
            unchecked
            {
                var h = ((uint)cell * 0x9E3779B1u) ^ ((uint)seed * 0x85EBCA77u);
                h ^= h >> 15;
                h *= 0x2C1B3C6Du;
                h ^= h >> 12;
                h *= 0x297A2D39u;
                h ^= h >> 15;
                return ((h & 0xFFFFu) / 32767.5f) - 1f;
            }
        }

        private static float RequireNonNegativeFinite(float value, string paramName)
        {
            if (!(value >= 0f) || float.IsInfinity(value))
            {
                throw new ArgumentOutOfRangeException(paramName, value, "value must be a non-negative finite number");
            }

            return value;
        }

        private static Vector3 RequireNonNegativeFinite(Vector3 value, string paramName)
        {
            if (!(value.X >= 0f) || float.IsInfinity(value.X)
                || !(value.Y >= 0f) || float.IsInfinity(value.Y)
                || !(value.Z >= 0f) || float.IsInfinity(value.Z))
            {
                throw new ArgumentOutOfRangeException(paramName, value, "every component must be a non-negative finite number");
            }

            return value;
        }
    }

    public readonly struct CameraShakeSample
    {
        public static readonly CameraShakeSample Zero = new CameraShakeSample(Vector3.Zero, Vector3.Zero);

        public Vector3 Angles { get; }

        public Vector3 Offset { get; }

        public bool IsZero => Angles == Vector3.Zero && Offset == Vector3.Zero;

        public CameraShakeSample(Vector3 angles, Vector3 offset)
        {
            Angles = angles;
            Offset = offset;
        }
    }
}
