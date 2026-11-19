using System;
using System.Collections.Generic;
using System.Globalization;
using Shooter.Core;

namespace Shooter.Server
{
    // 起動を断る理由と試合ごとの行を同じ書き出し (playlist <id> entry <n>/<数>) にそろえ、どの項目の話かをログで追えるようにする。
    // 断る理由に [Server] を付けないのは、ServerProgram がほかの読み込みの失敗と同じく付けて出すため
    internal static class ServerPlaylistLog
    {
        public static string Loaded(ServerPlaylist playlist)
        {
            ArgumentNullException.ThrowIfNull(playlist);
            return ServerProgram.LogTag + " playlist " + playlist.Playlist.Id
                + " order=" + playlist.Playlist.Order
                + " entries=" + Count(playlist.Entries.Count)
                + " maps=" + string.Join(", ", playlist.MapNames)
                + " from=" + playlist.Path
                + " mutators=" + (playlist.MutatorsPath ?? "none");
        }

        public static string Entry(ServerPlaylist playlist, ServerPlaylistEntry entry, int round)
        {
            ArgumentNullException.ThrowIfNull(playlist);
            ArgumentNullException.ThrowIfNull(entry);
            var mutators = entry.MutatorIds.Count == 0 ? "none" : string.Join(", ", entry.MutatorIds);
            return ServerProgram.LogTag + " " + Where(playlist.Playlist, entry.Position) + " round=" + Count(round + 1)
                + ": map=" + entry.Map + " rules=" + entry.RulesName + " objective=" + entry.Rules.Objective + " mutators=" + mutators;
        }

        public static string MapVotedOver(ServerPlaylist playlist, ServerPlaylistEntry entry, string votedMap)
        {
            ArgumentNullException.ThrowIfNull(playlist);
            ArgumentNullException.ThrowIfNull(entry);
            return ServerProgram.LogTag + " " + Where(playlist.Playlist, entry.Position) + ": the map vote chose " + votedMap + " over " + entry.Map;
        }

        public static string RulesRefused(ServerPlaylist playlist, ServerPlaylistEntry entry, string problem)
        {
            ArgumentNullException.ThrowIfNull(playlist);
            ArgumentNullException.ThrowIfNull(entry);
            return ServerProgram.LogTag + " " + Where(playlist.Playlist, entry.Position) + ": the next match keeps the rules of the last one: " + problem;
        }

        public static string CannotUseRules(Playlist playlist, int position, string rulesName, string problem)
        {
            return Where(playlist, position) + " cannot use rules " + rulesName + ": " + problem;
        }

        public static string NotFound(string id, IReadOnlyList<string> looked, IReadOnlyCollection<string> known)
        {
            ArgumentNullException.ThrowIfNull(looked);
            ArgumentNullException.ThrowIfNull(known);
            var list = new List<string>(known);
            list.Sort(StringComparer.Ordinal);
            return "playlist " + id + " not found in " + string.Join(" or ", looked)
                + "; known ids are " + (list.Count == 0 ? "(none)" : string.Join(", ", list));
        }

        public static string CannotRead(string path, string problem)
        {
            return "cannot read playlists " + path + ": " + problem;
        }

        public static string DuplicateId(string id, string firstPath, string secondPath)
        {
            return "playlist id '" + id + "' is used in both " + firstPath + " and " + secondPath;
        }

        public static string CannotReadMutators(string path, string problem)
        {
            return "cannot read mutators " + path + ": " + problem;
        }

        public static string NotAMap(Playlist playlist, int position, string scene)
        {
            return Where(playlist, position) + ": scene '" + scene + "' is not a map name of letters, digits, '_' or '-'";
        }

        public static string RulesNotLoaded(Playlist playlist, int position, string problem)
        {
            return Where(playlist, position) + ": " + problem;
        }

        public static string UnknownMutator(Playlist playlist, int position, string id, IReadOnlyList<GameRulesMutator> available, string mutatorsPath)
        {
            ArgumentNullException.ThrowIfNull(available);
            var known = new List<string>(available.Count);
            foreach (var mutator in available)
            {
                known.Add(mutator.Id);
            }

            return Where(playlist, position) + ": no mutator has the id '" + id + "' in "
                + (mutatorsPath ?? ServerMapData.DataDirectoryName + "/" + ServerRulesData.TuningDirectoryName + "/" + ServerPlaylist.MutatorsTuningFileName + " (not found)")
                + "; known ids are " + (known.Count == 0 ? "(none)" : string.Join(", ", known));
        }

        public static string CannotApplyMutators(Playlist playlist, int position, string rulesName, string problem)
        {
            return Where(playlist, position) + ": cannot put the mutators on rules " + rulesName + ": " + problem;
        }

        public static string MapOutsideTheMatch(Playlist playlist, int position, string map)
        {
            return Where(playlist, position) + ": map " + map + " is not one of the maps of the match";
        }

        public static string StartsOnAnotherMap(Playlist playlist, string first, string current)
        {
            ArgumentNullException.ThrowIfNull(playlist);
            return "playlist " + playlist.Id + " starts on map " + first + " but the match is on " + current;
        }

        private static string Where(Playlist playlist, int position)
        {
            ArgumentNullException.ThrowIfNull(playlist);
            return "playlist " + playlist.Id + " entry " + Count(position + 1) + "/" + Count(playlist.Entries.Count);
        }

        private static string Count(int value)
        {
            return value.ToString(CultureInfo.InvariantCulture);
        }
    }
}
