using System;
using System.Collections.Generic;
using Shooter.Core;
using Shooter.Runtime;

namespace Shooter.NetV2
{
    // サーバーのルールのファイルも同じアセットから書き出すので、アセットは入口 (GameContent) のものを使う。
    internal sealed class NetV2Weapons
    {
        private string _triggerWeaponId;
        private ClientTrigger _trigger;
        private string _triggerError;
        private readonly Dictionary<string, WeaponSpec> _specs = new Dictionary<string, WeaponSpec>(StringComparer.Ordinal);
        private string _ownWeaponId;
        private IReadOnlyList<string> _ownAttachments = WeaponAttachments.None;
        private WeaponSpec _ownSpec;

        public Func<string, IReadOnlyList<string>> OwnAttachments { get; set; }

        // 乗り物の座席の武器はロードアウトにも一覧にも無く、マップのシーンの乗り物が持つので、そこから引く口を外に開ける。
        public Func<string, WeaponDefinition> SceneDefinitions { get; set; }

        // クライアントは試合の設定のアセットを持たないので、サーバーへの書き出しと同じ選び方に入口だけを渡す
        public LoadoutDefinition Loadout => NetMatchDefinition.PickLoadout(null);

        public WeaponCatalogAsset Catalog => NetMatchDefinition.PickCatalog(null);

        public string TriggerError => _triggerError;

        // 武器がどのアセットにも無いと、サーバーに弾かれない頻度が分からないので、引き金を作らず撃たない。
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

        // 他人の同じ武器に自分の付けた物を効かせないよう、SpecFor とは分ける
        public WeaponSpec OwnSpecFor(string weaponId)
        {
            var spec = SpecFor(weaponId);
            var attached = spec != null && OwnAttachments != null ? OwnAttachments(weaponId) ?? WeaponAttachments.None : WeaponAttachments.None;
            if (spec == null || attached.Count == 0)
            {
                return spec;
            }

            // 毎 tick 引くので、武器と付けた物が変わったときだけ作り直す
            if (!string.Equals(_ownWeaponId, weaponId, StringComparison.Ordinal) || !WeaponAttachments.SameAs(_ownAttachments, attached))
            {
                _ownWeaponId = weaponId;
                _ownAttachments = attached;
                _ownSpec = WeaponAttachments.Apply(spec, attached, KitTuning.Items);
            }

            return _ownSpec;
        }

        // 見た目と音は WeaponSpec に載らないので、武器の定義そのものを値と同じ順 (ロードアウト・一覧) に探す。
        public WeaponDefinition DefinitionFor(string weaponId)
        {
            if (string.IsNullOrEmpty(weaponId))
            {
                return null;
            }

            var scene = SceneDefinitions?.Invoke(weaponId);
            if (scene != null)
            {
                return scene;
            }

            var loadout = Loadout;
            if (loadout != null)
            {
                foreach (var slot in loadout.Slots)
                {
                    if (slot?.Weapon != null && string.Equals(slot.Weapon.Id.Trim(), weaponId, StringComparison.Ordinal))
                    {
                        return slot.Weapon;
                    }
                }
            }

            var catalog = Catalog;
            if (catalog == null)
            {
                return null;
            }

            try
            {
                return catalog.TryFind(weaponId, out var definition) ? definition : null;
            }
            catch (WeaponDefinitionException)
            {
                // 見た目と音が引けないだけなので、コードで作る形と無音で出す。
                return null;
            }
        }

        public bool TryFind(string weaponId, out WeaponSpec spec, out string error)
        {
            spec = null;
            if (string.IsNullOrEmpty(weaponId))
            {
                error = "before the server equips a weapon";
                return false;
            }

            var scene = SceneDefinitions?.Invoke(weaponId);
            if (scene != null)
            {
                try
                {
                    spec = scene.ToSpec();
                    error = null;
                    return true;
                }
                catch (WeaponDefinitionException e)
                {
                    error = "with an invalid vehicle weapon: " + e.Message;
                    return false;
                }
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
