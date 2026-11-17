using System;
using Shooter.Core;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Shooter.Runtime
{
    public static class MenuNavigator
    {
        public const string LogTag = "[Menu]";

        private static IMenuSceneHost _host = SceneManagerHost.Instance;
        private static MenuReturnReason _returnReason;
        private static bool _pendingTutorial;
        private static MenuScenes _scenes;
        private static string _sceneRoot;

        public static event Action ReturningToMenu;

        public static IMenuSceneHost Host
        {
            get => _host;
            set => _host = value ?? SceneManagerHost.Instance;
        }

        public static MenuScenes Scenes
        {
            get => _scenes ??= MenuSceneSettings.LoadScenes();
            set => _scenes = value;
        }

        public static string SceneRoot
        {
            get => _sceneRoot ??= MenuSceneSettings.LoadSceneRoot();
            set => _sceneRoot = value;
        }

        public static MenuTransition Plan(MenuDestination destination)
        {
            return MenuRoute.Plan(destination, SceneRoot, Scenes);
        }

        public static bool PendingTutorial => _pendingTutorial;

        public static MenuReturnReason PendingReturnReason => _returnReason;

        public static bool Go(MenuDestination destination)
        {
            return Go(destination, out _);
        }

        public static bool Go(MenuDestination destination, out LocalizedText refusal)
        {
            refusal = default;
            if (IsOnline(destination))
            {
                var starter = GameUi.OnlineStarter;
                if (starter == null)
                {
                    Debug.LogError(LogTag + " " + destination + ": no online client is installed (is the Shooter.NetV2 assembly in the build?)");
                    return false;
                }

                Debug.Log(LogTag + " " + destination + " -> " + starter.GetType().Name);
                var started = starter.Start(destination, out var reason);
                if (!started)
                {
                    Debug.Log(LogTag + " " + destination + " refused" + (reason != null ? " (" + reason.Describe() + ")" : ""));
                    refusal = DisconnectNoticeText.For(reason);
                }

                return started;
            }

            return Execute(Plan(destination));
        }

        public static bool IsOnline(MenuDestination destination)
        {
            return destination == MenuDestination.QuickMatch;
        }

        // beforeLoad (「最初から」でセーブを消すなど) は、シーンが Build Settings にあると確かめてから呼ぶ。先に呼ぶと、始められないままセーブだけ消える。
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
                LogMissingScene(entry.ScenePath, "Shooter.Editor.CampaignSceneBuilder.Build");
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

        public static bool ReturnToMenu(MenuReturnReason reason = null)
        {
            var transition = MenuRoute.BackToMenu(SceneRoot, Scenes);
            var buildIndex = _host.BuildIndexOf(transition.ScenePath);
            if (buildIndex < 0)
            {
                LogMissingScene(transition.ScenePath, "Shooter.Editor.MenuSceneBuilder.Build");
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
            _returnReason = reason;

            Debug.Log(LogTag + " back to the menu" + (_returnReason == null ? "" : " (" + _returnReason.Describe() + ")"));
            _host.Load(buildIndex);
            return true;
        }

        public static bool TakeTutorialRequest()
        {
            var requested = _pendingTutorial;
            _pendingTutorial = false;
            return requested;
        }

        public static MenuReturnReason TakeReturnReason()
        {
            var reason = _returnReason;
            _returnReason = null;
            return reason;
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
                + " (or Shooter/Register Build Scenes) and rebuild");
        }

        // Enter Play Mode でドメインを読み直さない設定でも、前の Play の要求や購読を持ち越さない。
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            _host = SceneManagerHost.Instance;
            _pendingTutorial = false;
            _returnReason = null;
            _scenes = null;
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
