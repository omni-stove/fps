using Shooter.Core;

namespace Shooter.Runtime
{
    public static class AmmoCratePrompt
    {
        public const string PromptKey = "hud.ammoCrate.prompt";

        public const string FullKey = "hud.ammoCrate.full";

        public static LocalizedText Describe(AmmoCrateHint hint, string keyLabel)
        {
            return hint.HasRoom ? new LocalizedText(PromptKey, keyLabel ?? string.Empty) : new LocalizedText(FullKey);
        }
    }
}
