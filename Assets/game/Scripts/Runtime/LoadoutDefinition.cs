using System;
using System.Collections.Generic;
using Fps.Core;
using UnityEngine;

namespace Fps.Runtime
{
    /// <summary>
    /// ロードアウトをアセットとして持つ。スロットの並び (各スロットが許す種別と入れる武器) と持ち替え秒数、能力の枠に入れる能力 (<see cref="Abilities"/>)。
    /// <see cref="ToRule"/> で Core の <see cref="LoadoutRule"/> に、<see cref="ToSpecs"/> でスロット順の <see cref="WeaponSpec"/> に変換する。
    /// ルールに合わない・武器が空・値が不正なときは、どのアセットかを添えて <see cref="WeaponDefinitionException"/> にする。
    /// </summary>
    [CreateAssetMenu(fileName = "Loadout", menuName = "Fps/Loadout Definition")]
    public sealed class LoadoutDefinition : ScriptableObject
    {
        public const float DefaultSwitchSeconds = 0.4f;

        [SerializeField, Tooltip("スロットの並び。i 番目の武器は i 番目のスロットに入る")]
        private List<LoadoutSlotDefinition> _slots = new List<LoadoutSlotDefinition>();

        [SerializeField, Min(0f), Tooltip("武器を持ち替えるのにかかる秒数")]
        private float _switchSeconds = DefaultSwitchSeconds;

        [SerializeField, Tooltip("能力の枠に入れる能力。i 番目は AbilityHost の i 番目の枠 (0 が Ability1) に入る。空の要素はその枠を空にする")]
        private List<AbilityDefinition> _abilities = new List<AbilityDefinition>();

        /// <summary>スロット順の定義。中身を書き換えるときは <see cref="SetSlots"/> を使う。</summary>
        public IReadOnlyList<LoadoutSlotDefinition> Slots => _slots ??= new List<LoadoutSlotDefinition>();

        /// <summary>
        /// 枠の順の能力 (<see cref="AbilityHost.Equip(IReadOnlyList{AbilityDefinition})"/> に渡す)。空の枠は null。
        /// 中身を書き換えるときは <see cref="SetAbilities"/> を使う。
        /// </summary>
        public IReadOnlyList<AbilityDefinition> Abilities => _abilities ??= new List<AbilityDefinition>();

        /// <summary>能力をまとめて差し替える。null は空にする。渡した列は複製して持つ (能力のアセットは共有する)。</summary>
        public void SetAbilities(IEnumerable<AbilityDefinition> abilities)
        {
            _abilities = abilities == null ? new List<AbilityDefinition>() : new List<AbilityDefinition>(abilities);
        }

        /// <summary>武器を持ち替えるのにかかる秒数。<see cref="ToRule"/> が 0 以上の有限値かを確かめる。</summary>
        public float SwitchSeconds
        {
            get => _switchSeconds;
            set => _switchSeconds = value;
        }

        /// <summary>スロットをまとめて差し替える。null は空にする。渡した列は複製して持つ (各スロットと武器のアセットは共有する)。</summary>
        public void SetSlots(IEnumerable<LoadoutSlotDefinition> slots)
        {
            _slots = slots == null ? new List<LoadoutSlotDefinition>() : new List<LoadoutSlotDefinition>(slots);
        }

        /// <summary>スロットが許す種別から <see cref="LoadoutRule"/> を作る。呼ぶたびに新しいインスタンスを返す。</summary>
        /// <exception cref="WeaponDefinitionException">スロットが無い・空のスロットがある・種別が不正・持ち替え秒数が不正なとき。</exception>
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

        /// <summary>
        /// スロット順の武器を <see cref="WeaponSpec"/> に変換し、<see cref="ToRule"/> のルールに合うことを確かめて返す。
        /// 呼ぶたびに新しい列を返す。
        /// </summary>
        /// <exception cref="WeaponDefinitionException">ルールか武器が不正・武器が空・ルールに合わないとき。</exception>
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

    /// <summary><see cref="LoadoutDefinition"/> の 1 スロット。許す種別と、そこに入れる武器。</summary>
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

        /// <param name="allowedCategories">許す種別。null は空として扱う。渡した列は複製して持つ。</param>
        /// <param name="weapon">入れる武器。null も持てる (<see cref="LoadoutDefinition.ToSpecs"/> が拒む)。</param>
        public LoadoutSlotDefinition(IEnumerable<WeaponCategory> allowedCategories, WeaponDefinition weapon)
        {
            _allowedCategories = allowedCategories == null
                ? new List<WeaponCategory>()
                : new List<WeaponCategory>(allowedCategories);
            _weapon = weapon;
        }

        /// <summary>許す種別。並びと重複はアセットに書かれたまま。</summary>
        public IReadOnlyList<WeaponCategory> AllowedCategories => _allowedCategories ??= new List<WeaponCategory>();

        /// <summary>入れる武器。未設定なら null。</summary>
        public WeaponDefinition Weapon => _weapon;
    }
}
