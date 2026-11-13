using System;
using Fps.Core;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Fps.Runtime
{
    /// <summary>
    /// メニューで選んだ <see cref="CampaignLaunch"/> を、ミッションのシーンの <see cref="CampaignRunner"/> へ渡す置き場所。
    /// シーンを読み直しても (プレイヤーが倒れたときの再開でも) 選び直すまで残る。
    /// Play を始めるたびに空に戻すので、シーンを直接開いて Play したときは何も選ばれていない。
    /// </summary>
    public static class CampaignLaunchSelection
    {
        public const string LogTag = CampaignRunner.LogTag;

        /// <summary>今の選択。何も選ばれていなければ null。</summary>
        public static CampaignLaunch Current { get; private set; }

        /// <summary>launch を選ぶ。前の選択は置き換える。</summary>
        public static void Select(CampaignLaunch launch)
        {
            Current = launch ?? throw new ArgumentNullException(nameof(launch));
            Debug.Log($"{LogTag} selected launch mission={launch.MissionId} difficulty={CampaignLaunchOptions.DifficultyName(launch.Difficulty)}");
        }

        /// <summary>選択を消す。</summary>
        public static void Clear()
        {
            Current = null;
        }

        /// <summary>
        /// catalog の missionId のミッションを difficulty で選び、そのシーンを sceneLoader (null なら SceneManager.LoadScene) で開く。
        /// catalog が不正なら <see cref="MissionCatalogException"/>、missionId が無ければ <see cref="ArgumentException"/>。
        /// sceneLoader が null のときはシーンが Build Settings に無いと <see cref="InvalidOperationException"/>。
        /// どの例外でも前の選択は変えない。
        /// </summary>
        /// <returns>開いたミッション。</returns>
        public static MissionCatalogEntry Launch(
            MissionCatalog catalog,
            string missionId,
            CampaignDifficulty difficulty,
            Action<string> sceneLoader = null)
        {
            if (catalog == null)
            {
                throw new ArgumentNullException(nameof(catalog));
            }

            var launch = new CampaignLaunch(missionId, difficulty);
            if (!catalog.TryFind(missionId, out var entry))
            {
                throw new ArgumentException($"mission '{missionId}' is not in mission catalog '{catalog.name}'", nameof(missionId));
            }

            if (sceneLoader == null)
            {
                // LoadScene は Build Settings に無いシーンでもエラーを出すだけで戻るので、選択だけが残らないよう先に確かめる。
                if (SceneUtility.GetBuildIndexByScenePath(entry.ScenePath) < 0)
                {
                    throw new InvalidOperationException(
                        $"scene '{entry.ScenePath}' of mission '{missionId}' is not in Build Settings");
                }

                sceneLoader = path => SceneManager.LoadScene(path);
            }

            var previous = Current;
            Select(launch);
            try
            {
                sceneLoader(entry.ScenePath);
            }
            catch
            {
                Current = previous;
                throw;
            }

            return entry;
        }

        // Enter Play Mode でドメインを読み直さない設定なので、前の Play の選択が残らないよう Play のたびに消す。
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetOnPlay()
        {
            Current = null;
        }
    }
}
