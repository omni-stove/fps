using System;
using System.Collections.Generic;
using Shooter.Core;
using UnityEngine;
using UnityEngine.Serialization;

namespace Shooter.Runtime
{
    [CreateAssetMenu(fileName = "Mission", menuName = "Shooter/Mission Definition")]
    public sealed class MissionDefinition : ScriptableObject
    {
        [SerializeField, Tooltip("ミッション Id (英数字・'_'・'-' の 1-64 文字)。セーブのスロット名・メニューの選択・MissionCatalog の照合に使う。アセット名とは別")]
        private string _id = string.Empty;

        // 文を直に持っていた頃のアセットは、その文がキーとして読まれ、表に無いキーとして警告付きでそのまま画面に出る。
        [SerializeField, FormerlySerializedAs("_displayName"), Tooltip("プレイヤーに見せるミッション名の、言語の表のキー (campaign.mission.<Id>.name)")]
        private string _displayNameKey = string.Empty;

        public string Id
        {
            get => _id ?? string.Empty;
            set => _id = value ?? string.Empty;
        }

        [SerializeField, Tooltip("目標。前提は同じミッションの目標の Id を指す")]
        private List<ObjectiveDefinition> _objectives = new List<ObjectiveDefinition>();

        [SerializeField, TextArea(3, 12), Tooltip("難易度のルールのうち、このミッションだけ変える項目を書いたルールの JSON (例: {\"viewMode\": \"ThirdPerson\", \"actions\": {\"cover\": \"On\"}})。空なら難易度のルールのまま")]
        private string _rulesOverride = string.Empty;

        public string DisplayNameKey
        {
            get => _displayNameKey ?? string.Empty;
            set => _displayNameKey = value ?? string.Empty;
        }

        public static string NameKeyOf(string missionId)
        {
            RequireKeyPart(missionId, nameof(missionId));
            return "campaign.mission." + missionId + ".name";
        }

        public static string ObjectiveKeyOf(string missionId, string objectiveId)
        {
            RequireKeyPart(missionId, nameof(missionId));
            RequireKeyPart(objectiveId, nameof(objectiveId));
            return "campaign.mission." + missionId + ".objective." + objectiveId;
        }

        public string RulesOverride
        {
            get => _rulesOverride ?? string.Empty;
            set => _rulesOverride = value ?? string.Empty;
        }

        public GameRules ApplyRulesOverride(GameRules baseRules)
        {
            if (baseRules == null)
            {
                throw new ArgumentNullException(nameof(baseRules));
            }

            if (string.IsNullOrWhiteSpace(_rulesOverride))
            {
                return baseRules;
            }

            var source = string.IsNullOrEmpty(name) ? "(unnamed mission)" : name;
            try
            {
                return GameRulesJson.Parse(_rulesOverride, $"mission '{source}' rules override", baseRules);
            }
            catch (GameRulesJsonException e)
            {
                throw new MissionDefinitionException($"mission '{source}' has an invalid rules override: {e.Message}", e);
            }
        }

        public IReadOnlyList<ObjectiveDefinition> Objectives => _objectives ??= new List<ObjectiveDefinition>();

        public void SetObjectives(IEnumerable<ObjectiveDefinition> objectives)
        {
            var copy = new List<ObjectiveDefinition>();
            if (objectives != null)
            {
                foreach (var objective in objectives)
                {
                    copy.Add(objective?.Clone());
                }
            }

            _objectives = copy;
        }

        public Mission CreateMission()
        {
            return CreateMission(name, Objectives);
        }

        // 空の前提を無視するのは、Inspector で配列の要素を増やしただけの空欄を許すため。
        public static Mission CreateMission(string source, IReadOnlyList<ObjectiveDefinition> definitions)
        {
            source = string.IsNullOrEmpty(source) ? "(unnamed mission)" : source;
            if (definitions == null || definitions.Count == 0)
            {
                throw new MissionDefinitionException($"mission '{source}' has no objectives");
            }

            var objectives = new List<Objective>(definitions.Count);
            for (var i = 0; i < definitions.Count; i++)
            {
                var definition = definitions[i];
                if (definition == null)
                {
                    throw new MissionDefinitionException($"mission '{source}' objective #{i} is empty");
                }

                try
                {
                    objectives.Add(definition.ToObjective());
                }
                catch (ArgumentException e)
                {
                    throw new MissionDefinitionException(
                        $"mission '{source}' objective #{i} ('{definition.Id}') is invalid: {e.Message}", e);
                }
            }

            try
            {
                return new Mission(objectives);
            }
            catch (ArgumentException e)
            {
                throw new MissionDefinitionException($"mission '{source}' is invalid: {e.Message}", e);
            }
        }

        private static void RequireKeyPart(string part, string parameterName)
        {
            if (string.IsNullOrWhiteSpace(part))
            {
                throw new ArgumentException("must not be empty", parameterName);
            }
        }
    }

    [Serializable]
    public sealed class ObjectiveDefinition
    {
        [SerializeField, Tooltip("ミッション内で一意な識別子。トリガーはこの Id で目標を指す")]
        private string _id = string.Empty;

        // ミッション名と同じく、文を直に持っていた頃のアセットはその文をキーとして読む。
        [SerializeField, FormerlySerializedAs("_description"), Tooltip("プレイヤーに見せる説明の、言語の表のキー (campaign.mission.<ミッション Id>.objective.<目標 Id>)")]
        private string _descriptionKey = string.Empty;

        [SerializeField, Min(1), Tooltip("完了までに必要な進み具合")]
        private int _requiredCount = 1;

        [SerializeField, Tooltip("任意の目標ならオン。ミッションの成否に影響しない")]
        private bool _optional;

        [SerializeField, Tooltip("先に完了している必要がある目標の Id")]
        private string[] _prerequisites = Array.Empty<string>();

        public ObjectiveDefinition()
        {
        }

        public ObjectiveDefinition(
            string id,
            string descriptionKey,
            int requiredCount = 1,
            bool optional = false,
            params string[] prerequisites)
        {
            _id = id;
            _descriptionKey = descriptionKey;
            _requiredCount = requiredCount;
            _optional = optional;
            _prerequisites = prerequisites == null ? Array.Empty<string>() : (string[])prerequisites.Clone();
        }

        public string Id => _id;
        public string DescriptionKey => _descriptionKey ?? string.Empty;
        public int RequiredCount => _requiredCount;
        public bool Optional => _optional;
        public IReadOnlyList<string> Prerequisites => _prerequisites ?? Array.Empty<string>();

        public Objective ToObjective()
        {
            var prerequisites = new List<string>();
            foreach (var prerequisite in Prerequisites)
            {
                var trimmed = prerequisite?.Trim();
                if (!string.IsNullOrEmpty(trimmed))
                {
                    prerequisites.Add(trimmed);
                }
            }

            return new Objective(_id?.Trim() ?? string.Empty, DescriptionKey.Trim(), _requiredCount, _optional, prerequisites);
        }

        public ObjectiveDefinition Clone()
        {
            return new ObjectiveDefinition(_id, _descriptionKey, _requiredCount, _optional, _prerequisites);
        }
    }

    public sealed class MissionDefinitionException : Exception
    {
        public MissionDefinitionException(string message)
            : base(message)
        {
        }

        public MissionDefinitionException(string message, Exception innerException)
            : base(message, innerException)
        {
        }
    }
}
