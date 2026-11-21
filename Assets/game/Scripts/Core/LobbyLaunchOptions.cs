using System;
using System.Collections.Generic;

namespace Shooter.Core
{
    public sealed class LobbyLaunchOptions
    {
        public const string LobbyFlag = "-lobby";

        public const int DefaultMaxPlayers = NetMatchSettings.DefaultMaxPlayers;

        // 人数は属性で載せ、メンバーの上限はサーバー自身の分を足して決める (ServerLobby.MaxMembers) ので、
        // 試合が 1 人までのサーバーも載せられるようにする。
        public const int MinMaxPlayers = NetMatchSettings.MinMaxPlayers;

        // EOS_LOBBY_MAX_LOBBY_MEMBERS に合わせてある。
        public const int MaxMaxPlayers = 64;

        public const float QuickMatchIntervalSeconds = 5f;

        public const float QuickMatchTimeoutSeconds = 30f;

        public const float JoinCodeTimeoutSeconds = 15f;

        public const int MaxSearchResults = 25;

        // クイックマッチと違って人が選ぶ一覧なので、1 つを選ぶ検索より多く取る。EOS の検索の上限 (EOS_LOBBY_MAX_SEARCH_RESULTS) は 200。
        public const int MaxBrowserResults = 100;

        public const float ServerBrowserTimeoutSeconds = 15f;

        // 返らない ping (ICMP を落とすサーバー) を待ち続けず、測れなかったものとして一覧に出す。
        public const float ServerPingTimeoutSeconds = 2f;

        public const int MaxBucketVersionLength = 32;

        private static readonly LobbyCommand[] _commands = { LobbyCommand.QuickMatch };

        // つなぐサーバーはロビーで決まるので、-lobby とは一緒に指定させない。
        private static readonly string[] _conflictingFlags =
        {
            NetLaunchOptions.ModeFlag,
            NetLaunchOptions.AddressFlag,
            NetLaunchOptions.PortFlag,
            NetLaunchOptions.EosServerFlag,
            NetLaunchOptions.RegisterLobbyFlag,
        };

        public static LobbyLaunchOptions None { get; } = new LobbyLaunchOptions(LobbyCommand.None);

        public LobbyCommand Command { get; }

        public bool IsRequested => Command != LobbyCommand.None;

        private LobbyLaunchOptions(LobbyCommand command)
        {
            Command = command;
        }

        public static LobbyLaunchOptions QuickMatch()
        {
            return new LobbyLaunchOptions(LobbyCommand.QuickMatch);
        }

        public static bool HasLobbyFlag(IReadOnlyList<string> args)
        {
            if (args == null)
            {
                throw new ArgumentNullException(nameof(args));
            }

            foreach (var arg in args)
            {
                if (IsFlag(arg, LobbyFlag))
                {
                    return true;
                }
            }

            return false;
        }

        // 知らない引数を断らないのは、Unity 自身の -batchmode などが混ざって渡るため。
        public static bool TryParse(IReadOnlyList<string> args, out LobbyLaunchOptions options, out string error)
        {
            if (args == null)
            {
                throw new ArgumentNullException(nameof(args));
            }

            options = null;
            string commandText = null;
            var seen = false;
            string conflict = null;

            for (var i = 0; i < args.Count; i++)
            {
                if (!IsFlag(args[i], LobbyFlag))
                {
                    if (conflict == null)
                    {
                        conflict = MatchFlag(args[i], _conflictingFlags);
                    }

                    continue;
                }

                if (i + 1 >= args.Count || IsFlagLike(args[i + 1]))
                {
                    error = LobbyFlag + " requires a value";
                    return false;
                }

                if (seen)
                {
                    error = LobbyFlag + " is given more than once";
                    return false;
                }

                seen = true;
                commandText = args[++i];
            }

            if (!seen)
            {
                options = None;
                error = null;
                return true;
            }

            if (!TryParseCommand(commandText, out var command))
            {
                error = LobbyFlag + " must be " + CommandName(LobbyCommand.QuickMatch) + " (got '" + commandText + "')";
                return false;
            }

            if (conflict != null)
            {
                error = conflict + " cannot be used with " + LobbyFlag + "; the lobby decides the server to connect to";
                return false;
            }

            options = new LobbyLaunchOptions(command);
            error = null;
            return true;
        }

        internal static string BucketVersion(string version)
        {
            var trimmed = string.IsNullOrWhiteSpace(version) ? "0" : version.Trim();
            if (trimmed.Length > MaxBucketVersionLength)
            {
                trimmed = trimmed.Substring(0, MaxBucketVersionLength);
            }

            return trimmed;
        }

        public static string CommandName(LobbyCommand command)
        {
            switch (command)
            {
                case LobbyCommand.None:
                    return "none";
                case LobbyCommand.QuickMatch:
                    return "quickmatch";
                default:
                    throw new ArgumentOutOfRangeException(nameof(command), command, "unknown lobby command");
            }
        }

        public override string ToString()
        {
            return CommandName(Command);
        }

        private static bool TryParseCommand(string text, out LobbyCommand command)
        {
            foreach (var candidate in _commands)
            {
                if (string.Equals(text, CommandName(candidate), StringComparison.OrdinalIgnoreCase))
                {
                    command = candidate;
                    return true;
                }
            }

            command = LobbyCommand.None;
            return false;
        }

        private static string MatchFlag(string arg, string[] flags)
        {
            foreach (var flag in flags)
            {
                if (IsFlag(arg, flag))
                {
                    return flag;
                }
            }

            return null;
        }

        private static bool IsFlag(string arg, string flag)
        {
            return string.Equals(arg, flag, StringComparison.OrdinalIgnoreCase);
        }

        // 次の引数が別のフラグに見えたら値として読まないのは、値の書き忘れを見つけるため。
        private static bool IsFlagLike(string arg)
        {
            return string.IsNullOrEmpty(arg) || arg[0] == '-';
        }
    }
}
