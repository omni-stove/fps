using System.Collections.Generic;
using Shooter.Core;

namespace Shooter.Runtime
{
    /// <summary>
    /// 観戦の欄 (<see cref="SpectateView"/>) に出す文。見ている味方・前と次へ替える操作・復活までの秒数を、上から 1 行ずつにする。
    /// 語は表 (Resources/Localization/&lt;言語&gt;/spectate.json) のキーで持ち、操作の名前は <see cref="InputActionLabels"/> から引く。
    /// </summary>
    public static class SpectateText
    {
        /// <summary>見ている味方の行。{0} が名前。</summary>
        public const string TargetKey = "spectate.target";

        /// <summary>見られる味方がいないときの行。</summary>
        public const string NoTargetKey = "spectate.noTarget";

        /// <summary>参加者の表に無い Id の名前。{0} が Id。</summary>
        public const string UnknownTargetKey = "spectate.unknownTarget";

        /// <summary>操作の案内の行。{0} が前の味方へ替える操作、{1} が次の味方へ替える操作の名前。</summary>
        public const string ControlsKey = "spectate.controls";

        /// <summary>復活までの行。{0} が秒数。</summary>
        public const string RespawnKey = "spectate.respawn";

        /// <summary>前の味方へ替えるアクションの名前 (<see cref="InputActionPaths.Previous"/> のアクション名)。</summary>
        public static string PreviousActionName => ActionNameOf(InputActionPaths.Previous);

        /// <summary>次の味方へ替えるアクションの名前 (<see cref="InputActionPaths.Next"/> のアクション名)。</summary>
        public static string NextActionName => ActionNameOf(InputActionPaths.Next);

        /// <summary>id の参加者の名前。participants が null か、表に無ければ <see cref="UnknownTargetKey"/> の文。</summary>
        public static LocalizedText NameOf(int id, IParticipants participants)
        {
            return participants != null && participants.TryGet(id, out var participant)
                ? LocalizedText.Literal(participant.Name)
                : new LocalizedText(UnknownTargetKey, id);
        }

        /// <summary>見ている味方の行。targetId が null なら <see cref="NoTargetKey"/> の行。</summary>
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

        /// <summary>今の言語の操作の名前 (<see cref="InputActionLabels.LabelOf(string, string)"/>) で作る操作の案内の行。</summary>
        public static LocalizedText Controls()
        {
            return Controls(InputActionLabels.LabelOf(PreviousActionName), InputActionLabels.LabelOf(NextActionName));
        }

        public static LocalizedText Respawn(int seconds)
        {
            return new LocalizedText(RespawnKey, seconds);
        }

        /// <summary>
        /// 欄の行。見ている味方の行に続けて、替えられる相手が 2 人以上なら操作の案内、notice がリスポーンの案内なら復活までの秒数を足す。
        /// </summary>
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
