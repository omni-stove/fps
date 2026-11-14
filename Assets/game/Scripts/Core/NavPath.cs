using System;
using System.Collections.Generic;
using System.Numerics;

namespace Fps.Core
{
    /// <summary>
    /// <see cref="INavigation.CalculatePath"/> が埋める経路。毎回作らずに使い回せるよう、中身は <see cref="Clear"/> で捨てる。
    /// </summary>
    public sealed class NavPath
    {
        private readonly List<NavPathPoint> _points = new List<NavPathPoint>();

        /// <summary>目的地まで届いているか。角が無ければ常に <see cref="NavPathStatus.Invalid"/>。</summary>
        public NavPathStatus Status { get; private set; }

        /// <summary>出発点から順の角。</summary>
        public IReadOnlyList<NavPathPoint> Points => _points;

        /// <summary>経路を捨てて <see cref="NavPathStatus.Invalid"/> に戻す。</summary>
        public void Clear()
        {
            _points.Clear();
            Status = NavPathStatus.Invalid;
        }

        /// <summary>角を末尾に足す。</summary>
        /// <param name="position">角の位置。</param>
        /// <param name="linkId">この角から次の角までリンクを渡るならその Id、歩くなら <see cref="NavPathPoint.NoLink"/>。</param>
        public void Add(Vector3 position, int linkId = NavPathPoint.NoLink)
        {
            _points.Add(new NavPathPoint(position, linkId));
        }

        /// <summary>経路を求め終えたときに、届いているかを決める。</summary>
        /// <exception cref="InvalidOperationException">角が無いのに Invalid 以外にしようとした。</exception>
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

        /// <summary>角を順に結んだ長さ。リンクの区間も端と端の直線の長さで数える。</summary>
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
