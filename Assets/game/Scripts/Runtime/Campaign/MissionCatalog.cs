using System;
using System.Collections.Generic;
using Fps.Core;
using UnityEngine;

namespace Fps.Runtime
{
    /// <summary>
    /// キャンペーンのミッションの一覧。ミッションごとに Id・表示名・シーン・<see cref="MissionDefinition"/> を持つ。
    /// メニューはここから選んで <see cref="CampaignLaunchSelection.Launch"/> でシーンを開く。
    /// Editor の MissionCatalogBuilder が生成するので、Inspector では書き換えない。
    /// </summary>
    [CreateAssetMenu(fileName = "MissionCatalog", menuName = "Fps/Mission Catalog")]
    public sealed class MissionCatalog : ScriptableObject
    {
        [SerializeField, Tooltip("ミッション。並び順がメニューの順になる")]
        private List<MissionCatalogEntry> _missions = new List<MissionCatalogEntry>();

        /// <summary>定義順のミッション。中身を書き換えるときは <see cref="SetMissions"/> を使う。</summary>
        public IReadOnlyList<MissionCatalogEntry> Missions => _missions ??= new List<MissionCatalogEntry>();

        /// <summary>ミッションをまとめて差し替える。null は空にする。要素は複製して持つ (MissionDefinition のアセットは共有する)。</summary>
        public void SetMissions(IEnumerable<MissionCatalogEntry> missions)
        {
            var copy = new List<MissionCatalogEntry>();
            if (missions != null)
            {
                foreach (var mission in missions)
                {
                    copy.Add(mission?.Clone());
                }
            }

            _missions = copy;
        }

        /// <summary>
        /// 一覧を検証する。空・null 要素・Id の重複、各ミッションの不正 (<see cref="MissionCatalogEntry.Validate"/>) は、
        /// どのアセットのどのミッションかを添えて <see cref="MissionCatalogException"/> にする。
        /// </summary>
        public void Validate()
        {
            var source = string.IsNullOrEmpty(name) ? "(unnamed mission catalog)" : name;
            var missions = Missions;
            if (missions.Count == 0)
            {
                throw new MissionCatalogException($"mission catalog '{source}' has no missions");
            }

            var seen = new HashSet<string>(StringComparer.Ordinal);
            for (var i = 0; i < missions.Count; i++)
            {
                var mission = missions[i];
                if (mission == null)
                {
                    throw new MissionCatalogException($"mission catalog '{source}' mission #{i} is empty");
                }

                try
                {
                    mission.Validate();
                }
                catch (MissionCatalogException e)
                {
                    throw new MissionCatalogException($"mission catalog '{source}' mission #{i}: {e.Message}", e);
                }

                if (!seen.Add(mission.Id))
                {
                    throw new MissionCatalogException($"mission catalog '{source}' has mission id '{mission.Id}' more than once");
                }
            }
        }

        /// <summary>Id が id のミッションを探す。無ければ false と null。Id は大文字小文字を区別して比べる。</summary>
        /// <exception cref="MissionCatalogException">一覧が不正なとき (見つかるかどうかに関わらず)。</exception>
        public bool TryFind(string id, out MissionCatalogEntry entry)
        {
            // 不正な一覧から一部だけ引けてしまわないよう、先に全体を検証する。
            Validate();
            foreach (var mission in Missions)
            {
                if (string.Equals(mission.Id, id, StringComparison.Ordinal))
                {
                    entry = mission;
                    return true;
                }
            }

            entry = null;
            return false;
        }
    }

    /// <summary><see cref="MissionCatalog"/> の 1 ミッション。</summary>
    [Serializable]
    public sealed class MissionCatalogEntry
    {
        public const string SceneExtension = ".unity";

        [SerializeField, Tooltip("ミッション Id。MissionDefinition のアセット名と同じにする。セーブのスロット名にも使う")]
        private string _id = string.Empty;

        [SerializeField, Tooltip("メニューに出す名前")]
        private string _displayName = string.Empty;

        [SerializeField, Tooltip("ミッションのシーンのパス (Assets/.../*.unity)。Build Settings に入っていること")]
        private string _scenePath = string.Empty;

        [SerializeField, Tooltip("シーンの MissionRunner が使う定義")]
        private MissionDefinition _definition;

        public MissionCatalogEntry()
        {
        }

        public MissionCatalogEntry(string id, string displayName, string scenePath, MissionDefinition definition)
        {
            _id = id ?? string.Empty;
            _displayName = displayName ?? string.Empty;
            _scenePath = scenePath ?? string.Empty;
            _definition = definition;
        }

        public string Id => _id ?? string.Empty;

        public string DisplayName => _displayName ?? string.Empty;

        /// <summary>SceneManager.LoadScene に渡せるシーンのパス (Assets/ から始まり .unity で終わる)。</summary>
        public string ScenePath => _scenePath ?? string.Empty;

        public MissionDefinition Definition => _definition;

        public MissionCatalogEntry Clone()
        {
            return new MissionCatalogEntry(_id, _displayName, _scenePath, _definition);
        }

        /// <summary>
        /// Id がミッション Id として使え (<see cref="CampaignLaunch.IsValidMissionId"/>)、表示名が空でなく、シーンのパスが Assets/ 以下の .unity で、
        /// 定義があってその名前が Id と同じで、ミッションに変換できることを確かめる。不正なら <see cref="MissionCatalogException"/>。
        /// </summary>
        public void Validate()
        {
            // CampaignRunner はシーンの MissionDefinition の名前でセーブと選択を照合するので、Id と食い違うと選択もセーブも効かない。
            if (!CampaignLaunch.IsValidMissionId(Id))
            {
                throw new MissionCatalogException(
                    $"id '{Id}' must be 1-{CampaignLaunch.MaxMissionIdLength} characters of letters, digits, '_' or '-'");
            }

            if (string.IsNullOrWhiteSpace(DisplayName))
            {
                throw new MissionCatalogException($"mission '{Id}' has no display name");
            }

            if (!ScenePath.StartsWith("Assets/", StringComparison.Ordinal)
                || !ScenePath.EndsWith(SceneExtension, StringComparison.OrdinalIgnoreCase))
            {
                throw new MissionCatalogException($"mission '{Id}' scene path '{ScenePath}' must be an Assets/ path ending with {SceneExtension}");
            }

            if (_definition == null)
            {
                throw new MissionCatalogException($"mission '{Id}' has no MissionDefinition");
            }

            if (!string.Equals(_definition.name, Id, StringComparison.Ordinal))
            {
                throw new MissionCatalogException(
                    $"mission '{Id}' uses MissionDefinition '{_definition.name}'; the id must equal the definition's asset name");
            }

            try
            {
                _definition.CreateMission();
            }
            catch (MissionDefinitionException e)
            {
                throw new MissionCatalogException($"mission '{Id}': {e.Message}", e);
            }
        }
    }

    /// <summary><see cref="MissionCatalog"/> の中身が不正なときに投げる。</summary>
    public sealed class MissionCatalogException : Exception
    {
        public MissionCatalogException(string message)
            : base(message)
        {
        }

        public MissionCatalogException(string message, Exception innerException)
            : base(message, innerException)
        {
        }
    }
}
