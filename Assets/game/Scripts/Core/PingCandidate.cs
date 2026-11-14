using System.Numerics;

namespace Fps.Core
{
    /// <summary><see cref="PingAim.Resolve"/> に渡す、印を付けられる敵 1 人分。</summary>
    public readonly struct PingCandidate
    {
        public PingCandidate(Vector3 position, bool isAlive, bool isVisible)
        {
            Position = position;
            IsAlive = isAlive;
            IsVisible = isVisible;
        }

        /// <summary>印を出す位置 (体の中心など、狙う点)。</summary>
        public Vector3 Position { get; }

        public bool IsAlive { get; }

        /// <summary>付ける人から遮るものなく見えているか。見えていない敵には照準が近くても付けない。</summary>
        public bool IsVisible { get; }
    }
}
