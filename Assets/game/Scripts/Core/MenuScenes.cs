using System;

namespace Shooter.Core
{
    /// <summary>
    /// メニューから移るシーンの名前 (拡張子なし)。<see cref="Default"/> は <see cref="MenuRoute"/> の既定の名前で、
    /// ゲームはデータ (Shooter.Runtime の MenuSceneSettings) で上書きする。シーンの経路は <see cref="MenuRoute"/> がルートの下に組む。
    /// </summary>
    public sealed class MenuScenes
    {
        /// <summary><see cref="MenuRoute"/> の既定の名前 (MainMenu / Bootstrap / Campaign01)。</summary>
        public static MenuScenes Default { get; } = new MenuScenes(null, null, null);

        /// <summary>引数ごとに、null か空白だけなら既定の名前を使う。</summary>
        /// <exception cref="ArgumentException">名前に / や \ を含む、または <see cref="KitPaths.SceneExtension"/> で終わる。</exception>
        public MenuScenes(string mainMenu, string range, string campaign)
        {
            MainMenu = Resolve(mainMenu, MenuRoute.MainMenuSceneName, nameof(mainMenu));
            Range = Resolve(range, MenuRoute.RangeSceneName, nameof(range));
            Campaign = Resolve(campaign, MenuRoute.CampaignSceneName, nameof(campaign));
        }

        /// <summary>起動時のシーンで、ポーズや切断から戻るメインメニュー。</summary>
        public string MainMenu { get; }

        /// <summary>射撃場 (チュートリアルもここで出す)。</summary>
        public string Range { get; }

        /// <summary>キャンペーンの最初のシーン。</summary>
        public string Campaign { get; }

        public override string ToString()
        {
            return $"mainMenu={MainMenu} range={Range} campaign={Campaign}";
        }

        private static string Resolve(string name, string fallback, string parameterName)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                return fallback;
            }

            var trimmed = name.Trim();
            if (trimmed.Contains('/') || trimmed.Contains('\\'))
            {
                throw new ArgumentException(
                    $"scene name '{name}' must be a plain file name under the Scenes folder, without a folder", parameterName);
            }

            // KitPaths.ScenePath が拡張子を足すので、付けたまま通すと Foo.unity.unity を探してしまう
            if (trimmed.EndsWith(KitPaths.SceneExtension, StringComparison.OrdinalIgnoreCase))
            {
                throw new ArgumentException(
                    $"scene name '{name}' must not end with '{KitPaths.SceneExtension}'", parameterName);
            }

            return trimmed;
        }
    }
}
