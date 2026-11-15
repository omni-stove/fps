using System;
using Fps.Core;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Fps.Runtime
{
    /// <summary>
    /// メインメニューと他のシーンの行き来をまとめる。メニューのボタンは <see cref="Go"/>、ポーズや切断から戻る画面は
    /// <see cref="ReturnToMenu"/>、キャンペーンの選択画面は <see cref="StartCampaign"/> を呼ぶ。どのシーンを読むかは <see cref="MenuRoute"/> が決め、ここは Build Settings から番号を引いて読む。
    /// オンラインを選んだときは、シーンを読まずに <see cref="OnlineStarter"/> (ネット対戦のクライアント) に任せる。
    /// チュートリアルを選んだことは、射撃場の <see cref="TutorialPresenter"/> が <see cref="TakeTutorialRequest"/> で受け取る。
    /// </summary>
    public static class MenuNavigator
    {
        public const string LogTag = "[Menu]";

        private static IMenuSceneHost _host = SceneManagerHost.Instance;
        private static LocalizedText _notice;
        private static bool _pendingTutorial;
        private static MenuScenes _scenes;
        private static string _sceneRoot;

        /// <summary>
        /// <see cref="ReturnToMenu"/> でメニューのシーンを読む直前に呼ぶ。シーンをまたいで残るもの (ネット対戦のクライアントなど) は
        /// ここで片付ける。購読した側の例外はログに出して、他の購読者と遷移は続ける。
        /// </summary>
        public static event Action ReturningToMenu;

        /// <summary>シーンを読む相手。null を入れると SceneManager を使う既定に戻す (テストで差し替える)。</summary>
        public static IMenuSceneHost Host
        {
            get => _host;
            set => _host = value ?? SceneManagerHost.Instance;
        }

        /// <summary>
        /// 移るシーンの名前。初めて使うときに <see cref="MenuSceneSettings.LoadScenes"/> で Resources の設定から決める (無ければ既定の名前)。
        /// null を入れると次に使うときに設定から決め直す (テストで差し替える)。
        /// </summary>
        public static MenuScenes Scenes
        {
            get => _scenes ??= MenuSceneSettings.LoadScenes();
            set => _scenes = value;
        }

        /// <summary>
        /// シーンを読むルート。初めて使うときに <see cref="MenuSceneSettings.LoadSceneRoot"/> で Resources の設定から決める
        /// (無ければ <see cref="KitPaths.DefaultRoot"/>)。null を入れると次に使うときに設定から決め直す (テストで差し替える)。
        /// </summary>
        public static string SceneRoot
        {
            get => _sceneRoot ??= MenuSceneSettings.LoadSceneRoot();
            set => _sceneRoot = value;
        }

        /// <summary><see cref="Scenes"/> の名前で、<see cref="SceneRoot"/> の下から destination へ行く遷移。</summary>
        public static MenuTransition Plan(MenuDestination destination)
        {
            return MenuRoute.Plan(destination, SceneRoot, Scenes);
        }

        /// <summary>射撃場にまだ渡していない、メニューでチュートリアルを選んだこと。</summary>
        public static bool PendingTutorial => _pendingTutorial;

        /// <summary>メニューに戻ったときに出す案内 (切断の理由など)。無ければ <c>default</c>。</summary>
        public static LocalizedText PendingNotice => _notice;

        /// <summary>
        /// オンラインのボタンを始める手順。ネット対戦のクライアント (Fps.NetV2) が起動時に入れる。null ならオンラインは始められない。
        /// </summary>
        public static IOnlineMatchStarter OnlineStarter { get; set; }

        /// <summary>
        /// destination へ進む。読むシーンが Build Settings に無ければエラーログを出して false を返し、何もしない。
        /// </summary>
        public static bool Go(MenuDestination destination)
        {
            return Go(destination, out _);
        }

        /// <summary>
        /// destination へ進む。オンラインの行き先は <see cref="OnlineStarter"/> に任せ、断られたか無ければ false と理由 refusal を返す。
        /// シーンへ移る行き先で読むシーンが Build Settings に無ければ、エラーログを出して false (refusal は空) を返す。
        /// </summary>
        public static bool Go(MenuDestination destination, out LocalizedText refusal)
        {
            refusal = default;
            if (IsOnline(destination))
            {
                var starter = OnlineStarter;
                if (starter == null)
                {
                    Debug.LogError(LogTag + " " + destination + ": no online client is installed (is the Fps.NetV2 assembly in the build?)");
                    return false;
                }

                Debug.Log(LogTag + " " + destination + " -> " + starter.GetType().Name);
                return starter.Start(destination, out refusal);
            }

            return Execute(Plan(destination));
        }

        /// <summary>オンラインの試合を始める行き先か。</summary>
        public static bool IsOnline(MenuDestination destination)
        {
            return destination == MenuDestination.QuickMatch;
        }

        /// <summary>
        /// catalog の launch のミッションを、その難易度で始める。シーンが Build Settings に入っていることを確かめてから beforeLoad
        /// (「最初から」でセーブを消すなど) を呼び、<see cref="CampaignLaunchSelection.Launch"/> で選択を入れてシーンを読む。
        /// シーンが Build Settings に無ければエラーログを出して false を返し、beforeLoad も呼ばずに何もしない。
        /// </summary>
        /// <exception cref="MissionCatalogException">catalog が不正。</exception>
        /// <exception cref="ArgumentException">launch のミッションが catalog に無い。</exception>
        /// <remarks>beforeLoad が投げた例外はそのまま投げ、選択もシーンも変えない。</remarks>
        public static bool StartCampaign(MissionCatalog catalog, CampaignLaunch launch, Action beforeLoad = null)
        {
            if (catalog == null)
            {
                throw new ArgumentNullException(nameof(catalog));
            }

            if (launch == null)
            {
                throw new ArgumentNullException(nameof(launch));
            }

            if (!catalog.TryFind(launch.MissionId, out var entry))
            {
                throw new ArgumentException($"mission '{launch.MissionId}' is not in mission catalog '{catalog.name}'", nameof(launch));
            }

            var buildIndex = _host.BuildIndexOf(entry.ScenePath);
            if (buildIndex < 0)
            {
                LogMissingScene(entry.ScenePath, "Fps.Editor.CampaignSceneBuilder.Build");
                return false;
            }

            beforeLoad?.Invoke();
            CampaignLaunchSelection.Launch(catalog, launch.MissionId, launch.Difficulty, path =>
            {
                _pendingTutorial = false;
                Debug.Log(LogTag + " campaign " + launch + " -> " + path);
                _host.Load(buildIndex);
            });
            return true;
        }

        /// <summary>
        /// メニューへ戻る。<see cref="ReturningToMenu"/> を呼び、止めていた時間とカーソルを戻してから MainMenu を読む。
        /// MainMenu が Build Settings に無ければエラーログを出して false を返し、今のシーンに留まる。
        /// </summary>
        /// <param name="notice">
        /// メニューに出す案内。メニューが出すときに、その時点の言語で文にする。<c>default</c> か、表を引かない空白だけの文なら出さない。
        /// </param>
        public static bool ReturnToMenu(LocalizedText notice = default)
        {
            var transition = MenuRoute.BackToMenu(SceneRoot, Scenes);
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
            _pendingTutorial = false;
            _notice = notice.IsLiteral && string.IsNullOrWhiteSpace(notice.Key) ? default : notice;

            Debug.Log(LogTag + " back to the menu" + (_notice.IsEmpty ? "" : " (" + _notice + ")"));
            _host.Load(buildIndex);
            return true;
        }

        /// <summary>メニューでチュートリアルを選んだかを受け取り、持っていた分は消す。</summary>
        public static bool TakeTutorialRequest()
        {
            var requested = _pendingTutorial;
            _pendingTutorial = false;
            return requested;
        }

        /// <summary>メニューに出す案内を受け取り、持っていた分は消す。無ければ <c>default</c>。</summary>
        public static LocalizedText TakeNotice()
        {
            var notice = _notice;
            _notice = default;
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

            _pendingTutorial = transition.StartsTutorial;
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
            _pendingTutorial = false;
            _notice = default;
            _scenes = null;
            ReturningToMenu = null;
            OnlineStarter = null;
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
