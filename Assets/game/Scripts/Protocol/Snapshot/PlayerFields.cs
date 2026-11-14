using System;

namespace Fps.Protocol
{
    /// <summary><see cref="PlayerSnapshot"/> の部分。変わった部分だけを送る単位。</summary>
    [Flags]
    public enum PlayerFields
    {
        None = 0,
        /// <summary>チームと Bot か。</summary>
        Identity = 1 << 0,
        /// <summary>位置・速度・向き・接地・しゃがみ・覗き込み。</summary>
        Motion = 1 << 1,
        Cover = 1 << 2,
        Dodge = 1 << 3,
        Vault = 1 << 4,
        /// <summary>体力と最後に受けた弾の向き。</summary>
        Health = 1 << 5,
        Downed = 1 << 6,
        /// <summary>持っている武器の Id。</summary>
        Weapon = 1 << 7,
        /// <summary>キル・デス・最後に倒した相手。</summary>
        Score = 1 << 8,
        SpawnProtection = 1 << 9,
        All = (1 << 10) - 1,
    }
}
