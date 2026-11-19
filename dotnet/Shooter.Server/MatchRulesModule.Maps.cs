using System;
using System.Globalization;
using Shooter.Core;

namespace Shooter.Server
{
    // 次の試合でマップが替わったら、出現地点と拠点を新しいマップのものにする。置き直しは続く Restarted (Session.Restart) が行う
    internal sealed partial class MatchRulesModule
    {
        private static NetMapPoints PointsOf(ServerMapData map)
        {
            return map.Points ?? throw new InvalidOperationException(
                "map " + map.Name + " has no spawn points: " + map.Name + NetMapPoints.Extension
                + " is not in " + map.Directory + "; export it from Unity with make export-server-rules");
        }

        // 替えた先で始められないと分かるのが試合の合間だと、遊んでいる人を残して止めるしかないので、起動のうちに断る
        // 投票しないプレイリストでは始めのルールのまま他のマップへ移らないので、他のマップは項目ごとに ServerMatch.CheckPlaylist に確かめさせる
        private void CheckOtherMaps()
        {
            if (_match.Playlist != null && !_match.HoldsMapVote)
            {
                return;
            }

            foreach (var map in _match.Maps)
            {
                if (ReferenceEquals(map, _context.Map))
                {
                    continue;
                }

                var points = PointsOf(map);
                RequireSpawnPointFor(map, points, Team.Alpha);
                RequireSpawnPointFor(map, points, Team.Bravo);
                _objective.CheckMap(map, points);
            }
        }

        private static void RequireSpawnPointFor(ServerMapData map, NetMapPoints points, Team team)
        {
            foreach (var point in points.SpawnPoints)
            {
                if (point.IsUsableBy(team))
                {
                    return;
                }
            }

            throw new InvalidOperationException(
                "cannot change to map " + map.Name + " (" + points.SpawnPoints.Count.ToString(CultureInfo.InvariantCulture)
                + " spawn points): no spawn point is usable by team " + team);
        }

        private void HandleMapChanged(ServerMapData map)
        {
            var points = PointsOf(map);
            _session.ReplaceSpawnPoints(points.SpawnPoints);
            _log.WriteLine(MatchLog.Tag + " spawnPoints=" + Text(points.SpawnPoints.Count) + " map=" + map.Name);
            // ルールが替わるなら今の目的は捨てるので渡さない。渡すと、前のモードに要るものが無いマップへ移るとき (Domination → Payload) に落ちる。
            // 新しい目的には UseNextRules が渡す
            if (_match.NextRules != null && !ReferenceEquals(_match.NextRules, _data.Rules))
            {
                return;
            }

            _objective.UseMap(map, points);
            foreach (var player in _match.Players)
            {
                ObjectivePlayerJoined(player.PlayerId);
            }
        }
    }
}
