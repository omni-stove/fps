using System;
using System.Collections.Generic;
using System.Globalization;

namespace Shooter.Core
{
    // サーバー一覧の絞り込みと並べ替え。値を替えるたびに作り直す (With*) のは、UI が前の条件を持ったまま比べられるようにするため。
    public sealed class ServerListQuery : IEquatable<ServerListQuery>
    {
        public ServerListQuery(
            string region = null,
            string mode = null,
            string map = null,
            bool hideFull = false,
            bool hideEmpty = false,
            int? maxPingMilliseconds = null,
            ServerListSort sort = ServerListSort.Ping,
            bool reversed = false)
        {
            if (!Enum.IsDefined(typeof(ServerListSort), sort))
            {
                throw new ArgumentOutOfRangeException(nameof(sort), sort, "is not a defined ServerListSort");
            }

            if (maxPingMilliseconds.HasValue && maxPingMilliseconds.Value < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(maxPingMilliseconds), maxPingMilliseconds, "must not be negative");
            }

            Region = Blank(region);
            Mode = Blank(mode);
            Map = Blank(map);
            HideFull = hideFull;
            HideEmpty = hideEmpty;
            MaxPingMilliseconds = maxPingMilliseconds;
            Sort = sort;
            Reversed = reversed;
        }

        public static ServerListQuery Default { get; } = new ServerListQuery();

        // null はどれでもよい。
        public string Region { get; }

        public string Mode { get; }

        public string Map { get; }

        public bool HideFull { get; }

        public bool HideEmpty { get; }

        // 測れていないサーバーは上限で落とさない。ICMP を落とすだけの近いサーバーまで、遅いとみなして隠さないため。
        public int? MaxPingMilliseconds { get; }

        public ServerListSort Sort { get; }

        // 並べ替えの向きを、その項目のふつうの向き (ping は小さい順、人数は多い順、名前は辞書順) から逆にする。
        public bool Reversed { get; }

        public ServerListQuery WithRegion(string region)
        {
            return new ServerListQuery(region, Mode, Map, HideFull, HideEmpty, MaxPingMilliseconds, Sort, Reversed);
        }

        public ServerListQuery WithMode(string mode)
        {
            return new ServerListQuery(Region, mode, Map, HideFull, HideEmpty, MaxPingMilliseconds, Sort, Reversed);
        }

        public ServerListQuery WithMap(string map)
        {
            return new ServerListQuery(Region, Mode, map, HideFull, HideEmpty, MaxPingMilliseconds, Sort, Reversed);
        }

        public ServerListQuery WithHideFull(bool hideFull)
        {
            return new ServerListQuery(Region, Mode, Map, hideFull, HideEmpty, MaxPingMilliseconds, Sort, Reversed);
        }

        public ServerListQuery WithHideEmpty(bool hideEmpty)
        {
            return new ServerListQuery(Region, Mode, Map, HideFull, hideEmpty, MaxPingMilliseconds, Sort, Reversed);
        }

        public ServerListQuery WithMaxPing(int? maxPingMilliseconds)
        {
            return new ServerListQuery(Region, Mode, Map, HideFull, HideEmpty, maxPingMilliseconds, Sort, Reversed);
        }

        public ServerListQuery WithSort(ServerListSort sort, bool reversed)
        {
            return new ServerListQuery(Region, Mode, Map, HideFull, HideEmpty, MaxPingMilliseconds, sort, reversed);
        }

        // 同じ項目をもう一度選んだら向きを逆にし、別の項目ならふつうの向きから始める。表の見出しを押したときの動きに合わせる。
        public ServerListQuery ToggleSort(ServerListSort sort)
        {
            return WithSort(sort, sort == Sort && !Reversed);
        }

        public bool Matches(ServerListing listing)
        {
            if (listing == null)
            {
                throw new ArgumentNullException(nameof(listing));
            }

            if (Region != null && !string.Equals(listing.Region, Region, StringComparison.Ordinal))
            {
                return false;
            }

            if (Mode != null && !string.Equals(listing.Mode, Mode, StringComparison.Ordinal))
            {
                return false;
            }

            if (Map != null && !string.Equals(listing.Map, Map, StringComparison.Ordinal))
            {
                return false;
            }

            if (HideFull && listing.IsFull)
            {
                return false;
            }

            if (HideEmpty && listing.IsEmpty)
            {
                return false;
            }

            return !MaxPingMilliseconds.HasValue || !listing.PingMilliseconds.HasValue
                || listing.PingMilliseconds.Value <= MaxPingMilliseconds.Value;
        }

        public IReadOnlyList<ServerListing> Apply(IEnumerable<ServerListing> listings)
        {
            if (listings == null)
            {
                throw new ArgumentNullException(nameof(listings));
            }

            var shown = new List<ServerListing>();
            foreach (var listing in listings)
            {
                if (listing != null && Matches(listing))
                {
                    shown.Add(listing);
                }
            }

            shown.Sort(Compare);
            return shown;
        }

