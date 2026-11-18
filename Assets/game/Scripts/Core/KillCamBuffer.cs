using System;
using System.Collections.Generic;

namespace Shooter.Core
{
    public sealed class KillCamBuffer
    {
        public const float DefaultSeconds = 4f;

        // これより短い間隔の記録を捨てるのは、フレームレートが高いと固定長の容量が足りなくなるため。
        public const float SampleInterval = 1f / 60f;

        // float の 1/60 は 1/60 より少し長く、60 Hz で記録すると間隔が SampleInterval に届かず 1 つおきに捨ててしまうので、この差までは同じ間隔とみなす。
        private const double _intervalTolerance = 1e-6;

        private readonly double[] _times;
        private readonly KillCamPose[] _poses;
        private int _start;

        public KillCamBuffer(float seconds = DefaultSeconds)
        {
            RequirePositive(seconds, nameof(seconds));
            Seconds = seconds;

            // 切り出しの先頭より前の記録も 1 件残すと、先頭の姿勢を補間で出せる
            var capacity = (int)Math.Ceiling(seconds / (SampleInterval - _intervalTolerance)) + 2;
            _times = new double[capacity];
            _poses = new KillCamPose[capacity];
        }

        public float Seconds { get; }

        public int Capacity => _times.Length;

        public int Count { get; private set; }

        public bool Record(double time, KillCamPose pose)
        {
            if (double.IsNaN(time) || double.IsInfinity(time))
            {
                throw new ArgumentOutOfRangeException(nameof(time), time, "time must be finite");
            }

            if (Count > 0)
            {
                var newestIndex = IndexOf(Count - 1);
                var newest = _times[newestIndex];
                if (time < newest)
                {
                    throw new ArgumentOutOfRangeException(nameof(time), time, "time must not be older than the newest record");
                }

                if (time == newest)
                {
                    _poses[newestIndex] = pose;
                    return true;
                }

                if (time - newest < SampleInterval - _intervalTolerance)
                {
                    return false;
                }
            }

            int index;
            if (Count < Capacity)
            {
                index = IndexOf(Count);
                Count++;
            }
            else
            {
                index = _start;
                _start = (_start + 1) % Capacity;
            }

            _times[index] = time;
            _poses[index] = pose;
            return true;
        }

        // 記録が途切れる (倒れている間・相手が消えていた間) 前後を補間でつながないために、途切れたら捨てる。
        public void Clear()
        {
            _start = 0;
            Count = 0;
        }

        public KillCamClip Capture(double endTime)
        {
            return Capture(endTime, Seconds);
        }

        public KillCamClip Capture(double endTime, float seconds)
        {
            if (double.IsNaN(endTime) || double.IsInfinity(endTime))
            {
                throw new ArgumentOutOfRangeException(nameof(endTime), endTime, "endTime must be finite");
            }

            RequirePositive(seconds, nameof(seconds));

            var last = -1;
            for (var i = Count - 1; i >= 0; i--)
            {
                if (_times[IndexOf(i)] <= endTime)
                {
                    last = i;
                    break;
                }
            }

            if (last < 0)
            {
                return KillCamClip.Empty;
            }

            var startTime = endTime - seconds;
            var first = 0;
            for (var i = last; i >= 0; i--)
            {
                if (_times[IndexOf(i)] <= startTime)
                {
                    first = i;
                    break;
                }
            }

            var firstTime = _times[IndexOf(first)];
            var clipStart = Math.Max(startTime, firstTime);
            if (!(_times[IndexOf(last)] > clipStart))
            {
                return KillCamClip.Empty;
            }

            var offsets = new List<float>(last - first + 1);
            var poses = new List<KillCamPose>(last - first + 1);
            var startPose = _poses[IndexOf(first)];
            if (firstTime < clipStart)
            {
                var nextTime = _times[IndexOf(first + 1)];
                var t = (float)((clipStart - firstTime) / (nextTime - firstTime));
                startPose = KillCamPose.Lerp(startPose, _poses[IndexOf(first + 1)], t);
            }

            offsets.Add(0f);
            poses.Add(startPose);
            for (var i = first + 1; i <= last; i++)
            {
                var offset = (float)(_times[IndexOf(i)] - clipStart);

                // 先頭を補間で出した直後の記録は、float に丸めると 0 と区別できないことがある
                if (offset > offsets[offsets.Count - 1])
                {
                    offsets.Add(offset);
                    poses.Add(_poses[IndexOf(i)]);
                }
            }

            return offsets.Count > 1 ? new KillCamClip(offsets, poses) : KillCamClip.Empty;
        }

        private int IndexOf(int logical)
        {
            return (_start + logical) % Capacity;
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
