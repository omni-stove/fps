using System;
using Shooter.Core;
using UnityEngine;

namespace Shooter.Runtime
{
    // 額と品の一覧は JSON (economy.json) に置き、このアセットは「このモードで経済を使う」ことと、どの JSON と武器のカタログを読むかだけを持つ。
    // 額をここにも並べると、同じ値を JSON とアセットの 2 か所で直すことになるため。
    [CreateAssetMenu(fileName = "Economy", menuName = "Shooter/Modes/Economy")]
    public sealed class EconomyDefinition : ScriptableObject
    {
        [SerializeField, Tooltip("お金と買える品の JSON (Resources/Tuning/economy.json と同じ形)。空なら入口 (GameContent) の EconomyTuning、それも無ければ Resources/Tuning/economy を読む")]
        private TextAsset _tuning;

        [SerializeField, Tooltip("買える武器の Id を引くカタログ。空なら入口 (GameContent) の WeaponCatalog を使う")]
        private WeaponCatalogAsset _weaponCatalog;

        public TextAsset Tuning
        {
            get => _tuning;
            set => _tuning = value;
        }

        public WeaponCatalogAsset WeaponCatalog
        {
            get => _weaponCatalog;
            set => _weaponCatalog = value;
        }

        public WeaponCatalogAsset EffectiveWeaponCatalog => GameContent.Pick(_weaponCatalog, c => c.WeaponCatalog);

        public EconomyRules ToRules()
        {
            if (_tuning == null)
            {
                return KitTuning.Economy;
            }

            try
            {
                return EconomyJson.Parse(_tuning.text, $"economy definition '{SourceName}' ({_tuning.name})");
            }
            catch (TuningJsonException e)
            {
                throw new InvalidOperationException($"economy definition '{SourceName}' is invalid: {e.Message}", e);
            }
        }

        // 見つからなければ null。カタログが無い・壊れているときは、買えない理由をログで追えるよう failure に書く。
        public WeaponDefinition FindWeapon(string weaponId, out string failure)
        {
            failure = null;
            var catalog = EffectiveWeaponCatalog;
            if (catalog == null)
            {
                failure = $"economy definition '{SourceName}' has no weapon catalog; wire WeaponCatalog or set WeaponCatalog in the GameContent";
                return null;
            }

            try
            {
                if (catalog.TryFind(weaponId, out var weapon))
                {
                    return weapon;
                }
            }
            catch (WeaponDefinitionException e)
            {
                failure = $"weapon catalog '{catalog.name}' is invalid: {e.Message}";
                return null;
            }

            failure = $"weapon catalog '{catalog.name}' has no weapon with the id '{weaponId}'";
            return null;
        }

        private string SourceName => string.IsNullOrEmpty(name) ? "(unnamed economy definition)" : name;
    }
}
