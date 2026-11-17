using System;
using System.Collections.Generic;

namespace Shooter.Core
{
    public sealed class MapVote
    {
        public const int DefaultCandidateCount = 3;

        private const long _modulus = 2147483647L;
        private const long _multiplier = 48271L;

        private readonly string[] _candidates;
        private readonly Dictionary<int, string> _votes = new Dictionary<int, string>();
        private long _randomState;
        private MapVoteDecision _decision;

        public MapVote(IReadOnlyList<string> mapPool, string currentMapId, float seconds, int seed, int candidateCount = DefaultCandidateCount)
        {
            if (mapPool == null)
            {
                throw new ArgumentNullException(nameof(mapPool));
            }

            if (!(seconds >= 0f) || float.IsInfinity(seconds))
            {
                throw new ArgumentOutOfRangeException(nameof(seconds), seconds, "seconds must be finite and not negative");
            }

            if (candidateCount < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(candidateCount), candidateCount, "candidateCount must be at least 1");
            }

            CurrentMapId = string.IsNullOrEmpty(currentMapId) ? string.Empty : currentMapId;
            var eligible = new List<string>(mapPool.Count);
            var seen = new HashSet<string>(StringComparer.Ordinal);
            for (var i = 0; i < mapPool.Count; i++)
            {
                var mapId = mapPool[i];
                if (string.IsNullOrWhiteSpace(mapId))
                {
                    throw new ArgumentException(nameof(mapPool) + "[" + i + "] must not be empty", nameof(mapPool));
                }

                if (!seen.Add(mapId))
                {
                    throw new ArgumentException(nameof(mapPool) + " lists '" + mapId + "' more than once", nameof(mapPool));
                }

                if (!string.Equals(mapId, CurrentMapId, StringComparison.Ordinal))
                {
                    eligible.Add(mapId);
                }
            }

            // 状態が 0 だと乱数の列が 0 のまま進まなくなるので、種を 1 から 2147483646 へ寄せる。
            _randomState = ((uint)seed % (_modulus - 1L)) + 1L;
            _candidates = Pick(eligible, candidateCount);
            RemainingSeconds = seconds;
        }

        public string CurrentMapId { get; }

        public IReadOnlyList<string> Candidates => _candidates;

        public float RemainingSeconds { get; private set; }

        public bool IsClosed { get; private set; }

        public MapVoteDecision Decision => _decision;

        public int VoteCount => _votes.Count;

        public bool IsCandidate(string mapId)
        {
            return mapId != null && Array.IndexOf(_candidates, mapId) >= 0;
        }

        public string VoteOf(int playerId)
        {
            return _votes.TryGetValue(playerId, out var mapId) ? mapId : string.Empty;
        }

        public int VotesFor(string mapId)
        {
            var count = 0;
            foreach (var vote in _votes.Values)
            {
                if (string.Equals(vote, mapId, StringComparison.Ordinal))
                {
                    count++;
                }
            }

            return count;
        }

        public IReadOnlyList<MapVoteCandidate> Tally()
        {
            var tally = new MapVoteCandidate[_candidates.Length];
            for (var i = 0; i < tally.Length; i++)
            {
                tally[i] = new MapVoteCandidate(_candidates[i], VotesFor(_candidates[i]));
            }

            return tally;
        }

        public MapVoteCast Cast(int playerId, string mapId)
        {
            if (IsClosed)
            {
                return MapVoteCast.Closed;
            }

            if (!IsCandidate(mapId))
            {
                return MapVoteCast.NotCandidate;
            }

            if (_votes.TryGetValue(playerId, out var previous))
            {
                if (string.Equals(previous, mapId, StringComparison.Ordinal))
                {
                    return MapVoteCast.Unchanged;
                }

                _votes[playerId] = mapId;
                return MapVoteCast.Changed;
            }

            _votes.Add(playerId, mapId);
            return MapVoteCast.Cast;
        }

        // 締め切った後に抜けた人の票を消すと、決めた結果と票の数が食い違うので消さない。
        public string Leave(int playerId)
        {
            if (IsClosed || !_votes.TryGetValue(playerId, out var mapId))
            {
                return string.Empty;
            }

            _votes.Remove(playerId);
            return mapId;
        }

        public bool Tick(float deltaSeconds)
        {
            if (!(deltaSeconds >= 0f) || float.IsInfinity(deltaSeconds))
            {
                throw new ArgumentOutOfRangeException(nameof(deltaSeconds), deltaSeconds, "deltaSeconds must be finite and not negative");
            }

            if (IsClosed)
            {
                return false;
            }

            RemainingSeconds = Math.Max(0f, RemainingSeconds - deltaSeconds);
            if (RemainingSeconds > 0f)
            {
                return false;
            }

            Close();
            return true;
        }

        // 2 度目に決め直すと乱数が進んで結果が変わりうるので、前に決めた結果を返す。
        public MapVoteDecision Close()
        {
            if (IsClosed)
            {
                return _decision;
            }

            IsClosed = true;
            RemainingSeconds = 0f;
            _decision = Decide();
            return _decision;
        }

        public MapVoteStatus StatusFor(int playerId)
        {
            return new MapVoteStatus(Tally(), VoteOf(playerId), RemainingSeconds, IsClosed ? _decision.MapId : null);
        }

        private MapVoteDecision Decide()
        {
            if (_candidates.Length == 0)
            {
                return new MapVoteDecision(string.Empty, MapVoteDecisionReason.NoCandidates, 0);
            }

            var top = 0;
            var leaders = new List<string>(_candidates.Length);
            foreach (var mapId in _candidates)
            {
                var votes = VotesFor(mapId);
                if (votes > top)
                {
                    top = votes;
                    leaders.Clear();
                }

                if (votes == top)
                {
                    leaders.Add(mapId);
                }
            }

            if (top == 0)
            {
                return new MapVoteDecision(leaders[NextIndex(leaders.Count)], MapVoteDecisionReason.NoVotes, 0);
            }

            if (leaders.Count == 1)
            {
                return new MapVoteDecision(leaders[0], MapVoteDecisionReason.Majority, top);
            }

            return new MapVoteDecision(leaders[NextIndex(leaders.Count)], MapVoteDecisionReason.Tie, top);
        }

        // 選んだ順のままだと同じ候補でも種ごとに並びが変わり UI の並びが揺れるので、マップの候補の並びに戻す。
        private string[] Pick(List<string> eligible, int count)
        {
            if (eligible.Count <= count)
            {
                return eligible.ToArray();
            }

            var order = new int[eligible.Count];
            for (var i = 0; i < order.Length; i++)
            {
                order[i] = i;
            }

            for (var i = 0; i < count; i++)
            {
                var j = i + NextIndex(order.Length - i);
                var swap = order[i];
                order[i] = order[j];
                order[j] = swap;
            }

            Array.Sort(order, 0, count);
            var picked = new string[count];
            for (var i = 0; i < count; i++)
            {
                picked[i] = eligible[order[i]];
            }

            return picked;
        }

        // System.Random は実装 (Mono と .NET) で列が変わりうるので使わず、Lehmer の乗算合同法 (MINSTD) で決める (Unity と dotnet で同じ種から同じ結果にするため)。
        private int NextIndex(int count)
        {
            _randomState = _randomState * _multiplier % _modulus;
            return (int)(_randomState % count);
        }
    }
}
