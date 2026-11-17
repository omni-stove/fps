using System;
using Shooter.Core;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Shooter.Runtime
{
    public static class CampaignLaunchSelection
    {
        public const string LogTag = CampaignRunner.LogTag;

        public static CampaignLaunch Current { get; private set; }

        public static bool IgnoresSaves { get; private set; }

        public static void Select(CampaignLaunch launch)
        {
            Current = launch ?? throw new ArgumentNullException(nameof(launch));
            IgnoresSaves = false;
            Debug.Log($"{LogTag} selected launch mission={launch.MissionId} difficulty={CampaignLaunchOptions.DifficultyName(launch.Difficulty)}");
        }

        public static void Clear()
        {
            Current = null;
            IgnoresSaves = false;
        }

        public static void IgnoreSaves()
        {
            IgnoresSaves = true;
        }

        public static void MarkSaved()
        {
            IgnoresSaves = false;
        }

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
            // ビルドは起動するたびにここを通るので、続きから遊べるようセーブを読む。エディタの Play だけ前の Play のセーブを持ち込まない。
            IgnoresSaves = Application.isEditor;
        }
    }
}
