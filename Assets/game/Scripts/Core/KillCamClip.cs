using System;
using System.Collections.Generic;
using System.Numerics;

namespace Fps.Core
{
    /// <summary>
    /// キルカム (<see cref="DeathCamera.KillCam"/>) で再生する、倒した相手の倒す直前の数秒。
    /// 先頭からの経過秒ごとの姿勢 (<see cref="KillCamPose"/>) を持ち、間は前後から補間して返す (<see cref="Sample"/>)。
    /// オフラインでは <see cref="KillCamBuffer.Capture(double)"/>、ネット対戦では <see cref="FromHistories"/> で作る。
    /// 長さが 0 (記録が 1 件以下) のものは再生できない (<see cref="IsEmpty"/>)。
    /// </summary>
    public sealed class KillCamClip
    {
        /// <summary>記録が無いときのクリップ。</summary>
        public static readonly KillCamClip Empty = new KillCamClip(Array.Empty<float>(), Array.Empty<KillCamPose>());

        private readonly float[] _offsets;
        private readonly KillCamPose[] _poses;

        /// <param name="offsets">先頭からの経過秒。最初は 0、以降は前より大きい有限値。</param>
        /// <param name="poses">offsets と同じ数の姿勢。</param>
        /// <exception cref="ArgumentNullException">offsets か poses が null。</exception>
        /// <exception cref="ArgumentException">数が違う・最初が 0 でない・増えていない・有限でない。</exception>
        public KillCamClip(IReadOnlyList<float> offsets, IReadOnlyList<KillCamPose> poses)
        {
            if (offsets == null)
            {
                throw new ArgumentNullException(nameof(offsets));
            }

            if (poses == null)
            {
                throw new ArgumentNullException(nameof(poses));
            }

            if (offsets.Count != poses.Count)
            {
                throw new ArgumentException("offsets and poses must have the same count", nameof(poses));
            }

            _offsets = new float[offsets.Count];
            _poses = new KillCamPose[poses.Count];
            for (var i = 0; i < offsets.Count; i++)
            {
                var offset = offsets[i];
                if (float.IsNaN(offset) || float.IsInfinity(offset))
                {
                    throw new ArgumentException("offsets must be finite", nameof(offsets));
                }

                if (i == 0 ? offset != 0f : offset <= _offsets[i - 1])
                {
                    throw new ArgumentException("offsets must start at 0 and strictly increase", nameof(offsets));
                }

                _offsets[i] = offset;
                _poses[i] = poses[i];
            }
        }

        /// <summary>姿勢の数。</summary>
        public int Count => _offsets.Length;

        /// <summary>再生の長さ (秒)。記録が 1 件以下なら 0。</summary>
        public float Duration => _offsets.Length > 0 ? _offsets[_offsets.Length - 1] : 0f;

        /// <summary>再生できる長さが無いか。</summary>
        public bool IsEmpty => !(Duration > 0f);

        /// <summary>
        /// 先頭から offset 秒の姿勢。前後の記録から <see cref="KillCamPose.Lerp"/> で補間し、範囲の外は端に、NaN は先頭に寄せる。
        /// </summary>
        /// <exception cref="InvalidOperationException">姿勢が 1 つも無い。</exception>
        public KillCamPose Sample(float offset)
        {
            if (_offsets.Length == 0)
            {
                throw new InvalidOperationException("an empty kill cam clip has no pose");
            }

            if (float.IsNaN(offset) || offset <= 0f)
            {
                return _poses[0];
            }

            var last = _offsets.Length - 1;
            if (offset >= _offsets[last])
            {
                return _poses[last];
            }

            // offset 以下で最も新しい記録を二分探索で探す。範囲内なので必ず見つかる。
            var lo = 0;
            var hi = last;
            while (lo < hi)
            {
                var mid = (lo + hi + 1) / 2;
                if (_offsets[mid] <= offset)
                {
                    lo = mid;
                }
                else
                {
                    hi = mid - 1;
                }
            }

            var t = (offset - _offsets[lo]) / (_offsets[lo + 1] - _offsets[lo]);
            return KillCamPose.Lerp(_poses[lo], _poses[lo + 1], t);
        }

