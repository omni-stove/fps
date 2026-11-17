using System;
using System.Collections.Generic;
using Fps.Core;
using UnityEngine;
using UnityEngine.Serialization;

namespace Fps.Runtime
{
    /// <summary>
    /// キャンペーンのミッションの一覧。ミッションごとに Id・表示名・シーン・<see cref="MissionDefinition"/> を持つ。
    /// メニューはここから選んで <see cref="CampaignLaunchSelection.Launch"/> でシーンを開く。
    /// キャンペーン全体の間合い (やられてから読み直すまでの秒数 <see cref="ReloadDelaySeconds"/>、セーブしたと出す秒数 <see cref="SavedNoticeSeconds"/>) も持ち、
    /// <see cref="CampaignRunner"/> が読む。
    /// Editor の MissionCatalogBuilder が生成するので、Inspector では書き換えない。
    /// </summary>
    [CreateAssetMenu(fileName = "MissionCatalog", menuName = "Fps/Mission Catalog")]
    public sealed class MissionCatalog : ScriptableObject
    {
        public const float DefaultReloadDelaySeconds = 3f;
        public const float DefaultSavedNoticeSeconds = 2.5f;

        [SerializeField, Tooltip("ミッション。並び順がメニューの順になる")]
        private List<MissionCatalogEntry> _missions = new List<MissionCatalogEntry>();

        [SerializeField, Min(0f), Tooltip("プレイヤーが倒れてから、チェックポイントから再開する (シーンを読み直す) までの秒数")]
        private float _reloadDelaySeconds = DefaultReloadDelaySeconds;

        [SerializeField, Min(0f), Tooltip("チェックポイントでセーブしたときに HUD へ「セーブしました」を出しておく秒数")]
        private float _savedNoticeSeconds = DefaultSavedNoticeSeconds;

        /// <summary>定義順のミッション。中身を書き換えるときは <see cref="SetMissions"/> を使う。</summary>
        public IReadOnlyList<MissionCatalogEntry> Missions => _missions ??= new List<MissionCatalogEntry>();

        /// <summary>プレイヤーが倒れてからシーンを読み直すまでの秒数。0 以上の有限の値。</summary>
        /// <exception cref="ArgumentOutOfRangeException">負・NaN・無限大を設定したとき。</exception>
        public float ReloadDelaySeconds
        {
            get => _reloadDelaySeconds;
            set => _reloadDelaySeconds = RequireSeconds(value, nameof(ReloadDelaySeconds));
        }

        /// <summary>セーブしたときに HUD へ「セーブしました」を出しておく秒数。0 以上の有限の値。</summary>
        /// <exception cref="ArgumentOutOfRangeException">負・NaN・無限大を設定したとき。</exception>
        public float SavedNoticeSeconds
        {
            get => _savedNoticeSeconds;
            set => _savedNoticeSeconds = RequireSeconds(value, nameof(SavedNoticeSeconds));
        }

        /// <summary>seconds が秒数として使える (0 以上の有限の値) か。</summary>
        public static bool IsValidSeconds(float seconds)
        {
            return seconds >= 0f && !float.IsInfinity(seconds);
        }

