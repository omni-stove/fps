using System;
using System.Collections.Generic;
using System.Numerics;

namespace Shooter.Core
{
    public sealed class DominationObjective
    {
        private readonly Dictionary<int, Team> _teams = new Dictionary<int, Team>();

        public Domination Mode { get; }

        public ControlPointMap Map { get; }

        public int PlayerCount => _teams.Count;

        public DominationObjective(
            ControlPointMap map,
            float captureSeconds,
            float pointsPerSecond,
            int scoreLimit,
            float timeLimitSeconds,
            int maxCapturers = DominationSpec.DefaultMaxCapturers)
        {
            Map = map ?? throw new ArgumentNullException(nameof(map));
            Mode = new Domination(new DominationSpec(map.Count, captureSeconds, pointsPerSecond, scoreLimit, timeLimitSeconds, maxCapturers));
        }

        public bool HasPlayer(int playerId) => _teams.ContainsKey(playerId);

        public void SetPlayer(int playerId, Team team)
        {
            Side.FromTeam(team);
            if (_teams.TryGetValue(playerId, out var current))
            {
                if (current == team)
                {
                    return;
                }

                Mode.RemovePlayer(playerId);
                _teams.Remove(playerId);
            }

            Mode.AddPlayer(playerId, team);
            _teams[playerId] = team;
        }

        public bool RemovePlayer(int playerId)
        {
            if (!_teams.Remove(playerId))
            {
                return false;
            }

            Mode.RemovePlayer(playerId);
            return true;
        }

        public int? Locate(int playerId, Vector3? position)
        {
            if (!_teams.ContainsKey(playerId))
            {
                throw new ArgumentOutOfRangeException(nameof(playerId), playerId, "no player with this id");
            }

            var point = position.HasValue ? Map.Locate(position.Value) : null;
            Mode.SetPlayerPoint(playerId, point);
            return point;
        }

        public bool Tick(bool matchPlaying, float deltaSeconds)
        {
            if (!matchPlaying)
            {
                return false;
            }

            if (Mode.State == GameModeState.Waiting)
            {
                Mode.Start();
            }

            if (Mode.State != GameModeState.Playing)
            {
                return false;
            }

            Mode.Tick(deltaSeconds);
            return Mode.State == GameModeState.Ended;
        }

        public Team? DecideWinner()
        {
            if (Mode.State == GameModeState.Ended)
            {
                return Mode.Winner;
            }

            return Leader(Mode.GetScore(Team.Alpha), Mode.GetScore(Team.Bravo));
        }

        public void Reset()
        {
            Mode.Reset();
        }

        public static Team? Leader(float alphaScore, float bravoScore)
        {
            if (alphaScore > bravoScore)
            {
                return Team.Alpha;
            }

            return bravoScore > alphaScore ? Team.Bravo : (Team?)null;
        }
    }
}
