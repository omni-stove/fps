using System;
using System.Collections.Generic;

namespace Fps.Core
{
    /// <summary>
    /// ロビーを使った起動の設定。コマンドライン引数 <c>-lobby quickmatch</c> から作る。つなぐサーバーはロビーで決まるので、
    /// <c>-mode</c> / <c>-address</c> / <c>-port</c> / <c>-eos-server</c> / <c>-register-lobby</c> とは一緒に使えない。
    /// 選んだサーバーのロビーの公開アドレスへ UDP でつなぐ (<see cref="ServerLobby.ConnectOptions"/>)。
    /// </summary>
    public sealed class LobbyLaunchOptions
    {
        public const string LobbyFlag = "-lobby";

        public const int DefaultMaxPlayers = NetMatchSettings.DefaultMaxPlayers;

        /// <summary>
        /// ロビーの最大人数の下限。ロビーに入るのはサーバーだけで、人数は属性で載せるので、試合が 1 人までのサーバーも載せられるようにする。
        /// </summary>
        public const int MinMaxPlayers = NetMatchSettings.MinMaxPlayers;

        /// <summary>ロビーの最大人数の上限 (EOS_LOBBY_MAX_LOBBY_MEMBERS)。</summary>
        public const int MaxMaxPlayers = 64;

        /// <summary>クイックマッチでつなげるサーバーが見つからないとき、検索し直すまでの秒数。</summary>
        public const float QuickMatchIntervalSeconds = 5f;

        /// <summary>クイックマッチでログインからサーバーが決まるまで待つ秒数。過ぎたら見つからなかったとして諦める。</summary>
        public const float QuickMatchTimeoutSeconds = 30f;

        /// <summary>1 回の検索で受け取るロビーの最大数。</summary>
        public const int MaxSearchResults = 25;

        /// <summary>バケット ID に入れるバージョンの最大の長さ。</summary>
        public const int MaxBucketVersionLength = 32;

        private static readonly LobbyCommand[] _commands = { LobbyCommand.QuickMatch };

        /// <summary>ロビーが決めるので、<c>-lobby</c> と一緒には指定できない起動引数。</summary>
        private static readonly string[] _conflictingFlags =
        {
            NetLaunchOptions.ModeFlag,
            NetLaunchOptions.AddressFlag,
            NetLaunchOptions.PortFlag,
            NetLaunchOptions.EosServerFlag,
            NetLaunchOptions.RegisterLobbyFlag,
        };

        /// <summary>ロビーを使わない設定。</summary>
        public static LobbyLaunchOptions None { get; } = new LobbyLaunchOptions(LobbyCommand.None);

        public LobbyCommand Command { get; }

        /// <summary>ロビーを使うか。</summary>
        public bool IsRequested => Command != LobbyCommand.None;

        private LobbyLaunchOptions(LobbyCommand command)
        {
            Command = command;
        }

        /// <summary>専用サーバーのロビーを探してつなぐ設定。</summary>
        public static LobbyLaunchOptions QuickMatch()
        {
            return new LobbyLaunchOptions(LobbyCommand.QuickMatch);
        }

        /// <summary>引数に <c>-lobby</c> が含まれるか。値の妥当性は見ない。</summary>
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

        /// <summary>
        /// コマンドライン引数を解析する。<c>-lobby</c> が無ければ <see cref="None"/> を返す。
        /// 知らない引数 (Unity 自身の <c>-batchmode</c> など) は無視する。
        /// </summary>
        /// <param name="args">コマンドライン引数。先頭が実行ファイルのパスでもよい。</param>
        /// <param name="options">成功したときの設定。失敗したときは null。</param>
        /// <param name="error">失敗したときの理由。成功したときは null。</param>
        /// <returns>引数が正しければ true。</returns>
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

        /// <summary>バケット ID に入れる版。空なら "0"、長すぎれば <see cref="MaxBucketVersionLength"/> 文字で切る。</summary>
        internal static string BucketVersion(string version)
        {
            var trimmed = string.IsNullOrWhiteSpace(version) ? "0" : version.Trim();
            if (trimmed.Length > MaxBucketVersionLength)
            {
                trimmed = trimmed.Substring(0, MaxBucketVersionLength);
            }

            return trimmed;
        }

        /// <summary>ログ向けの小文字の操作名 (none / quickmatch)。<c>-lobby</c> の値と同じ。</summary>
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

        /// <summary>次の引数が値ではなく別のフラグに見えるか (値の書き忘れを検出する)。</summary>
        private static bool IsFlagLike(string arg)
        {
            return string.IsNullOrEmpty(arg) || arg[0] == '-';
        }
    }
}
