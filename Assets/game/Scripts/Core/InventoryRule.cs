using System;

namespace Shooter.Core
{
    public sealed class InventoryRule
    {
        // 持ち物を送る電文が山の数を byte で送る
        public const int StackLimit = byte.MaxValue;

        public const int MaxGridWidth = 32;
        public const int MaxGridHeight = 64;

        public const int DefaultMaxStacks = 0;
        public const float DefaultMaxWeight = 0f;
        public const int DefaultGridWidth = 0;
        public const int DefaultGridHeight = 0;
        public const bool DefaultAllowRotation = true;
        public const bool DefaultCountWeightInCarryLoad = false;

        public static readonly InventoryRule Default = new InventoryRule();

        public InventoryRule(
            int maxStacks = DefaultMaxStacks,
            float maxWeight = DefaultMaxWeight,
            int gridWidth = DefaultGridWidth,
            int gridHeight = DefaultGridHeight,
            bool allowRotation = DefaultAllowRotation,
            bool countWeightInCarryLoad = DefaultCountWeightInCarryLoad)
        {
            if (maxStacks < 0 || maxStacks > StackLimit)
            {
                throw new ArgumentOutOfRangeException(nameof(maxStacks), maxStacks, "maxStacks must be between 0 and " + StackLimit);
            }

            if (gridWidth < 0 || gridWidth > MaxGridWidth)
            {
                throw new ArgumentOutOfRangeException(nameof(gridWidth), gridWidth, "gridWidth must be between 0 and " + MaxGridWidth);
            }

            if (gridHeight < 0 || gridHeight > MaxGridHeight)
            {
                throw new ArgumentOutOfRangeException(nameof(gridHeight), gridHeight, "gridHeight must be between 0 and " + MaxGridHeight);
            }

            if (gridWidth == 0 && gridHeight > 0)
            {
                throw new ArgumentException("gridHeight needs a gridWidth", nameof(gridHeight));
            }

            MaxStacks = maxStacks;
            MaxWeight = SpecGuard.NonNegativeFinite(maxWeight, nameof(maxWeight));
            GridWidth = gridWidth;
            GridHeight = gridHeight;
            AllowRotation = allowRotation;
            CountWeightInCarryLoad = countWeightInCarryLoad;
        }

        public int MaxStacks { get; }

        public float MaxWeight { get; }

        public int GridWidth { get; }

        public int GridHeight { get; }

        public bool AllowRotation { get; }

        public bool CountWeightInCarryLoad { get; }

        public bool HasGrid => GridWidth > 0;

        public bool HasFixedGridHeight => GridHeight > 0;

        public int StackCapacity => MaxStacks > 0 ? MaxStacks : StackLimit;
    }
}
