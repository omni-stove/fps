using System;
using System.Collections.Generic;
using Shooter.Core;

namespace Shooter.Runtime
{
    public static class ModerationText
    {
        public const string PlayersKey = "moderation.players";

        public const string BackKey = "moderation.back";

        public const string MuteKey = "moderation.mute";

        public const string UnmuteKey = "moderation.unmute";

        public const string BlockKey = "moderation.block";

        public const string UnblockKey = "moderation.unblock";

        public const string ReportKey = "moderation.report";

        public const string ReportedKey = "moderation.reported";

        public const string ReportPromptKey = "moderation.reportPrompt";

        public const string CancelKey = "moderation.cancel";

        public const string CheatingKey = "moderation.reason.cheating";

        public const string AbuseKey = "moderation.reason.abuse";

        public const string SpamKey = "moderation.reason.spam";

        public const string OffensiveNameKey = "moderation.reason.offensiveName";

        public const string OtherKey = "moderation.reason.other";

        public const string VoteKickStartKey = "moderation.voteKick.start";

        public const string VoteKickCalledKey = "moderation.voteKick.called";

        public const string VoteKickYesKey = "moderation.voteKick.yes";

        public const string VoteKickNoKey = "moderation.voteKick.no";

        public const string VoteKickRemainingKey = "moderation.voteKick.remaining";

        public const string VoteKickPassedKey = "moderation.voteKick.passed";

        public const string VoteKickFailedKey = "moderation.voteKick.failed";

        public static IReadOnlyList<string> Keys { get; } = new[]
        {
            PlayersKey, BackKey, MuteKey, UnmuteKey, BlockKey, UnblockKey, ReportKey, ReportedKey, ReportPromptKey, CancelKey,
            CheatingKey, AbuseKey, SpamKey, OffensiveNameKey, OtherKey,
            VoteKickStartKey, VoteKickCalledKey, VoteKickYesKey, VoteKickNoKey, VoteKickRemainingKey, VoteKickPassedKey, VoteKickFailedKey,
        };

        public static LocalizedText Players => new LocalizedText(PlayersKey);

        public static LocalizedText Back => new LocalizedText(BackKey);

        public static LocalizedText Cancel => new LocalizedText(CancelKey);

        public static LocalizedText VoteKickStart => new LocalizedText(VoteKickStartKey);

        public static LocalizedText VoteKickYes => new LocalizedText(VoteKickYesKey);

        public static LocalizedText VoteKickNo => new LocalizedText(VoteKickNoKey);

        public static LocalizedText VoteKickCalled(string callerName, string targetName)
        {
            return new LocalizedText(VoteKickCalledKey, callerName ?? string.Empty, targetName ?? string.Empty);
        }

        public static LocalizedText VoteKickRemaining(int seconds)
        {
            return new LocalizedText(VoteKickRemainingKey, seconds);
        }

        public static LocalizedText ReportPrompt(string name)
        {
            return new LocalizedText(ReportPromptKey, name ?? string.Empty);
        }

        public static LocalizedText Mute(bool muted)
        {
            return new LocalizedText(muted ? UnmuteKey : MuteKey);
        }

        public static LocalizedText Block(bool blocked)
        {
            return new LocalizedText(blocked ? UnblockKey : BlockKey);
        }

        public static LocalizedText Report(bool reported)
        {
            return new LocalizedText(reported ? ReportedKey : ReportKey);
        }

        public static LocalizedText Reason(ReportReason reason)
        {
            switch (reason)
            {
                case ReportReason.Cheating:
                    return new LocalizedText(CheatingKey);
                case ReportReason.Abuse:
                    return new LocalizedText(AbuseKey);
                case ReportReason.Spam:
                    return new LocalizedText(SpamKey);
                case ReportReason.OffensiveName:
                    return new LocalizedText(OffensiveNameKey);
                case ReportReason.Other:
                    return new LocalizedText(OtherKey);
                default:
                    throw new ArgumentOutOfRangeException(nameof(reason), reason, "reason is not a valid ReportReason");
            }
        }
    }
}
