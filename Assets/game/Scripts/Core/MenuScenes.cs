using System;

namespace Shooter.Core
{
    public sealed class MenuScenes
    {
        public static MenuScenes Default { get; } = new MenuScenes(null, null, null);

        public MenuScenes(string mainMenu, string range, string campaign)
        {
            MainMenu = Resolve(mainMenu, MenuRoute.MainMenuSceneName, nameof(mainMenu));
            Range = Resolve(range, MenuRoute.RangeSceneName, nameof(range));
            Campaign = Resolve(campaign, MenuRoute.CampaignSceneName, nameof(campaign));
        }

        public string MainMenu { get; }

        public string Range { get; }

        public string Campaign { get; }

        public override string ToString()
        {
            return $"mainMenu={MainMenu} range={Range} campaign={Campaign}";
        }

        private static string Resolve(string name, string fallback, string parameterName)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                return fallback;
            }

            var trimmed = name.Trim();
            if (trimmed.Contains('/') || trimmed.Contains('\\'))
            {
                throw new ArgumentException(
                    $"scene name '{name}' must be a plain file name under the Scenes folder, without a folder", parameterName);
            }

            // KitPaths.ScenePath が拡張子を足すので、付けたまま通すと Foo.unity.unity を探してしまう
            if (trimmed.EndsWith(KitPaths.SceneExtension, StringComparison.OrdinalIgnoreCase))
            {
                throw new ArgumentException(
                    $"scene name '{name}' must not end with '{KitPaths.SceneExtension}'", parameterName);
            }

            return trimmed;
        }
    }
}
