using System;
using Shooter.Core;
using UnityEngine;

namespace Shooter.Runtime
{
    public static class DefaultMenuScreens
    {
        // Edit Mode (テストやシーン生成) では Play の初期化が走らないので、エディターの読み込みでも登録する。
#if UNITY_EDITOR
        [UnityEditor.InitializeOnLoadMethod]
#endif
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Register()
        {
            GameUi.UseDefaultPause(new PauseMenuState());
            GameUi.UseDefaultLoadoutSelect(new LoadoutSelectState());
            GameUi.UseDefaultMenu(new SceneMenuNavigator());
            GameUi.UseDefaultInventoryScreen(new InventoryScreenState());
            GameUi.UseDefaultBuyMenuScreen(new BuyMenuScreenState());
            RegisterMainMenuPanels();
        }

        // 口 (GameSignals) がまだ出ていなくても登録する。サーバー一覧・リーダーボード・リプレイは出ていなければ「使えない」を出し、
        // 統計と見た目は記録係が無ければ開くときの例外をメインメニューが知らせに出す。
        public static void RegisterMainMenuPanels()
        {
            MainMenuPanels.UseDefault(MainMenuPanelKind.ServerBrowser, slot => new ServerBrowserPanel(slot));
            MainMenuPanels.UseDefault(MainMenuPanelKind.Stats, StatsPanel.Open);
            MainMenuPanels.UseDefault(MainMenuPanelKind.Leaderboard, slot => new LeaderboardPanel(slot));
            MainMenuPanels.UseDefault(MainMenuPanelKind.Replays, slot => new ReplayPanel(slot));
            MainMenuPanels.UseDefault(MainMenuPanelKind.Cosmetics, CosmeticsPanel.Open);
            MainMenuPanels.UseDefault(MainMenuPanelKind.Stash, StashPanel.Open);
            MainMenuPanels.UseDefault(MainMenuPanelKind.Playlists, PlaylistPanel.Open);
        }

        private sealed class PauseMenuState : IPauseState
        {
            public bool IsAnyOpen => PauseMenu.IsAnyOpen;
        }

        private sealed class LoadoutSelectState : ILoadoutSelect
        {
            private LoadoutSelectView _opened;

            public event Action<LoadoutChoice> Confirmed;

            public bool IsAnyOpen => LoadoutSelectView.IsAnyOpen;

            public bool IsOpen => _opened != null && _opened.IsOpen;

            public void Open(LoadoutSelection selection, AbilitySelection abilities)
            {
                var view = LoadoutSelectView.Active;
                if (view == null)
                {
                    return;
                }

                if (view != _opened)
                {
                    Release();
                    _opened = view;
                    _opened.Confirmed += HandleConfirmed;
                }

                view.Open(selection, abilities);
            }

            public void Close()
            {
                if (_opened != null)
                {
                    _opened.Close();
                }
            }

            public void ShowSending()
            {
                if (_opened != null)
                {
                    _opened.ShowSending();
                }
            }

            public void ShowAnswer(bool accepted, string reason)
            {
                if (_opened != null)
                {
                    _opened.ShowAnswer(accepted, reason);
                }
            }

            private void Release()
            {
                if (!ReferenceEquals(_opened, null))
                {
                    _opened.Confirmed -= HandleConfirmed;
                }

                _opened = null;
            }

            private void HandleConfirmed(LoadoutChoice choice)
            {
                Confirmed?.Invoke(choice);
            }
        }

        private sealed class InventoryScreenState : IInventoryScreen
        {
            public bool IsAnyOpen => InventoryView.IsAnyOpen;
        }

        private sealed class BuyMenuScreenState : IBuyMenuScreen
        {
            public bool IsAnyOpen => BuyMenuView.IsAnyOpen;
        }

        private sealed class SceneMenuNavigator : IMenuNavigator
        {
            public event Action ReturningToMenu
            {
                add => MenuNavigator.ReturningToMenu += value;
                remove => MenuNavigator.ReturningToMenu -= value;
            }

            public bool TakeTutorialRequest()
            {
                return MenuNavigator.TakeTutorialRequest();
            }

            public bool ReturnToMenu(MenuReturnReason reason)
            {
                return MenuNavigator.ReturnToMenu(reason);
            }
        }
    }
}
