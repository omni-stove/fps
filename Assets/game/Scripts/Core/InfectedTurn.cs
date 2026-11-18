namespace Shooter.Core
{
    public readonly struct InfectedTurn
    {
        public InfectedTurn(int playerId, int? infectorId, bool isFirst)
        {
            PlayerId = playerId;
            InfectorId = infectorId;
            IsFirst = isFirst;
        }

        public int PlayerId { get; }

        // 最初の感染者と、誰にも倒されずに倒れた生存者には感染させた人がいない。
        public int? InfectorId { get; }

        public bool IsFirst { get; }
    }
}
