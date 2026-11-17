using System;
using System.Collections.Generic;
using System.Globalization;

namespace Shooter.Core
{
    public readonly struct MapVoteStatus : IEquatable<MapVoteStatus>
    {
        // 渡された一覧を後から書き換えられても変わらないよう写しを持つ。default ではコンストラクタを通らず null になるので、読むときに空へ寄せる。
        private readonly MapVoteCandidate[] _candidates;
        private readonly string _myVote;
        private readonly string _decidedMapId;

        public MapVoteStatus(IReadOnlyList<MapVoteCandidate> candidates, string myVote, float remainingSeconds, string decidedMapId)
        {
            if (!(remainingSeconds >= 0f) || float.IsInfinity(remainingSeconds))
            {
                throw new ArgumentOutOfRangeException(nameof(remainingSeconds), remainingSeconds, "remainingSeconds must be finite and not negative");
            }

            var copy = new MapVoteCandidate[candidates?.Count ?? 0];
            var seen = new HashSet<string>(StringComparer.Ordinal);
            for (var i = 0; i < copy.Length; i++)
            {
                copy[i] = candidates[i];
                if (copy[i].MapId.Length == 0)
                {
                    throw new ArgumentException("candidates must not hold a default MapVoteCandidate", nameof(candidates));
                }

                if (!seen.Add(copy[i].MapId))
                {
                    throw new ArgumentException("candidates list '" + copy[i].MapId + "' more than once", nameof(candidates));
                }
            }

            myVote = string.IsNullOrEmpty(myVote) ? string.Empty : myVote;
            if (myVote.Length > 0 && !seen.Contains(myVote))
            {
                throw new ArgumentException("myVote '" + myVote + "' is not a candidate", nameof(myVote));
            }

            decidedMapId = string.IsNullOrEmpty(decidedMapId) ? string.Empty : decidedMapId;
            if (decidedMapId.Length > 0 && !seen.Contains(decidedMapId))
            {
                throw new ArgumentException("decidedMapId '" + decidedMapId + "' is not a candidate", nameof(decidedMapId));
            }

            _candidates = copy;
            _myVote = myVote;
            RemainingSeconds = remainingSeconds;
            _decidedMapId = decidedMapId;
        }

        public IReadOnlyList<MapVoteCandidate> Candidates => _candidates ?? Array.Empty<MapVoteCandidate>();

        public string MyVote => _myVote ?? string.Empty;

        public bool HasVoted => MyVote.Length > 0;

        public float RemainingSeconds { get; }

        public string DecidedMapId => _decidedMapId ?? string.Empty;

        public bool IsDecided => DecidedMapId.Length > 0;

        public bool Equals(MapVoteStatus other)
        {
            if (!string.Equals(MyVote, other.MyVote, StringComparison.Ordinal)
                || !RemainingSeconds.Equals(other.RemainingSeconds)
                || !string.Equals(DecidedMapId, other.DecidedMapId, StringComparison.Ordinal))
            {
                return false;
            }

            var mine = Candidates;
            var theirs = other.Candidates;
            if (mine.Count != theirs.Count)
            {
                return false;
            }

            for (var i = 0; i < mine.Count; i++)
            {
                if (!mine[i].Equals(theirs[i]))
                {
                    return false;
                }
            }

            return true;
        }

        public override bool Equals(object obj)
        {
            return obj is MapVoteStatus other && Equals(other);
        }

        public static bool operator ==(MapVoteStatus left, MapVoteStatus right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(MapVoteStatus left, MapVoteStatus right)
        {
            return !left.Equals(right);
        }

        public override int GetHashCode()
        {
            var hash = HashCode.Combine(StringComparer.Ordinal.GetHashCode(MyVote), RemainingSeconds, StringComparer.Ordinal.GetHashCode(DecidedMapId));
            foreach (var candidate in Candidates)
            {
                hash = HashCode.Combine(hash, candidate);
            }

            return hash;
        }

        public override string ToString()
        {
            var candidates = new string[Candidates.Count];
            for (var i = 0; i < candidates.Length; i++)
            {
                candidates[i] = Candidates[i].ToString();
            }

            return "candidates=[" + string.Join(", ", candidates) + "]"
                + " mine=" + (HasVoted ? MyVote : "none")
                + " left=" + RemainingSeconds.ToString("0.0", CultureInfo.InvariantCulture)
                + " decided=" + (IsDecided ? DecidedMapId : "none");
        }
    }
}