        /// <summary>
        /// ネット対戦の位置と向きの履歴 (<see cref="HitboxHistory"/>) から、endTick までの seconds 秒を 1 tick ごとに切り出す。
        /// 履歴より古い分は履歴の最初から始め、endTick が最も新しい記録より後なら最も新しい記録で終える。切り出せる長さが無ければ <see cref="Empty"/>。
        /// </summary>
        /// <param name="positions">体の位置の履歴。</param>
        /// <param name="yawDirections">向きを水平の単位ベクトル (sin yaw, 0, cos yaw) で記録した履歴。null なら向きはずっと fallbackYaw。</param>
        /// <param name="endTick">倒れた時点の (表示していた) tick。</param>
        /// <param name="tickRate">1 秒の tick 数。0 より大きい有限値。</param>
        /// <param name="seconds">切り出す長さ (秒)。0 より大きい有限値。</param>
        /// <param name="eyeOffset">体の位置から目までのオフセット。</param>
        /// <param name="fallbackYaw">向きの記録が無い・決まらないときの yaw (度)。</param>
        /// <exception cref="ArgumentNullException">positions が null。</exception>
        /// <exception cref="ArgumentOutOfRangeException">endTick が NaN、tickRate か seconds が正の有限値でない。</exception>
        public static KillCamClip FromHistories(
            HitboxHistory positions,
            HitboxHistory yawDirections,
            float endTick,
            float tickRate,
            float seconds,
            Vector3 eyeOffset,
            float fallbackYaw)
        {
            if (positions == null)
            {
                throw new ArgumentNullException(nameof(positions));
            }

            if (float.IsNaN(endTick))
            {
                throw new ArgumentOutOfRangeException(nameof(endTick), endTick, "endTick must not be NaN");
            }

            RequirePositive(tickRate, nameof(tickRate));
            RequirePositive(seconds, nameof(seconds));

            if (!positions.TryGetRange(out var oldest, out var newest))
            {
                return Empty;
            }

            var end = Math.Min((double)endTick, newest);
            var start = Math.Max(end - ((double)seconds * tickRate), oldest);
            if (!(end > start))
            {
                return Empty;
            }

            var offsets = new List<float>();
            var poses = new List<KillCamPose>();
            var yaw = float.IsNaN(fallbackYaw) || float.IsInfinity(fallbackYaw) ? 0f : fallbackYaw;
            var tick = start;
            while (true)
            {
                // start と end は記録の範囲に収めてあるので、間の tick はどれも取れる
                if (positions.TrySample((float)tick, out var position))
                {
                    yaw = SampleYaw(yawDirections, (float)tick, yaw);
                    var offset = (float)((tick - start) / tickRate);

                    // float に丸めると隣の tick と同じ秒になることがあり、同じ秒は 2 つ持てない
                    if (offsets.Count == 0 || offset > offsets[offsets.Count - 1])
                    {
                        offsets.Add(offset);
                        poses.Add(new KillCamPose(position + eyeOffset, yaw));
                    }
                }

                if (tick >= end)
                {
                    break;
                }

                tick = Math.Min(end, Math.Floor(tick) + 1d);
            }

            return offsets.Count > 1 ? new KillCamClip(offsets, poses) : Empty;
        }

        private static float SampleYaw(HitboxHistory yawDirections, float tick, float previous)
        {
            if (yawDirections == null || !yawDirections.TrySample(tick, out var facing))
            {
                return previous;
            }

            // 前後の向きが反対だと補間した長さが 0 近くになり、向きが決まらない
            if (!(facing.LengthSquared() > 1e-6f))
            {
                return previous;
            }

            return (float)(Math.Atan2(facing.X, facing.Z) * 180d / Math.PI);
        }

        private static void RequirePositive(float value, string name)
        {
            if (!(value > 0f) || float.IsInfinity(value))
            {
                throw new ArgumentOutOfRangeException(name, value, name + " must be positive and finite");
            }
        }
    }
}
