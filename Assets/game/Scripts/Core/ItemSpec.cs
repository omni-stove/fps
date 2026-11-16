using System;

namespace Fps.Core
{
    public sealed class ItemSpec
    {
        public const int DefaultMaxStack = 1;
        public const int DefaultMaxCarried = 0;
        public const float DefaultWeight = 0f;
        public const int DefaultWidth = 1;
        public const int DefaultHeight = 1;
        public const int DefaultUseHealth = 0;
        public const int DefaultUseArmor = 0;

        // 持ち物を送る電文が 1 山の数を ushort で送る
        public const int MaxStackLimit = ushort.MaxValue;

        public const int MaxSize = InventoryRule.MaxGridWidth;

        public ItemSpec(
            string id,
            string nameKey = null,
            string iconId = null,
            int maxStack = DefaultMaxStack,
            int maxCarried = DefaultMaxCarried,
            float weight = DefaultWeight,
            int width = DefaultWidth,
            int height = DefaultHeight,
            int useHealth = DefaultUseHealth,
            int useArmor = DefaultUseArmor)
        {
            Id = SpecGuard.LowercaseUuid(id, nameof(id));
            NameKey = SpecGuard.NameKey(nameKey);
            IconId = SpecGuard.ReferenceId(iconId);
            MaxStack = Between(maxStack, 1, MaxStackLimit, nameof(maxStack));
            MaxCarried = SpecGuard.NonNegative(maxCarried, nameof(maxCarried));
            Weight = SpecGuard.NonNegativeFinite(weight, nameof(weight));
            Width = Between(width, 1, MaxSize, nameof(width));
            Height = Between(height, 1, MaxSize, nameof(height));
            UseHealth = SpecGuard.NonNegative(useHealth, nameof(useHealth));
            UseArmor = SpecGuard.NonNegative(useArmor, nameof(useArmor));
        }

        public string Id { get; }

        public string NameKey { get; }

        public string IconId { get; }

        public int MaxStack { get; }

        public int MaxCarried { get; }

        public float Weight { get; }

        public int Width { get; }

        public int Height { get; }

        public int UseHealth { get; }

        public int UseArmor { get; }

        public bool IsUsable => UseHealth > 0 || UseArmor > 0;

        public int WidthWhen(bool rotated) => rotated ? Height : Width;

        public int HeightWhen(bool rotated) => rotated ? Width : Height;

        private static int Between(int value, int min, int max, string name)
        {
            if (value < min || value > max)
            {
                throw new ArgumentOutOfRangeException(name, value, name + " must be between " + min + " and " + max);
            }

            return value;
        }
    }
}
