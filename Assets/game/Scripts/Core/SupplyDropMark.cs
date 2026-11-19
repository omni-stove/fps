using System.Numerics;

namespace Shooter.Core
{
    public readonly struct SupplyDropMark
    {
        public SupplyDropMark(int number, Vector3 position, bool hasLanded)
        {
            Number = number;
            Position = position;
            HasLanded = hasLanded;
        }

        // 試合の中で落とした順の番号 (1 から)。
        public int Number { get; }

        public Vector3 Position { get; }

        public bool HasLanded { get; }
    }
}
