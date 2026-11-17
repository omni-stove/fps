using System;
using System.Globalization;

namespace Shooter.Core
{
    // マップを名前でなく Id で持つのは、名前に引くのを UI に任せるため。
    public readonly struct MapVoteCandidate : IEquatable<MapVoteCandidate>
    {
        // default ではコンストラクタを通らず null になるので、読むときに空文字へ寄せる。
        private readonly string _mapId;

        public MapVoteCandidate(string mapId, int votes)
        {
            if (string.IsNullOrWhiteSpace(mapId))
            {
                throw new ArgumentException("mapId must not be empty", nameof(mapId));
            }

            if (votes < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(votes), votes, "votes must not be negative");
            }

            _mapId = mapId;
            Votes = votes;
        }

        public string MapId => _mapId ?? string.Empty;

        public int Votes { get; }

        public bool Equals(MapVoteCandidate other)
        {
            return string.Equals(MapId, other.MapId, StringComparison.Ordinal) && Votes == other.Votes;
        }

        public override bool Equals(object obj)
        {
            return obj is MapVoteCandidate other && Equals(other);
        }

        public static bool operator ==(MapVoteCandidate left, MapVoteCandidate right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(MapVoteCandidate left, MapVoteCandidate right)
        {
            return !left.Equals(right);
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(StringComparer.Ordinal.GetHashCode(MapId), Votes);
        }

        public override string ToString()
        {
            return MapId + "=" + Votes.ToString(CultureInfo.InvariantCulture);
        }
    }
}
