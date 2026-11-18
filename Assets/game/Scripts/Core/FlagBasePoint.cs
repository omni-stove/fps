using System;
using System.Numerics;

namespace Shooter.Core
{
    // Unity 抜きのサーバーは FlagBase を読めないので、Editor がシーンの台から書き出した位置と陣営を持つ
    public readonly struct FlagBasePoint
    {
        public FlagBasePoint(Team team, Vector3 position)
        {
            Side.FromTeam(team);
            if (!IsFinite(position))
            {
                throw new ArgumentOutOfRangeException(nameof(position), position, "position must be finite");
            }

            Team = team;
            Position = position;
        }

        public Team Team { get; }

        public Vector3 Position { get; }

        private static bool IsFinite(Vector3 v)
        {
            return !float.IsNaN(v.X) && !float.IsInfinity(v.X)
                && !float.IsNaN(v.Y) && !float.IsInfinity(v.Y)
                && !float.IsNaN(v.Z) && !float.IsInfinity(v.Z);
        }
    }
}
