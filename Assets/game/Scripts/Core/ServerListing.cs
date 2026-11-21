using System;
using System.Globalization;

namespace Shooter.Core
{
    // サーバー一覧の 1 行。地域・モード・地図はサーバーが載せた名前 (キー) のままで持ち、表示の文字にするのは UI。
    public sealed class ServerListing : IEquatable<ServerListing>
    {
        public ServerListing(
            string id, string endpoint, string region, string mode, string map, int players, int capacity, int? pingMilliseconds)
        {
            if (string.IsNullOrWhiteSpace(id))
            {
                throw new ArgumentException("must not be null or blank", nameof(id));
            }

            if (!NetLaunchOptions.TryParseEndpoint(endpoint, out var address, out var port))
            {
                throw new ArgumentException("'" + endpoint + "' is not an IPv4 address:port", nameof(endpoint));
            }

            if (capacity < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(capacity), capacity, "must be at least 1");
            }

            if (players < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(players), players, "must not be negative");
            }

            if (pingMilliseconds.HasValue && pingMilliseconds.Value < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(pingMilliseconds), pingMilliseconds, "must not be negative");
            }

            Id = id;
            Endpoint = endpoint;
            Address = address;
            Port = port;
            Region = Blank(region);
            Mode = Blank(mode);
            Map = Blank(map);
            Players = players;
            Capacity = capacity;
            PingMilliseconds = pingMilliseconds;
        }

        // ロビーの Id。並べ替えの最後の決め手と、選んだ行を入るときに引く鍵に使う。
        public string Id { get; }

        public string Endpoint { get; }

        public string Address { get; }

        public ushort Port { get; }

        public string Region { get; }

        public string Mode { get; }

        public string Map { get; }

        public int Players { get; }

        public int Capacity { get; }

        // 測れていない (測っている途中・返らない) ときは null。
        public int? PingMilliseconds { get; }

        public int FreeSlots => Math.Max(0, Capacity - Players);

        public bool IsFull => FreeSlots == 0;

        public bool IsEmpty => Players == 0;

        // 入れないサーバー (非公開・版違い・宛先や人数の無い壊れたもの) は一覧に出さず、満員のものは出して入れないことを見せる。
        public static bool TryFrom(LobbyInfo lobby, string version, out ServerListing listing)
        {
            if (lobby == null)
            {
                throw new ArgumentNullException(nameof(lobby));
            }

            listing = null;
            if (!LobbySelector.IsListable(lobby, version))
            {
                return false;
            }

            lobby.TryGetText(ServerLobby.ServerKey, out var endpoint);
            lobby.TryGetNumber(ServerLobby.PlayersKey, out var players);
            var capacity = ServerLobby.PlayerSlots(lobby);
            if (capacity < 1)
            {
                return false;
            }

            listing = new ServerListing(
                lobby.Id,
                endpoint,
                lobby.Region,
                lobby.Mode,
                lobby.Map,
                (int)Math.Min(players, int.MaxValue),
                capacity,
                null);
            return true;
        }

        public bool CanTake(int partySize)
        {
            if (partySize < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(partySize), partySize, "must be at least 1");
            }

            return FreeSlots >= partySize;
        }

        public ServerListing WithPing(int? pingMilliseconds)
        {
            return new ServerListing(Id, Endpoint, Region, Mode, Map, Players, Capacity, pingMilliseconds);
        }

        public bool Equals(ServerListing other)
        {
            return other != null
                && string.Equals(Id, other.Id, StringComparison.Ordinal)
                && string.Equals(Endpoint, other.Endpoint, StringComparison.Ordinal)
                && string.Equals(Region, other.Region, StringComparison.Ordinal)
                && string.Equals(Mode, other.Mode, StringComparison.Ordinal)
                && string.Equals(Map, other.Map, StringComparison.Ordinal)
                && Players == other.Players
                && Capacity == other.Capacity
                && PingMilliseconds == other.PingMilliseconds;
        }

        public override bool Equals(object obj)
        {
            return obj is ServerListing other && Equals(other);
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(StringComparer.Ordinal.GetHashCode(Id), StringComparer.Ordinal.GetHashCode(Endpoint), Players, Capacity, PingMilliseconds);
        }

        public override string ToString()
        {
            return "id=" + Id + " server=" + Endpoint
                + " region=" + (Region ?? "none") + " mode=" + (Mode ?? "none") + " map=" + (Map ?? "none")
                + " players=" + Players.ToString(CultureInfo.InvariantCulture) + "/" + Capacity.ToString(CultureInfo.InvariantCulture)
                + " ping=" + (PingMilliseconds.HasValue ? PingMilliseconds.Value.ToString(CultureInfo.InvariantCulture) + "ms" : "none");
        }

        private static string Blank(string text)
        {
            return string.IsNullOrWhiteSpace(text) ? null : text;
        }
    }
}
