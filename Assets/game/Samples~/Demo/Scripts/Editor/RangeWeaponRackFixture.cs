using System;
using System.Collections.Generic;
using System.Globalization;
using Shooter.Core;
using Shooter.Runtime;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Shooter.Editor
{
    // 武器をサンプルの武器の JSON すべて (WeaponAssetBuilder.LoadRangeWeapons) から並べるのは、サンプルの武器を足す issue が JSON を置くだけで拾える武器を増やせるようにするため。
    // 武器のモデルに照準を合わせて拾う (Look) ので、隣の武器と範囲が重なっても狙った 1 本だけを拾える。
    public sealed class RangeWeaponRackFixture : ITpsRangeFixture
    {
        public const string RootName = "WeaponRack";
        public const string PickupNamePrefix = "WeaponPickup-";
        public const int FixtureOrder = 570;

        // 列でなく横に広げるのは、奥 (z) へ 1 列足すと射撃場の床の端 (TpsRangeSceneBuilder.FloorHalfExtent) を越えるため。
        public const int Columns = 10;
        public const int Rows = 3;
        public const float ColumnSpacing = 2.5f;
        public const float RowSpacing = 3f;
        public const float RespawnSeconds = 5f;

        // 武器の前に立って見下ろせば届く広さにする。
        public const float PickupRadius = 1.5f;

        public static readonly Vector3 FirstSlot = new Vector3(0f, 0f, 30f);

        public static readonly Vector3 Size = new Vector3((Columns - 1) * ColumnSpacing + 2.5f, 2f, (Rows - 1) * RowSpacing + 3f);

        public int Order => FixtureOrder;

        public string Name => RootName;

        public Bounds Footprint => new Bounds(
            FirstSlot + new Vector3((Columns - 1) * ColumnSpacing / 2f, Size.y / 2f, (Rows - 1) * RowSpacing / 2f), Size);

        public static Vector3 SlotPosition(int index)
        {
            if (index < 0 || index >= Columns * Rows)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(index), index, $"the weapon rack holds {(Columns * Rows).ToString(CultureInfo.InvariantCulture)} weapons");
            }

            return FirstSlot + new Vector3((index % Columns) * ColumnSpacing, 0f, (index / Columns) * RowSpacing);
        }

        public GameObject Place(Scene scene, GameRules rules)
        {
            return Place(scene, WeaponAssetBuilder.LoadRangeWeapons().Weapons, OpticItemIds(KitTuning.Items));
        }

        public static IReadOnlyList<string> OpticItemIds(ItemCatalog items)
        {
            if (items == null)
            {
                throw new ArgumentNullException(nameof(items));
            }

            var ids = new List<string>();
            foreach (var item in items.Items)
            {
                if (item.IsAttachment && item.Attachment.IsOptic)
                {
                    ids.Add(item.Id);
                }
            }

            return ids;
        }

        public static GameObject Place(Scene scene, IReadOnlyList<WeaponDefinition> weapons)
        {
            return Place(scene, weapons, Array.Empty<string>());
        }

        public static GameObject Place(Scene scene, IReadOnlyList<WeaponDefinition> weapons, IReadOnlyList<string> opticItemIds)
        {
            if (weapons == null)
            {
                throw new ArgumentNullException(nameof(weapons));
            }

            if (opticItemIds == null)
            {
                throw new ArgumentNullException(nameof(opticItemIds));
            }

            if (weapons.Count + opticItemIds.Count > Columns * Rows)
            {
                throw new InvalidOperationException(
                    $"{RootName} holds {(Columns * Rows).ToString(CultureInfo.InvariantCulture)} pickups, but the range lists "
                    + $"{weapons.Count.ToString(CultureInfo.InvariantCulture)} weapons and {opticItemIds.Count.ToString(CultureInfo.InvariantCulture)} optics; "
                    + $"add a row to {nameof(RangeWeaponRackFixture)}");
            }

            // 中身を先に作って検査し、不正なら何も置かない。
            var contents = new PickupContents[weapons.Count];
            for (var i = 0; i < weapons.Count; i++)
            {
                var weapon = weapons[i];
                if (weapon == null)
                {
                    throw new ArgumentException($"range weapon #{i.ToString(CultureInfo.InvariantCulture)} is empty", nameof(weapons));
                }

                contents[i] = PickupContents.ForWeapon(weapon, weapon.ToSpec().MagazineSize);
            }

            var root = new GameObject(RootName);
            SceneManager.MoveGameObjectToScene(root, scene);
            for (var i = 0; i < weapons.Count; i++)
            {
                CreatePickup(root.transform, weapons[i].name, contents[i], SlotPosition(i));
            }

            for (var i = 0; i < opticItemIds.Count; i++)
            {
                CreatePickup(root.transform, opticItemIds[i], PickupContents.ForItem(opticItemIds[i], 1), SlotPosition(weapons.Count + i));
            }

            return root;
        }

        public static PickupSpawnPoint CreatePickup(Transform parent, string weaponName, PickupContents contents, Vector3 position)
        {
            var pointObject = new GameObject(PickupNamePrefix + weaponName);
            pointObject.transform.SetParent(parent, false);
            pointObject.transform.position = position;
            var point = pointObject.AddComponent<PickupSpawnPoint>();
            point.Contents = contents;
            point.CollectMode = PickupCollectMode.Look;
            point.Radius = PickupRadius;
            point.RespawnSeconds = RespawnSeconds;
            return point;
        }
    }
}
