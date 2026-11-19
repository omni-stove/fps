using System;
using Shooter.Protocol;
using Shooter.Runtime;
using UnityEngine;

namespace Shooter.NetV2
{
    // オフラインの LootContainer・SupplyDropper・ExtractionZone の差し替えの見た目と同じ口。空の欄はそれぞれの部品がコードで形を作る
    [Serializable]
    public sealed class NetV2LootVisuals
    {
        [SerializeField, Tooltip("マップに置いた箱の、開ける前の見た目。原点が底面の中心")]
        private GameObject _boxVisual;

        [SerializeField, Tooltip("マップに置いた箱の、開けた後の見た目")]
        private GameObject _openedBoxVisual;

        [SerializeField, Tooltip("物資投下の箱の、開ける前の見た目")]
        private GameObject _crateVisual;

        [SerializeField, Tooltip("物資投下の箱の、開けた後の見た目")]
        private GameObject _openedCrateVisual;

        [SerializeField, Tooltip("倒れた人の持ち物の箱の、開ける前の見た目")]
        private GameObject _deathBoxVisual;

        [SerializeField, Tooltip("倒れた人の持ち物の箱の、開けた後の見た目")]
        private GameObject _openedDeathBoxVisual;

        [SerializeField, Tooltip("脱出地点が閉じている間の見た目。原点が地面の中心")]
        private GameObject _closedExtractionVisual;

        [SerializeField, Tooltip("脱出地点が開いている間の見た目")]
        private GameObject _openExtractionVisual;

        public GameObject BoxVisual
        {
            get => _boxVisual;
            set => _boxVisual = value;
        }

        public GameObject OpenedBoxVisual
        {
            get => _openedBoxVisual;
            set => _openedBoxVisual = value;
        }

        public GameObject CrateVisual
        {
            get => _crateVisual;
            set => _crateVisual = value;
        }

        public GameObject OpenedCrateVisual
        {
            get => _openedCrateVisual;
            set => _openedCrateVisual = value;
        }

        public GameObject DeathBoxVisual
        {
            get => _deathBoxVisual;
            set => _deathBoxVisual = value;
        }

        public GameObject OpenedDeathBoxVisual
        {
            get => _openedDeathBoxVisual;
            set => _openedDeathBoxVisual = value;
        }

        public GameObject ClosedExtractionVisual
        {
            get => _closedExtractionVisual;
            set => _closedExtractionVisual = value;
        }

        public GameObject OpenExtractionVisual
        {
            get => _openExtractionVisual;
            set => _openExtractionVisual = value;
        }

        // シーンに置いた物資投下と脱出地点の差し替えの見た目を引き継ぐ。オフラインと同じシーンで同じ見た目にするため
        public static NetV2LootVisuals From(SupplyDropper dropper, ExtractionZone zone, LootContainer box = null)
        {
            var visuals = new NetV2LootVisuals();
            if (box != null)
            {
                visuals._boxVisual = box.VisualPrefab;
                visuals._openedBoxVisual = box.OpenedVisualPrefab;
            }

            if (dropper != null)
            {
                visuals._crateVisual = dropper.CrateVisualPrefab;
                visuals._openedCrateVisual = dropper.OpenedCrateVisualPrefab;
            }

            if (zone != null)
            {
                visuals._closedExtractionVisual = zone.ClosedVisualPrefab;
                visuals._openExtractionVisual = zone.OpenVisualPrefab;
            }

            return visuals;
        }

        public GameObject ClosedBoxOf(ObjectiveItemKind kind)
        {
            switch (kind)
            {
                case ObjectiveItemKind.SupplyCrate:
                    return _crateVisual;
                case ObjectiveItemKind.DeathBox:
                    return _deathBoxVisual;
                default:
                    return _boxVisual;
            }
        }

        public GameObject OpenedBoxOf(ObjectiveItemKind kind)
        {
            switch (kind)
            {
                case ObjectiveItemKind.SupplyCrate:
                    return _openedCrateVisual;
                case ObjectiveItemKind.DeathBox:
                    return _openedDeathBoxVisual;
                default:
                    return _openedBoxVisual;
            }
        }
    }
}
