using System;

namespace Shooter.Protocol
{
    // Gun Game の段とストリークを別々の部分にせず Progress にまとめるのは、SnapshotList が部分のビットを 2 バイトで書き、
    // これで 16 個の空きを使い切るため。
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
        Progress = 1 << 15,
        All = (1 << 16) - 1,
        Optional = Traversal | Assists | Progress,
    }
}
