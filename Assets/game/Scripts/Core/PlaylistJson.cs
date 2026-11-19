using System;
using System.Collections.Generic;

namespace Shooter.Core
{
    public static class PlaylistJson
    {
        public const string ResourcePath = "Tuning/playlists";

        public const string PlaylistsItem = "playlists";

        public const string IdItem = "id";
        public const string OrderItem = "order";
        public const string NextDelaySecondsItem = "nextDelaySeconds";
        public const string EntriesItem = "entries";

        public const string SceneItem = "scene";
        public const string RulesItem = "rules";
        public const string MutatorsItem = "mutators";

        private const string _kind = "playlist JSON";

        public static IReadOnlyList<Playlist> Empty { get; } = Array.AsReadOnly(Array.Empty<Playlist>());

        public static IReadOnlyList<string> ItemNames { get; } = new[] { PlaylistsItem };

        public static IReadOnlyList<string> PlaylistItemNames { get; } = new[] { IdItem, OrderItem, NextDelaySecondsItem, EntriesItem };

        public static IReadOnlyList<string> EntryItemNames { get; } = new[] { SceneItem, RulesItem, MutatorsItem };

        public static IReadOnlyList<Playlist> Parse(string text, string sourceName)
        {
            return SpecJsonReader.Read(text, sourceName, _kind, ItemNames, fields =>
            {
                var playlists = SpecJsonReader.ReadList(fields, PlaylistsItem, PlaylistItemNames, ReadPlaylist);
                if (playlists == null)
                {
                    return Empty;
                }

                var ids = new HashSet<string>(StringComparer.Ordinal);
                for (var i = 0; i < playlists.Count; i++)
                {
                    if (!ids.Add(playlists[i].Id))
                    {
                        var item = fields.ElementPath(PlaylistsItem, i) + "." + IdItem;
                        throw fields.Rejected(item, $"id '{playlists[i].Id}' is used by more than one playlist");
                    }
                }

                return (IReadOnlyList<Playlist>)playlists.AsReadOnly();
            });
        }

        private static Playlist ReadPlaylist(ServerDataJson.Fields fields)
        {
            var id = SpecJsonReader.RequireText(fields, IdItem);
            var order = fields.ReadEnum(OrderItem, PlaylistOrder.Sequential);
            var delay = fields.ReadFloat(NextDelaySecondsItem, Playlist.DefaultNextDelaySeconds);

            // 書き忘れた一覧を「項目が無い」として既定の一覧で埋めると、空のプレイリストを黙って作るので、ここは必ず書かせる。
            fields.Require(EntriesItem);
            var entries = SpecJsonReader.ReadList(fields, EntriesItem, EntryItemNames, ReadEntry);
            return new Playlist(id, order, entries, delay);
        }

        private static PlaylistEntry ReadEntry(ServerDataJson.Fields fields)
        {
            var scene = SpecJsonReader.RequireText(fields, SceneItem);
            var rules = fields.ReadString(RulesItem, string.Empty);
            var mutators = SpecJsonReader.ReadStrings(fields, MutatorsItem);
            return new PlaylistEntry(scene, rules, mutators);
        }
    }
}
