using System;
using System.Collections.Generic;
using Shooter.Core;
using Shooter.Editor;
using Shooter.Runtime;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Shooter.Tests.EditMode
{
    public sealed class LoadoutTestAssets : IDisposable
    {
        public const string SmgId = "00000000-0000-4000-8000-0000000000c1";

        private readonly List<Object> _created = new List<Object>();

        public LoadoutTestAssets()
        {
            Rifle = Weapon("Rifle", TestWeapons.RifleSpec());
            Handgun = Weapon("Handgun", TestWeapons.HandgunSpec());
            Smg = Weapon("SMG", new WeaponSpec(
                SmgId, "SMG", WeaponCategory.SubmachineGun, FireMode.Auto, 25, 100, 0.07f, 2f, 18, 60f, DamageFalloff.None));

            Loadout = Create<LoadoutDefinition>("TestLoadout");
            WeaponAssets.Configure(Loadout, TestWeapons.LoadoutSlots(Rifle, Handgun), LoadoutDefinition.DefaultSwitchSeconds);

            Catalog = Create<WeaponCatalogAsset>("TestCatalog");
            WeaponAssets.Configure(Catalog, new[] { Rifle, Smg, Handgun });
        }

        public WeaponDefinition Rifle { get; }

        public WeaponDefinition Handgun { get; }

        public WeaponDefinition Smg { get; }

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
