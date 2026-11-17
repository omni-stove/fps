namespace Shooter.Core
{
    public readonly struct TeamBalanceMember
    {
        public TeamBalanceMember(int playerId, string partyId = null)
        {
            PlayerId = playerId;
            PartyId = string.IsNullOrEmpty(partyId) ? null : partyId;
        }

        public int PlayerId { get; }

        public string PartyId { get; }
    }
}
