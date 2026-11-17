using System;
using Shooter.Core;

namespace Shooter.Runtime
{
    public static class SquadOrderText
    {
        public const string LineKey = "squad.reply.line";

        public const string TargetDownKey = "squad.reply.targetDown";

        public static string ReplyKey(BotErrandKind kind)
        {
            RequireOrder(kind);
            return "squad.reply." + kind;
        }

        public static string MarkerKey(BotErrandKind kind)
        {
            RequireOrder(kind);
            return "squad.marker." + kind;
        }

        public static string Reply(string speaker, BotErrandKind kind)
        {
            return Line(speaker, ReplyKey(kind));
        }

        public static string Reply(SquadOrderReply reply)
        {
            return reply.IsTargetDown ? Line(reply.Speaker, TargetDownKey) : Reply(reply.Speaker, reply.Order);
        }

        public static string Line(string speaker, string replyKey)
        {
            if (replyKey == null)
            {
                throw new ArgumentNullException(nameof(replyKey));
            }

            return GameText.Format(LineKey, string.IsNullOrWhiteSpace(speaker) ? "?" : speaker.Trim(), GameText.Get(replyKey));
        }

        public static string Marker(BotErrandKind kind)
        {
            return GameText.Get(MarkerKey(kind));
        }

        private static void RequireOrder(BotErrandKind kind)
        {
            if (!BotErrand.IsOrderKind(kind))
            {
                throw new ArgumentOutOfRangeException(nameof(kind), kind, "kind must be a squad order (Hold, Attack or Follow)");
            }
        }
    }
}
