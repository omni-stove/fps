using System;
using System.Collections.Generic;
using Fps.Core;
using UnityEngine;

namespace Fps.Runtime
{
    /// <summary>
    /// キャンペーンの 1 ミッションの定義。目標の一覧をアセットとして持ち、<see cref="CreateMission"/> で
    /// <see cref="Fps.Core.Mission"/> を作る。検証は <see cref="Fps.Core.Mission"/> と <see cref="Objective"/> に任せ、
    /// 失敗したらどのアセットのどの目標かを添えて <see cref="MissionDefinitionException"/> を投げる。
    /// </summary>
    [CreateAssetMenu(fileName = "Mission", menuName = "Fps/Mission Definition")]
    public sealed class MissionDefinition : ScriptableObject
    {
        [SerializeField, Tooltip("プレイヤーに見せるミッション名")]
        private string _displayName = string.Empty;

        [SerializeField, Tooltip("目標。前提は同じミッションの目標の Id を指す")]
        private List<ObjectiveDefinition> _objectives = new List<ObjectiveDefinition>();

        public string DisplayName
        {
            get => _displayName ?? string.Empty;
            set => _displayName = value ?? string.Empty;
        }

        /// <summary>定義順の目標。中身を書き換えるときは <see cref="SetObjectives"/> を使う。</summary>
        public IReadOnlyList<ObjectiveDefinition> Objectives => _objectives ??= new List<ObjectiveDefinition>();

        /// <summary>目標をまとめて差し替える。null は空にする。要素は複製して持つので、渡した側の変更は反映しない。</summary>
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

        /// <summary>定義から新しい <see cref="Fps.Core.Mission"/> を作る。呼ぶたびに進み具合が 0 のミッションを返す。</summary>
        /// <exception cref="MissionDefinitionException">定義が不正なとき。</exception>
        public Mission CreateMission()
        {
            return CreateMission(name, Objectives);
        }

        /// <summary>
        /// 目標の定義を <see cref="Objective"/> に変換して <see cref="Fps.Core.Mission"/> を作る。
        /// 文字列の前後の空白は取り除き、空の前提は無視する (Inspector で配列の要素を増やしただけの空欄を許す)。
        /// </summary>
        /// <param name="source">エラーメッセージに出す定義の名前。</param>
        /// <param name="definitions">目標の定義。</param>
        /// <exception cref="MissionDefinitionException">定義が不正なとき。</exception>
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
    }

    /// <summary><see cref="MissionDefinition"/> の 1 目標。Inspector で編集できる形の <see cref="Objective"/>。</summary>
    [Serializable]
    public sealed class ObjectiveDefinition
    {
        [SerializeField, Tooltip("ミッション内で一意な識別子。トリガーはこの Id で目標を指す")]
        private string _id = string.Empty;

        [SerializeField, Tooltip("プレイヤーに見せる説明")]
        private string _description = string.Empty;

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
            string description,
            int requiredCount = 1,
            bool optional = false,
            params string[] prerequisites)
        {
            _id = id;
            _description = description;
            _requiredCount = requiredCount;
            _optional = optional;
            _prerequisites = prerequisites == null ? Array.Empty<string>() : (string[])prerequisites.Clone();
        }

        public string Id => _id;
        public string Description => _description;
        public int RequiredCount => _requiredCount;
        public bool Optional => _optional;
        public IReadOnlyList<string> Prerequisites => _prerequisites ?? Array.Empty<string>();

        /// <summary>不変の <see cref="Objective"/> に変換する。不正な値なら <see cref="ArgumentException"/> を投げる。</summary>
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

            return new Objective(_id?.Trim() ?? string.Empty, _description, _requiredCount, _optional, prerequisites);
        }

        public ObjectiveDefinition Clone()
        {
            return new ObjectiveDefinition(_id, _description, _requiredCount, _optional, _prerequisites);
        }
    }

    /// <summary><see cref="MissionDefinition"/> の内容が不正で <see cref="Fps.Core.Mission"/> を作れない。</summary>
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
