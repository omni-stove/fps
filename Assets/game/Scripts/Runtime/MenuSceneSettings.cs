using System;
using Fps.Core;
using UnityEngine;

namespace Fps.Runtime
{
    /// <summary>
    /// メニューから移るシーンの名前を、ゲームが <see cref="MenuRoute"/> の既定から変えるためのデータ。
    /// Resources の <see cref="ResourcePath"/> に置くと、メニューの遷移 (MenuNavigator) とネット対戦の起動 (NetBootstrap) がこの名前を読む。
    /// 置かなければ、また空にした欄は既定の名前 (<see cref="MenuScenes.Default"/>) を使う。
    /// </summary>
    [CreateAssetMenu(fileName = "MenuSceneSettings", menuName = "Fps/Menu Scene Settings")]
    public sealed class MenuSceneSettings : ScriptableObject
    {
        /// <summary>メインメニューのレイアウト (Resources の Menu/MainMenu) と同じフォルダ。</summary>
        public const string ResourcePath = "Menu/MenuSceneSettings";

        public const string LogTag = "[Menu]";

        [SerializeField, Tooltip("起動時とポーズ・切断から戻るメインメニューのシーン名 (Scenes フォルダの下、拡張子なし)。空なら MainMenu")]
        private string _mainMenu = MenuRoute.MainMenuSceneName;

        [SerializeField, Tooltip("射撃場とチュートリアルのシーン名。空なら Bootstrap")]
        private string _range = MenuRoute.RangeSceneName;

        [SerializeField, Tooltip("メニューのキャンペーンで読む最初のシーン名。空なら Campaign01")]
        private string _campaign = MenuRoute.CampaignSceneName;

        [SerializeField, Tooltip("オンラインと -mode / -lobby の起動で読むネット対戦のシーン名。空なら NetTest")]
        private string _net = MenuRoute.NetSceneName;

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

        public string Net
        {
            get => _net;
            set => _net = value;
        }

        /// <summary>Resources の <see cref="ResourcePath"/> の設定。無ければ null。</summary>
        public static MenuSceneSettings Load()
        {
            return Resources.Load<MenuSceneSettings>(ResourcePath);
        }

        /// <summary>
        /// Resources の設定から決めたシーンの名前。設定が無ければ <see cref="MenuScenes.Default"/>。
        /// 設定が不正ならエラーログを出して <see cref="MenuScenes.Default"/> を返す。
        /// </summary>
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
                Debug.Log($"{LogTag} scenes from Resources/{ResourcePath}: {scenes}", settings);
                return scenes;
            }
            catch (MenuSceneSettingsException e)
            {
                // 遷移先が決まらずメニューから出られなくなるよりは、既定のシーンで動かして設定の誤りをログで知らせる
                Debug.LogError($"{LogTag} {e.Message}; using the default scenes ({MenuScenes.Default})", settings);
                return MenuScenes.Default;
            }
        }

        /// <summary>この設定のシーンの名前。空の欄は既定の名前にする。</summary>
        /// <exception cref="MenuSceneSettingsException">名前にフォルダの区切りや拡張子が入っている。</exception>
        public MenuScenes ToScenes()
        {
            try
            {
                return new MenuScenes(_mainMenu, _range, _campaign, _net);
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
            _net = MenuRoute.NetSceneName;
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
