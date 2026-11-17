using System;
using Shooter.Core;
using UnityEngine;

namespace Shooter.Runtime
{
    [CreateAssetMenu(fileName = "MenuSceneSettings", menuName = "Shooter/Menu Scene Settings")]
    public sealed class MenuSceneSettings : ScriptableObject
    {
        public const string ResourcePath = "Menu/MenuSceneSettings";

        public const string LogTag = "[Menu]";

        [SerializeField, Tooltip("起動時とポーズ・切断から戻るメインメニューのシーン名 (Scenes フォルダの下、拡張子なし)。空なら MainMenu")]
        private string _mainMenu = MenuRoute.MainMenuSceneName;

        [SerializeField, Tooltip("射撃場とチュートリアルのシーン名。空なら Bootstrap")]
        private string _range = MenuRoute.RangeSceneName;

        [SerializeField, Tooltip("メニューのキャンペーンで読む最初のシーン名。空なら Campaign01")]
        private string _campaign = MenuRoute.CampaignSceneName;

        [SerializeField, Tooltip("上のシーンを置いたルートのフォルダ (その下の Scenes/<名前>.unity を読む)。空なら Assets/game")]
        private string _sceneRoot = string.Empty;

        public string MainMenu
        {
            get => _mainMenu;
            set => _mainMenu = value;
        }

        public string Range
        {
            get => _range;
            set => _range = value;
        }

        public string Campaign
        {
            get => _campaign;
            set => _campaign = value;
        }

        public string SceneRoot
        {
            get => _sceneRoot;
            set => _sceneRoot = value;
        }

        public static MenuSceneSettings Load()
        {
            var fromContent = GameContent.Pick(c => c.MenuScenes);
            return fromContent != null ? fromContent : Resources.Load<MenuSceneSettings>(ResourcePath);
        }

        public static MenuScenes LoadScenes()
        {
            var settings = Load();
            if (settings == null)
            {
                return MenuScenes.Default;
            }

            try
            {
                var scenes = settings.ToScenes();
                Debug.Log($"{LogTag} scenes from '{settings.name}': {scenes}", settings);
                return scenes;
            }
            catch (MenuSceneSettingsException e)
            {
                // 遷移先が決まらずメニューから出られなくなるよりは、既定のシーンで動かして設定の誤りをログで知らせる
                Debug.LogError($"{LogTag} {e.Message}; using the default scenes ({MenuScenes.Default})", settings);
                return MenuScenes.Default;
            }
        }

        public static string LoadSceneRoot()
        {
            var settings = Load();
            if (settings == null)
            {
                return KitPaths.DefaultRoot;
            }

            try
            {
                return settings.ToSceneRoot();
            }
            catch (MenuSceneSettingsException e)
            {
                Debug.LogError($"{LogTag} {e.Message}; using the default scene root ({KitPaths.DefaultRoot})", settings);
                return KitPaths.DefaultRoot;
            }
        }

        public string ToSceneRoot()
        {
            if (string.IsNullOrWhiteSpace(_sceneRoot))
            {
                return KitPaths.DefaultRoot;
            }

            try
            {
                return KitPaths.NormalizeRoot(_sceneRoot);
            }
            catch (ArgumentException e)
            {
                var source = string.IsNullOrEmpty(name) ? "(unnamed menu scene settings)" : name;
                throw new MenuSceneSettingsException($"menu scene settings '{source}': {e.Message}", e);
            }
        }

        public MenuScenes ToScenes()
        {
            try
            {
                return new MenuScenes(_mainMenu, _range, _campaign);
            }
            catch (ArgumentException e)
            {
                var source = string.IsNullOrEmpty(name) ? "(unnamed menu scene settings)" : name;
                throw new MenuSceneSettingsException($"menu scene settings '{source}': {e.Message}", e);
            }
        }

        public void ResetToDefaults()
        {
            _mainMenu = MenuRoute.MainMenuSceneName;
            _range = MenuRoute.RangeSceneName;
            _campaign = MenuRoute.CampaignSceneName;
            _sceneRoot = string.Empty;
        }
    }

    public sealed class MenuSceneSettingsException : Exception
    {
        public MenuSceneSettingsException(string message, Exception inner)
            : base(message, inner)
        {
        }
    }
}
