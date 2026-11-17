using System;
using System.Collections.Generic;

namespace Shooter.Core
{
    public static class MenuRoute
    {
        public const string MainMenuSceneName = "MainMenu";
        public const string RangeSceneName = "Bootstrap";
        public const string CampaignSceneName = "Campaign01";

        public const string NetSceneName = "NetTest";

        // 以下の既定のシーン経路はテストの属性からも引くので、KitPaths.ScenePath を呼ばずに const で組む。

        public const string MainMenuScene = KitPaths.DefaultScenesFolder + "/" + MainMenuSceneName + KitPaths.SceneExtension;

        public const string RangeScene = KitPaths.DefaultScenesFolder + "/" + RangeSceneName + KitPaths.SceneExtension;

        public const string CampaignScene = KitPaths.DefaultScenesFolder + "/" + CampaignSceneName + KitPaths.SceneExtension;

        public static string MainMenuSceneUnder(string root) => KitPaths.ScenePath(root, MainMenuSceneName);

        public static string RangeSceneUnder(string root) => KitPaths.ScenePath(root, RangeSceneName);

        public static string CampaignSceneUnder(string root) => KitPaths.ScenePath(root, CampaignSceneName);

        public static string MainMenuSceneUnder(string root, MenuScenes scenes) => KitPaths.ScenePath(root, Require(scenes).MainMenu);

        public static string RangeSceneUnder(string root, MenuScenes scenes) => KitPaths.ScenePath(root, Require(scenes).Range);

        public static string CampaignSceneUnder(string root, MenuScenes scenes) => KitPaths.ScenePath(root, Require(scenes).Campaign);

        public static IReadOnlyList<MenuDestination> Destinations { get; } = new[]
        {
            MenuDestination.Campaign,
            MenuDestination.QuickMatch,
            MenuDestination.Range,
            MenuDestination.Tutorial,
            MenuDestination.Quit,
        };

        public static MenuTransition Plan(MenuDestination destination)
        {
            return Plan(destination, KitPaths.DefaultRoot);
        }

        public static MenuTransition Plan(MenuDestination destination, string sceneRoot)
        {
            return Plan(destination, sceneRoot, MenuScenes.Default);
        }

        public static MenuTransition Plan(MenuDestination destination, string sceneRoot, MenuScenes scenes)
        {
            Require(scenes);
            switch (destination)
            {
                case MenuDestination.Campaign:
                    return MenuTransition.Load(CampaignSceneUnder(sceneRoot, scenes));
                case MenuDestination.QuickMatch:
                    throw new ArgumentException("online destinations are started by the online client, not by loading a scene", nameof(destination));
                case MenuDestination.Range:
                    return MenuTransition.Load(RangeSceneUnder(sceneRoot, scenes));
                case MenuDestination.Tutorial:
                    return MenuTransition.Load(RangeSceneUnder(sceneRoot, scenes), startsTutorial: true);
                case MenuDestination.Quit:
                    return MenuTransition.Quit;
                default:
                    throw new ArgumentOutOfRangeException(nameof(destination), destination, "unknown menu destination");
            }
        }

        public static MenuTransition BackToMenu()
        {
            return BackToMenu(KitPaths.DefaultRoot);
        }

        public static MenuTransition BackToMenu(string sceneRoot)
        {
            return BackToMenu(sceneRoot, MenuScenes.Default);
        }

        public static MenuTransition BackToMenu(string sceneRoot, MenuScenes scenes)
        {
            return MenuTransition.Load(MainMenuSceneUnder(sceneRoot, scenes));
        }

        public static int MoveFocus(int current, int step, int count)
        {
            if (count < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(count), count, "count must not be negative");
            }

            if (count == 0)
            {
                return -1;
            }

            if (current < 0 || current >= count)
            {
                return step < 0 ? count - 1 : 0;
            }

            var next = (current + step) % count;
            return next < 0 ? next + count : next;
        }

        private static MenuScenes Require(MenuScenes scenes)
        {
            return scenes ?? throw new ArgumentNullException(nameof(scenes));
        }
    }
}
