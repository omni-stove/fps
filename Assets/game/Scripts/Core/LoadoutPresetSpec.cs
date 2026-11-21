namespace Shooter.Core
{
    public sealed class LoadoutPresetSpec
    {
        public const int DefaultMaxPresets = 5;

        public const int DefaultMaxNameLength = 24;

        public static LoadoutPresetSpec Default { get; } = new LoadoutPresetSpec();

        // 保存しておけるロードアウトの組の数
        public int MaxPresets { get; }

        // 組に付ける名前の長さ (文字数) の上限
        public int MaxNameLength { get; }

        public LoadoutPresetSpec(int maxPresets = DefaultMaxPresets, int maxNameLength = DefaultMaxNameLength)
        {
            MaxPresets = SpecGuard.AtLeast(maxPresets, 1, nameof(maxPresets));
            MaxNameLength = SpecGuard.AtLeast(maxNameLength, 1, nameof(maxNameLength));
        }
    }
}
