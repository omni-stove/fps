using System;
using System.Numerics;

namespace Fps.Core
{
    /// <summary><see cref="SideSpawnSelector"/> に渡す、陣営ごとの出現点 1 か所。</summary>
    public readonly struct SideSpawn
    {
        /// <summary>この点から出る陣営。</summary>
        public Side Side { get; }

        /// <summary>出現する位置。</summary>
        public Vector3 Position { get; }

        /// <param name="side">この点から出る陣営。</param>
        /// <param name="position">出現する位置。各成分は有限の値。</param>
        public SideSpawn(Side side, Vector3 position)
        {
            if (!HostileSelector.IsFinite(position))
            {
                throw new ArgumentOutOfRangeException(nameof(position), position, "position must be finite");
            }

            Side = side;
            Position = position;
        }
    }
}
