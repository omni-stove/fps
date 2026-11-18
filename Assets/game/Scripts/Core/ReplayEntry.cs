using System;
using System.Globalization;

namespace Shooter.Core
{
    public readonly struct ReplayEntry : IEquatable<ReplayEntry>
    {
        public ReplayEntry(string id, string mapId, DateTime recordedAtUtc, float durationSeconds)
        {
            if (string.IsNullOrWhiteSpace(id))
            {
                throw new ArgumentException("id must not be empty", nameof(id));
            }

            // 一覧は端末をまたいで並べるので、地域の時刻が混ざると順が狂う。
            if (recordedAtUtc.Kind != DateTimeKind.Utc)
            {
                throw new ArgumentException("recordedAtUtc must be a UTC time", nameof(recordedAtUtc));
            }

            if (!(durationSeconds >= 0f) || float.IsInfinity(durationSeconds))
            {
                throw new ArgumentOutOfRangeException(nameof(durationSeconds), durationSeconds, "durationSeconds must be a non-negative finite number");
            }

            Id = id.Trim();
            MapId = mapId ?? string.Empty;
            RecordedAtUtc = recordedAtUtc;
            DurationSeconds = durationSeconds;
        }

        public string Id { get; }

        public string MapId { get; }

        public DateTime RecordedAtUtc { get; }

        public float DurationSeconds { get; }

        public bool Equals(ReplayEntry other)
        {
            return string.Equals(Id, other.Id, StringComparison.Ordinal)
                && string.Equals(MapId, other.MapId, StringComparison.Ordinal)
                && RecordedAtUtc == other.RecordedAtUtc
                && DurationSeconds.Equals(other.DurationSeconds);
        }

        public override bool Equals(object obj)
        {
            return obj is ReplayEntry other && Equals(other);
        }

        public static bool operator ==(ReplayEntry left, ReplayEntry right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(ReplayEntry left, ReplayEntry right)
        {
            return !left.Equals(right);
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(
                Id == null ? 0 : StringComparer.Ordinal.GetHashCode(Id),
                MapId == null ? 0 : StringComparer.Ordinal.GetHashCode(MapId),
                RecordedAtUtc,
                DurationSeconds);
        }

        public override string ToString()
        {
            return Id + " " + MapId + " " + RecordedAtUtc.ToString("o", CultureInfo.InvariantCulture)
                + " " + DurationSeconds.ToString("0.#", CultureInfo.InvariantCulture) + "s";
        }
    }
}
