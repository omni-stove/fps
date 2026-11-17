using System.Collections.Generic;
using Shooter.Core;

namespace Shooter.Runtime
{
    public static class SpectateText
    {
        public const string TargetKey = "spectate.target";

        public const string NoTargetKey = "spectate.noTarget";

        public const string UnknownTargetKey = "spectate.unknownTarget";

        public const string ControlsKey = "spectate.controls";

        public const string RespawnKey = "spectate.respawn";

        public static string PreviousActionName => ActionNameOf(InputActionPaths.Previous);

        public static string NextActionName => ActionNameOf(InputActionPaths.Next);

        public static LocalizedText NameOf(int id, IParticipants participants)
        {
            return participants != null && participants.TryGet(id, out var participant)
                ? LocalizedText.Literal(participant.Name)
                : new LocalizedText(UnknownTargetKey, id);
        }

        public static LocalizedText Target(int? targetId, IParticipants participants)
        {
            return targetId.HasValue
                ? new LocalizedText(TargetKey, NameOf(targetId.Value, participants))
                : new LocalizedText(NoTargetKey);
        }

        public static LocalizedText Controls(string previousLabel, string nextLabel)
        {
            return new LocalizedText(ControlsKey, previousLabel ?? string.Empty, nextLabel ?? string.Empty);
        }

        public static LocalizedText Controls()
        {
            return Controls(InputActionLabels.LabelOf(PreviousActionName), InputActionLabels.LabelOf(NextActionName));
        }

        public static LocalizedText Respawn(int seconds)
        {
            return new LocalizedText(RespawnKey, seconds);
        }

        public static List<LocalizedText> Lines(SpectateStatus status, IParticipants participants, HudNotice notice)
        {
            var lines = new List<LocalizedText> { Target(status.TargetId, participants) };
            // 1 人しかいなければ Next / Previous を押しても同じ味方のままなので、案内しない
            if (status.CandidateCount > 1)
            {
                lines.Add(Controls());
            }

            if (notice.Kind == HudNoticeKind.Respawn)
            {
                lines.Add(Respawn(notice.Seconds));
            }

            return lines;
        }

        private static string ActionNameOf(string path)
        {
            var slash = path.LastIndexOf('/');
            return slash < 0 ? path : path.Substring(slash + 1);
        }
    }
}
