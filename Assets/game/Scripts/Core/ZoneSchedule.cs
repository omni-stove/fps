using System;
using System.Collections.Generic;
using System.Numerics;

namespace Fps.Core
{
    /// <summary>
    /// 時間とともに縮むエリアの予定。最初の円から、段階 (<see cref="ZonePhase"/>) ごとに待ってから次の円へ縮む。
    /// 次の円は今の円の中に収まるよう、中心を今の円の中から乱数で選ぶ。乱数は <see cref="Seed"/> と段階の番号だけで決まるので、
    /// 同じシードなら実行環境によらず同じ円の列になる。円の列は作ったときにすべて決め、時刻は外から渡す (<see cref="Evaluate"/>)。
    /// 位置は水平面 (X がワールドの x、Y がワールドの z) で持ち、高さは見ない。
    /// </summary>
    public sealed class ZoneSchedule
    {
        private static readonly double _unitScale = 1.0 / (1UL << 53);

        private readonly ZonePhase[] _phases;
        private readonly Vector2[] _centers;
        private readonly float[] _startSeconds;

        /// <summary>最初の円の中心。</summary>
        public Vector2 InitialCenter { get; }

        /// <summary>最初の円の半径 (m)。</summary>
        public float InitialRadius { get; }

        /// <summary>次の円の中心を選ぶ乱数のシード。</summary>
        public int Seed { get; }

        /// <summary>段階の並び。1 つ以上。</summary>
        public IReadOnlyList<ZonePhase> Phases => _phases;

        public int PhaseCount => _phases.Length;

        /// <summary>すべての段階を終える時刻 (秒)。これ以降は最後の円のまま。</summary>
        public float TotalSeconds { get; }

        /// <param name="initialCenter">最初の円の中心。各成分は有限値。</param>
        /// <param name="initialRadius">最初の円の半径 (m)。0 より大きい有限値。</param>
        /// <param name="phases">段階の並び。1 つ以上で、各段階の半径はその前の円の半径以下。</param>
        /// <param name="seed">次の円の中心を選ぶ乱数のシード。</param>
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

        /// <summary>段階 phaseIndex を始める時刻 (秒)。</summary>
        public float PhaseStartSeconds(int phaseIndex)
        {
            CheckPhaseIndex(phaseIndex);
            return _startSeconds[phaseIndex];
        }

        /// <summary>段階 phaseIndex の終わりの円の中心。</summary>
        public Vector2 TargetCenter(int phaseIndex)
        {
            CheckPhaseIndex(phaseIndex);
            return _centers[phaseIndex];
        }

        /// <summary>段階 phaseIndex の終わりの円の半径 (m)。</summary>
        public float TargetRadius(int phaseIndex)
        {
            CheckPhaseIndex(phaseIndex);
            return _phases[phaseIndex].Radius;
        }

        /// <summary>
        /// 開始から elapsedSeconds 経った時点の様子。段階の境目ちょうどの時刻は後の段階 (の待ち) に入れる。
        /// 縮んでいる間は、中心と半径を前の円から次の円へ時間に比例して動かす。
        /// </summary>
        /// <param name="elapsedSeconds">開始からの経過秒数。0 以上の有限値。</param>
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

        /// <summary>開始から elapsedSeconds 経った時点の円の中心。</summary>
        public Vector2 CenterAt(float elapsedSeconds)
        {
            return Evaluate(elapsedSeconds).Center;
        }

        /// <summary>開始から elapsedSeconds 経った時点の円の半径 (m)。</summary>
        public float RadiusAt(float elapsedSeconds)
        {
            return Evaluate(elapsedSeconds).Radius;
        }

        /// <summary>
        /// 今の中心から maxOffset 以内の、円の中で一様な点を選ぶ。maxOffset が 0 なら今の中心のまま。
        /// </summary>
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
