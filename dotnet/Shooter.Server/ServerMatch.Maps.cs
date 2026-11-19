using System;
using System.Collections.Generic;
using Shooter.Core;

namespace Shooter.Server
{
    // マップを試合の途中で替えず次の試合の始めに替えるのは、当たり判定と出現地点が試合の途中で入れ替わると、
    // 今いる位置が新しいマップの壁の中や宙になるため。
    internal sealed partial class ServerMatch
    {
        private readonly List<ServerMapData> _maps = new List<ServerMapData>();

        // Restarted より前に呼ぶのは、モジュールが当たり判定・出現地点などを新しいマップのものに替えてから、ルールが全員を新しいマップの出現地点へ置き直すため。
        public event Action<ServerMapData> MapChanged;

        // モジュールの Tick でなくマップを替える前のこのイベントで選ばせるのは、試合の枠はモジュールより先に Tick するので、投票の締め切りと
        // 次の試合の始まりが同じ Tick に重なると、モジュールの Tick で締め切っていては決まったマップが間に合わないため。
        public event Action NextMapChoosing;

        public float MapVoteSeconds { get; }

        public bool HoldsMapVote => MapVoteSeconds > 0f && _maps.Count > 1;

        public ServerMapData Map { get; private set; }

        public IReadOnlyList<ServerMapData> Maps => _maps;

        public string NextMap { get; private set; }

        public void UseMaps(ServerMapData current, IReadOnlyList<ServerMapData> maps = null)
        {
            ArgumentNullException.ThrowIfNull(current);
            if (Map != null)
            {
                throw new InvalidOperationException("the maps of the match are already set");
            }

            var added = new List<ServerMapData> { current };
            if (maps != null)
            {
                foreach (var map in maps)
                {
                    if (map == null)
                    {
                        throw new ArgumentException("maps must not contain null", nameof(maps));
                    }

                    var same = added.Find(m => string.Equals(m.Name, map.Name, StringComparison.Ordinal));
                    if (same == null)
                    {
                        added.Add(map);
                    }
                    else if (!ReferenceEquals(same, map))
                    {
                        throw new ArgumentException("maps lists map " + map.Name + " more than once", nameof(maps));
                    }
                }
            }

            _maps.AddRange(added);
            Map = current;
        }

        public ServerMapData FindMap(string mapId)
        {
            foreach (var map in _maps)
            {
                if (string.Equals(map.Name, mapId, StringComparison.Ordinal))
                {
                    return map;
                }
            }

            return null;
        }

        public bool RequestNextMap(string mapId)
        {
            if (FindMap(mapId) == null)
            {
                return false;
            }

            NextMap = mapId;
            return true;
        }

        public void WriteMapVote(int playerId, string mapId)
        {
            if (_events.Enabled)
            {
                _events.Write(ServerEventLog.MapVote(_utcNow(), MatchId, playerId, ProductUserIdOf(playerId), mapId));
            }
        }

        // 試合の合間に次の項目を選ぶ。null なら選ばず、マップは投票か RequestNextMap、ルールは RequestNextRules で決まる
        public ServerPlaylist Playlist { get; private set; }

        // 始めのマップは最初の項目のマップでなければならないので、UseMaps の後に呼ぶ
        public void UsePlaylist(ServerPlaylist playlist)
        {
            ArgumentNullException.ThrowIfNull(playlist);
            if (Playlist != null)
            {
                throw new InvalidOperationException("the playlist of the match is already set");
            }

            if (Map == null)
            {
                throw new InvalidOperationException("use the maps of the match before the playlist");
            }

            foreach (var entry in playlist.Entries)
            {
                if (FindMap(entry.Map) == null)
                {
                    throw new ArgumentException(ServerPlaylistLog.MapOutsideTheMatch(playlist.Playlist, entry.Position, entry.Map), nameof(playlist));
                }
            }

            if (!string.Equals(Map.Name, playlist.Current.Map, StringComparison.Ordinal))
            {
                throw new ArgumentException(ServerPlaylistLog.StartsOnAnotherMap(playlist.Playlist, playlist.Current.Map, Map.Name), nameof(playlist));
            }

            Playlist = playlist;
        }

        // CheckNextRules はルールのモジュールが始まってから差し込まれるので、モジュールを始めた後に呼ぶ。
        // 試合の合間に使えないと分かると、項目を飛ばしたルールで遊ばせ続けることになるため、起動のうちにすべての項目を確かめる
        public string CheckPlaylist()
        {
            if (Playlist == null || CheckNextRules == null)
            {
                return null;
            }

            var checkedMaps = new Dictionary<GameRules, HashSet<string>>(ReferenceEqualityComparer.Instance);
            foreach (var entry in Playlist.Entries)
            {
                if (!checkedMaps.TryGetValue(entry.Rules, out var maps))
                {
                    maps = new HashSet<string>(StringComparer.Ordinal);
                    checkedMaps.Add(entry.Rules, maps);
                }

                if (!maps.Add(HoldsMapVote ? string.Empty : entry.Map))
                {
                    continue;
                }

                var problem = CheckNextRules(entry.Rules, MapsOf(entry));
                if (problem != null)
                {
                    return ServerPlaylistLog.CannotUseRules(Playlist.Playlist, entry.Position, entry.RulesName, problem);
                }
            }

            return null;
        }

        partial void ChangeMapForNextMatch()
        {
            NextMapChoosing?.Invoke();
            if (Playlist != null)
            {
                ChooseNextPlaylistEntry();
            }

            var next = NextMap;
            NextMap = null;
            if (next == null || Map == null || string.Equals(next, Map.Name, StringComparison.Ordinal))
            {
                return;
            }

            var from = Map;
            Map = FindMap(next);
            _log.WriteLine(MatchLog.MapChanged(from.Name, Map.Name));
            if (_events.Enabled)
            {
                _events.Write(ServerEventLog.MapChanged(_utcNow(), MatchId, from.Name, Map.Name));
            }

            MapChanged?.Invoke(Map);
        }

        // 投票で決まったマップ (NextMapChoosing の中で RequestNextMap される) は項目のマップより優先する。投票は票が無くても今のマップ以外の候補に
        // 決めるので、項目のマップを優先すると、投票を開いた運用で票が一度も効かなくなるため。投票しない運用では項目のマップで回る
        private void ChooseNextPlaylistEntry()
        {
            var entry = Playlist.Advance();
            if (NextMap != null && !string.Equals(NextMap, entry.Map, StringComparison.Ordinal))
            {
                _log.WriteLine(ServerPlaylistLog.MapVotedOver(Playlist, entry, NextMap));
            }
            else
            {
                NextMap = entry.Map;
            }

            _log.WriteLine(ServerPlaylistLog.Entry(Playlist, entry, Playlist.Round));
            if (!RequestNextRules(entry.Rules, MapsOf(entry), out var problem))
            {
                _log.WriteLine(ServerPlaylistLog.RulesRefused(Playlist, entry, problem));
            }
        }

        // 投票しないなら次の試合のマップは項目のマップに決まる (RequestNextMap は投票しか呼ばない) ので、そのマップだけで確かめる。
        // すべてのマップで確かめると、台車の道筋のように一部のマップにしか無いものを要るモードを、別のマップの項目と同じプレイリストに入れられないため
        private ServerMapData[] MapsOf(ServerPlaylistEntry entry)
        {
            return HoldsMapVote ? null : new[] { FindMap(entry.Map) };
        }
    }
}
