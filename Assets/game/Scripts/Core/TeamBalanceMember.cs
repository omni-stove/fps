namespace Shooter.Core
{
    /// <summary>チームの人数をそろえるときに見る、チームにいる 1 人。</summary>
    public readonly struct TeamBalanceMember
    {
        /// <param name="playerId">プレイヤーの Id。</param>
        /// <param name="partyId">入っているパーティの Id。無ければ null か空。</param>
        public TeamBalanceMember(int playerId, string partyId = null)
        {
            PlayerId = playerId;
            PartyId = string.IsNullOrEmpty(partyId) ? null : partyId;
        }

        public int PlayerId { get; }

        /// <summary>入っているパーティの Id。無ければ null。</summary>
        public string PartyId { get; }
    }
}
