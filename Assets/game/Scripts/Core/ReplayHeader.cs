using System;
using System.Globalization;

namespace Shooter.Core
{
    public sealed class ReplayHeader
    {
        public const int UnknownDuration = -1;

        public ReplayHeader(
            ushort protocolVersion,
            string clientVersion,
            string mapId,
            DateTime recordedAtUtc,
            int tickRate,
            int playerId,
            int durationTicks = UnknownDuration)
        {
            if (recordedAtUtc.Kind != DateTimeKind.Utc)
            {
                throw new ArgumentException("recordedAtUtc must be a UTC time", nameof(recordedAtUtc));
            }

            if (tickRate < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(tickRate), tickRate, "tickRate must be at least 1");
            }

            if (playerId < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(playerId), playerId, "playerId must not be negative");
            }

            if (durationTicks < UnknownDuration)
            {
                throw new ArgumentOutOfRangeException(nameof(durationTicks), durationTicks, "durationTicks must be -1 (unknown) or more");
            }

            ProtocolVersion = protocolVersion;
            ClientVersion = CheckText(clientVersion, nameof(clientVersion));
            MapId = CheckText(mapId, nameof(mapId));
            RecordedAtUtc = recordedAtUtc;
            TickRate = tickRate;
            PlayerId = playerId;
            DurationTicks = durationTicks;
        }

        public ushort ProtocolVersion { get; }

        public string ClientVersion { get; }

        public string MapId { get; }

        public DateTime RecordedAtUtc { get; }

        public int TickRate { get; }

        public int PlayerId { get; }

        // 記録の途中でアプリが落ちると書き戻せないので -1 (UnknownDuration) のまま残り、読む側が最後の記録から数える。
        public int DurationTicks { get; }

        public bool HasDuration => DurationTicks >= 0;

        public float DurationSeconds => HasDuration ? DurationTicks / (float)TickRate : 0f;

        public ReplayHeader WithDuration(int durationTicks)
        {
            return new ReplayHeader(ProtocolVersion, ClientVersion, MapId, RecordedAtUtc, TickRate, PlayerId, durationTicks);
        }

        public override string ToString()
        {
            return "map=" + MapId + " recorded=" + RecordedAtUtc.ToString("o", CultureInfo.InvariantCulture)
                + " tickRate=" + TickRate.ToString(CultureInfo.InvariantCulture)
                + " player=" + PlayerId.ToString(CultureInfo.InvariantCulture)
                + " protocol=" + ProtocolVersion.ToString(CultureInfo.InvariantCulture)
                + " client=" + ClientVersion
                + " duration=" + (HasDuration ? DurationTicks.ToString(CultureInfo.InvariantCulture) + " ticks" : "unknown");
        }

        private static string CheckText(string value, string name)
        {
            if (value == null)
            {
                throw new ArgumentNullException(name);
            }

            if (value.Length > ReplayFile.MaxTextLength)
            {
                throw new ArgumentException(
                    name + " is " + value.Length.ToString(CultureInfo.InvariantCulture) + " characters; at most "
                    + ReplayFile.MaxTextLength.ToString(CultureInfo.InvariantCulture) + " fit in a replay",
                    name);
            }

            return value;
        }
    }
}
