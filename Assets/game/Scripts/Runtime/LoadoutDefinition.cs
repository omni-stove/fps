using System;
using System.Collections.Generic;
using Shooter.Core;
using UnityEngine;

namespace Shooter.Runtime
{
    [CreateAssetMenu(fileName = "Loadout", menuName = "Shooter/Loadout Definition")]
    public sealed class LoadoutDefinition : ScriptableObject
    {
        public const float DefaultSwitchSeconds = 0.4f;

        [SerializeField, Tooltip("スロットの並び。i 番目の武器は i 番目のスロットに入る")]
        private List<LoadoutSlotDefinition> _slots = new List<LoadoutSlotDefinition>();

        [SerializeField, Min(0f), Tooltip("武器を持ち替えるのにかかる秒数")]
        private float _switchSeconds = DefaultSwitchSeconds;

        [SerializeField, Tooltip("能力の枠に入れる能力。i 番目は AbilityHost の i 番目の枠 (0 が Ability1) に入る。空の要素はその枠を空にする")]
        private List<AbilityDefinition> _abilities = new List<AbilityDefinition>();

        public IReadOnlyList<LoadoutSlotDefinition> Slots => _slots ??= new List<LoadoutSlotDefinition>();

        public IReadOnlyList<AbilityDefinition> Abilities => _abilities ??= new List<AbilityDefinition>();

        public void SetAbilities(IEnumerable<AbilityDefinition> abilities)
        {
            _abilities = abilities == null ? new List<AbilityDefinition>() : new List<AbilityDefinition>(abilities);
        }

        public float SwitchSeconds
        {
            get => _switchSeconds;
            set => _switchSeconds = value;
        }

        public void SetSlots(IEnumerable<LoadoutSlotDefinition> slots)
        {
            _slots = slots == null ? new List<LoadoutSlotDefinition>() : new List<LoadoutSlotDefinition>(slots);
        }

        public LoadoutRule ToRule()
        {
            var source = SourceName();
            if (!(_switchSeconds >= 0f) || float.IsInfinity(_switchSeconds))
            {
                throw new WeaponDefinitionException(
                    $"loadout '{source}' switch seconds must be a non-negative finite number (got {_switchSeconds})");
            }

            var slots = Slots;
            if (slots.Count == 0)
            {
                throw new WeaponDefinitionException($"loadout '{source}' has no slots");
            }

            var categories = new List<IEnumerable<WeaponCategory>>(slots.Count);
            for (var i = 0; i < slots.Count; i++)
            {
                if (slots[i] == null)
                {
                    throw new WeaponDefinitionException($"loadout '{source}' slot #{i} is empty");
                }

                categories.Add(slots[i].AllowedCategories);
            }

            try
            {
                return new LoadoutRule(categories);
            }
            catch (ArgumentException e)
            {
                throw new WeaponDefinitionException($"loadout '{source}' is invalid: {e.Message}", e);
            }
        }

        public IReadOnlyList<WeaponSpec> ToSpecs()
        {
            var source = SourceName();
            var rule = ToRule();
            var slots = Slots;
            var specs = new List<WeaponSpec>(slots.Count);
            for (var i = 0; i < slots.Count; i++)
            {
                var weapon = slots[i].Weapon;

                // 破棄されたアセットや参照切れも Unity の == null で拾う。
                if (weapon == null)
                {
                    throw new WeaponDefinitionException($"loadout '{source}' slot #{i} has no weapon");
                }

                try
                {
                    specs.Add(weapon.ToSpec());
                }
                catch (WeaponDefinitionException e)
                {
                    throw new WeaponDefinitionException($"loadout '{source}' slot #{i}: {e.Message}", e);
                }
            }

            var reason = rule.Validate(specs);
            if (reason != null)
            {
                throw new WeaponDefinitionException($"loadout '{source}' is invalid: {reason}");
            }

            return specs.AsReadOnly();
        }

        private string SourceName()
        {
            return string.IsNullOrEmpty(name) ? "(unnamed loadout)" : name;
        }
    }

    [Serializable]
    public sealed class LoadoutSlotDefinition
    {
        [SerializeField, Tooltip("このスロットに入れてよい武器の種別")]
        private List<WeaponCategory> _allowedCategories = new List<WeaponCategory>();

        [SerializeField, Tooltip("このスロットに入れる武器")]
        private WeaponDefinition _weapon;

        // インスペクターでリストに要素を足すとき、Unity のシリアライザーが引数なしで作る
        private LoadoutSlotDefinition()
        {
        }

        public LoadoutSlotDefinition(IEnumerable<WeaponCategory> allowedCategories, WeaponDefinition weapon)
        {
            _allowedCategories = allowedCategories == null
                ? new List<WeaponCategory>()
                : new List<WeaponCategory>(allowedCategories);
            _weapon = weapon;
        }

        public IReadOnlyList<WeaponCategory> AllowedCategories => _allowedCategories ??= new List<WeaponCategory>();

        public WeaponDefinition Weapon => _weapon;
    }
}
