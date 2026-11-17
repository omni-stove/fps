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

        partial void ChangeMapForNextMatch()
        {
            NextMapChoosing?.Invoke();
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
    }
}
