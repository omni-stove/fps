using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Threading;
using Shooter.Core;
using Shooter.Protocol;

namespace Shooter.Server
{
    // 標準入力の読み取りは行が来るまで止まるので別のスレッドで行い、行の実行は試合と同じ Tick の中で行う。
    // 試合・BAN の一覧・ログをループのスレッドの外から触らないため。
    internal sealed class ServerAdminConsole : IServerModule
    {
        public const string PlayersCommand = "players";
        public const string KickCommand = "kick";
        public const string BanCommand = "ban";
        public const string UnbanCommand = "unban";
        public const string BansCommand = "bans";
        public const string HelpCommand = "help";

        private static readonly char[] _space = { ' ', '\t' };

        private readonly TextReader _input;
        private readonly ServerBanList _bans;
        private readonly Func<DateTime> _utcNow;
        private readonly ConcurrentQueue<string> _lines = new ConcurrentQueue<string>();
        private readonly List<ServerPlayer> _matched = new List<ServerPlayer>();
        private ServerContext _context;
        private volatile bool _inputEnded;
        private bool _reportedInputEnd;

        public ServerAdminConsole(TextReader input, ServerBanList bans, Func<DateTime> utcNow)
        {
            _input = input;
            _bans = bans ?? throw new ArgumentNullException(nameof(bans));
            _utcNow = utcNow ?? throw new ArgumentNullException(nameof(utcNow));
        }

        public string Name => "admin-console";

        public void Start(ServerContext context)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
            if (_input == null)
            {
                return;
            }

            context.Log.WriteLine(ServerAdminLog.Ready());

            // 止めるときに ReadLine を抜けさせる手段が無いので、背景のスレッドにしてプロセスの終わりを待たせない
            var reader = new Thread(ReadInput) { IsBackground = true, Name = "admin-console" };
            reader.Start();
        }

        public void Tick(long tick, float deltaSeconds)
        {
            while (_lines.TryDequeue(out var line))
            {
                Execute(line);
            }

            if (_inputEnded && !_reportedInputEnd && _lines.IsEmpty)
            {
                _reportedInputEnd = true;
                _context.Log.WriteLine(ServerAdminLog.InputEnded());
            }
        }

        public void OnConnected(ConnectionId connection)
        {
        }

        public void OnDisconnected(ConnectionId connection)
        {
        }

        public void OnMessage(ConnectionId connection, IMessage message)
        {
        }

        public void Stop()
        {
        }

        public void Execute(string line)
        {
            if (_context == null)
            {
                throw new InvalidOperationException("the admin console runs commands only after it starts");
            }

            Split(line, out var command, out var target, out var rest);
            if (command.Length == 0)
            {
                return;
            }

            if (Is(command, PlayersCommand))
            {
                ListPlayers();
            }
            else if (Is(command, KickCommand))
            {
                Kick(target, rest);
            }
            else if (Is(command, BanCommand))
            {
                Ban(target, rest);
            }
            else if (Is(command, UnbanCommand))
            {
                Unban(target);
            }
            else if (Is(command, BansCommand))
            {
                ListBans();
            }
            else if (Is(command, HelpCommand))
            {
                foreach (var usage in ServerAdminLog.Usage)
                {
                    _context.Log.WriteLine(usage);
                }
            }
            else
            {
                _context.Log.WriteLine(ServerAdminLog.UnknownCommand(command));
            }
        }

        private static bool Is(string command, string name)
        {
            return string.Equals(command, name, StringComparison.OrdinalIgnoreCase);
        }

        private void ReadInput()
        {
            try
            {
                string line;
                while ((line = _input.ReadLine()) != null)
                {
                    _lines.Enqueue(line);
                }
            }
            catch (Exception e) when (e is IOException || e is ObjectDisposedException)
            {
                // 読めなくなった入力はこの先も行を返さないので、閉じたものとして下で終わりを知らせるだけにする
            }
            finally
            {
                _inputEnded = true;
            }
        }

        private static void Split(string line, out string command, out string target, out string rest)
        {
            var text = (line ?? "").Trim();
            command = NextWord(ref text);
            target = NextWord(ref text);
            rest = text.Length == 0 ? null : text;
        }

        private static string NextWord(ref string text)
        {
            var end = text.IndexOfAny(_space);
            var word = end < 0 ? text : text.Substring(0, end);
            text = end < 0 ? "" : text.Substring(end).Trim();
            return word;
        }

        private void ListPlayers()
        {
            var count = 0;
            foreach (var player in _context.Match.Players)
            {
                if (player.IsBot)
                {
                    continue;
                }

                count++;
                _context.Log.WriteLine(ServerAdminLog.Player(player, AddressOf(player)));
            }

            _context.Log.WriteLine(count == 0 ? ServerAdminLog.NoPlayers() : ServerAdminLog.Players(count));
        }

