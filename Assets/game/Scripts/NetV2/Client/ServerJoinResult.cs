using System;
using Shooter.Core;

namespace Shooter.NetV2
{
    public sealed class ServerJoinResult
    {
        private ServerJoinResult(ServerJoinOutcome outcome, string serverId, NetLaunchOptions options, string reason, MatchLobby matchLobby)
        {
            Outcome = outcome;
            ServerId = serverId;
            Options = options;
            Reason = reason;
            MatchLobby = matchLobby;
        }

        public ServerJoinOutcome Outcome { get; }

        public string ServerId { get; }

        public NetLaunchOptions Options { get; }

        public string Reason { get; }

        public MatchLobby MatchLobby { get; }

        public static ServerJoinResult Joined(string serverId, NetLaunchOptions options, MatchLobby matchLobby)
        {
            if (string.IsNullOrWhiteSpace(serverId))
            {
                throw new ArgumentException("must not be null or blank", nameof(serverId));
            }

            if (options == null)
            {
                throw new ArgumentNullException(nameof(options));
            }

            return new ServerJoinResult(ServerJoinOutcome.Joined, serverId, options, null, matchLobby);
        }

        public static ServerJoinResult NotJoined(ServerJoinOutcome outcome, string serverId, string reason)
        {
            if (outcome == ServerJoinOutcome.Joined || !Enum.IsDefined(typeof(ServerJoinOutcome), outcome))
            {
                throw new ArgumentOutOfRangeException(nameof(outcome), outcome, "must be a defined outcome other than Joined");
            }

            return new ServerJoinResult(outcome, serverId, null, reason, null);
        }
    }
}
