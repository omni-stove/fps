using System;
using System.Collections.Generic;
using UnityEngine;

namespace Shooter.Runtime
{
    [CreateAssetMenu(fileName = "AbilityCatalog", menuName = "Shooter/Ability Catalog")]
    public sealed class AbilityCatalogAsset : ScriptableObject
    {
        [SerializeField, Tooltip("装備画面で選べる能力の定義。並び順が装備画面の順になる")]
        private List<AbilityDefinition> _abilities = new List<AbilityDefinition>();

        public IReadOnlyList<AbilityDefinition> Abilities => _abilities ??= new List<AbilityDefinition>();

        public void SetAbilities(IEnumerable<AbilityDefinition> abilities)
        {
            _abilities = abilities == null ? new List<AbilityDefinition>() : new List<AbilityDefinition>(abilities);
        }

        public bool TryFind(string id, out AbilityDefinition definition)
        {
            // 不正な一覧から一部だけ引けてしまわないよう、先に全体を検証する。
            var ids = ToIds();
            definition = null;
            if (string.IsNullOrEmpty(id))
            {
                return false;
            }

            for (var i = 0; i < ids.Count; i++)
            {
                if (string.Equals(ids[i], id, StringComparison.Ordinal))
                {
                    // ToIds は Abilities と同じ順に並べるので、添字で定義に戻せる。
                    definition = Abilities[i];
                    return true;
                }
            }

            return false;
        }

        public IReadOnlyList<string> ToIds()
        {
            var source = string.IsNullOrEmpty(name) ? "(unnamed ability catalog)" : name;
            var abilities = Abilities;
            if (abilities.Count == 0)
            {
                throw new AbilityDefinitionException($"ability catalog '{source}' has no abilities");
            }

            var ids = new List<string>(abilities.Count);
            var owners = new Dictionary<string, string>(StringComparer.Ordinal);
            for (var i = 0; i < abilities.Count; i++)
            {
                var definition = abilities[i];

                // 破棄されたアセットや参照切れも Unity の == null で拾う。
                if (definition == null)
                {
                    throw new AbilityDefinitionException($"ability catalog '{source}' ability #{i} is empty");
                }

                // 空の Id は装備画面の「なし」(AbilitySelection.None) と見分けられなくなる
                var id = definition.Id;
                if (string.IsNullOrEmpty(id))
                {
                    throw new AbilityDefinitionException($"ability catalog '{source}' ability #{i} has no id");
                }

                try
                {
                    definition.ToSpec();
                }
                catch (AbilityDefinitionException e)
                {
                    throw new AbilityDefinitionException($"ability catalog '{source}' ability #{i}: {e.Message}", e);
                }

                if (owners.TryGetValue(id, out var owner))
                {
                    throw new AbilityDefinitionException(
                        $"ability catalog '{source}' ability #{i} ('{definition.name}') has the id '{id}' already used by '{owner}'");
                }

                owners.Add(id, definition.name);
                ids.Add(id);
            }

            return ids.AsReadOnly();
        }
    }
}
