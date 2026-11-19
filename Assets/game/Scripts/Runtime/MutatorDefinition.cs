using Shooter.Core;
using UnityEngine;

namespace Shooter.Runtime
{
    [CreateAssetMenu(fileName = "Mutator", menuName = "Shooter/Mutator")]
    public sealed class MutatorDefinition : ScriptableObject
    {
        [SerializeField, Tooltip("ミューテーターの Id。重ねたときのログに出る")]
        private string _id;

        [SerializeField, TextArea(3, 20), Tooltip("元のルールの上に重ねるルールの JSON の一部 (例: { \"playerMaxHealth\": 50 })。書いていない欄は元のルールのまま")]
        private string _rulesJson = "{}";

        public string Id
        {
            get => _id;
            set => _id = value;
        }

        public string RulesJson
        {
            get => _rulesJson;
            set => _rulesJson = value;
        }

        public GameRulesMutator ToMutator()
        {
            return new GameRulesMutator(_id, _rulesJson ?? string.Empty, $"MutatorDefinition '{name}'");
        }
    }
}
