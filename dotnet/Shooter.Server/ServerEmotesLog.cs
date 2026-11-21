using System;
using System.Collections.Generic;
using System.Globalization;
using Shooter.Core;

namespace Shooter.Server
{
    // タグはオフラインと同じ EmoteLog.Tag にそろえ、同じ grep でオフラインとサーバーの行を拾えるようにする
    internal static class ServerEmotesLog
    {
        public static string Started(Emote rule, IReadOnlyList<EmoteSpec> emotes, string path)
        {
            ArgumentNullException.ThrowIfNull(emotes);
            return string.Format(
                CultureInfo.InvariantCulture,
                "{0} server rule={1} emotes={2} tuning={3}",
                EmoteLog.Tag, rule, emotes.Count, path ?? "default");
        }

        public static string Played(int playerId, EmoteSpec emote, int recipients)
        {
            ArgumentNullException.ThrowIfNull(emote);
            return string.Format(
                CultureInfo.InvariantCulture,
                "{0} server played player={1} id={2} seconds={3} loops={4} stopsOnMove={5} to={6}",
                EmoteLog.Tag, playerId, emote.Id, emote.DurationSeconds, emote.Loops, emote.StopsOnMove, recipients);
        }

        public static string Stopped(int playerId, string emoteId, EmoteStopReason reason, float elapsedSeconds)
        {
            return string.Format(
                CultureInfo.InvariantCulture,
                "{0} server stopped player={1} id={2} reason={3} after={4:0.##}s",
                EmoteLog.Tag, playerId, Id(emoteId), reason, elapsedSeconds);
        }

        public static string Refused(int playerId, string emoteId, EmoteRefusal reason)
        {
            return string.Format(
                CultureInfo.InvariantCulture, "{0} server refused player={1} id={2} reason={3}", EmoteLog.Tag, playerId, Id(emoteId), reason);
        }

        private static string Id(string id)
        {
            return string.IsNullOrEmpty(id) ? "-" : id;
        }
    }
}
