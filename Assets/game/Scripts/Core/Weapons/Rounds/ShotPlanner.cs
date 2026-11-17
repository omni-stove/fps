using System;
using System.Numerics;

namespace Shooter.Core
{
    public static class ShotPlanner
    {
        public static ShotPlan Plan(RoundSpec round, Vector3 origin, Vector3 aim, float coneDegrees, int playerId, int shotIndex)
        {
            if (round == null)
            {
                throw new ArgumentNullException(nameof(round));
            }

            if (!IsFinite(origin))
            {
                throw new ArgumentOutOfRangeException(nameof(origin), origin, "origin must be finite");
            }

            // 腰だめ・ADS・移動の散らばりは円錐の中心にだけ掛け、ペレットはその周りに広げる。
            var center = SpreadPattern.Direction(aim, coneDegrees, playerId, shotIndex);
            var count = PelletPattern.Count(round);
            var rounds = new PlannedRound[count];
            for (var i = 0; i < count; i++)
            {
                rounds[i] = new PlannedRound(origin, PelletPattern.Direction(round, center, playerId, shotIndex, i), i);
            }

            return new ShotPlan(round, shotIndex, Array.AsReadOnly(rounds));
        }

        private static bool IsFinite(Vector3 value)
        {
            return !float.IsNaN(value.X) && !float.IsInfinity(value.X)
                && !float.IsNaN(value.Y) && !float.IsInfinity(value.Y)
                && !float.IsNaN(value.Z) && !float.IsInfinity(value.Z);
        }
    }
}
