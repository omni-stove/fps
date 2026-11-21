using System;
using Shooter.Core;

namespace Shooter.Runtime
{
    public static class ServerBrowserText
    {
        public const string TitleKey = "serverBrowser.title";
        public const string RefreshKey = "serverBrowser.refresh";
        public const string BackKey = "serverBrowser.back";
        public const string LoadingKey = "serverBrowser.loading";
        public const string UnavailableKey = "serverBrowser.unavailable";
        public const string EmptyKey = "serverBrowser.empty";
        public const string NoMatchKey = "serverBrowser.noMatch";
        public const string JoiningKey = "serverBrowser.joining";
        public const string JoinedKey = "serverBrowser.joined";
        public const string NotFoundKey = "serverBrowser.notFound";
        public const string FullKey = "serverBrowser.full";
        public const string FailedKey = "serverBrowser.failed";
        public const string AnyKey = "serverBrowser.any";
        public const string UnknownKey = "serverBrowser.unknown";
        public const string RegionFilterKey = "serverBrowser.filter.region";
        public const string ModeFilterKey = "serverBrowser.filter.mode";
        public const string MapFilterKey = "serverBrowser.filter.map";
        public const string FullShownKey = "serverBrowser.filter.fullShown";
        public const string FullHiddenKey = "serverBrowser.filter.fullHidden";
        public const string EmptyShownKey = "serverBrowser.filter.emptyShown";
        public const string EmptyHiddenKey = "serverBrowser.filter.emptyHidden";
        public const string SortKey = "serverBrowser.sort";
        public const string SortReversedKey = "serverBrowser.sortReversed";
        public const string PlayersKey = "serverBrowser.players";
        public const string PingKey = "serverBrowser.ping";
        public const string ModeNameKeyPrefix = "serverBrowser.mode.";
        public const string ColumnKeyPrefix = "serverBrowser.column.";

        public static LocalizedText Title => new LocalizedText(TitleKey);

        public static LocalizedText Refresh => new LocalizedText(RefreshKey);

        public static LocalizedText Back => new LocalizedText(BackKey);

        // 一覧の代わりに出す一文。行を出すときは空を返す。読み直しの間 (Loading で行がある) は前の行を出したままにする。
        public static LocalizedText Notice(ServerBrowserStatus? status, bool hasControl, int shownCount)
        {
            if (!hasControl)
            {
                return new LocalizedText(UnavailableKey);
            }

            if (!status.HasValue)
            {
                return new LocalizedText(LoadingKey);
            }

            var value = status.Value;
            switch (value.Stage)
            {
                case ServerBrowserStage.Loading:
                    return value.Servers.Count == 0 ? new LocalizedText(LoadingKey) : default;
                case ServerBrowserStage.Unavailable:
                    return new LocalizedText(UnavailableKey);
                default:
                    if (value.Servers.Count == 0)
                    {
                        return new LocalizedText(EmptyKey);
                    }

                    return shownCount == 0 ? new LocalizedText(NoMatchKey) : default;
            }
        }

        public static LocalizedText Outcome(ServerJoinOutcome outcome)
        {
            switch (outcome)
            {
                case ServerJoinOutcome.Joined:
                    return new LocalizedText(JoinedKey);
                case ServerJoinOutcome.NotFound:
                    return new LocalizedText(NotFoundKey);
                case ServerJoinOutcome.Full:
                    return new LocalizedText(FullKey);
                default:
                    return new LocalizedText(FailedKey);
            }
        }

        // 一覧に名前の無い地域・モード・地図は、サーバーが載せた名前をそのまま出す。載せていなければ「不明」。
        public static LocalizedText Region(string region)
        {
            return region == null ? new LocalizedText(UnknownKey) : LocalizedText.Literal(SettingsView.FormatRegion(region));
        }

        public static LocalizedText Mode(string mode)
        {
            if (mode == null)
            {
                return new LocalizedText(UnknownKey);
            }

            return Localizer.TryGet(ModeNameKeyPrefix + mode, out var name) ? LocalizedText.Literal(name) : LocalizedText.Literal(mode);
        }

        public static LocalizedText Map(string map)
        {
            return map == null ? new LocalizedText(UnknownKey) : LocalizedText.Literal(MapVoteView.MapName(map));
        }

        public static LocalizedText Players(ServerListing listing)
        {
            if (listing == null)
            {
                throw new ArgumentNullException(nameof(listing));
            }

            return new LocalizedText(PlayersKey, listing.Players, listing.Capacity);
        }

        public static LocalizedText Ping(ServerListing listing)
        {
            if (listing == null)
            {
                throw new ArgumentNullException(nameof(listing));
            }

            return listing.PingMilliseconds.HasValue ? new LocalizedText(PingKey, listing.PingMilliseconds.Value) : new LocalizedText(UnknownKey);
        }

        public static LocalizedText RegionFilter(string region)
        {
            return new LocalizedText(RegionFilterKey, region == null ? new LocalizedText(AnyKey) : Region(region));
        }

        public static LocalizedText ModeFilter(string mode)
        {
            return new LocalizedText(ModeFilterKey, mode == null ? new LocalizedText(AnyKey) : Mode(mode));
        }

        public static LocalizedText MapFilter(string map)
        {
            return new LocalizedText(MapFilterKey, map == null ? new LocalizedText(AnyKey) : Map(map));
        }

        public static LocalizedText FullFilter(bool hidden)
        {
            return new LocalizedText(hidden ? FullHiddenKey : FullShownKey);
        }

        public static LocalizedText EmptyFilter(bool hidden)
        {
            return new LocalizedText(hidden ? EmptyHiddenKey : EmptyShownKey);
        }

        public static string ColumnKey(ServerListSort sort)
        {
            switch (sort)
            {
                case ServerListSort.Ping:
                    return ColumnKeyPrefix + "ping";
                case ServerListSort.Players:
                    return ColumnKeyPrefix + "players";
                case ServerListSort.Region:
                    return ColumnKeyPrefix + "region";
                case ServerListSort.Mode:
                    return ColumnKeyPrefix + "mode";
                case ServerListSort.Map:
                    return ColumnKeyPrefix + "map";
                default:
                    throw new ArgumentOutOfRangeException(nameof(sort), sort, "unknown server list sort");
            }
        }

        // 並べ替えている見出しには印を付け、逆向きならそれと分かる印にする。
        public static LocalizedText Column(ServerListSort column, ServerListQuery query)
        {
            if (query == null)
            {
                throw new ArgumentNullException(nameof(query));
            }

            var name = new LocalizedText(ColumnKey(column));
            if (query.Sort != column)
            {
                return name;
            }

            return new LocalizedText(query.Reversed ? SortReversedKey : SortKey, name);
        }
    }
}
