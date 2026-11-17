using System;
using System.Globalization;

namespace Shooter.Core
{
    public static class GrappleLog
    {
        public const string Tag = "[Grapple]";

        public const string AttachedPrefix = Tag + " attached ";
        public const string MissPrefix = Tag + " miss ";
        public const string ArrivedPrefix = Tag + " arrived ";
        public const string ReleasedPrefix = Tag + " released ";
        public const string ClimbPrefix = Tag + " climb ";

        public static string Attached(string surface, float distance, bool snappedToEdge)
        {
            return string.Format(
                CultureInfo.InvariantCulture,
                "{0}surface={1} distance={2:0.00} edge={3}",
                AttachedPrefix,
                NameOrDash(surface),
                distance,
                snappedToEdge ? "true" : "false");
        }

        public static string Miss(GrappleMiss reason, string surface, float? distance)
        {
            if (reason == GrappleMiss.None)
            {
                throw new ArgumentOutOfRangeException(nameof(reason), reason, "a miss needs a reason");
            }

            var measured = distance.HasValue ? distance.Value.ToString("0.00", CultureInfo.InvariantCulture) : "-";
            return $"{MissPrefix}reason={reason} surface={NameOrDash(surface)} distance={measured}";
        }

        public static string Arrived(float seconds, bool wall)
        {
            return string.Format(
                CultureInfo.InvariantCulture, "{0}seconds={1:0.00} wall={2}", ArrivedPrefix, seconds, wall ? "true" : "false");
        }

        public static string Released(GrappleEnd reason, float speed)
        {
            if (reason == GrappleEnd.None || reason == GrappleEnd.Arrived)
            {
                throw new ArgumentOutOfRangeException(nameof(reason), reason, "a release needs a reason other than None or Arrived");
            }

            return string.Format(CultureInfo.InvariantCulture, "{0}reason={1} speed={2:0.00}", ReleasedPrefix, reason, speed);
        }

        public static string Climb(bool started)
        {
            return $"{ClimbPrefix}started={(started ? "true" : "false")}";
        }

        private static string NameOrDash(string name)
        {
            return string.IsNullOrEmpty(name) ? "-" : name;
        }
    }
}
