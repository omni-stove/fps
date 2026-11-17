using System;
using Shooter.Core;

namespace Shooter.NetV2
{
    public sealed class JoinCodeResult
    {
        private JoinCodeResult(JoinCodeOutcome outcome, NetLaunchOptions options, string joinCode, string reason, MatchLobby matchLobby)
        {
            Outcome = outcome;
            Options = options;
            JoinCode = joinCode;
            Reason = reason;
            MatchLobby = matchLobby;
        }

        public JoinCodeOutcome Outcome { get; }

        public NetLaunchOptions Options { get; }

        // 入力のままでなく整えた形で持つのは、サーバーの JoinCode.Matches が整えて比べ、承認データは [0-9A-Za-z_-] しか通さないため。
        public string JoinCode { get; }

        public string Reason { get; }

        public MatchLobby MatchLobby { get; }

        public static JoinCodeResult Joined(NetLaunchOptions options, string joinCode)
        {
            return Joined(options, joinCode, null);
        }

        public static JoinCodeResult Joined(NetLaunchOptions options, string joinCode, MatchLobby matchLobby)
        {
            if (options == null)
            {
                throw new ArgumentNullException(nameof(options));
            }

            if (!ConnectionExtras.IsValidField(joinCode, ConnectionExtras.MaxJoinCodeLength))
            {
                throw new ArgumentException("must be a normalized join code", nameof(joinCode));
            }

            return new JoinCodeResult(JoinCodeOutcome.Joined, options, joinCode, null, matchLobby);
        }

        public static JoinCodeResult NotJoined(JoinCodeOutcome outcome, string reason)
        {
            if (outcome == JoinCodeOutcome.Joined || !Enum.IsDefined(typeof(JoinCodeOutcome), outcome))
            {
                throw new ArgumentOutOfRangeException(nameof(outcome), outcome, "must be a defined outcome other than Joined");
            }

            return new JoinCodeResult(outcome, null, null, reason, null);
        }

        public ConnectionExtras WithJoinCode(ConnectionExtras extras)
        {
            if (extras == null)
            {
                throw new ArgumentNullException(nameof(extras));
            }

            if (Outcome != JoinCodeOutcome.Joined)
            {
                throw new InvalidOperationException("no server was found for the join code (" + Outcome + ")");
            }

            return new ConnectionExtras(extras.PartyId, JoinCode, extras.SkillRating, extras.ProductUserId);
        }
    }
}
