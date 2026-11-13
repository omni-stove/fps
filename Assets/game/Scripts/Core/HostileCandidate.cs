using System.Numerics;

namespace Fps.Core
{
    /// <summary><see cref="HostileSelector"/> に渡す、狙う相手の候補 1 人分。</summary>
    public readonly struct HostileCandidate
    {
        /// <summary>候補の陣営。</summary>
        public Side Side { get; }

        /// <summary>候補の位置 (足元など、選ぶ側と同じ基準の点)。</summary>
        public Vector3 Position { get; }

        /// <summary>生きていれば true。倒れている候補は選ばない。</summary>
        public bool IsAlive { get; }

        public HostileCandidate(Side side, Vector3 position, bool isAlive)
        {
            Side = side;
            Position = position;
            IsAlive = isAlive;
        }
    }
}
