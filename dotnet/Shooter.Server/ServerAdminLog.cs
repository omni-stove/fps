using System.Collections.Generic;
using System.Globalization;
using Shooter.Core;

namespace Shooter.Server
{
    internal static class ServerAdminLog
    {
        public const string LogTag = "[Admin]";

        private const string _tag = LogTag + " ";

        public static IReadOnlyList<string> Usage { get; } = new[]
        {
            _tag + "commands (one per line on stdin):",
            _tag + "  players                                         list the connected players",
            _tag + "  kick <player> [reason]                          disconnect a player (the number from players)",
            _tag + "  ban <player|product user id|address> [reason]   refuse them from now on and disconnect them",
            _tag + "  unban <product user id|address>                 let them in again",
            _tag + "  bans                                            list the bans",
            _tag + "  help                                            show this list",
        };

        public static string Loaded(ServerBanList bans)
        {
            if (bans.Path == null)
            {
                return _tag + "no ban list (" + ServerLaunch.BanFileFlag + "); bans last until the server stops";
            }

            return _tag + "ban list " + bans.Path + ": " + Count(bans.Entries.Count) + " ban(s)";
        }

        public static string Ready()
        {
            return _tag + "reading commands from stdin; type help to list them";
        }

        public static string InputEnded()
        {
            return _tag + "stdin closed; no more commands are read";
        }

        public static string Refused(ServerBanEntry entry)
        {
            return "banned (" + entry.Describe() + ")";
        }

        public static string NoPlayers()
        {
            return _tag + "no players are connected";
        }

        public static string Player(ServerPlayer player, string address)
        {
            return _tag + "player " + Count(player.PlayerId)
                + " team=" + player.Team
                + " client=" + player.Connection.Value.ToString(CultureInfo.InvariantCulture)
                + " product_user_id=" + (player.ProductUserId ?? "-")
                + " address=" + (address ?? "-");
        }

        public static string Players(int count)
        {
            return _tag + Count(count) + " player(s)";
        }

        public static string Kicking(int playerId)
        {
            return _tag + "kicking player " + Count(playerId);
        }

        public static string KickedLogText(string reason)
        {
            return "kicked from the admin console" + (string.IsNullOrWhiteSpace(reason) ? "" : " (reason=\"" + reason + "\")");
        }

        public static string BannedLogText(ServerBanEntry entry)
        {
            return "banned from the admin console (" + entry.Describe() + ")";
        }

        public static string Banned(ServerBanEntry entry, int disconnected)
        {
            return _tag + "banned " + entry.Describe() + "; disconnecting " + Count(disconnected) + " player(s)";
        }

        public static string AlreadyBanned(ServerBanEntry entry, int disconnected)
        {
            return _tag + "already banned: " + entry.Describe() + "; disconnecting " + Count(disconnected) + " player(s)";
        }

        public static string Unbanned(ServerBanEntry entry)
        {
            return _tag + "unbanned " + entry.Describe();
        }

        public static string NotBanned(ServerBanKind kind, string value)
        {
            return _tag + "no ban for " + ServerBanEntry.KindName(kind) + " " + value;
        }

        public static string Saved(ServerBanList bans)
        {
            return bans.Path == null
                ? _tag + "the ban is kept until the server stops (pass " + ServerLaunch.BanFileFlag + " to keep it in a file)"
                : _tag + "wrote " + Count(bans.Entries.Count) + " ban(s) to " + bans.Path;
        }

        public static string SaveFailed(string error)
        {
            return _tag + error + "; the change is kept until the server stops";
        }

        public static string NoBans()
        {
            return _tag + "no bans";
        }

        public static string Ban(ServerBanEntry entry)
        {
            return _tag + "ban " + entry.Describe();
        }

        public static string UnknownCommand(string command)
        {
            return _tag + "unknown command '" + command + "'; type help to list the commands";
        }

        public static string NeedsTarget(string command, string what)
        {
            return _tag + command + " needs " + what;
        }

        public static string NoSuchPlayer(string text)
        {
            return _tag + "no connected player " + text + "; type players to list them";
        }

        public static string BotNotRemoved(int playerId)
        {
            return _tag + "player " + Count(playerId) + " is a bot; bots are not kicked or banned";
        }

        public static string UnknownTarget(string text)
        {
            return _tag + "'" + text + "' is not a player number, a " + NetLaunchOptions.ProductUserIdLength.ToString(CultureInfo.InvariantCulture)
                + "-digit product user id or an IP address";
        }

        public static string NothingToBan(int playerId)
        {
            return _tag + "player " + Count(playerId) + " has no product user id or address to ban; kick them instead";
        }

        public static string ReasonTooLong()
        {
            return _tag + "the reason must be at most " + Count(ServerBanList.MaxReasonLength) + " characters";
        }

        private static string Count(int value)
        {
            return value.ToString(CultureInfo.InvariantCulture);
        }
    }
}
