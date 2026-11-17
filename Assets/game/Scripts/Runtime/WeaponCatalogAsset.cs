using System;
using System.Collections.Generic;
using Shooter.Core;
using UnityEngine;

namespace Shooter.Runtime
{
    [CreateAssetMenu(fileName = "WeaponCatalog", menuName = "Shooter/Weapon Catalog")]
    public sealed class WeaponCatalogAsset : ScriptableObject
    {
        [SerializeField, Tooltip("武器の定義。並び順がカタログの順になる")]
        private List<WeaponDefinition> _weapons = new List<WeaponDefinition>();

        public IReadOnlyList<WeaponDefinition> Weapons => _weapons ??= new List<WeaponDefinition>();

        public void SetWeapons(IEnumerable<WeaponDefinition> weapons)
        {
            _weapons = weapons == null ? new List<WeaponDefinition>() : new List<WeaponDefinition>(weapons);
        }

        public bool TryFind(string id, out WeaponDefinition definition)
        {
            // 不正な一覧から一部だけ引けてしまわないよう、先に全体を検証する。
            var catalog = ToCatalog();
            definition = null;
            if (id == null)
            {
                return false;
            }

            for (var i = 0; i < catalog.All.Count; i++)
            {
                if (string.Equals(catalog.All[i].Id, id, StringComparison.Ordinal))
                {
                    // ToCatalog は Weapons と同じ順に変換するので、添字で定義に戻せる。
                    definition = Weapons[i];
                    return true;
                }
            }

            return false;
        }

        public WeaponCatalog ToCatalog()
        {
            var source = string.IsNullOrEmpty(name) ? "(unnamed weapon catalog)" : name;
            var weapons = Weapons;
            if (weapons.Count == 0)
            {
                throw new WeaponDefinitionException($"weapon catalog '{source}' has no weapons");
            }

            var specs = new List<WeaponSpec>(weapons.Count);
            var owners = new Dictionary<string, string>(StringComparer.Ordinal);
            for (var i = 0; i < weapons.Count; i++)
            {
                var definition = weapons[i];

                // 破棄されたアセットや参照切れも Unity の == null で拾う。
                if (definition == null)
                {
                    throw new WeaponDefinitionException($"weapon catalog '{source}' weapon #{i} is empty");
                }

                WeaponSpec spec;
                try
                {
                    spec = definition.ToSpec();
                }
                catch (WeaponDefinitionException e)
                {
                    throw new WeaponDefinitionException($"weapon catalog '{source}' weapon #{i}: {e.Message}", e);
                }

                if (owners.TryGetValue(spec.Id, out var owner))
                {
                    throw new WeaponDefinitionException(
                        $"weapon catalog '{source}' weapon #{i} ('{definition.name}') has the id '{spec.Id}' already used by '{owner}'");
                }

                owners.Add(spec.Id, definition.name);
                specs.Add(spec);
            }

            try
            {
                return new WeaponCatalog(specs);
            }
            catch (ArgumentException e)
            {
                throw new WeaponDefinitionException($"weapon catalog '{source}' is invalid: {e.Message}", e);
            }
        }
    }
}
