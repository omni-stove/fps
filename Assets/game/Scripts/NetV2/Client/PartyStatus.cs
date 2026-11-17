using System;
using System.Collections.Generic;
using Shooter.Core;

namespace Shooter.NetV2
{
    /// <summary><see cref="PartySession"/> の、UI に見せる今の状態。ID はどれも EOS の ProductUserId とロビーの Id の文字列のまま持つ。</summary>
    public sealed class PartyStatus : IEquatable<PartyStatus>
    {
        private readonly string[] _members;

        /// <summary>パーティに入っていない状態。</summary>
        public static readonly PartyStatus None = new PartyStatus(
            PartyPhase.None, null, null, 0, Array.Empty<string>(), null, PartyState.Idle, PartyError.None);

        /// <param name="phase">パーティに入っているかの段階。</param>
        /// <param name="partyId">パーティ (メンバー用ロビー) の Id。招待のコードにもなる。入っていなければ null。</param>
        /// <param name="localUserId">自分の ProductUserId。ログイン前は null。</param>
        /// <param name="maxMembers">パーティに入れる人数。入っていなければ 0。</param>
        /// <param name="members">メンバーの ProductUserId。入っていなければ空。</param>
        /// <param name="leaderUserId">リーダーの ProductUserId。入っていなければ null。</param>
        /// <param name="state">リーダーの今の状態。</param>
        /// <param name="error">最後にパーティに入れなかった・外れた理由。</param>
        public PartyStatus(
            PartyPhase phase,
            string partyId,
            string localUserId,
            int maxMembers,
            IReadOnlyList<string> members,
            string leaderUserId,
            PartyState state,
            PartyError error)
        {
            if (members == null)
            {
                throw new ArgumentNullException(nameof(members));
            }

            Phase = phase;
            PartyId = partyId;
            LocalUserId = localUserId;
            MaxMembers = maxMembers;
            _members = new string[members.Count];
            for (var i = 0; i < _members.Length; i++)
            {
                _members[i] = members[i];
            }

            LeaderUserId = leaderUserId;
            State = state;
            Error = error;
        }

        public PartyPhase Phase { get; }

        /// <summary>パーティの Id。招待のコードとして仲間に渡す。入っていなければ null。</summary>
        public string PartyId { get; }

        public string LocalUserId { get; }

        public int MaxMembers { get; }

        /// <summary>メンバーの ProductUserId。入った順。</summary>
        public IReadOnlyList<string> Members => _members;

        public string LeaderUserId { get; }

        /// <summary>リーダーの今の状態。メンバーはリーダーの属性を読んだ値、リーダーは自分が書いた値。</summary>
        public PartyState State { get; }

        public PartyError Error { get; }

        public bool InParty => Phase == PartyPhase.InParty;

        /// <summary>自分がリーダーか。</summary>
        public bool IsLeader =>
            LocalUserId != null && string.Equals(LeaderUserId, LocalUserId, StringComparison.OrdinalIgnoreCase);

        public bool Equals(PartyStatus other)
        {
            if (other == null)
            {
                return false;
            }

            if (Phase != other.Phase
                || !string.Equals(PartyId, other.PartyId, StringComparison.Ordinal)
                || !string.Equals(LocalUserId, other.LocalUserId, StringComparison.Ordinal)
                || MaxMembers != other.MaxMembers
                || !string.Equals(LeaderUserId, other.LeaderUserId, StringComparison.Ordinal)
                || State != other.State
                || Error != other.Error
                || _members.Length != other._members.Length)
            {
                return false;
            }

            for (var i = 0; i < _members.Length; i++)
            {
                if (!string.Equals(_members[i], other._members[i], StringComparison.Ordinal))
                {
                    return false;
                }
            }

            return true;
        }

        public override bool Equals(object obj)
        {
            return Equals(obj as PartyStatus);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                var hash = (int)Phase;
                hash = (hash * 397) ^ (PartyId == null ? 0 : StringComparer.Ordinal.GetHashCode(PartyId));
                hash = (hash * 397) ^ (LeaderUserId == null ? 0 : StringComparer.Ordinal.GetHashCode(LeaderUserId));
                hash = (hash * 397) ^ _members.Length;
                hash = (hash * 397) ^ (int)State;
                hash = (hash * 397) ^ (int)Error;
                return hash;
            }
        }
    }
}
