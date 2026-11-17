namespace Shooter.Core
{
    public readonly struct SprintChange
    {
        public SprintChange(bool isActive, HoldMode mode, string reason)
        {
            IsActive = isActive;
            Mode = mode;
            Reason = reason;
        }

        public bool IsActive { get; }

        public HoldMode Mode { get; }

        public string Reason { get; }
    }
}
