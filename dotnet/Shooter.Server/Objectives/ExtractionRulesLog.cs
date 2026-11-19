using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using Shooter.Core;

namespace Shooter.Server
{
    internal static class ExtractionRulesLog
    {
        public const string Tag = ExtractionLog.Tag;

        public const string StashTag = StashLog.Tag;

        public static string StashDirectory(string directory)
        {
            return StashTag + " directory=" + directory;
        }

        public static string Started(ExtractionSpec spec, IReadOnlyList<NetMapExtractionPoint> points, IReadOnlyList<string> openPointIds, string stashDirectory)
        {
            ArgumentNullException.ThrowIfNull(spec);
            ArgumentNullException.ThrowIfNull(points);
            ArgumentNullException.ThrowIfNull(openPointIds);
            var ids = new string[points.Count];
            for (var i = 0; i < ids.Length; i++)
            {
                ids[i] = points[i].Id;
            }

            return Tag + " net points=" + Ids(ids) + " open=" + Ids(openPointIds)
                + " raid=" + Seconds(spec.RaidSeconds)
                + " open-after=" + Seconds(spec.OpenAfterSeconds)
                + " hold=" + Seconds(spec.ExtractHoldSeconds)
                + " keepOnDeath=" + (spec.KeepCarriedOnDeath ? "true" : "false")
                + " stash=" + stashDirectory;
        }

        public static string Restart(IReadOnlyList<string> openPointIds)
        {
            ArgumentNullException.ThrowIfNull(openPointIds);
            return Tag + " restart open=" + Ids(openPointIds);
        }

        public static string Loaded(int playerId, string accountId, string path, int stacks, bool saved)
        {
            return StashTag + " load player=" + Text(playerId) + " account=" + accountId + " stacks=" + Text(stacks)
                + (saved ? " from=" + path : " new=" + path);
        }

        public static string NoAccount(int playerId)
        {
            return StashTag + " player=" + Text(playerId) + " has no account key; the stash starts from the starting items and is not saved";
        }

        public static string LoadFailed(int playerId, string accountId, string message)
        {
            return StashTag + " cannot read the stash of account=" + accountId + " player=" + Text(playerId) + ": " + message
                + "; the stash starts from the starting items and is not saved, so the file is left as it is";
        }

        public static string Saved(int playerId, string accountId, string path, int stacks)
        {
            return StashTag + " saved player=" + Text(playerId) + " account=" + accountId + " stacks=" + Text(stacks) + " to=" + path;
        }

        public static string SaveFailed(int playerId, string accountId, string message)
        {
            return StashTag + " cannot save the stash of account=" + accountId + " player=" + Text(playerId) + ": " + message;
        }

        public static string RaidStarted(int playerId, ExtractionSession session)
        {
            return ExtractionLog.Started(session) + " player=" + Text(playerId);
        }

        public static string RaidEnded(int playerId, string accountId, ExtractionSession session)
        {
            return ExtractionLog.Ended(session) + " player=" + Text(playerId) + " account=" + (accountId ?? "-");
        }

        public static string NoRaid(int playerId, float elapsedSeconds)
        {
            return Tag + " player=" + Text(playerId) + " spawned after the raid time ran out at=" + Seconds(elapsedSeconds) + "; no raid this match";
        }

        public static string NotInStash(int playerId, string weaponId)
        {
            return Tag + " player=" + Text(playerId) + " weapon=" + weaponId + " is not in the stash; left behind";
        }

        public static string NotAnItem(int playerId, string weaponId)
        {
            return Tag + " player=" + Text(playerId) + " weapon=" + weaponId + " is not in inventory.json, so the stash cannot hold it; left behind";
        }

        public static string PickRefused(int playerId, string itemId, string message)
        {
            return Tag + " player=" + Text(playerId) + " item=" + itemId + " is not counted as carried: " + message;
        }

        public static string Abandoned(int playerId, IReadOnlyList<ItemCount> returned)
        {
            ArgumentNullException.ThrowIfNull(returned);
            var text = new StringBuilder(Tag).Append(" raid player=").Append(Text(playerId)).Append(" called off returned=");
            if (returned.Count == 0)
            {
                text.Append('-');
            }

            for (var i = 0; i < returned.Count; i++)
            {
                if (i > 0)
                {
                    text.Append(',');
                }

                text.Append(returned[i].ItemId).Append('x').Append(Text(returned[i].Count));
            }

            return text.ToString();
        }

        public static string AllOver(int raids)
        {
            return Tag + " every raid of the match is over raids=" + Text(raids);
        }

        private static string Ids(IReadOnlyList<string> ids)
        {
            return ids.Count == 0 ? "-" : string.Join(",", ids);
        }

        private static string Text(int value) => value.ToString(CultureInfo.InvariantCulture);

        private static string Seconds(float value) => value.ToString("F1", CultureInfo.InvariantCulture) + "s";
    }
}
