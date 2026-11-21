using System.Globalization;
using System.Numerics;
using Shooter.Core;

namespace Shooter.Server
{
    // Unity のサーバー (NetworkPlayerMotor) と同じ行にして、ログを見る手順と監視をそのまま使えるようにする。
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

        public static string Mantle(int playerId, int tick, bool mantling)
        {
            return Head("mantle", playerId, tick) + " mantling=" + Bool(mantling);
        }

        // オフラインの PlayerController.FormatMantleProbeLog と同じ値を出し、上れなかった理由と測った段を同じ見方で追えるようにする
        public static string MantleProbe(int playerId, int tick, VaultVerdict verdict, float height, float depth)
        {
            var depthText = float.IsPositiveInfinity(depth) ? "inf" : depth.ToString("F2", CultureInfo.InvariantCulture);
            return Head("mantle", playerId, tick) + " probe=" + verdict + " height=" + height.ToString("F2", CultureInfo.InvariantCulture)
                + " depth=" + depthText;
        }

        // オフラインの ZiplineMovement.FormatGrabLog と同じ値を出す。線は名前を持たないので、マップの並びの番号で示す
        public static string ZiplineGrab(int playerId, int tick, int line, float at, float remaining, float speed)
        {
            return Head("zipline", playerId, tick) + " grab line=" + line.ToString(CultureInfo.InvariantCulture)
                + " at=" + F2(at) + " remaining=" + F2(remaining) + " speed=" + F2(speed);
        }

        public static string ZiplineRelease(int playerId, int tick, string reason, float speed)
        {
            return Head("zipline", playerId, tick) + " release reason=" + reason + " speed=" + F2(speed);
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

        public static string Lean(int playerId, int tick, int side)
        {
            return Head("lean", playerId, tick) + " side=" + (side < 0 ? "left" : side > 0 ? "right" : "none");
        }

        public static string Swim(int playerId, int tick, bool swimming)
        {
            return Head("swim", playerId, tick) + " swimming=" + Bool(swimming);
        }

        public static string Underwater(int playerId, int tick, bool underwater)
        {
            return Head("swim", playerId, tick) + " underwater=" + Bool(underwater);
        }

        public static string OutOfBreath(int playerId, int tick, int drownings)
        {
            return Head("swim", playerId, tick) + " outOfBreath=" + drownings.ToString(CultureInfo.InvariantCulture);
        }

        public static string DrownDamage(int playerId, int tick, int damage, DamageOutcome outcome)
        {
            return Head("drown", playerId, tick) + " damage=" + damage.ToString(CultureInfo.InvariantCulture) + " outcome=" + outcome;
        }

        public static string WallRun(int playerId, int tick, WallRunSide side)
        {
            var log = Head("wallrun", playerId, tick) + " running=" + Bool(side != WallRunSide.None);
            return side == WallRunSide.None ? log : log + " side=" + (side == WallRunSide.Left ? "left" : "right");
        }

        public static string AirJump(int playerId, int tick, int used)
        {
            return Head("airjump", playerId, tick) + " used=" + used.ToString(CultureInfo.InvariantCulture);
        }

        public static string KnockbackLaunched(int playerId, int tick, Vector3 velocity)
        {
            return Head("knockback", playerId, tick) + " launched velocity=" + Vector(velocity);
        }

        public static string KnockbackLanded(int playerId, int tick, Vector3 position)
        {
            return Head("knockback", playerId, tick) + " landed at=" + Vector(position);
        }

        // オフラインの ParachuteMovement.FormatOpenLog と同じ書き方にし、開けなかった理由と測った高さを同じ見方で追えるようにする
        public static string ParachuteOpen(int playerId, int tick, ParachuteOpenVerdict verdict, float heightAboveGround)
        {
            var height = float.IsPositiveInfinity(heightAboveGround) ? "inf" : heightAboveGround.ToString("F2", CultureInfo.InvariantCulture);
            return verdict == ParachuteOpenVerdict.Open
                ? Head("parachute", playerId, tick) + " opened height=" + height
                : Head("parachute", playerId, tick) + " open=" + verdict + " height=" + height;
        }

        public static string ParachuteClosed(int playerId, int tick, ParachuteCloseReason reason)
        {
            return Head("parachute", playerId, tick) + " closed reason=" + reason;
        }

        // オフラインの SlideMotion.FormatStartLog・FormatEndLog と同じ項目と桁にし、同じ見方で追えるようにする
        public static string SlideStart(int playerId, int tick, float speed, Vector2 direction)
        {
            return Head("slide", playerId, tick)
                + string.Format(CultureInfo.InvariantCulture, " start speed={0:0.00} direction=({1:0.00}, {2:0.00})", speed, direction.X, direction.Y);
        }

        public static string SlideEnd(int playerId, int tick, Shooter.Core.SlideEnd reason, float speed, float seconds, float distance)
        {
            return Head("slide", playerId, tick)
                + string.Format(
                    CultureInfo.InvariantCulture, " end reason={0} speed={1:0.00} seconds={2:0.00} distance={3:0.00}", reason, speed, seconds, distance);
        }

        private static string Vector(Vector3 value)
        {
            return string.Format(CultureInfo.InvariantCulture, "({0:0.0}, {1:0.0}, {2:0.0})", value.X, value.Y, value.Z);
        }

        private static string F2(float value)
        {
            return value.ToString("0.00", CultureInfo.InvariantCulture);
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