        private static float RequireSeconds(float value, string name)
        {
            if (!IsValidSeconds(value))
            {
                throw new ArgumentOutOfRangeException(name, value, $"{name} must be a non-negative finite number");
            }

            return value;
        }

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
        /// 一覧を検証する。秒数の不正、空・null 要素・Id の重複、各ミッションの不正 (<see cref="MissionCatalogEntry.Validate"/>) は、
        /// どのアセットのどのミッションかを添えて <see cref="MissionCatalogException"/> にする。
        /// </summary>
        public void Validate()
        {
            var source = string.IsNullOrEmpty(name) ? "(unnamed mission catalog)" : name;
            if (!IsValidSeconds(_reloadDelaySeconds))
            {
                throw new MissionCatalogException($"mission catalog '{source}' reload delay {_reloadDelaySeconds} must be a non-negative finite number of seconds");
            }

            if (!IsValidSeconds(_savedNoticeSeconds))
            {
                throw new MissionCatalogException($"mission catalog '{source}' saved notice {_savedNoticeSeconds} must be a non-negative finite number of seconds");
            }

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

        /// <summary>
        /// 定義の Id (<see cref="MissionDefinition.Id"/>) が id のミッションを探す。無ければ false と null。
        /// Id は大文字小文字を区別して比べる。定義のアセット名は見ない。
        /// </summary>
        /// <exception cref="MissionCatalogException">一覧が不正なとき (見つかるかどうかに関わらず)。</exception>
        public bool TryFind(string id, out MissionCatalogEntry entry)
        {
            // 不正な一覧から一部だけ引けてしまわないよう、先に全体を検証する。
            Validate();
            foreach (var mission in Missions)
            {
                if (string.Equals(mission.Definition.Id, id, StringComparison.Ordinal))
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

        [SerializeField, Tooltip("ミッション Id。MissionDefinition の Id と同じにする。セーブのスロット名にも使う")]
        private string _id = string.Empty;

        // 文を直に持っていた頃のアセットは、その文がキーとして読まれ、表に無いキーとして警告付きでそのまま画面に出る。
        [SerializeField, FormerlySerializedAs("_displayName"), Tooltip("メニューに出す名前の、言語の表のキー (campaign.mission.<Id>.name)")]
        private string _displayNameKey = string.Empty;

        [SerializeField, Tooltip("ミッションのシーンのパス (Assets/.../*.unity)。Build Settings に入っていること")]
        private string _scenePath = string.Empty;

        [SerializeField, Tooltip("シーンの MissionRunner が使う定義")]
        private MissionDefinition _definition;

        public MissionCatalogEntry()
        {
        }

        public MissionCatalogEntry(string id, string displayNameKey, string scenePath, MissionDefinition definition)
        {
            _id = id ?? string.Empty;
            _displayNameKey = displayNameKey ?? string.Empty;
            _scenePath = scenePath ?? string.Empty;
            _definition = definition;
        }

        public string Id => _id ?? string.Empty;

        /// <summary>メニューに出す名前の、言語の表のキー。</summary>
        public string DisplayNameKey => _displayNameKey ?? string.Empty;

        /// <summary>メニューに出す名前。<see cref="DisplayNameKey"/> が空なら <c>default</c> (文にすると空文字)。</summary>
        public LocalizedText DisplayName => string.IsNullOrWhiteSpace(_displayNameKey) ? default : new LocalizedText(_displayNameKey.Trim());

        /// <summary>SceneManager.LoadScene に渡せるシーンのパス (Assets/ から始まり .unity で終わる)。</summary>
        public string ScenePath => _scenePath ?? string.Empty;

        public MissionDefinition Definition => _definition;

        public MissionCatalogEntry Clone()
        {
            return new MissionCatalogEntry(_id, _displayNameKey, _scenePath, _definition);
        }

        /// <summary>
        /// Id がミッション Id として使え (<see cref="CampaignLaunch.IsValidMissionId"/>)、表示名のキーが空でなく、シーンのパスが Assets/ 以下の .unity で、
        /// 定義があってその <see cref="MissionDefinition.Id"/> が空でなく Id と同じで、ミッションに変換できることを確かめる。
        /// 不正なら <see cref="MissionCatalogException"/>。
        /// </summary>
        public void Validate()
        {
            if (!CampaignLaunch.IsValidMissionId(Id))
            {
                throw new MissionCatalogException(
                    $"id '{Id}' must be 1-{CampaignLaunch.MaxMissionIdLength} characters of letters, digits, '_' or '-'");
            }

            if (string.IsNullOrWhiteSpace(DisplayNameKey))
            {
                throw new MissionCatalogException($"mission '{Id}' has no display name key");
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

            if (string.IsNullOrWhiteSpace(_definition.Id))
            {
                throw new MissionCatalogException($"mission '{Id}' uses MissionDefinition '{_definition.name}', which has no id");
            }

            // CampaignRunner はシーンの MissionDefinition の Id でセーブと選択を照合するので、Id と食い違うと選択もセーブも効かない。
            if (!string.Equals(_definition.Id, Id, StringComparison.Ordinal))
            {
                throw new MissionCatalogException(
                    $"mission '{Id}' uses MissionDefinition '{_definition.name}' whose id is '{_definition.Id}'; the ids must be equal");
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
