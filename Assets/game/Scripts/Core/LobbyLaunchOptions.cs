using System;
using System.Collections.Generic;
using System.Globalization;

namespace Fps.Core
{
    /// <summary>
    /// ロビーを使った起動の設定。コマンドライン引数 <c>-lobby create|join</c> / <c>-lobby-id</c> /
    /// <c>-lobby-max-players</c> から作る。ロビーを使うときは、起動モードと接続先はロビーで決まるので
    /// <c>-mode</c> / <c>-address</c> / <c>-port</c> / <c>-eos-server</c> は受け付けず、transport は EOS に固定する。
    /// </summary>
    public sealed class LobbyLaunchOptions
    {
        public const string LobbyFlag = "-lobby";
        public const string LobbyIdFlag = "-lobby-id";
        public const string MaxPlayersFlag = "-lobby-max-players";

        /// <summary>ロビーの既定の最大人数。</summary>
        public const int DefaultMaxPlayers = 8;

        /// <summary>ロビーの最大人数の下限。1 人ではロビーにする意味が無い。</summary>
        public const int MinMaxPlayers = 2;

        /// <summary>ロビーの最大人数の上限 (EOS_LOBBY_MAX_LOBBY_MEMBERS)。</summary>
        public const int MaxMaxPlayers = 64;

        /// <summary><c>-lobby-id</c> に受け付ける最大の長さ。EOS のロビー ID はこれより十分短い。</summary>
        public const int MaxLobbyIdLength = 128;

        /// <summary>参加できるロビーが見つからないとき、検索し直すまでの秒数。</summary>
        public const float SearchIntervalSeconds = 3f;

        /// <summary>ログインからロビーの作成・参加が終わるまで待つ秒数。</summary>
        public const float TimeoutSeconds = 60f;

        /// <summary>1 回の検索で受け取るロビーの最大数。</summary>
        public const int MaxSearchResults = 25;

        /// <summary>バケット ID の接頭辞。同じ EOS のサンドボックスを使う別のゲームのロビーと混ざらないようにする。</summary>
        public const string BucketPrefix = "fps:";

        /// <summary>バケット ID に入れるバージョンの最大の長さ。</summary>
        public const int MaxBucketVersionLength = 32;

        private static readonly string[] _ownFlags = { LobbyFlag, LobbyIdFlag, MaxPlayersFlag };

        private static readonly LobbyCommand[] _commands = { LobbyCommand.Create, LobbyCommand.Join };

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
        public static LobbyLaunchOptions None { get; } = new LobbyLaunchOptions(LobbyCommand.None, null, DefaultMaxPlayers);

        public LobbyCommand Command { get; }

        /// <summary><see cref="LobbyCommand.Join"/> で参加するロビーの ID。null なら検索して選ぶ。</summary>
        public string LobbyId { get; }

        /// <summary><see cref="LobbyCommand.Create"/> で作るロビーの最大人数。</summary>
        public int MaxPlayers { get; }

        /// <summary>ロビーを使うか。</summary>
        public bool IsRequested => Command != LobbyCommand.None;

        private LobbyLaunchOptions(LobbyCommand command, string lobbyId, int maxPlayers)
        {
            Command = command;
            LobbyId = lobbyId;
            MaxPlayers = maxPlayers;
        }

        /// <summary>ロビーを作る設定。</summary>
        public static LobbyLaunchOptions Create(int maxPlayers = DefaultMaxPlayers)
        {
            var error = ValidateMaxPlayers(maxPlayers);
            if (error != null)
            {
                throw new ArgumentOutOfRangeException(nameof(maxPlayers), maxPlayers, error);
            }

            return new LobbyLaunchOptions(LobbyCommand.Create, null, maxPlayers);
        }

