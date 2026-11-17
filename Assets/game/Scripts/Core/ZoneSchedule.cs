using System;
using System.Collections.Generic;
using System.Numerics;

namespace Shooter.Core
{
    public sealed class ZoneSchedule
    {
        private static readonly double _unitScale = 1.0 / (1UL << 53);

        private readonly ZonePhase[] _phases;
        private readonly Vector2[] _centers;
        private readonly float[] _startSeconds;

        public Vector2 InitialCenter { get; }

        public float InitialRadius { get; }

        public int Seed { get; }

        public IReadOnlyList<ZonePhase> Phases => _phases;

        public int PhaseCount => _phases.Length;

        public float TotalSeconds { get; }

        public ZoneSchedule(Vector2 initialCenter, float initialRadius, IReadOnlyList<ZonePhase> phases, int seed)
        {
            if (!IsFinite(initialCenter.X) || !IsFinite(initialCenter.Y))
            {
                throw new ArgumentOutOfRangeException(nameof(initialCenter), initialCenter, "initialCenter must be finite");
            }

            if (!(initialRadius > 0f) || float.IsInfinity(initialRadius))
            {
                throw new ArgumentOutOfRangeException(nameof(initialRadius), initialRadius, "initialRadius must be positive and finite");
            }

            if (phases == null)
            {
                throw new ArgumentNullException(nameof(phases));
            }

            if (phases.Count == 0)
            {
                throw new ArgumentException("phases must contain at least one phase", nameof(phases));
            }

            InitialCenter = initialCenter;
            InitialRadius = initialRadius;
            Seed = seed;

            _phases = new ZonePhase[phases.Count];
            _centers = new Vector2[phases.Count];
            _startSeconds = new float[phases.Count];

            var center = initialCenter;
            var radius = initialRadius;
            var start = 0d;
            for (var i = 0; i < phases.Count; i++)
            {
                var phase = phases[i];
                if (phase.Radius > radius)
                {
                    throw new ArgumentException(
                        "phase " + i + " radius " + phase.Radius + " is larger than the previous radius " + radius, nameof(phases));
                }

                _phases[i] = phase;
                _startSeconds[i] = (float)start;
                center = PickCenter(seed, i, center, radius - phase.Radius);
                _centers[i] = center;
                radius = phase.Radius;
                start += phase.DurationSeconds;
            }

            TotalSeconds = (float)start;
        }

        public float PhaseStartSeconds(int phaseIndex)
        {
            CheckPhaseIndex(phaseIndex);
            return _startSeconds[phaseIndex];
        }

        public Vector2 TargetCenter(int phaseIndex)
        {
            CheckPhaseIndex(phaseIndex);
            return _centers[phaseIndex];
        }

        public float TargetRadius(int phaseIndex)
        {
            CheckPhaseIndex(phaseIndex);
            return _phases[phaseIndex].Radius;
        }

        public ZoneState Evaluate(float elapsedSeconds)
        {
            if (!(elapsedSeconds >= 0f) || float.IsInfinity(elapsedSeconds))
            {
                throw new ArgumentOutOfRangeException(nameof(elapsedSeconds), elapsedSeconds, "elapsedSeconds must be a non-negative finite number");
            }

            var last = _phases.Length - 1;
            if (elapsedSeconds >= TotalSeconds)
            {
                var finalCenter = _centers[last];
                var finalRadius = _phases[last].Radius;
                return new ZoneState(
                    last, ZoneStage.Closed, finalCenter, finalRadius, finalCenter, finalRadius, _phases[last].DamagePerSecond, 0f, 0f);
            }

            var index = last;
            while (index > 0 && elapsedSeconds < _startSeconds[index])
            {
                index--;
            }

            var phase = _phases[index];
            var fromCenter = index == 0 ? InitialCenter : _centers[index - 1];
            var fromRadius = index == 0 ? InitialRadius : _phases[index - 1].Radius;
            var toCenter = _centers[index];
            var local = elapsedSeconds - _startSeconds[index];

            if (local < phase.WaitSeconds)
            {
                var untilShrink = phase.WaitSeconds - local;
                return new ZoneState(
                    index, ZoneStage.Waiting, fromCenter, fromRadius, toCenter, phase.Radius, phase.DamagePerSecond, untilShrink, untilShrink);
            }

            var shrinkElapsed = local - phase.WaitSeconds;

            // 段階の始めの時刻は double で足してから float にしているので、縮む時間が 0 でも丸めの差でここに来ることがある。
            var t = phase.ShrinkSeconds > 0f ? Clamp01(shrinkElapsed / phase.ShrinkSeconds) : 1f;
            var center = Vector2.Lerp(fromCenter, toCenter, t);
            var radius = fromRadius + ((phase.Radius - fromRadius) * t);
            return new ZoneState(
                index,
                ZoneStage.Shrinking,
                center,
                radius,
                toCenter,
                phase.Radius,
                phase.DamagePerSecond,
                0f,
                Math.Max(0f, phase.ShrinkSeconds - shrinkElapsed));
        }

        public Vector2 CenterAt(float elapsedSeconds)
        {
            return Evaluate(elapsedSeconds).Center;
        }

        public float RadiusAt(float elapsedSeconds)
        {
            return Evaluate(elapsedSeconds).Radius;
        }

        private static Vector2 PickCenter(int seed, int phaseIndex, Vector2 current, float maxOffset)
        {
            if (!(maxOffset > 0f))
            {
                return current;
            }

            // 円の中で一様にするには、中心からの距離を半径の平方根で取る (面積は距離の 2 乗に比例する)。
            var distance = Math.Sqrt(Sample(seed, phaseIndex, 0UL)) * maxOffset;
            var angle = Sample(seed, phaseIndex, 1UL) * 2.0 * Math.PI;
            return current + new Vector2((float)(Math.Cos(angle) * distance), (float)(Math.Sin(angle) * distance));
        }

        // System.Random は実装 (Mono と .NET) で列が変わりうるので使わず、整数の演算だけで決める (サーバーとクライアントで円を揃えるため)。
        private static double Sample(int seed, int phaseIndex, ulong stream)
        {
            var x = Mix(((ulong)(uint)seed << 32) | (uint)phaseIndex);
            x = Mix(x ^ (stream * 0x9E3779B97F4A7C15UL));
            return (x >> 11) * _unitScale;
        }

        // SplitMix64 の仕上げ。
        private static ulong Mix(ulong z)
        {
            z += 0x9E3779B97F4A7C15UL;
            z = (z ^ (z >> 30)) * 0xBF58476D1CE4E5B9UL;
            z = (z ^ (z >> 27)) * 0x94D049BB133111EBUL;
            return z ^ (z >> 31);
        }

        private void CheckPhaseIndex(int phaseIndex)
        {
            if (phaseIndex < 0 || phaseIndex >= _phases.Length)
            {
                throw new ArgumentOutOfRangeException(nameof(phaseIndex), phaseIndex, "phaseIndex must be between 0 and " + (_phases.Length - 1));
            }
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
