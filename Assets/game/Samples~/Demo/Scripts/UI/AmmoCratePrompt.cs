using Shooter.Core;

namespace Shooter.Runtime
{
    /// <summary>弾薬箱の近くで出す案内 (<see cref="AmmoCrateHint"/>) の文。</summary>
    public static class AmmoCratePrompt
    {
        /// <summary>使える間の案内の文のキー。{0} は Interact のキー。</summary>
        public const string PromptKey = "hud.ammoCrate.prompt";

        /// <summary>どの武器も満タンの間の案内の文のキー。</summary>
        public const string FullKey = "hud.ammoCrate.full";

        /// <summary>hint の案内の文。keyLabel は Interact のキーの表示で、null は空文字にする。</summary>
        public static LocalizedText Describe(AmmoCrateHint hint, string keyLabel)
        {
            return hint.HasRoom ? new LocalizedText(PromptKey, keyLabel ?? string.Empty) : new LocalizedText(FullKey);
        }
    }
}