        /// <summary>ロビーに参加する設定。</summary>
        /// <param name="lobbyId">参加するロビーの ID。null なら検索して選ぶ。</param>
        public static LobbyLaunchOptions Join(string lobbyId = null)
        {
            if (lobbyId != null)
            {
                var error = ValidateLobbyId(lobbyId);
                if (error != null)
                {
                    throw new ArgumentException(error, nameof(lobbyId));
                }
            }

            return new LobbyLaunchOptions(LobbyCommand.Join, lobbyId, DefaultMaxPlayers);
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
            var values = new Dictionary<string, string>(StringComparer.Ordinal);
            string transportText = null;
            string conflict = null;

            for (var i = 0; i < args.Count; i++)
            {
                var flag = MatchFlag(args[i], _ownFlags);
                if (flag == null)
                {
                    if (IsFlag(args[i], NetLaunchOptions.TransportFlag) && i + 1 < args.Count)
                    {
                        transportText = args[i + 1];
                    }
                    else if (conflict == null)
                    {
                        conflict = MatchFlag(args[i], _conflictingFlags);
                    }

                    continue;
                }

                if (i + 1 >= args.Count || IsFlagLike(args[i + 1]))
                {
                    error = flag + " requires a value";
                    return false;
                }

                if (values.ContainsKey(flag))
                {
                    error = flag + " is given more than once";
                    return false;
                }

                values.Add(flag, args[++i]);
            }

            if (!values.TryGetValue(LobbyFlag, out var commandText))
            {
                foreach (var flag in _ownFlags)
                {
                    if (values.ContainsKey(flag))
                    {
                        error = flag + " is only used with " + LobbyFlag + " create|join";
                        return false;
                    }
                }

                options = None;
                error = null;
                return true;
            }

            if (!TryParseCommand(commandText, out var command))
            {
                error = LobbyFlag + " must be create or join (got '" + commandText + "')";
                return false;
            }

            if (conflict != null)
            {
                error = conflict + " cannot be used with " + LobbyFlag + "; the lobby decides the mode and the host to connect to";
                return false;
            }

            if (transportText != null
                && !string.Equals(transportText, NetLaunchOptions.TransportName(NetTransportKind.Eos), StringComparison.OrdinalIgnoreCase))
            {
                error = LobbyFlag + " always uses " + NetLaunchOptions.TransportFlag + " "
                    + NetLaunchOptions.TransportName(NetTransportKind.Eos) + " (got '" + transportText + "')";
                return false;
            }

            if (command == LobbyCommand.Create)
            {
                if (values.ContainsKey(LobbyIdFlag))
                {
                    error = LobbyIdFlag + " is only used with " + LobbyFlag + " join";
                    return false;
                }

                var maxPlayers = DefaultMaxPlayers;
                if (values.TryGetValue(MaxPlayersFlag, out var maxText))
                {
                    if (!int.TryParse(maxText, NumberStyles.None, CultureInfo.InvariantCulture, out maxPlayers))
                    {
                        error = MaxPlayersFlag + " must be an integer (got '" + maxText + "')";
                        return false;
                    }

                    var maxError = ValidateMaxPlayers(maxPlayers);
                    if (maxError != null)
                    {
                        error = MaxPlayersFlag + " " + maxError + " (got '" + maxText + "')";
                        return false;
                    }
                }

                options = new LobbyLaunchOptions(LobbyCommand.Create, null, maxPlayers);
                error = null;
                return true;
            }

            if (values.ContainsKey(MaxPlayersFlag))
            {
                error = MaxPlayersFlag + " is only used with " + LobbyFlag + " create";
                return false;
            }

            values.TryGetValue(LobbyIdFlag, out var lobbyId);
            if (lobbyId != null)
            {
                var idError = ValidateLobbyId(lobbyId);
                if (idError != null)
                {
                    error = LobbyIdFlag + " " + idError;
                    return false;
                }
            }

            options = new LobbyLaunchOptions(LobbyCommand.Join, lobbyId, DefaultMaxPlayers);
            error = null;
            return true;
        }

        /// <summary>
        /// ロビーが決まったあとのネットワークの起動設定。作ったときは EOS の Host、参加したときはオーナーへ接続する EOS のクライアント。
        /// </summary>
        /// <param name="ownerUserId">ロビーのオーナーの ProductUserId。<see cref="LobbyCommand.Join"/> でだけ使う。</param>
        /// <exception cref="InvalidOperationException">ロビーを使わない設定。</exception>
        /// <exception cref="ArgumentException">参加したのに、オーナーの ID が ProductUserId の形でない。</exception>
        public NetLaunchOptions ToNetOptions(string ownerUserId)
        {
            switch (Command)
            {
                case LobbyCommand.Create:
                    return new NetLaunchOptions(
                        NetLaunchMode.Host, NetLaunchOptions.DefaultAddress, NetLaunchOptions.DefaultPort, NetTransportKind.Eos, null);
                case LobbyCommand.Join:
                    if (!NetLaunchOptions.IsProductUserId(ownerUserId))
                    {
                        throw new ArgumentException(
                            "the lobby owner '" + ownerUserId + "' is not an EOS product user id", nameof(ownerUserId));
                    }

                    return new NetLaunchOptions(
                        NetLaunchMode.Client, NetLaunchOptions.DefaultAddress, NetLaunchOptions.DefaultPort, NetTransportKind.Eos, ownerUserId);
                default:
                    throw new InvalidOperationException("no lobby command was given");
            }
        }

        /// <summary>
        /// ロビーを作る・探すときのバケット ID。バージョンの違うビルド同士は同じロビーに入らない。
        /// </summary>
        /// <param name="version">アプリのバージョン。空なら "0"。</param>
        public static string BucketId(string version)
        {
            return BucketPrefix + BucketVersion(version);
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

        /// <summary>ログ向けの小文字の操作名 (none / create / join)。</summary>
        public static string CommandName(LobbyCommand command)
        {
            switch (command)
            {
                case LobbyCommand.None:
                    return "none";
                case LobbyCommand.Create:
                    return "create";
                case LobbyCommand.Join:
                    return "join";
                default:
                    throw new ArgumentOutOfRangeException(nameof(command), command, "unknown lobby command");
            }
        }

        public override string ToString()
        {
            switch (Command)
            {
                case LobbyCommand.Create:
                    return "create max=" + MaxPlayers.ToString(CultureInfo.InvariantCulture);
                case LobbyCommand.Join:
                    return LobbyId == null ? "join (search)" : "join id=" + LobbyId;
                default:
                    return CommandName(Command);
            }
        }

        /// <returns>正しければ null、誤りならその理由。</returns>
        private static string ValidateMaxPlayers(int maxPlayers)
        {
            if (maxPlayers < MinMaxPlayers || maxPlayers > MaxMaxPlayers)
            {
                return "must be between " + MinMaxPlayers.ToString(CultureInfo.InvariantCulture) + " and "
                    + MaxMaxPlayers.ToString(CultureInfo.InvariantCulture);
            }

            return null;
        }

        /// <returns>正しければ null、誤りならその理由。</returns>
        private static string ValidateLobbyId(string lobbyId)
        {
            if (string.IsNullOrWhiteSpace(lobbyId))
            {
                return "must not be blank";
            }

            if (lobbyId.Length > MaxLobbyIdLength)
            {
                return "must be at most " + MaxLobbyIdLength.ToString(CultureInfo.InvariantCulture) + " characters";
            }

            foreach (var c in lobbyId)
            {
                if (char.IsWhiteSpace(c) || char.IsControl(c))
                {
                    return "must not contain spaces or control characters (got '" + lobbyId + "')";
                }
            }

            return null;
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
