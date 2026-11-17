using System;

namespace Shooter.Protocol
{
    [Flags]
    public enum PlayerFields
    {
        None = 0,
        Identity = 1 << 0,
        Motion = 1 << 1,
        Cover = 1 << 2,
        Dodge = 1 << 3,
        Vault = 1 << 4,
        Health = 1 << 5,
        Downed = 1 << 6,
        Weapon = 1 << 7,
        Score = 1 << 8,
        SpawnProtection = 1 << 9,
        Prone = 1 << 10,
        Ladder = 1 << 11,
        Vehicle = 1 << 12,
        Traversal = 1 << 13,
        Assists = 1 << 14,
        All = (1 << 15) - 1,
        Optional = Traversal | Assists,
    }
}
