using System;
using System.Collections.Generic;
using Shooter.Core;

namespace Shooter.Runtime
{
    // 武器の名前を出さないのは、Id の意味がオフラインと違う画面 (ネット対戦) では、倒した側の持っている武器を部品から引けないため。
    public sealed class ParticipantKillFeedNames : IKillFeedNames
    {
        public ParticipantKillFeedNames(IParticipants participants)
        {
            Participants = participants ?? throw new ArgumentNullException(nameof(participants));
        }

        public IParticipants Participants { get; }

        public KillFeedLine Describe(KillFeedEntry entry)
        {
            var killer = NameOf(entry.KillerId);
            var victim = entry.IsSuicide ? killer : NameOf(entry.VictimId);
            return new KillFeedLine(entry, killer, victim, default, AnyLocal(Participants, entry.AssisterIds));
        }

        public static bool AnyLocal(IParticipants participants, IReadOnlyList<int> ids)
        {
            if (participants == null)
            {
                throw new ArgumentNullException(nameof(participants));
            }

            if (ids == null)
            {
                return false;
            }

            for (var i = 0; i < ids.Count; i++)
            {
                if (participants.TryGet(ids[i], out var participant) && participant.IsLocal)
                {
                    return true;
                }
            }

            return false;
        }

        private KillFeedName NameOf(int id)
        {
            return Participants.TryGet(id, out var participant)
                ? new KillFeedName(participant)
                : new KillFeedName(KillFeedText.Unknown(id), null, false);
        }
    }
}
