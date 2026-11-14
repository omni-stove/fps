using System;
using System.Collections.Generic;

namespace Fps.Core
{
    /// <summary>
    /// メインメニューの遷移先の選び方。ボタンの行き先 (<see cref="MenuDestination"/>) から読むシーンとロビーの設定を決め
    /// (<see cref="Plan(MenuDestination, string, MenuScenes)"/>。シーンのルートを省くと <see cref="KitPaths.DefaultRoot"/>、
    /// シーンの名前 (<see cref="MenuScenes"/>) を省くと下の const の既定の名前)、起動引数でメニューを飛ばすか (<see cref="ShouldSkipMenu"/>) と、ゲームパッドで動かすフォーカスの
    /// 次の位置 (<see cref="MoveFocus"/>) を決める。実際にシーンを読むのは Fps.Runtime の MenuNavigator。
    /// </summary>
    public static class MenuRoute
    {
        public const string MainMenuSceneName = "MainMenu";
        public const string RangeSceneName = "Bootstrap";
        public const string CampaignSceneName = "Campaign01";
        public const string NetSceneName = "NetTest";

        // 以下の既定のシーン経路はテストの属性からも引くので、KitPaths.ScenePath を呼ばずに const で組む。

        /// <summary><see cref="KitPaths.DefaultRoot"/> のメインメニュー。起動時のシーンで、ポーズや切断からもここへ戻る。</summary>
        public const string MainMenuScene = KitPaths.DefaultScenesFolder + "/" + MainMenuSceneName + KitPaths.SceneExtension;

        /// <summary><see cref="KitPaths.DefaultRoot"/> の射撃場。</summary>
        public const string RangeScene = KitPaths.DefaultScenesFolder + "/" + RangeSceneName + KitPaths.SceneExtension;

        /// <summary><see cref="KitPaths.DefaultRoot"/> のキャンペーンの最初のシーン。</summary>
        public const string CampaignScene = KitPaths.DefaultScenesFolder + "/" + CampaignSceneName + KitPaths.SceneExtension;

        /// <summary>
        /// <see cref="KitPaths.DefaultRoot"/> のネット対戦のシーン。Fps.Net の NetBootstrap.ScenePath はこれを引く。
        /// </summary>
        public const string NetScene = KitPaths.DefaultScenesFolder + "/" + NetSceneName + KitPaths.SceneExtension;

        /// <summary>root の下のメインメニュー (<see cref="KitPaths.ScenePath"/>)。</summary>
        public static string MainMenuSceneUnder(string root) => KitPaths.ScenePath(root, MainMenuSceneName);

        /// <summary>root の下の射撃場。</summary>
        public static string RangeSceneUnder(string root) => KitPaths.ScenePath(root, RangeSceneName);

        /// <summary>root の下のキャンペーンの最初のシーン。</summary>
        public static string CampaignSceneUnder(string root) => KitPaths.ScenePath(root, CampaignSceneName);

        /// <summary>root の下のネット対戦のシーン。</summary>
        public static string NetSceneUnder(string root) => KitPaths.ScenePath(root, NetSceneName);

        /// <summary>root の下の、scenes で決めたメインメニュー。</summary>
        public static string MainMenuSceneUnder(string root, MenuScenes scenes) => KitPaths.ScenePath(root, Require(scenes).MainMenu);

        /// <summary>root の下の、scenes で決めた射撃場。</summary>
        public static string RangeSceneUnder(string root, MenuScenes scenes) => KitPaths.ScenePath(root, Require(scenes).Range);

        /// <summary>root の下の、scenes で決めたキャンペーンの最初のシーン。</summary>
        public static string CampaignSceneUnder(string root, MenuScenes scenes) => KitPaths.ScenePath(root, Require(scenes).Campaign);

        /// <summary>root の下の、scenes で決めたネット対戦のシーン。</summary>
        public static string NetSceneUnder(string root, MenuScenes scenes) => KitPaths.ScenePath(root, Require(scenes).Net);

        /// <summary>メニューから選べる行き先。ボタンを並べる順と同じ。</summary>
        public static IReadOnlyList<MenuDestination> Destinations { get; } = new[]
        {
            MenuDestination.Campaign,
            MenuDestination.CreateLobby,
            MenuDestination.JoinLobby,
            MenuDestination.QuickMatch,
            MenuDestination.Range,
            MenuDestination.Tutorial,
            MenuDestination.Quit,
        };

