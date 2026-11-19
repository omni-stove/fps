using System;

namespace Shooter.Protocol
{
    [Flags]
    public enum ObjectiveItemFields
    {
        None = 0,
        Identity = 1 << 0,
        Position = 1 << 1,
        State = 1 << 2,
        Access = 1 << 3,
        All = Identity | Position | State | Access,

        // 旗・爆弾・タグ・台車は開けることも半径も持たないので、既定のままなら丸ごとのスナップショットにも載せない
        Optional = Access,
    }
}
