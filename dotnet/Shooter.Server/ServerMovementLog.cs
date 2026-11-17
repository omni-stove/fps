using System.Globalization;
using System.Numerics;
using Shooter.Core;

namespace Shooter.Server
{
    /// <summary>Unity のサーバー (NetworkPlayerMotor) と同じ行にして、ログを見る手順と監視をそのまま使えるようにする。</summary>
    internal static class ServerMovementLog
    {
        public static string Stance(int playerId, int tick, bool crouched)
        {
            return Head("stance", playerId, tick) + " crouched=" + Bool(crouched);
        }

        public static string Cover(int playerId, int tick, CoverPhase phase, CoverHeight? height)
        {
            var log = Head("cover", playerId, tick) + " phase=" + phase;
            return phase == CoverPhase.Out || !height.HasValue ? log : log + " height=" + height.Value;
        }

        public static string Dodge(int playerId, int tick, bool dodging)
        {
            return Head("dodge", playerId, tick) + " dodging=" + Bool(dodging);
        }

        public static string Vault(int playerId, int tick, bool vaulting)
        {
            return Head("vault", playerId, tick) + " vaulting=" + Bool(vaulting);
        }

        public static string Prone(int playerId, int tick, bool prone)
        {
            return Head("prone", playerId, tick) + " prone=" + Bool(prone);
        }

        public static string Dive(int playerId, int tick, bool diving)
        {
            return Head("dive", playerId, tick) + " diving=" + Bool(diving);
        }

        public static string Ladder(int playerId, int tick, int ladder)
        {
            var log = Head("ladder", playerId, tick) + " climbing=" + Bool(ladder >= 0);
            return ladder >= 0 ? log + " ladder=" + ladder.ToString(CultureInfo.InvariantCulture) : log;
        }

        public static string KnockbackLaunched(int playerId, int tick, Vector3 velocity)
        {
            return Head("knockback", playerId, tick) + " launched velocity=" + Vector(velocity);
        }

        public static string KnockbackLanded(int playerId, int tick, Vector3 position)
        {
            return Head("knockback", playerId, tick) + " landed at=" + Vector(position);
        }

        private static string Vector(Vector3 value)
        {
            return string.Format(CultureInfo.InvariantCulture, "({0:0.0}, {1:0.0}, {2:0.0})", value.X, value.Y, value.Z);
        }

        private static string Head(string what, int playerId, int tick)
        {
            return "[Net] " + what + " owner=" + playerId.ToString(CultureInfo.InvariantCulture)
                + " tick=" + tick.ToString(CultureInfo.InvariantCulture);
        }

        private static string Bool(bool value)
        {
            return value ? "true" : "false";
        }
    }
}
