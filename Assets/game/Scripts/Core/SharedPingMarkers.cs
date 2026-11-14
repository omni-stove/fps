using System;
using System.Collections.Generic;
using System.Numerics;

namespace Fps.Core
{
    public sealed class SharedPingMarkers
    {
        private readonly List<PingMarker> _markers = new List<PingMarker>();

        public IReadOnlyList<PingMarker> Markers => _markers;

        public int Count => _markers.Count;

        public int Version { get; private set; }

        // サーバーとクライアントの時計は揃っていないので、消える時刻ではなく残りの秒数を受け取り、この時計の now に足す。
        public bool Place(int id, PingKind kind, Vector3 position, bool tracking, float remainingSeconds, float now)
        {
            if (!Enum.IsDefined(typeof(PingKind), kind) || !IsFinite(position) || !IsFiniteNumber(now)
                || !(remainingSeconds > 0f) || float.IsInfinity(remainingSeconds))
            {
                return false;
            }

            var index = IndexOf(id);
            if (index >= 0)
            {
                _markers.RemoveAt(index);
            }

            _markers.Add(new PingMarker(id, kind, null, position, kind == PingKind.Enemy && tracking, now, now + remainingSeconds));
            Version++;
            return true;
        }

        // 位置の知らせは届く順が保証されないので、見失った後や消えた後に遅れて届いたものは捨てる。
        public bool Move(int id, Vector3 position)
        {
            var index = IndexOf(id);
            if (index < 0 || !IsFinite(position) || !_markers[index].IsTracking)
            {
                return false;
            }

            var marker = _markers[index];
            if (marker.Position.Equals(position))
            {
                return false;
            }

            _markers[index] = marker.WithPosition(position);
            Version++;
            return true;
        }

        public bool Lose(int id, Vector3 position)
        {
            var index = IndexOf(id);
            if (index < 0 || !IsFinite(position))
            {
                return false;
            }

            _markers[index] = _markers[index].WithPosition(position).Lost();
            Version++;
            return true;
        }

        public bool Remove(int id)
        {
            var index = IndexOf(id);
            if (index < 0)
            {
                return false;
            }

            _markers.RemoveAt(index);
            Version++;
            return true;
        }

        // 消えた知らせはサーバーが送るが、届く前に切断されたときなどに古い印を出し続けないよう、こちらの時計でも消す。
        public int Expire(float now)
        {
            if (!IsFiniteNumber(now))
            {
                throw new ArgumentOutOfRangeException(nameof(now), now, "now must be finite");
            }

            var removed = _markers.RemoveAll(marker => marker.IsExpiredAt(now));
            if (removed > 0)
            {
                Version++;
            }

            return removed;
        }

        public int Clear()
        {
            var count = _markers.Count;
            if (count > 0)
            {
                _markers.Clear();
                Version++;
            }

            return count;
        }

        public bool TryGet(int id, out PingMarker marker)
        {
            var index = IndexOf(id);
            marker = index >= 0 ? _markers[index] : default;
            return index >= 0;
        }

        private int IndexOf(int id)
        {
            for (var i = 0; i < _markers.Count; i++)
            {
                if (_markers[i].Id == id)
                {
                    return i;
                }
            }

            return -1;
        }

        private static bool IsFinite(Vector3 value) => PingAim.IsFinite(value);

        private static bool IsFiniteNumber(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
    }
}
