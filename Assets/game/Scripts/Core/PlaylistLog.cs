using System;
using System.Collections.Generic;
using System.Globalization;

namespace Shooter.Core
{
    public static class PlaylistLog
    {
        public const string LogTag = "[Playlist]";

        public static string Started(Playlist playlist)
        {
            Require(playlist);
            return $"{LogTag} started id={playlist.Id} order={playlist.Order} entries={Count(playlist.Entries.Count)}";
        }

        public static string Loading(PlaylistCursor cursor)
        {
            if (cursor == null)
            {
                throw new ArgumentNullException(nameof(cursor));
            }

            var entry = cursor.Current;
            var rules = entry.HasRules ? entry.Rules : "(scene)";
            var mutators = entry.MutatorIds.Count == 0 ? "none" : string.Join(", ", entry.MutatorIds);
            return $"{LogTag} id={cursor.Playlist.Id} {Where(cursor.Playlist, cursor.Position)} round={Count(cursor.Round + 1)}: "
                + $"scene={entry.Scene} rules={rules} mutators={mutators}";
        }

        public static string MatchFinished(Playlist playlist, float delaySeconds)
        {
            Require(playlist);
            return $"{LogTag} id={playlist.Id} match finished; loading the next entry in {delaySeconds.ToString("0.##", CultureInfo.InvariantCulture)}s";
        }

        public static string UnknownMutator(Playlist playlist, int position, string id, IReadOnlyList<GameRulesMutator> available)
        {
            Require(playlist);
            if (available == null)
            {
                throw new ArgumentNullException(nameof(available));
            }

            var known = new List<string>(available.Count);
            foreach (var mutator in available)
            {
                known.Add(mutator.Id);
            }

            var list = known.Count == 0 ? "(none)" : string.Join(", ", known);
            return $"{LogTag} id={playlist.Id} {Where(playlist, position)} skipped mutator id={id}: no mutator has this id; known ids are {list}";
        }

        public static string SceneNotLoaded(Playlist playlist, int position, string scene)
        {
            Require(playlist);
            return $"{LogTag} id={playlist.Id} {Where(playlist, position)} skipped: scene '{scene}' cannot be loaded; add it to the Build Settings";
        }

        public static string NoSceneLoaded(Playlist playlist)
        {
            Require(playlist);
            return $"{LogTag} id={playlist.Id} stopped: no entry has a scene that can be loaded";
        }

        public static string Stopped(Playlist playlist)
        {
            Require(playlist);
            return $"{LogTag} id={playlist.Id} stopped";
        }

        public static string UnknownRules(Playlist playlist, IReadOnlyCollection<string> missing, IEnumerable<string> known)
        {
            Require(playlist);
            if (missing == null)
            {
                throw new ArgumentNullException(nameof(missing));
            }

            if (known == null)
            {
                throw new ArgumentNullException(nameof(known));
            }

            var knownList = new List<string>(known);
            knownList.Sort(StringComparer.Ordinal);
            var knownText = knownList.Count == 0 ? "(none)" : string.Join(", ", knownList);
            return $"playlist '{playlist.Id}' names rules that are not in the rules table: {string.Join(", ", missing)}; known rules are {knownText}";
        }

        private static string Where(Playlist playlist, int position)
        {
            return $"entry {Count(position + 1)}/{Count(playlist.Entries.Count)}";
        }

        private static string Count(int value)
        {
            return value.ToString(CultureInfo.InvariantCulture);
        }

        private static void Require(Playlist playlist)
        {
            if (playlist == null)
            {
                throw new ArgumentNullException(nameof(playlist));
            }
        }
    }
}
