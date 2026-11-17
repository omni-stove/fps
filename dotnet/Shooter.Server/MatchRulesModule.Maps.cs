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
        private void CheckOtherMaps(GameRules rules)
        {
            foreach (var map in _match.Maps)
            {
                if (ReferenceEquals(map, _context.Map))
                {
                    continue;
                }

                var points = PointsOf(map);
                RequireSpawnPointFor(map, points, Team.Alpha);
                RequireSpawnPointFor(map, points, Team.Bravo);

                if (rules.Objective == MatchObjective.Domination && points.ControlPoints.Count == 0)
                {
                    throw new InvalidOperationException(
                        "cannot change to map " + map.Name + " for the " + rules.Objective + " match: it has no control points");
                }
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
            if (_domination == null)
            {
                return;
            }

            StopObjective();
            PrepareObjective(_data.Rules, _data.Settings, points, out _);
            StartObjective();
            foreach (var player in _match.Players)
            {
                ObjectivePlayerJoined(player.PlayerId);
            }
        }
    }
}
