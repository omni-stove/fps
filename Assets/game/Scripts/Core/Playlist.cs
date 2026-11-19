using System;
using System.Collections.Generic;

namespace Shooter.Core
{
    public sealed class Playlist
    {
        public const float DefaultNextDelaySeconds = 10f;

        // 引数名は playlists.json の項目名と同じにする。SpecJsonReader が ArgumentException の ParamName で断った項目を示すため。
        public Playlist(string id, PlaylistOrder order, IEnumerable<PlaylistEntry> entries, float nextDelaySeconds = DefaultNextDelaySeconds)
        {
            if (string.IsNullOrWhiteSpace(id))
            {
                throw new ArgumentException("a playlist needs an id", nameof(id));
            }

            if (!Enum.IsDefined(typeof(PlaylistOrder), order))
            {
                throw new ArgumentOutOfRangeException(nameof(order), order, "unknown playlist order");
            }

            if (entries == null)
            {
                throw new ArgumentNullException(nameof(entries));
            }

            if (float.IsNaN(nextDelaySeconds) || float.IsInfinity(nextDelaySeconds) || nextDelaySeconds < 0f)
            {
                throw new ArgumentOutOfRangeException(nameof(nextDelaySeconds), nextDelaySeconds, "nextDelaySeconds must be a non-negative finite number");
            }

            var list = new List<PlaylistEntry>();
            foreach (var entry in entries)
            {
                list.Add(entry ?? throw new ArgumentException("playlist entries must not contain null", nameof(entries)));
            }

            if (list.Count == 0)
            {
                throw new ArgumentException($"playlist '{id}' needs at least one entry", nameof(entries));
            }

            Id = id;
            Order = order;
            Entries = list.AsReadOnly();
            NextDelaySeconds = nextDelaySeconds;
        }

        public string Id { get; }

        public PlaylistOrder Order { get; }

        public IReadOnlyList<PlaylistEntry> Entries { get; }

        public float NextDelaySeconds { get; }

        public static Playlist Find(IEnumerable<Playlist> playlists, string id)
        {
            if (playlists == null)
            {
                throw new ArgumentNullException(nameof(playlists));
            }

            foreach (var playlist in playlists)
            {
                if (playlist != null && string.Equals(playlist.Id, id, StringComparison.Ordinal))
                {
                    return playlist;
                }
            }

            return null;
        }
    }
}
