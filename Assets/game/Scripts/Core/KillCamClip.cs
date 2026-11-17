using System;
using System.Collections.Generic;
using System.Numerics;

namespace Shooter.Core
{
    public sealed class KillCamClip
    {
        public static readonly KillCamClip Empty = new KillCamClip(Array.Empty<float>(), Array.Empty<KillCamPose>());

        private readonly float[] _offsets;
        private readonly KillCamPose[] _poses;

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

        public int Count => _offsets.Length;

        public float Duration => _offsets.Length > 0 ? _offsets[_offsets.Length - 1] : 0f;

        public bool IsEmpty => !(Duration > 0f);

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

            // 直前で範囲内と確かめてあり必ず見つかるので、見つからない場合は扱わない。
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
