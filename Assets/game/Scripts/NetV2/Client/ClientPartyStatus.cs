using System;
using System.Collections.Generic;
using Shooter.Core;
using CorePartyStatus = Shooter.Core.PartyStatus;

namespace Shooter.NetV2
{
    // UI が読む Core の PartyStatus には抜けている途中も失敗の理由も無いので、抜けている途中は待っている間 (Joining) として見せ、理由はログ (PartyLog) に任せる。
    public static class ClientPartyStatus
    {
        // EOS のメンバーには表示名が無いので、ProductUserId をそのまま名前にする。
        public static CorePartyStatus ToShown(PartyStatus status)
        {
            if (status == null)
            {
                throw new ArgumentNullException(nameof(status));
            }

            switch (status.Phase)
            {
                case PartyPhase.InParty:
                    break;
                case PartyPhase.Joining:
                case PartyPhase.Leaving:
                    return new CorePartyStatus(PartyStage.Joining, null, null, false);
                default:
                    return new CorePartyStatus(PartyStage.None, null, null, false);
            }

            var members = new List<PartyMember>(status.Members.Count);
            var hasLeader = false;
            foreach (var member in status.Members)
            {
                if (string.IsNullOrWhiteSpace(member))
                {
                    continue;
                }

                var isLeader = !hasLeader && string.Equals(member, status.LeaderUserId, StringComparison.OrdinalIgnoreCase);
                hasLeader |= isLeader;
                members.Add(new PartyMember(member, isLeader));
            }

            var inviteCode = ConnectionExtras.IsValidField(status.PartyId, ConnectionExtras.MaxPartyIdLength) ? status.PartyId : null;
            var stage = status.State == PartyState.Searching ? PartyStage.Searching : PartyStage.InParty;

            // リーダーとメンバーの一覧は別々に読み直すので、リーダーが一覧にまだ載っていない間は自分をリーダーと見せない (CorePartyStatus が拒むため)。
            return new CorePartyStatus(stage, members, inviteCode, hasLeader && status.IsLeader);
        }
    }
}
