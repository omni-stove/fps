using System;
using System.Globalization;
using System.Numerics;

namespace Shooter.Core
{
    public static class SquadOrderLog
    {
        public const string Tag = "[Squad]";

        public static string Spec(SquadOrderSpec spec)
        {
            if (spec == null)
            {
                throw new ArgumentNullException(nameof(spec));
            }

            return string.Format(
                CultureInfo.InvariantCulture,
                "{0} spec hold={1:0.00} distance={2:0.0} nearby={3:0.0} follow={4:0.0} leash={5:0.0}",
                Tag,
                spec.HoldSeconds,
                spec.MaxDistance,
                spec.NearbyRadius,
                spec.FollowDistance,
                spec.LeashDistance);
        }

        public static string Issued(BotErrandKind kind, string target, Vector3 at, int recipients, bool squads)
        {
            return Tag + " order kind=" + kind + " target=" + Token(target) + " at=" + Point(at)
                + " recipients=" + recipients.ToString(CultureInfo.InvariantCulture) + " squads=" + (squads ? "true" : "false");
        }

        public static string Refused(SquadOrderRefusal reason)
        {
            return Tag + " refused reason=" + reason;
        }

        public static string Accepted(string botId, BotErrandKind kind, string target, Vector3 at)
        {
            return Tag + " accepted id=" + Token(botId) + " kind=" + kind + " target=" + Token(target) + " at=" + Point(at);
        }

        public static string Arrived(string botId, Vector3 at)
        {
            return Tag + " arrived id=" + Token(botId) + " at=" + Point(at);
        }

        public static string Engage(string botId, string target)
        {
            return Tag + " engage id=" + Token(botId) + " target=" + Token(target);
        }

        public static string Done(string botId, BotErrandKind kind, string reason, BotErrandKind next)
        {
            return Tag + " done id=" + Token(botId) + " kind=" + kind + " reason=" + Token(reason) + " next=" + next;
        }

        public static string Screen(BotErrandKind kind, PingScreenPlacement placement)
        {
            return string.Format(
                CultureInfo.InvariantCulture,
                "{0} screen kind={1} onScreen={2} edge={3} at=({4:0.00}, {5:0.00})",
                Tag,
                kind,
                placement.IsOnScreen ? "true" : "false",
                placement.Edge,
                placement.X,
                placement.Y);
        }

        public static string View(string view)
        {
            return Tag + " view=" + Token(view);
        }

        private static string Point(Vector3 point)
        {
            return string.Format(CultureInfo.InvariantCulture, "({0:0.00}, {1:0.00}, {2:0.00})", point.X, point.Y, point.Z);
        }

        // 名前に空白があってもログの区切りが崩れないよう、空白は _ に、空なら none にする。
        private static string Token(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                return "none";
            }

            var chars = text.Trim().ToCharArray();
            for (var i = 0; i < chars.Length; i++)
            {
                if (char.IsWhiteSpace(chars[i]))
                {
                    chars[i] = '_';
                }
            }

            return new string(chars);
        }
    }
}
