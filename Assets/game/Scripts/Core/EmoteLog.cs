using System;
using System.Collections.Generic;
using System.Globalization;

namespace Shooter.Core
{
    public static class EmoteLog
    {
        public const string Tag = "[Emote]";

        public static string Rule(Emote rule, IReadOnlyList<EmoteSpec> emotes)
        {
            if (emotes == null)
            {
                throw new ArgumentNullException(nameof(emotes));
            }

            return string.Format(CultureInfo.InvariantCulture, "{0} rule={1} emotes={2}", Tag, rule, emotes.Count);
        }

        public static string Played(EmoteSpec emote, bool hasAnimation)
        {
            if (emote == null)
            {
                throw new ArgumentNullException(nameof(emote));
            }

            return string.Format(
                CultureInfo.InvariantCulture,
                "{0} played id={1} seconds={2} loops={3} stopsOnMove={4} animation={5}",
                Tag, emote.Id, emote.DurationSeconds, emote.Loops, emote.StopsOnMove, hasAnimation ? "yes" : "none");
        }

        public static string Stopped(string emoteId, EmoteStopReason reason, float elapsedSeconds)
        {
            return string.Format(
                CultureInfo.InvariantCulture, "{0} stopped id={1} reason={2} after={3:0.##}s", Tag, Id(emoteId), reason, elapsedSeconds);
        }

        public static string Refused(string emoteId, EmoteRefusal reason)
        {
            return string.Format(CultureInfo.InvariantCulture, "{0} refused id={1} reason={2}", Tag, Id(emoteId), reason);
        }

        public static string WheelOpened(int count)
        {
            return string.Format(CultureInfo.InvariantCulture, "{0} wheel opened emotes={1}", Tag, count);
        }

        public static string WheelClosed()
        {
            return Tag + " wheel closed";
        }

        public static string NoAnimation(string emoteId)
        {
            return string.Format(
                CultureInfo.InvariantCulture, "{0} no animation id={1}; the emote runs without moving the body (add it to the EmoteCatalog)", Tag, Id(emoteId));
        }

        public static string ClipStarted(string characterName, string clipName)
        {
            return string.Format(CultureInfo.InvariantCulture, "{0} clip started on '{1}' clip={2}", Tag, characterName, Id(clipName));
        }

        public static string NoInput(string actionPath)
        {
            return string.Format(CultureInfo.InvariantCulture, "{0} no input action '{1}'; the emote wheel is opened only from code", Tag, actionPath);
        }

        public static string NetRequested(string emoteId)
        {
            return string.Format(CultureInfo.InvariantCulture, "{0} net requested id={1}", Tag, Id(emoteId));
        }

        public static string NetStopRequested()
        {
            return Tag + " net stop requested";
        }

        public static string NetNotSent(string emoteId, string reason)
        {
            return string.Format(CultureInfo.InvariantCulture, "{0} net not sent id={1} reason={2}", Tag, Id(emoteId), reason);
        }

        public static string NetPlayed(int playerId, string emoteId, bool hasAnimation)
        {
            return string.Format(
                CultureInfo.InvariantCulture,
                "{0} net played player={1} id={2} animation={3}",
                Tag, playerId, Id(emoteId), hasAnimation ? "yes" : "none");
        }

        public static string NetStopped(int playerId, string emoteId, EmoteStopReason reason)
        {
            return string.Format(
                CultureInfo.InvariantCulture, "{0} net stopped player={1} id={2} reason={3}", Tag, playerId, Id(emoteId), reason);
        }

        public static string NetNoBody(int playerId, string emoteId)
        {
            return string.Format(
                CultureInfo.InvariantCulture, "{0} net no body player={1} id={2}; the emote is not shown (the player is out of view)", Tag, playerId, Id(emoteId));
        }

        private static string Id(string id)
        {
            return string.IsNullOrEmpty(id) ? "-" : id;
        }
    }
}
