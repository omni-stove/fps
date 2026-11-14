using System;
using Fps.Core;
using UnityEngine;

namespace Fps.Runtime
{
    /// <summary>
    /// ポーズ (<see cref="PauseMenu"/>)・ロードアウトの選択 (<see cref="LoadoutSelectView"/>)・メニューとの行き来 (<see cref="MenuNavigator"/>) を、
    /// ゲーム進行から使うインターフェースとして <see cref="GameUi"/> に既定で登録する。
    /// シーンを読み込む前 (エディターでは読み込み時にも) に登録する。
    /// </summary>
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
        }

        private sealed class PauseMenuState : IPauseState
        {
            public bool IsAnyOpen => PauseMenu.IsAnyOpen;
        }

        private sealed class LoadoutSelectState : ILoadoutSelect
        {
            public bool IsAnyOpen => LoadoutSelectView.IsAnyOpen;
        }

        private sealed class SceneMenuNavigator : IMenuNavigator
        {
            public event Action ReturningToMenu
            {
                add => MenuNavigator.ReturningToMenu += value;
                remove => MenuNavigator.ReturningToMenu -= value;
            }

            public LobbyLaunchOptions TakeLobbyRequest()
            {
                return MenuNavigator.TakeLobbyRequest();
            }

            public bool TakeTutorialRequest()
            {
                return MenuNavigator.TakeTutorialRequest();
            }

            public bool ReturnToMenu(LocalizedText notice)
            {
                // メニューは戻ってから案内を文字列のまま出すので、戻る時点の言語で文にしておく
                return MenuNavigator.ReturnToMenu(notice.IsEmpty ? null : Localizer.Resolve(notice));
            }
        }
    }
}
