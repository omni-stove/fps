using System;
using System.Collections.Generic;
using System.Numerics;

namespace Shooter.Core
{
    // 呼ぶ側が渡して埋めさせ、中身を Clear で捨てるのは、経路を毎回作らずに使い回せるようにするため。
    public sealed class NavPath
    {
        private readonly List<NavPathPoint> _points = new List<NavPathPoint>();

        public NavPathStatus Status { get; private set; }

        public IReadOnlyList<NavPathPoint> Points => _points;

        public void Clear()
        {
            _points.Clear();
            Status = NavPathStatus.Invalid;
        }

        public void Add(Vector3 position, int linkId = NavPathPoint.NoLink)
        {
            _points.Add(new NavPathPoint(position, linkId));
        }

        public void SetStatus(NavPathStatus status)
        {
            if (!Enum.IsDefined(typeof(NavPathStatus), status))
            {
                throw new ArgumentOutOfRangeException(nameof(status), status, "unknown path status");
            }

            if (status != NavPathStatus.Invalid && _points.Count == 0)
            {
                throw new InvalidOperationException("a path with no points can only be " + nameof(NavPathStatus.Invalid));
            }

            Status = status;
        }

        public float Length()
        {
            var length = 0f;
            for (var i = 1; i < _points.Count; i++)
            {
                length += Vector3.Distance(_points[i - 1].Position, _points[i].Position);
            }

            return length;
        }
    }
}