        private void Kick(string target, string reason)
        {
            if (target.Length == 0)
            {
                _context.Log.WriteLine(ServerAdminLog.NeedsTarget(KickCommand, "a player number (see " + PlayersCommand + ")"));
                return;
            }

            if (!TryGetHuman(target, out var player))
            {
                return;
            }

            _context.Log.WriteLine(ServerAdminLog.Kicking(player.PlayerId));
            _context.Control.Kick(
                player.Connection, DisconnectNotice.RejectedReason(DisconnectNotice.KickedDetail), ServerAdminLog.KickedLogText(reason));
        }

        private void Ban(string target, string reason)
        {
            if (target.Length == 0)
            {
                _context.Log.WriteLine(ServerAdminLog.NeedsTarget(BanCommand, "a player number, a product user id or an address"));
                return;
            }

            if (reason != null && reason.Length > ServerBanList.MaxReasonLength)
            {
                _context.Log.WriteLine(ServerAdminLog.ReasonTooLong());
                return;
            }

            if (!TryResolveBanTarget(target, out var kind, out var value))
            {
                return;
            }

            var entry = new ServerBanEntry(kind, value, reason, _utcNow());
            var added = _bans.Add(entry);
            if (!added)
            {
                entry = _bans.Find(kind, value);
            }

            var disconnected = KickBanned(entry);
            _context.Log.WriteLine(added ? ServerAdminLog.Banned(entry, disconnected) : ServerAdminLog.AlreadyBanned(entry, disconnected));
            if (added)
            {
                Save();
            }
        }

        private void Unban(string target)
        {
            if (!ServerBanEntry.TryParseTarget(target, out var kind, out var value))
            {
                _context.Log.WriteLine(target.Length == 0
                    ? ServerAdminLog.NeedsTarget(UnbanCommand, "a product user id or an address (see " + BansCommand + ")")
                    : ServerAdminLog.UnknownTarget(target));
                return;
            }

            var removed = _bans.Remove(kind, value);
            if (removed == null)
            {
                _context.Log.WriteLine(ServerAdminLog.NotBanned(kind, value));
                return;
            }

            _context.Log.WriteLine(ServerAdminLog.Unbanned(removed));
            Save();
        }

        private void ListBans()
        {
            if (_bans.Entries.Count == 0)
            {
                _context.Log.WriteLine(ServerAdminLog.NoBans());
                return;
            }

            foreach (var entry in _bans.Entries)
            {
                _context.Log.WriteLine(ServerAdminLog.Ban(entry));
            }
        }

        private void Save()
        {
            _context.Log.WriteLine(_bans.TrySave(out var error) ? ServerAdminLog.Saved(_bans) : ServerAdminLog.SaveFailed(error));
        }

        // UDP では名乗った ProductUserId を確かめられず、名乗らなければ逃れられるので、通信路が確かめた Id が無い人は接続元で BAN する
        private bool TryResolveBanTarget(string target, out ServerBanKind kind, out string value)
        {
            if (IsPlayerNumber(target))
            {
                kind = default;
                value = null;
                if (!TryGetHuman(target, out var player))
                {
                    return false;
                }

                if (player.ProductUserId != null)
                {
                    kind = ServerBanKind.ProductUserId;
                    value = player.ProductUserId;
                    return true;
                }

                var address = AddressOf(player);
                if (address != null && ServerBanEntry.TryNormalize(ServerBanKind.Address, address, out value))
                {
                    kind = ServerBanKind.Address;
                    return true;
                }

                _context.Log.WriteLine(ServerAdminLog.NothingToBan(player.PlayerId));
                return false;
            }

            if (ServerBanEntry.TryParseTarget(target, out kind, out value))
            {
                return true;
            }

            _context.Log.WriteLine(ServerAdminLog.UnknownTarget(target));
            return false;
        }

        // 32 桁の数字だけの ProductUserId を番号と読まないよう、int に収まる長さだけを番号とみなす
        private static bool IsPlayerNumber(string text)
        {
            return text.Length < NetLaunchOptions.ProductUserIdLength
                && int.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out _);
        }

        private bool TryGetHuman(string text, out ServerPlayer player)
        {
            player = null;
            if (!IsPlayerNumber(text)
                || !int.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out var playerId)
                || !_context.Match.TryGetPlayer(playerId, out player))
            {
                _context.Log.WriteLine(ServerAdminLog.NoSuchPlayer(text));
                return false;
            }

            if (player.IsBot)
            {
                _context.Log.WriteLine(ServerAdminLog.BotNotRemoved(player.PlayerId));
                player = null;
                return false;
            }

            return true;
        }

        private int KickBanned(ServerBanEntry entry)
        {
            _matched.Clear();
            foreach (var player in _context.Match.Players)
            {
                if (!player.IsBot && entry.Matches(player.ProductUserId, AddressOf(player)))
                {
                    _matched.Add(player);
                }
            }

            foreach (var player in _matched)
            {
                _context.Control.Kick(
                    player.Connection, DisconnectNotice.RejectedReason(DisconnectNotice.BannedDetail), ServerAdminLog.BannedLogText(entry));
            }

            return _matched.Count;
        }

        private string AddressOf(ServerPlayer player)
        {
            return _context.Addresses != null && _context.Addresses.TryGetRemoteAddress(player.Connection, out var address) ? address : null;
        }
    }
}