        /// <summary>
        /// destination へ行く遷移。オンラインは <c>-lobby create</c> / <c>-lobby join</c> / <c>-lobby quickmatch</c> を起動引数で渡したときと同じ設定
        /// (最大人数は既定、参加するロビーは検索して選ぶ) で NetTest を読む。
        /// チュートリアルは射撃場を読み、終えたことがあってもチュートリアルを出すよう求める (<see cref="MenuTransition.StartsTutorial"/>)。
        /// </summary>
        public static MenuTransition Plan(MenuDestination destination)
        {
            return Plan(destination, KitPaths.DefaultRoot);
        }

        /// <summary><see cref="Plan(MenuDestination)"/> のシーンを sceneRoot の下から読む遷移。</summary>
        /// <exception cref="ArgumentException">sceneRoot が空か不正な区切りを含む。</exception>
        public static MenuTransition Plan(MenuDestination destination, string sceneRoot)
        {
            return Plan(destination, sceneRoot, MenuScenes.Default);
        }

        /// <summary><see cref="Plan(MenuDestination)"/> のシーンを、scenes で決めた名前で sceneRoot の下から読む遷移。</summary>
        /// <exception cref="ArgumentNullException">scenes が null。</exception>
        /// <exception cref="ArgumentException">sceneRoot が空か不正な区切りを含む。</exception>
        public static MenuTransition Plan(MenuDestination destination, string sceneRoot, MenuScenes scenes)
        {
            Require(scenes);
            switch (destination)
            {
                case MenuDestination.Campaign:
                    return MenuTransition.Load(CampaignSceneUnder(sceneRoot, scenes));
                case MenuDestination.CreateLobby:
                    return MenuTransition.Load(NetSceneUnder(sceneRoot, scenes), LobbyLaunchOptions.Create());
                case MenuDestination.JoinLobby:
                    return MenuTransition.Load(NetSceneUnder(sceneRoot, scenes), LobbyLaunchOptions.Join());
                case MenuDestination.QuickMatch:
                    return MenuTransition.Load(NetSceneUnder(sceneRoot, scenes), LobbyLaunchOptions.QuickMatch());
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

        /// <summary>ポーズや切断からメニューへ戻る遷移。</summary>
        public static MenuTransition BackToMenu()
        {
            return BackToMenu(KitPaths.DefaultRoot);
        }

        /// <summary>sceneRoot の下のメインメニューへ戻る遷移。</summary>
        /// <exception cref="ArgumentException">sceneRoot が空か不正な区切りを含む。</exception>
        public static MenuTransition BackToMenu(string sceneRoot)
        {
            return BackToMenu(sceneRoot, MenuScenes.Default);
        }

        /// <summary>sceneRoot の下の、scenes で決めたメインメニューへ戻る遷移。</summary>
        /// <exception cref="ArgumentNullException">scenes が null。</exception>
        /// <exception cref="ArgumentException">sceneRoot が空か不正な区切りを含む。</exception>
        public static MenuTransition BackToMenu(string sceneRoot, MenuScenes scenes)
        {
            return MenuTransition.Load(MainMenuSceneUnder(sceneRoot, scenes));
        }

        /// <summary>
        /// メニューを出さずに飛ばすか。<see cref="NetStartup.ShouldLoadNetScene"/> と同じ条件 (プレイヤーで <c>-mode</c> か
        /// <c>-lobby</c> が渡された、または専用サーバー) で、そのときは NetBootstrap がネット対戦のシーンへ切り替える。
        /// </summary>
        public static bool ShouldSkipMenu(bool isEditor, bool isDedicatedServer, IReadOnlyList<string> args)
        {
            return NetStartup.ShouldLoadNetScene(isEditor, isDedicatedServer, args);
        }

        /// <summary>
        /// count 個のボタンが並ぶメニューで、current から step だけフォーカスを動かした位置。端では反対側へ回り込む。
        /// current が範囲外 (まだどこにもフォーカスが無い) なら、進む向きなら先頭、戻る向きなら末尾を返す。
        /// </summary>
        /// <returns>次の位置。count が 0 なら -1。</returns>
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
