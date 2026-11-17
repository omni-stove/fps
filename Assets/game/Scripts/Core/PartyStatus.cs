using System;
using System.Collections.Generic;

namespace Shooter.Core
{
    public readonly struct PartyStatus : IEquatable<PartyStatus>
    {
        // 渡された一覧を後から書き換えられても変わらないよう写しを持つ。default ではコンストラクタを通らず null になるので、読むときに空へ寄せる。
        private readonly PartyMember[] _members;
        private readonly string _inviteCode;

        public PartyStatus(PartyStage stage, IReadOnlyList<PartyMember> members, string inviteCode, bool isLocalLeader)
        {
            if (!Enum.IsDefined(typeof(PartyStage), stage))
            {
                throw new ArgumentOutOfRangeException(nameof(stage), stage, "stage must be a defined PartyStage");
            }

            var copy = new PartyMember[members?.Count ?? 0];
            var leaders = 0;
            for (var i = 0; i < copy.Length; i++)
            {
                copy[i] = members[i];
                if (copy[i].Name.Length == 0)
                {
                    throw new ArgumentException("members must not hold a default PartyMember", nameof(members));
                }

                if (copy[i].IsLeader)
                {
                    leaders++;
                }
            }

            if (leaders > 1)
            {
                throw new ArgumentException("members list " + leaders + " leaders; a party has at most one", nameof(members));
            }

            inviteCode = string.IsNullOrEmpty(inviteCode) ? string.Empty : inviteCode;
            if (inviteCode.Length > 0 && !ConnectionExtras.IsValidField(inviteCode, ConnectionExtras.MaxPartyIdLength))
            {
                throw new ArgumentException(
                    "inviteCode must be 1-" + ConnectionExtras.MaxPartyIdLength + " characters of [0-9A-Za-z_-]", nameof(inviteCode));
            }

            if (isLocalLeader && leaders == 0)
            {
                throw new ArgumentException("the local player cannot lead a party whose members have no leader", nameof(isLocalLeader));
            }

            if (stage == PartyStage.None && (copy.Length > 0 || inviteCode.Length > 0))
            {
                throw new ArgumentException("a status outside a party must have no members and no invite code", nameof(stage));
            }

            Stage = stage;
            _members = copy;
            _inviteCode = inviteCode;
            IsLocalLeader = isLocalLeader;
        }

        public PartyStage Stage { get; }

        public IReadOnlyList<PartyMember> Members => _members ?? Array.Empty<PartyMember>();

        public string InviteCode => _inviteCode ?? string.Empty;

        public bool HasInviteCode => InviteCode.Length > 0;

        public bool IsLocalLeader { get; }

        public bool Equals(PartyStatus other)
        {
            if (Stage != other.Stage
                || IsLocalLeader != other.IsLocalLeader
                || !string.Equals(InviteCode, other.InviteCode, StringComparison.Ordinal))
            {
                return false;
            }

            var mine = Members;
            var theirs = other.Members;
            if (mine.Count != theirs.Count)
            {
                return false;
            }

            for (var i = 0; i < mine.Count; i++)
            {
                if (!mine[i].Equals(theirs[i]))
                {
                    return false;
                }
            }

            return true;
        }

        public override bool Equals(object obj)
        {
            return obj is PartyStatus other && Equals(other);
        }

        public static bool operator ==(PartyStatus left, PartyStatus right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(PartyStatus left, PartyStatus right)
        {
            return !left.Equals(right);
        }

        public override int GetHashCode()
        {
            var hash = HashCode.Combine((int)Stage, IsLocalLeader, StringComparer.Ordinal.GetHashCode(InviteCode));
            foreach (var member in Members)
            {
                hash = HashCode.Combine(hash, member);
            }

            return hash;
        }
    }
}
