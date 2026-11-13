using System;
using System.Collections.Generic;
using Fps.Core;
using UnityEngine;

namespace Fps.Runtime
{
    /// <summary>
    /// 武器の一覧をアセットとして持つ。<see cref="ToCatalog"/> で Core の <see cref="WeaponCatalog"/> に変換する。
    /// 空・null 要素・Id の重複は、どのアセットかを添えて <see cref="WeaponDefinitionException"/> にする。
    /// </summary>
    [CreateAssetMenu(fileName = "WeaponCatalog", menuName = "Fps/Weapon Catalog")]
    public sealed class WeaponCatalogAsset : ScriptableObject
    {
        [SerializeField, Tooltip("武器の定義。並び順がカタログの順になる")]
        private List<WeaponDefinition> _weapons = new List<WeaponDefinition>();

        /// <summary>定義順の武器。中身を書き換えるときは <see cref="SetWeapons"/> を使う。</summary>
        public IReadOnlyList<WeaponDefinition> Weapons => _weapons ??= new List<WeaponDefinition>();

        /// <summary>武器をまとめて差し替える。null は空にする。渡した列は複製して持つ (定義アセットそのものは共有する)。</summary>
        public void SetWeapons(IEnumerable<WeaponDefinition> weapons)
        {
            _weapons = weapons == null ? new List<WeaponDefinition>() : new List<WeaponDefinition>(weapons);
        }

        /// <summary>
        /// Id が id の武器の定義を探す。無い・null なら false と null。Id は <see cref="WeaponSpec.Id"/> (前後の空白を除いた値) と
        /// 大文字小文字を区別して比べる。
        /// </summary>
        /// <exception cref="WeaponDefinitionException">一覧か、そのどれかの定義が不正なとき (見つかるかどうかに関わらず)。</exception>
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

        /// <summary>定義を変換して <see cref="WeaponCatalog"/> を作る。呼ぶたびに新しいインスタンスを返す。</summary>
        /// <exception cref="WeaponDefinitionException">一覧か、そのどれかの定義が不正なとき。</exception>
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
