namespace Shooter.Core
{
    public sealed class KillConfirmedTag
    {
        internal KillConfirmedTag(int id, int ownerId, Side ownerSide, float secondsLeft)
        {
            Id = id;
            OwnerId = ownerId;
            OwnerSide = ownerSide;
            Remaining = secondsLeft;
        }

        public int Id { get; }

        public int OwnerId { get; }

        public Side OwnerSide { get; }

        public float SecondsLeft => (float)Remaining;

        // float の Tick を float のまま引き続けると誤差が溜まって消える時刻がずれるので、double で数える。
        internal double Remaining { get; set; }
    }
}
