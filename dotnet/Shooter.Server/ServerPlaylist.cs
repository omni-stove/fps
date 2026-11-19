using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using Shooter.Core;

namespace Shooter.Server
{
    // Unity の PlaylistRunner と同じ playlists.json (Core の PlaylistJson) を読み、試合ごとに次の項目へ進める。
    // 次の項目を 1 つ先に決めておくのは、試合の合間のマップ投票で「次はこの項目」とクライアントへ先に知らせるため。
    // 混ぜる並びは周をまたぐと組み直されるので、PlaylistCursor を 1 つ先へ進めておかないと次の項目は分からない
    internal sealed class ServerPlaylist
    {
        // Unity では Resources の Tuning/mutators から読むので、server-data では調整値と並べて置く
        public const string MutatorsTuningFileName = "mutators.json";

        public const string FilePattern = "*.json";

        private readonly System.Collections.ObjectModel.ReadOnlyCollection<ServerPlaylistEntry> _entries;
        private readonly IReadOnlyList<string> _mapNames;
        private readonly PlaylistCursor _cursor;

        public ServerPlaylist(Playlist playlist, IReadOnlyList<ServerPlaylistEntry> entries, int seed, string path = "", string mutatorsPath = null)
        {
            ArgumentNullException.ThrowIfNull(playlist);
            ArgumentNullException.ThrowIfNull(entries);
            if (entries.Count != playlist.Entries.Count)
            {
                throw new ArgumentException(
                    "playlist " + playlist.Id + " has " + playlist.Entries.Count.ToString(CultureInfo.InvariantCulture) + " entries but "
                    + entries.Count.ToString(CultureInfo.InvariantCulture) + " were given",
                    nameof(entries));
            }

            var list = new List<ServerPlaylistEntry>(entries.Count);
            var maps = new List<string>();
            for (var i = 0; i < entries.Count; i++)
            {
                var entry = entries[i] ?? throw new ArgumentException("entries must not contain null", nameof(entries));
                if (entry.Position != i || !ReferenceEquals(entry.Entry, playlist.Entries[i]))
                {
                    throw new ArgumentException(
                        "entries[" + i.ToString(CultureInfo.InvariantCulture) + "] is not entry " + (i + 1).ToString(CultureInfo.InvariantCulture)
                        + " of playlist " + playlist.Id,
                        nameof(entries));
                }

                list.Add(entry);
                if (!maps.Contains(entry.Map))
                {
                    maps.Add(entry.Map);
                }
            }

            _entries = list.AsReadOnly();
            _mapNames = maps.AsReadOnly();
            Path = path ?? "";
            MutatorsPath = mutatorsPath;
            _cursor = new PlaylistCursor(playlist, seed);
            Current = _entries[_cursor.Position];
            Round = _cursor.Round;
            _cursor.Next();
        }

        public Playlist Playlist => _cursor.Playlist;

        public IReadOnlyList<ServerPlaylistEntry> Entries => _entries;

        // 項目に出てくるマップを、初めて出てくる順に 1 つずつ並べる。試合の枠はこれを全部先に読んでおく
        public IReadOnlyList<string> MapNames => _mapNames;

        public string Path { get; }

        public string MutatorsPath { get; }

        public ServerPlaylistEntry Current { get; private set; }

        // 今の項目が何周目か (0 から)
        public int Round { get; private set; }

        public ServerPlaylistEntry Upcoming => _entries[_cursor.Position];

        public ServerPlaylistEntry Advance()
        {
            Current = Upcoming;
            Round = _cursor.Round;
            _cursor.Next();
            return Current;
        }

        public static bool TryLoad(string id, IReadOnlyList<string> searchRoots, int seed, out ServerPlaylist playlist, out string error)
        {
            ArgumentNullException.ThrowIfNull(searchRoots);

            playlist = null;
            if (string.IsNullOrWhiteSpace(id))
            {
                error = "a playlist id is needed";
                return false;
            }

            var looked = new List<string>();
            var known = new SortedSet<string>(StringComparer.Ordinal);
            foreach (var root in searchRoots)
            {
                var directory = System.IO.Path.GetFullPath(
                    System.IO.Path.Combine(root, ServerMapData.DataDirectoryName, ServerRulesData.PlaylistsDirectoryName));
                looked.Add(directory);
                if (!Directory.Exists(directory))
                {
                    continue;
                }

                if (!TryReadDirectory(directory, out var found, out error))
                {
                    return false;
                }

                if (found.TryGetValue(id, out var hit))
                {
                    return TryBuild(hit.Playlist, hit.Path, root, searchRoots, seed, out playlist, out error);
                }

                known.UnionWith(found.Keys);
            }

            error = ServerPlaylistLog.NotFound(id, looked, known);
            return false;
        }

