using System;
using System.Collections.Generic;

namespace Fps.Core
{
    /// <summary>
    /// オフラインのキルカム (<see cref="DeathCamera.KillCam"/>) のために、相手 1 人分の姿勢 (<see cref="KillCamPose"/>) を
    /// 直近 <see cref="Seconds"/> 秒ぶんためておく固定長のリングバッファ。倒されたら <see cref="Capture(double)"/> で
    /// 倒された時点までの数秒を <see cref="KillCamClip"/> として切り出す。
    /// 記録は <see cref="SampleInterval"/> 秒より細かくは取らない (フレームレートで容量が足りなくならないように)。容量を超えたら古い記録から上書きする。
    /// </summary>
    public sealed class KillCamBuffer
    {
        /// <summary>既定でためておく長さ (秒)。キルカムで再生する長さの既定値でもある。</summary>
        public const float DefaultSeconds = 4f;

        /// <summary>記録の最小の間隔 (秒)。これより短い間隔の記録は捨てる。</summary>
        public const float SampleInterval = 1f / 60f;

        private readonly double[] _times;
        private readonly KillCamPose[] _poses;
        private int _start;

        /// <param name="seconds">ためておく長さ (秒)。0 より大きい有限値。</param>
        /// <exception cref="ArgumentOutOfRangeException">seconds が正の有限値でない。</exception>
        public KillCamBuffer(float seconds = DefaultSeconds)
        {
            RequirePositive(seconds, nameof(seconds));
            Seconds = seconds;

            // 切り出しの先頭より前の記録も 1 件残すと、先頭の姿勢を補間で出せる
            var capacity = (int)Math.Ceiling(seconds / SampleInterval) + 2;
            _times = new double[capacity];
            _poses = new KillCamPose[capacity];
        }

        /// <summary>ためておく長さ (秒)。</summary>
        public float Seconds { get; }

        /// <summary>保持できる記録の数。</summary>
        public int Capacity => _times.Length;

        /// <summary>今保持している記録の数。</summary>
        public int Count { get; private set; }

        /// <summary>
        /// time (秒) の姿勢を記録する。time は前回以上でなければならない。前回と同じ time なら上書きし、
        /// 前回から <see cref="SampleInterval"/> 秒経っていなければ捨てる。記録したら true。
        /// </summary>
        /// <exception cref="ArgumentOutOfRangeException">time が有限でないか、前回より前。</exception>
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

                if (time - newest < SampleInterval)
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

        /// <summary>記録をすべて捨てる。記録が途切れる (倒れている間・相手が消えていた間) 前後を補間でつながないために使う。</summary>
        public void Clear()
        {
            _start = 0;
            Count = 0;
        }

        /// <summary>endTime までの <see cref="Seconds"/> 秒を切り出す (<see cref="Capture(double, float)"/>)。</summary>
        public KillCamClip Capture(double endTime)
        {
            return Capture(endTime, Seconds);
        }

        /// <summary>
        /// endTime (秒) までの seconds 秒を切り出す。記録が seconds 秒に満たなければ最も古い記録から、
        /// endTime より後の記録は使わず endTime 以前で最も新しい記録で終える。先頭は前後の記録から補間する。
        /// 切り出せる長さが無ければ <see cref="KillCamClip.Empty"/>。
        /// </summary>
        /// <exception cref="ArgumentOutOfRangeException">endTime が有限でないか、seconds が正の有限値でない。</exception>
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
