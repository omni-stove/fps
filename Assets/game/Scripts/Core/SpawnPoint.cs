using System;
using System.Numerics;

namespace Fps.Core
{
    /// <summary>
    /// リスポーン地点。Team が null の地点はどちらのチームも使える。
    /// </summary>
    public sealed class SpawnPoint
    {
        public Vector3 Position { get; }

        /// <summary>この地点を使えるチーム。null はどちらのチームも使える。</summary>
        public Team? Team { get; }

        /// <param name="position">地点の位置。各成分は有限の値。</param>
        /// <param name="team">使えるチーム。null はどちらのチームも使える。</param>
        public SpawnPoint(Vector3 position, Team? team = null)
        {
            if (!IsFinite(position))
            {
                throw new ArgumentOutOfRangeException(nameof(position), position, "position must be finite");
            }

            Position = position;
            Team = team;
        }

        /// <summary>指定のチームがこの地点を使えるか。</summary>
        public bool IsUsableBy(Team team)
        {
            return Team == null || Team == team;
        }

        private static bool IsFinite(Vector3 v)
        {
            return !float.IsNaN(v.X) && !float.IsInfinity(v.X)
                && !float.IsNaN(v.Y) && !float.IsInfinity(v.Y)
                && !float.IsNaN(v.Z) && !float.IsInfinity(v.Z);
        }
    }
}
