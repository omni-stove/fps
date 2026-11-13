using System;
using Fps.Core;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Fps.Runtime
{
    /// <summary>
    /// メインメニューと他のシーンの行き来をまとめる。メニューのボタンは <see cref="Go"/>、ポーズや切断から戻る画面は
    /// <see cref="ReturnToMenu"/> を呼ぶ。どのシーンを読むかは <see cref="MenuRoute"/> が決め、ここは Build Settings から番号を引いて読む。
    /// オンラインを選んだときのロビーの設定は、NetTest の NetBootstrap が <see cref="TakeLobbyRequest"/> で受け取る
    /// (起動引数の <c>-lobby</c> と同じ扱いになる)。
    /// </summary>
    public static class MenuNavigator
    {
        public const string LogTag = "[Menu]";

        private static IMenuSceneHost _host = SceneManagerHost.Instance;
        private static LobbyLaunchOptions _pendingLobby = LobbyLaunchOptions.None;
        private static string _notice;

        /// <summary>
        /// <see cref="ReturnToMenu"/> でメニューのシーンを読む直前に呼ぶ。シーンをまたいで残るもの (NetworkManager など) は
        /// ここで片付ける。購読した側の例外はログに出して、他の購読者と遷移は続ける。
        /// </summary>
        public static event Action ReturningToMenu;

        /// <summary>シーンを読む相手。null を入れると SceneManager を使う既定に戻す (テストで差し替える)。</summary>
        public static IMenuSceneHost Host
        {
            get => _host;
            set => _host = value ?? SceneManagerHost.Instance;
        }

        /// <summary>NetBootstrap にまだ渡していない、メニューで選んだロビーの設定。無ければ <see cref="LobbyLaunchOptions.None"/>。</summary>
        public static LobbyLaunchOptions PendingLobby => _pendingLobby;

        /// <summary>メニューに戻ったときに出す案内 (切断の理由など)。無ければ null。</summary>
        public static string PendingNotice => _notice;

        /// <summary>
        /// destination へ進む。読むシーンが Build Settings に無ければエラーログを出して false を返し、何もしない。
        /// </summary>
        public static bool Go(MenuDestination destination)
        {
            return Execute(MenuRoute.Plan(destination));
        }

        /// <summary>
        /// メニューへ戻る。<see cref="ReturningToMenu"/> を呼び、止めていた時間とカーソルを戻してから MainMenu を読む。
        /// MainMenu が Build Settings に無ければエラーログを出して false を返し、今のシーンに留まる。
        /// </summary>
        /// <param name="notice">メニューに出す案内。null なら出さない。</param>
        public static bool ReturnToMenu(string notice = null)
        {
            var transition = MenuRoute.BackToMenu();
            var buildIndex = _host.BuildIndexOf(transition.ScenePath);
            if (buildIndex < 0)
            {
                LogMissingScene(transition.ScenePath, "Fps.Editor.MenuSceneBuilder.Build");
                return false;
            }

            var handlers = ReturningToMenu;
            if (handlers != null)
            {
                foreach (Action handler in handlers.GetInvocationList())
                {
                    try
                    {
                        handler();
                    }
                    catch (Exception e)
                    {
                        // 片付けの 1 つが失敗しても、メニューに戻れずに止まるよりは戻るほうがよい
                        Debug.LogException(e);
                    }
                }
            }

            Time.timeScale = 1f;
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
            _pendingLobby = LobbyLaunchOptions.None;
            _notice = string.IsNullOrWhiteSpace(notice) ? null : notice;

            Debug.Log(LogTag + " back to the menu" + (_notice == null ? "" : " (" + _notice + ")"));
            _host.Load(buildIndex);
            return true;
        }

        /// <summary>メニューで選んだロビーの設定を受け取り、持っていた分は消す。無ければ <see cref="LobbyLaunchOptions.None"/>。</summary>
        public static LobbyLaunchOptions TakeLobbyRequest()
        {
            var lobby = _pendingLobby;
            _pendingLobby = LobbyLaunchOptions.None;
            return lobby;
        }

        /// <summary>メニューに出す案内を受け取り、持っていた分は消す。無ければ null。</summary>
        public static string TakeNotice()
        {
            var notice = _notice;
            _notice = null;
            return notice;
        }

        private static bool Execute(MenuTransition transition)
        {
            if (transition.Kind == MenuTransitionKind.Quit)
            {
                Debug.Log(LogTag + " quit");
                _host.Quit();
                return true;
            }

            var buildIndex = _host.BuildIndexOf(transition.ScenePath);
            if (buildIndex < 0)
            {
                LogMissingScene(transition.ScenePath, "its scene builder");
                return false;
            }

            _pendingLobby = transition.Lobby;
            Debug.Log(LogTag + " " + transition);
            _host.Load(buildIndex);
            return true;
        }

        private static void LogMissingScene(string path, string builder)
        {
            Debug.LogError(LogTag + " " + path + " is not in the build settings; run " + builder
                + " (or Fps/Register Build Scenes) and rebuild");
        }

        /// <summary>Enter Play Mode でドメインを読み直さない設定でも、前の Play の要求や購読を持ち越さない。</summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            _host = SceneManagerHost.Instance;
            _pendingLobby = LobbyLaunchOptions.None;
            _notice = null;
            ReturningToMenu = null;
        }

        private sealed class SceneManagerHost : IMenuSceneHost
        {
            public static readonly SceneManagerHost Instance = new SceneManagerHost();

            public int BuildIndexOf(string path)
            {
                return SceneUtility.GetBuildIndexByScenePath(path);
            }

            public void Load(int buildIndex)
            {
                SceneManager.LoadScene(buildIndex, LoadSceneMode.Single);
            }

            public void Quit()
            {
#if UNITY_EDITOR
                // Application.Quit はエディタでは何もしないので、Play Mode を止めて終わったことにする
                UnityEditor.EditorApplication.isPlaying = false;
#else
                Application.Quit();
#endif
            }
        }
    }
}