        // 値の無いもの (測れていない ping・地域を載せないサーバー) は向きによらず後ろに置く。どちらの向きでも「良い」とも「悪い」とも言えないため。
        // 同じ値の間は ping・人数・Id の順で決め、検索の返す順で行が入れ替わらないようにする。
        public int Compare(ServerListing a, ServerListing b)
        {
            if (a == null)
            {
                throw new ArgumentNullException(nameof(a));
            }

            if (b == null)
            {
                throw new ArgumentNullException(nameof(b));
            }

            var primary = ComparePrimary(a, b);
            if (primary != 0)
            {
                return primary;
            }

            if (Sort != ServerListSort.Ping)
            {
                var ping = CompareMissingLast(a.PingMilliseconds, b.PingMilliseconds, false);
                if (ping != 0)
                {
                    return ping;
                }
            }

            if (Sort != ServerListSort.Players)
            {
                var players = b.Players.CompareTo(a.Players);
                if (players != 0)
                {
                    return players;
                }
            }

            var id = string.CompareOrdinal(a.Id, b.Id);
            return id != 0 ? id : string.CompareOrdinal(a.Endpoint, b.Endpoint);
        }

        public static IReadOnlyList<string> Regions(IEnumerable<ServerListing> listings)
        {
            return Distinct(listings, listing => listing.Region);
        }

        public static IReadOnlyList<string> Modes(IEnumerable<ServerListing> listings)
        {
            return Distinct(listings, listing => listing.Mode);
        }

        public static IReadOnlyList<string> Maps(IEnumerable<ServerListing> listings)
        {
            return Distinct(listings, listing => listing.Map);
        }

        public bool Equals(ServerListQuery other)
        {
            return other != null
                && string.Equals(Region, other.Region, StringComparison.Ordinal)
                && string.Equals(Mode, other.Mode, StringComparison.Ordinal)
                && string.Equals(Map, other.Map, StringComparison.Ordinal)
                && HideFull == other.HideFull
                && HideEmpty == other.HideEmpty
                && MaxPingMilliseconds == other.MaxPingMilliseconds
                && Sort == other.Sort
                && Reversed == other.Reversed;
        }

        public override bool Equals(object obj)
        {
            return obj is ServerListQuery other && Equals(other);
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(
                Region == null ? 0 : StringComparer.Ordinal.GetHashCode(Region),
                Mode == null ? 0 : StringComparer.Ordinal.GetHashCode(Mode),
                Map == null ? 0 : StringComparer.Ordinal.GetHashCode(Map),
                HideFull,
                HideEmpty,
                MaxPingMilliseconds,
                Sort,
                Reversed);
        }

        public override string ToString()
        {
            return "region=" + (Region ?? "any") + " mode=" + (Mode ?? "any") + " map=" + (Map ?? "any")
                + (HideFull ? " hide-full" : "") + (HideEmpty ? " hide-empty" : "")
                + (MaxPingMilliseconds.HasValue ? " max-ping=" + MaxPingMilliseconds.Value.ToString(CultureInfo.InvariantCulture) + "ms" : "")
                + " sort=" + Sort + (Reversed ? " reversed" : "");
        }

        private int ComparePrimary(ServerListing a, ServerListing b)
        {
            switch (Sort)
            {
                case ServerListSort.Ping:
                    return CompareMissingLast(a.PingMilliseconds, b.PingMilliseconds, Reversed);
                case ServerListSort.Players:
                    var players = b.Players.CompareTo(a.Players);
                    return Reversed ? -players : players;
                case ServerListSort.Region:
                    return CompareTextMissingLast(a.Region, b.Region, Reversed);
                case ServerListSort.Mode:
                    return CompareTextMissingLast(a.Mode, b.Mode, Reversed);
                default:
                    return CompareTextMissingLast(a.Map, b.Map, Reversed);
            }
        }

        private static int CompareMissingLast(int? a, int? b, bool reversed)
        {
            if (!a.HasValue || !b.HasValue)
            {
                return a.HasValue == b.HasValue ? 0 : a.HasValue ? -1 : 1;
            }

            var order = a.Value.CompareTo(b.Value);
            return reversed ? -order : order;
        }

        private static int CompareTextMissingLast(string a, string b, bool reversed)
        {
            if (a == null || b == null)
            {
                return (a == null) == (b == null) ? 0 : a != null ? -1 : 1;
            }

            var order = string.CompareOrdinal(a, b);
            return reversed ? -order : order;
        }

        private static List<string> Distinct(IEnumerable<ServerListing> listings, Func<ServerListing, string> pick)
        {
            if (listings == null)
            {
                throw new ArgumentNullException(nameof(listings));
            }

            var seen = new HashSet<string>(StringComparer.Ordinal);
            var values = new List<string>();
            foreach (var listing in listings)
            {
                var value = listing == null ? null : pick(listing);
                if (value != null && seen.Add(value))
                {
                    values.Add(value);
                }
            }

            values.Sort(StringComparer.Ordinal);
            return values;
        }

        private static string Blank(string text)
        {
            return string.IsNullOrWhiteSpace(text) ? null : text;
        }
    }
}
