using System;
using Shooter.Core;
using Shooter.Protocol;
using Shooter.Runtime;
using UnityEngine;

namespace Shooter.NetV2
{
    // オフラインの Pickup・AmmoCrate の差し替えの見た目と同じ口。空の欄はオフラインと同じ形と色 (中身の種類で色を分けた箱・仮の銃・くすんだ緑の箱) をコードで作る
    [Serializable]
    public sealed class NetV2PickupVisuals
    {
        [SerializeField, Tooltip("武器の拾い物の見た目。原点を見た目の中心にする (足元から浮かせて回す)")]
        private GameObject _weaponVisual;

        [SerializeField, Tooltip("予備弾の拾い物の見た目")]
        private GameObject _ammoVisual;

        [SerializeField, Tooltip("回復の拾い物の見た目")]
        private GameObject _healthVisual;

        [SerializeField, Tooltip("防具の拾い物の見た目")]
        private GameObject _armorVisual;

        [SerializeField, Tooltip("アイテムの拾い物の見た目")]
        private GameObject _itemVisual;

        [SerializeField, Tooltip("弾薬箱の見た目。原点が底面の中心")]
        private GameObject _ammoCrateVisual;

        public GameObject WeaponVisual
        {
            get => _weaponVisual;
            set => _weaponVisual = value;
        }

        public GameObject AmmoVisual
        {
            get => _ammoVisual;
            set => _ammoVisual = value;
        }

        public GameObject HealthVisual
        {
            get => _healthVisual;
            set => _healthVisual = value;
        }

        public GameObject ArmorVisual
        {
            get => _armorVisual;
            set => _armorVisual = value;
        }

        public GameObject ItemVisual
        {
            get => _itemVisual;
            set => _itemVisual = value;
        }

        public GameObject AmmoCrateVisual
        {
            get => _ammoCrateVisual;
            set => _ammoCrateVisual = value;
        }

        // シーンに置いた弾薬箱の差し替えの見た目を引き継ぐ。オフラインと同じシーンで同じ見た目にするため
        public static NetV2PickupVisuals From(AmmoCrate crate)
        {
            var visuals = new NetV2PickupVisuals();
            if (crate != null)
            {
                visuals._ammoCrateVisual = crate.VisualPrefab;
            }

            return visuals;
        }

        // 空なら null (呼ぶ側がコードで形を作る)
        public GameObject Of(ObjectiveItemKind kind)
        {
            if (kind == ObjectiveItemKind.AmmoCrate)
            {
                return _ammoCrateVisual;
            }

            if (!PickupItemKinds.TryGetPickupKind(kind, out var pickupKind))
            {
                return null;
            }

            switch (pickupKind)
            {
                case PickupKind.Weapon:
                    return _weaponVisual;
                case PickupKind.ReserveAmmo:
                    return _ammoVisual;
                case PickupKind.Health:
                    return _healthVisual;
                case PickupKind.Armor:
                    return _armorVisual;
                case PickupKind.Item:
                    return _itemVisual;
                default:
                    return null;
            }
        }
    }
}
