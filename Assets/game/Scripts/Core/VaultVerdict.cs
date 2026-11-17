namespace Shooter.Core
{
    // 値は後ろにだけ足す (前の値の番号を変えない)。
    public enum VaultVerdict
    {
        NoObstacle,

        TooFar,

        NotAWall,

        NotFacing,

        TooLow,

        TooHigh,

        TooDeep,

        Blocked,

        Vault,

        Mantle,

        TooNarrow,

        NarrowFace,
    }
}
