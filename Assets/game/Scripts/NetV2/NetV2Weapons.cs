using System;
using System.Collections.Generic;
using Fps.Core;
using Fps.Runtime;

namespace Fps.NetV2
{
    /// <summary>
    /// ネット対戦の、対戦で選べる武器 (ロードアウトとその一覧のアセット) と、今の武器の引き金 (<see cref="ClientTrigger"/>)。
    /// アセットは Netcode のクライアントと同じく入口 (<see cref="GameContent"/>) のものを使う (サーバーのルールのファイルも同じアセットから書き出す)。
    /// </summary>
    internal sealed class NetV2Weapons
    {
        private string _triggerWeaponId;
        private ClientTrigger _trigger;
        private string _triggerError;
        private readonly Dictionary<string, WeaponSpec> _specs = new Dictionary<string, WeaponSpec>(StringComparer.Ordinal);

        public LoadoutDefinition Loadout => GameContent.Pick<LoadoutDefinition>(c => c.Loadout);

        public WeaponCatalogAsset Catalog => GameContent.Pick<WeaponCatalogAsset>(c => c.WeaponCatalog);

        /// <summary>なぜ <see cref="TriggerFor"/> が null を返したか。</summary>
        public string TriggerError => _triggerError;

        /// <summary>
        /// weaponId の武器の撃ち方で判断する引き金。武器が変わったら作り直す (撃ち始めからの間隔も持ち越さない)。
        /// 武器がどのアセットにも無いなら null (サーバーに弾かれない頻度が分からないので撃たない)。
        /// </summary>
        public ClientTrigger TriggerFor(string weaponId)
        {
            if (string.Equals(weaponId, _triggerWeaponId, StringComparison.Ordinal))
            {
                return _trigger;
            }

            _triggerWeaponId = weaponId;
            _trigger = TryFind(weaponId, out var spec, out _triggerError) ? new ClientTrigger(spec) : null;
            return _trigger;
        }

        // 毎 tick 届くスナップショットや知らせから引くので、引いた武器 (無かったことも) を覚えてアセットを読み直さない。
        public WeaponSpec SpecFor(string weaponId)
        {
            if (string.IsNullOrEmpty(weaponId))
            {
                return null;
            }

            if (!_specs.TryGetValue(weaponId, out var spec))
            {
                spec = TryFind(weaponId, out var found, out _) ? found : null;
                _specs.Add(weaponId, spec);
            }

            return spec;
        }

        /// <summary>weaponId の武器の値を、ロードアウト・一覧の順に探す。</summary>
        public bool TryFind(string weaponId, out WeaponSpec spec, out string error)
        {
            spec = null;
            if (string.IsNullOrEmpty(weaponId))
            {
                error = "before the server equips a weapon";
                return false;
            }

            var loadout = Loadout;
            var catalog = Catalog;
            if (loadout == null && catalog == null)
            {
                error = "without a loadout or weapon catalog in GameContent";
                return false;
            }

            string loadoutError = null;
            if (loadout != null)
            {
                try
                {
                    foreach (var candidate in loadout.ToSpecs())
                    {
                        if (string.Equals(candidate.Id, weaponId, StringComparison.Ordinal))
                        {
                            spec = candidate;
                            error = null;
                            return true;
                        }
                    }
                }
                catch (WeaponDefinitionException e)
                {
                    // 一覧の側にあれば撃てるので、ここでは諦めない
                    loadoutError = "with an invalid loadout: " + e.Message;
                }
            }

            if (catalog != null)
            {
                try
                {
                    if (catalog.ToCatalog().TryGet(weaponId, out spec))
                    {
                        error = null;
                        return true;
                    }
                }
                catch (WeaponDefinitionException e)
                {
                    error = "with an invalid weapon catalog: " + e.Message;
                    return false;
                }
            }

            error = loadoutError ?? "weapon " + weaponId + " is in neither the loadout nor the weapon catalog";
            return false;
        }
    }
}
