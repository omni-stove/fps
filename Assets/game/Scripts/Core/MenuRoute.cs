using System;
using System.Collections.Generic;

namespace Fps.Core
{
    /// <summary>
    /// メインメニューの遷移先の選び方。ボタンの行き先 (<see cref="MenuDestination"/>) から読むシーンとロビーの設定を決め
    /// (<see cref="Plan"/>)、起動引数でメニューを飛ばすか (<see cref="ShouldSkipMenu"/>) と、ゲームパッドで動かすフォーカスの
    /// 次の位置 (<see cref="MoveFocus"/>) を決める。実際にシーンを読むのは Fps.Runtime の MenuNavigator。
    /// </summary>
    public static class MenuRoute
    {
        /// <summary>メインメニュー。起動時のシーンで、ポーズや切断からもここへ戻る。</summary>
        public const string MainMenuScene = "Assets/game/Scenes/MainMenu.unity";

        /// <summary>射撃場。</summary>
        public const string RangeScene = "Assets/game/Scenes/Bootstrap.unity";

        /// <summary>キャンペーンの最初のシーン。</summary>
        public const string CampaignScene = "Assets/game/Scenes/Campaign01.unity";

        /// <summary>
        /// ネット対戦のシーン。Fps.Net の NetBootstrap.ScenePath と同じ値で、Fps.Core は Fps.Net を参照しないので文字列で持つ
        /// (一致はテストで確かめる)。
        /// </summary>
        public const string NetScene = "Assets/game/Scenes/NetTest.unity";

        /// <summary>メニューから選べる行き先。ボタンを並べる順と同じ。</summary>
        public static IReadOnlyList<MenuDestination> Destinations { get; } = new[]
        {
            MenuDestination.Campaign,
            MenuDestination.CreateLobby,
            MenuDestination.JoinLobby,
            MenuDestination.Range,
            MenuDestination.Quit,
        };

        /// <summary>
        /// destination へ行く遷移。オンラインは <c>-lobby create</c> / <c>-lobby join</c> を起動引数で渡したときと同じ設定
        /// (最大人数は既定、参加するロビーは検索して選ぶ) で NetTest を読む。
        /// </summary>
        public static MenuTransition Plan(MenuDestination destination)
        {
            switch (destination)
            {
                case MenuDestination.Campaign:
                    return MenuTransition.Load(CampaignScene);
                case MenuDestination.CreateLobby:
                    return MenuTransition.Load(NetScene, LobbyLaunchOptions.Create());
                case MenuDestination.JoinLobby:
                    return MenuTransition.Load(NetScene, LobbyLaunchOptions.Join());
                case MenuDestination.Range:
                    return MenuTransition.Load(RangeScene);
                case MenuDestination.Quit:
                    return MenuTransition.Quit;
                default:
                    throw new ArgumentOutOfRangeException(nameof(destination), destination, "unknown menu destination");
            }
        }

        /// <summary>ポーズや切断からメニューへ戻る遷移。</summary>
        public static MenuTransition BackToMenu()
        {
            return MenuTransition.Load(MainMenuScene);
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
    }
}
