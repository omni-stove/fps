using System;
using System.Collections.Generic;
using Shooter.Core;

namespace Shooter.Server
{
    /// <summary>
    /// 次の試合のマップ。起動時に読んだマップ (<see cref="Maps"/>) の中から <see cref="RequestNextMap"/> で選び、
    /// 次の試合を始めるときに <see cref="Map"/> を替えて <see cref="MapChanged"/> で知らせる。
    /// </summary>
    /// <remarks>
    /// 試合の途中で替えず次の試合の始めに替えるのは、当たり判定と出現地点が試合の途中で入れ替わると、
    /// 今いる位置が新しいマップの壁の中や宙になるため。
    /// </remarks>
    internal sealed partial class ServerMatch
    {
        private readonly List<ServerMapData> _maps = new List<ServerMapData>();

        /// <summary>
        /// 次の試合のマップを替えた直後、<see cref="Restarted"/> より前に呼ぶ。モジュールはここで当たり判定・出現地点・はしご・建物・乗り物を
        /// 新しいマップのものに替える。続く <see cref="Restarted"/> でルールが全員を新しいマップの出現地点へ置き直す。
        /// </summary>
        public event Action<ServerMapData> MapChanged;

        /// <summary>今の試合のマップ。<see cref="UseMaps"/> の前は null。</summary>
        public ServerMapData Map { get; private set; }

        /// <summary>替えられるマップ。今のマップを先頭に、起動時に読んだ順。</summary>
        public IReadOnlyList<ServerMapData> Maps => _maps;

        /// <summary><see cref="RequestNextMap"/> で選んだ次の試合のマップの Id。選んでいなければ null。</summary>
        public string NextMap { get; private set; }

        /// <summary>今のマップを current にし、替えられるマップに maps を足す。current と同じものが maps にあってもよい。</summary>
        /// <exception cref="InvalidOperationException">もう呼んだ。</exception>
        /// <exception cref="ArgumentException">maps に null か、同じ Id の別のマップがある。</exception>
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

        /// <summary>読んであるマップから Id で引く。無ければ null。</summary>
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

        /// <summary>
        /// 次の試合を mapId のマップで始める。替えるのは次の試合を始めるときで、それまでは選び直せる。
        /// 読んでいないマップなら何もせず false。
        /// </summary>
        public bool RequestNextMap(string mapId)
        {
            if (FindMap(mapId) == null)
            {
                return false;
            }

            NextMap = mapId;
            return true;
        }

        partial void ChangeMapForNextMatch()
        {
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
