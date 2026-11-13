namespace Fps.Core
{
    /// <summary><see cref="SprintInput.Changed"/> で渡す、変化した後の状態と理由。</summary>
    public readonly struct SprintChange
    {
        public SprintChange(bool isActive, HoldMode mode, string reason)
        {
            IsActive = isActive;
            Mode = mode;
            Reason = reason;
        }

        /// <summary>変化した後にダッシュ中か。</summary>
        public bool IsActive { get; }

        /// <summary>変化した後のモード。</summary>
        public HoldMode Mode { get; }

        /// <summary>理由 (<see cref="SprintInput.ReasonButton"/> など)。</summary>
        public string Reason { get; }
    }
}
