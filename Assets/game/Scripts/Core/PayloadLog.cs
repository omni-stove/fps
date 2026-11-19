using System;
using System.Globalization;
using System.Text;

namespace Shooter.Core
{
    public static class PayloadLog
    {
        public const string LogPrefix = ModeLog.Tag + " payload";

        public const string Idle = "idle";
        public const string Pushing = "pushing";
        public const string Contested = "contested";
        public const string RollingBack = "rolling-back";

        public static string Started(Payload payload)
        {
            RequirePayload(payload);
            return new StringBuilder(LogPrefix)
                .Append(" started attackers=").Append(Number(payload.Attackers.Id))
                .Append(" length=").Append(Meters(payload.Route.Length))
                .Append(" checkpoints=").Append(Number(payload.Route.CheckpointCount))
                .Append(" limit=").Append(Seconds(payload.Session.TimeLimitSeconds))
                .ToString();
        }

        // 1 回の Tick で台車が中継点を越えて進むこともあるので、進みは台車の今の位置でなく中継点の位置で書く。
        public static string Checkpoint(Payload payload, int checkpoint)
        {
            RequirePayload(payload);
            var route = payload.Route;
            return new StringBuilder(LogPrefix)
                .Append(" checkpoint=").Append(Number(checkpoint + 1)).Append('/').Append(Number(route.CheckpointCount))
                .Append(" progress=").Append(Fraction(route.FractionAt(route.CheckpointDistance(checkpoint))))
                .Append(" bonus=").Append(Seconds(payload.Spec.CheckpointBonusSeconds))
                .Append(" remaining=").Append(Seconds(payload.Session.RemainingSeconds))
                .ToString();
        }

        public static string Motion(Payload payload)
        {
            RequirePayload(payload);
            return new StringBuilder(LogPrefix)
                .Append(" cart=").Append(MotionName(payload))
                .Append(" pushers=").Append(Number(payload.Pushers))
                .Append(" defenders=").Append(Number(payload.CountNearCart(payload.Defenders)))
                .Append(" progress=").Append(Fraction(payload.Progress))
                .ToString();
        }

        public static string Ended(Payload payload, Side? winner)
        {
            RequirePayload(payload);
            return new StringBuilder(LogPrefix)
                .Append(" ended winner=").Append(winner.HasValue ? Number(winner.Value.Id) : ModeLog.Draw)
                .Append(" delivered=").Append(payload.IsDelivered ? "true" : "false")
                .Append(" progress=").Append(Fraction(payload.Progress))
                .Append(" checkpoints=").Append(Number(payload.CheckpointsReached)).Append('/').Append(Number(payload.Route.CheckpointCount))
                .ToString();
        }

        public static string BuiltDefaultCart(string cart)
        {
            return LogPrefix + " cart '" + cart + "' has no Collider, so a default box was built";
        }

        public static string BuiltZone(string cart)
        {
            return LogPrefix + " cart '" + cart + "' has no ControlPointZone, so one with the default radius was added";
        }

        public static string Obstructed(string cart)
        {
            return LogPrefix + " cart '" + cart + "' squeezed a rider against the ceiling; that step is not carried";
        }

        public static string MotionName(Payload payload)
        {
            RequirePayload(payload);
            if (payload.IsContested)
            {
                return Contested;
            }

            if (payload.CurrentSpeed > 0f)
            {
                return Pushing;
            }

            return payload.IsRollingBack ? RollingBack : Idle;
        }

        private static void RequirePayload(Payload payload)
        {
            if (payload == null)
            {
                throw new ArgumentNullException(nameof(payload));
            }
        }

        private static string Number(int value)
        {
            return value.ToString(CultureInfo.InvariantCulture);
        }

        private static string Fraction(float value)
        {
            return value.ToString("F2", CultureInfo.InvariantCulture);
        }

        private static string Meters(double value)
        {
            return value.ToString("F1", CultureInfo.InvariantCulture) + "m";
        }

        private static string Seconds(float seconds)
        {
            return seconds.ToString("F1", CultureInfo.InvariantCulture) + "s";
        }
    }
}