        // 探している id が別のファイルにあっても壊れたファイルで断るのは、直さないまま置いたファイルに後で別の id を足して初めて気づくことにしないため
        private static bool TryReadDirectory(
            string directory, out Dictionary<string, (Playlist Playlist, string Path)> found, out string error)
        {
            found = new Dictionary<string, (Playlist Playlist, string Path)>(StringComparer.Ordinal);
            string[] files;
            try
            {
                files = Directory.GetFiles(directory, FilePattern);
            }
            catch (Exception e) when (e is IOException || e is UnauthorizedAccessException)
            {
                error = ServerPlaylistLog.CannotRead(directory, e.Message);
                return false;
            }

            // ファイルの並びは OS で変わるので、2 つのファイルで同じ id を使ったときにどちらを先と書くかを揃える
            Array.Sort(files, StringComparer.Ordinal);
            foreach (var file in files)
            {
                IReadOnlyList<Playlist> playlists;
                try
                {
                    playlists = PlaylistJson.Parse(File.ReadAllText(file), file);
                }
                catch (Exception e) when (e is IOException || e is UnauthorizedAccessException || e is TuningJsonException || e is ServerDataJsonException)
                {
                    error = ServerPlaylistLog.CannotRead(file, e.Message);
                    return false;
                }

                foreach (var playlist in playlists)
                {
                    if (found.TryGetValue(playlist.Id, out var earlier))
                    {
                        error = ServerPlaylistLog.DuplicateId(playlist.Id, earlier.Path, file);
                        return false;
                    }

                    found.Add(playlist.Id, (playlist, file));
                }
            }

            error = null;
            return true;
        }

        // Unity の PlaylistRunner は知らないミューテーターを飛ばして回すが、専用サーバーは起動のうちに断る。
        // 試合の合間に分かっても運用する人はログを見ていないことが多く、意図と違うルールで回り続けるため
        private static bool TryBuild(
            Playlist source, string path, string root, IReadOnlyList<string> searchRoots, int seed, out ServerPlaylist playlist, out string error)
        {
            playlist = null;
            var mutatorsPath = System.IO.Path.GetFullPath(
                System.IO.Path.Combine(root, ServerMapData.DataDirectoryName, ServerRulesData.TuningDirectoryName, MutatorsTuningFileName));
            var mutators = MutatorJson.Empty;
            if (File.Exists(mutatorsPath))
            {
                try
                {
                    mutators = MutatorJson.Parse(File.ReadAllText(mutatorsPath), mutatorsPath);
                }
                catch (Exception e) when (e is IOException || e is UnauthorizedAccessException || e is TuningJsonException || e is ServerDataJsonException)
                {
                    error = ServerPlaylistLog.CannotReadMutators(mutatorsPath, e.Message);
                    return false;
                }
            }
            else
            {
                mutatorsPath = null;
            }

            // 同じルールと同じミューテーターの項目には同じ GameRules を渡す。次の試合のルールが今と同じ物なら、
            // 試合の枠は目的と Session を作り直さずに Restart するので、1 項目だけのプレイリストで毎試合作り直さないため
            var loaded = new Dictionary<string, ServerRulesData>(StringComparer.Ordinal);
            var mutated = new Dictionary<string, ServerRulesData>(StringComparer.Ordinal);
            var entries = new List<ServerPlaylistEntry>(source.Entries.Count);
            for (var i = 0; i < source.Entries.Count; i++)
            {
                var entry = source.Entries[i];
                if (!NetLaunchOptions.IsMapName(entry.Scene))
                {
                    error = ServerPlaylistLog.NotAMap(source, i, entry.Scene);
                    return false;
                }

                // Unity では rules の無い項目はシーンに置いたルールで遊ぶが、マップの JSON はルールを持たないので、Unity のサーバーの既定のルールにする
                var rulesName = entry.HasRules ? entry.Rules : ServerLaunch.DefaultRules;
                if (!loaded.TryGetValue(rulesName, out var data))
                {
                    if (!ServerRulesData.TryLoad(rulesName, searchRoots, out data, out var problem))
                    {
                        error = ServerPlaylistLog.RulesNotLoaded(source, i, problem);
                        return false;
                    }

                    loaded.Add(rulesName, data);
                }

                if (entry.MutatorIds.Count > 0)
                {
                    var key = rulesName + "\n" + string.Join("\n", entry.MutatorIds);
                    if (!mutated.TryGetValue(key, out var withMutators))
                    {
                        var chosen = new List<GameRulesMutator>(entry.MutatorIds.Count);
                        foreach (var id in entry.MutatorIds)
                        {
                            var mutator = GameRulesMutator.Find(mutators, id);
                            if (mutator == null)
                            {
                                error = ServerPlaylistLog.UnknownMutator(source, i, id, mutators, mutatorsPath);
                                return false;
                            }

                            chosen.Add(mutator);
                        }

                        try
                        {
                            withMutators = data.WithRules(GameRulesMutator.ApplyAll(data.Rules, chosen));
                        }
                        catch (Exception e) when (e is GameRulesJsonException || e is ArgumentException || e is InvalidOperationException)
                        {
                            error = ServerPlaylistLog.CannotApplyMutators(source, i, rulesName, e.Message);
                            return false;
                        }

                        mutated.Add(key, withMutators);
                    }

                    data = withMutators;
                }

                entries.Add(new ServerPlaylistEntry(i, entry, rulesName, data));
            }

            playlist = new ServerPlaylist(source, entries, seed, path, mutatorsPath);
            error = null;
            return true;
        }
    }
}
