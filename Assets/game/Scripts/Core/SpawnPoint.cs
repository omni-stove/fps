using System;
using System.Numerics;

namespace Shooter.Core
{
    public sealed class SpawnPoint
    {
        public Vector3 Position { get; }

        public Team? Team { get; }

        public SpawnPoint(Vector3 position, Team? team = null)
        {
            if (!IsFinite(position))
            {
                throw new ArgumentOutOfRangeException(nameof(position), position, "position must be finite");
            }

            Position = position;
            Team = team;
        }

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
