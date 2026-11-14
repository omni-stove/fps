using System;
using System.Collections.Generic;
using Fps.Core;
using Fps.Editor;
using Fps.Runtime;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Fps.Tests.EditMode
{
    /// <summary>
    /// ロードアウトの選択のテストに使う、ディスクに保存しないアセット。デフォルトロードアウト (rifle・handgun) と同じルールに、
    /// メインのスロットに入る SMG を足した一覧 (rifle, smg, handgun の順) を作る。<see cref="IDisposable.Dispose"/> で全部消す。
    /// </summary>
    internal sealed class LoadoutTestAssets : IDisposable
    {
        public const string SmgId = "00000000-0000-4000-8000-0000000000c1";

        private readonly List<Object> _created = new List<Object>();

        public LoadoutTestAssets()
        {
            var rifle = Weapon("Rifle", TestWeapons.RifleSpec());
            var handgun = Weapon("Handgun", TestWeapons.HandgunSpec());
            var smg = Weapon("SMG", new WeaponSpec(
                SmgId, "SMG", WeaponCategory.SubmachineGun, FireMode.Auto, 25, 100, 0.07f, 2f, 18, 60f, DamageFalloff.None));

            Loadout = Create<LoadoutDefinition>("TestLoadout");
            WeaponAssets.Configure(Loadout, TestWeapons.LoadoutSlots(rifle, handgun), LoadoutDefinition.DefaultSwitchSeconds);

            Catalog = Create<WeaponCatalogAsset>("TestCatalog");
            WeaponAssets.Configure(Catalog, new[] { rifle, smg, handgun });
        }

        public LoadoutDefinition Loadout { get; }

        public WeaponCatalogAsset Catalog { get; }

        public void Dispose()
        {
            foreach (var created in _created)
            {
                if (created != null)
                {
                    Object.DestroyImmediate(created);
                }
            }

            _created.Clear();
        }

        private WeaponDefinition Weapon(string assetName, WeaponSpec spec)
        {
            var definition = Create<WeaponDefinition>(assetName);
            WeaponAssets.Configure(definition, spec);
            return definition;
        }

        private T Create<T>(string assetName)
            where T : ScriptableObject
        {
            var asset = ScriptableObject.CreateInstance<T>();
            asset.name = assetName;
            _created.Add(asset);
            return asset;
        }
    }
}
